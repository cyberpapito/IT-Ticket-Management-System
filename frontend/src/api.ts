// Types and calls for the ASP.NET API. Enums come over JSON as numbers (see the backend's
// TicketStatus, TicketPriority and UserRole), so the labels below map them back to names.

export const TicketStatus = { Open: 0, InProgress: 1, Resolved: 2, Closed: 3 } as const
export type TicketStatus = (typeof TicketStatus)[keyof typeof TicketStatus]

export const TicketPriority = { Low: 0, Medium: 1, High: 2, Critical: 3 } as const
export type TicketPriority = (typeof TicketPriority)[keyof typeof TicketPriority]

export const UserRole = { User: 0, Technician: 1 } as const
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
}

// Same limits as Ticket.TitleMaxLength / DescriptionMaxLength and User.NameMaxLength. The API
// enforces them; the form uses them only to stop typing early.
export const TITLE_MAX_LENGTH = 255
export const DESCRIPTION_MAX_LENGTH = 2000
export const NAME_MAX_LENGTH = 100
export const COMMENT_MAX_LENGTH = 2000 // TicketActivity.NoteMaxLength

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

// One entry in a ticket's history. `userId` is the person it names: the reporter on Created,
// the technician on Assigned, the author on Comment; null otherwise.
export interface TicketActivity {
  id: string
  ticketId: string
  type: TicketActivityType
  at: string
  userId: string | null
  note: string | null
  fromStatus: TicketStatus | null
  toStatus: TicketStatus | null
}

export interface CreateTicketRequest {
  title: string
  description: string
  priority: TicketPriority
  createdByUserId: string
}

export interface CreateUserRequest {
  name: string
  email: string
  role: UserRole
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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: init?.body ? { 'Content-Type': 'application/json' } : undefined,
  })

  if (!response.ok) throw await errorFrom(response)
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })

export const api = {
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
  addComment: (id: string, authorUserId: string, text: string) =>
    post<TicketActivity>(`/tickets/${id}/comments`, { authorUserId, text }),

  getUsers: (role?: UserRole) =>
    request<User[]>(role === undefined ? '/users' : `/users?role=${role}`),
  createUser: (user: CreateUserRequest) => post<User>('/users', user),
}

// Which workflow steps to offer. These mirror the rules on the backend's Ticket entity so the
// page doesn't show buttons that can only fail; the API still decides, and its 409 reason is
// shown if the two ever disagree.
export function availableSteps(ticket: Ticket) {
  const assigned = ticket.assignedToUserId !== null
  return {
    assign: ticket.status === TicketStatus.Open,
    start: ticket.status === TicketStatus.Open && assigned,
    resolve:
      assigned && (ticket.status === TicketStatus.Open || ticket.status === TicketStatus.InProgress),
    close: ticket.status === TicketStatus.Resolved,
  }
}
