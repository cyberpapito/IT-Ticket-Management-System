// Types and calls for the ASP.NET API. Enums come over JSON as numbers (see the backend's
// TicketStatus, TicketPriority and UserRole), so the labels below map them back to names.

export const TicketStatus = { Open: 0, InProgress: 1, Resolved: 2, Closed: 3 } as const
export type TicketStatus = (typeof TicketStatus)[keyof typeof TicketStatus]

export const TicketPriority = { Low: 0, Medium: 1, High: 2, Critical: 3 } as const
export type TicketPriority = (typeof TicketPriority)[keyof typeof TicketPriority]

export const UserRole = { User: 0, Technician: 1, Admin: 2 } as const
export type UserRole = (typeof UserRole)[keyof typeof UserRole]

// TicketActivityType on the backend: one entry per workflow step, plus comments.
export const TicketActivityType = {
  Created: 0,
  Assigned: 1,
  Started: 2,
  Resolved: 3,
  Closed: 4,
  Deleted: 5,
  Comment: 6,
} as const
export type TicketActivityType = (typeof TicketActivityType)[keyof typeof TicketActivityType]

export const statusLabels: Record<TicketStatus, string> = {
  0: 'Open',
  1: 'In progress',
  2: 'Resolved',
  3: 'Closed',
}

export const priorityLabels: Record<TicketPriority, string> = {
  0: 'Low',
  1: 'Medium',
  2: 'High',
  3: 'Critical',
}

export const roleLabels: Record<UserRole, string> = {
  0: 'User',
  1: 'Technician',
  2: 'Admin',
}

// Technicians and admins work tickets; plain users report them (User.IsStaff on the backend).
export const isStaff = (user: User) => user.role === UserRole.Technician || user.role === UserRole.Admin

// Same limits as Ticket.TitleMaxLength / DescriptionMaxLength and User.NameMaxLength. The API
// enforces them; the form uses them only to stop typing early.
export const TITLE_MAX_LENGTH = 255
export const DESCRIPTION_MAX_LENGTH = 2000
export const NAME_MAX_LENGTH = 100
export const COMMENT_MAX_LENGTH = 2000 // TicketActivity.NoteMaxLength
export const PASSWORD_MIN_LENGTH = 12 // User.PasswordMinLength
export const PASSWORD_MAX_LENGTH = 128

export interface Ticket {
  id: string
  title: string
  description: string
  priority: TicketPriority
  status: TicketStatus
  assignedToUserId: string | null
  createdByUserId: string
  createdAt: string
  resolvedAt: string | null
  deletedAt: string | null
  resolutionSummary: string | null
}

export interface User {
  id: string
  name: string
  email: string
  role: UserRole
  createdAt: string
}

// One entry in a ticket's history. `actorUserId` is who did it (null on entries from before
// sign-in); `userId` is the person it names: the reporter on Created, the technician on Assigned,
// the author on Comment; null otherwise.
export interface TicketActivity {
  id: string
  ticketId: string
  type: TicketActivityType
  at: string
  actorUserId: string | null
  userId: string | null
  note: string | null
  fromStatus: TicketStatus | null
  toStatus: TicketStatus | null
}

// The reporter is whoever is signed in; the API takes it from the session.
export interface CreateTicketRequest {
  title: string
  description: string
  priority: TicketPriority
}

export interface CreateUserRequest {
  name: string
  email: string
  role: UserRole
  password: string
}

// A failed call. `message` is the API's own reason when it sent one (ProblemDetails.detail,
// e.g. "Cannot close a ticket that is Open..."), so the UI can show it as is.
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function errorFrom(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {}
  try {
    problem = await response.json()
  } catch {
    // Not JSON (a bare 404, or the proxy failing to reach the API)
  }

  // Model-binding failures (e.g. a malformed GUID) list their reasons under `errors`
  const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : []
  const message =
    problem.detail ??
    (fieldErrors.length > 0 ? fieldErrors.join(' ') : undefined) ??
    problem.title ??
    (response.status === 404
      ? 'Not found.'
      : response.status >= 500
        ? `The API failed or isn't running (${response.status}).`
        : `Request failed (${response.status}).`)

  return new ApiError(response.status, message)
}

// The session is an HttpOnly cookie the browser sends by itself. Every POST, PUT and DELETE also
// carries the antiforgery token from /api/auth/csrf; it's tied to who is signed in, so it's
// dropped and fetched again after signing in or out.
let csrfToken: Promise<string> | null = null

function csrf(): Promise<string> {
  csrfToken ??= fetch('/api/auth/csrf')
    .then(async (r) => {
      if (!r.ok) throw await errorFrom(r)
      return ((await r.json()) as { token: string }).token
    })
    .catch((e) => {
      csrfToken = null
      throw e
    })
  return csrfToken
}

// Called when the API answers 401 to a signed-in page (the session expired, or an admin reset the
// password), so the app can go back to the sign-in page.
let onSignedOut: (() => void) | null = null
export function whenSignedOut(callback: (() => void) | null) {
  onSignedOut = callback
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const method = init?.method ?? 'GET'
  const headers: Record<string, string> = {}
  if (init?.body) headers['Content-Type'] = 'application/json'
  if (method !== 'GET') headers['X-XSRF-TOKEN'] = await csrf()

  const response = await fetch(`/api${path}`, {
    ...init,
    headers: Object.keys(headers).length > 0 ? headers : undefined,
  })

  if (response.status === 401 && path !== '/auth/login' && path !== '/auth/me') onSignedOut?.()
  if (!response.ok) throw await errorFrom(response)
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })

const put = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'PUT', body: JSON.stringify(body) })

export const api = {
  // 401 when nobody is signed in.
  me: () => request<User>('/auth/me'),
  login: async (email: string, password: string) => {
    const user = await post<User>('/auth/login', { email, password })
    csrfToken = null
    return user
  },
  logout: async () => {
    try {
      await post<void>('/auth/logout')
    } finally {
      csrfToken = null
    }
  },

  getTickets: () => request<Ticket[]>('/tickets'),
  getTicket: (id: string) => request<Ticket>(`/tickets/${id}`),
  getDeletedTickets: () => request<Ticket[]>('/tickets/deleted'),
  createTicket: (ticket: CreateTicketRequest) => post<Ticket>('/tickets', ticket),
  deleteTicket: (id: string) => request<void>(`/tickets/${id}`, { method: 'DELETE' }),

  assignTicket: (id: string, technicianId: string) =>
    post<Ticket>(`/tickets/${id}/assign`, { technicianId }),
  startWork: (id: string) => post<Ticket>(`/tickets/${id}/start`),
  resolveTicket: (id: string, resolutionSummary: string) =>
    post<Ticket>(`/tickets/${id}/resolve`, { resolutionSummary }),
  closeTicket: (id: string) => post<Ticket>(`/tickets/${id}/close`),

  getActivity: (id: string) => request<TicketActivity[]>(`/tickets/${id}/activity`),
  addComment: (id: string, text: string) => post<TicketActivity>(`/tickets/${id}/comments`, { text }),

  getUsers: (role?: UserRole) =>
    request<User[]>(role === undefined ? '/users' : `/users?role=${role}`),
  createUser: (user: CreateUserRequest) => post<User>('/users', user),
  changeRole: (id: string, role: UserRole) => put<User>(`/users/${id}/role`, { role }),
  setPassword: (id: string, password: string) => put<void>(`/users/${id}/password`, { password }),
}

// Only for tests: forget the cached token so each test starts signed out.
export function resetSession() {
  csrfToken = null
  onSignedOut = null
}

// Which workflow steps to offer this user. These mirror the backend's rules (the state rules on
// the Ticket entity and who-may-do-what in TicketPermissions) so the page doesn't show buttons
// that can only fail; the API still decides, and its 403/409 reason is shown if they disagree.
export function availableSteps(ticket: Ticket, me: User) {
  const assigned = ticket.assignedToUserId !== null
  const admin = me.role === UserRole.Admin
  const works = admin || (me.role === UserRole.Technician && ticket.assignedToUserId === me.id)
  const closes = admin || ticket.createdByUserId === me.id || ticket.assignedToUserId === me.id
  return {
    assign: isStaff(me) && ticket.status === TicketStatus.Open,
    start: works && ticket.status === TicketStatus.Open && assigned,
    resolve:
      works &&
      assigned &&
      (ticket.status === TicketStatus.Open || ticket.status === TicketStatus.InProgress),
    close: closes && ticket.status === TicketStatus.Resolved,
    delete: admin,
  }
}
