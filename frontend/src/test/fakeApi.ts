import { vi } from 'vitest'
import type { Ticket, User } from '../api'

// Answers fetch calls from a table of "METHOD /api/path" → [status, body]. Anything not in the
// table is a 404, so a test fails loudly if the page calls something unexpected.
export type Routes = Record<string, [number, unknown?]>

export function fakeApi(routes: Routes) {
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${url}`
    const [status, body] = routes[key] ?? [404]
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export const adrian: User = {
  id: 'a0000000-0000-0000-0000-000000000001',
  name: 'Adrian Rodriguez',
  email: 'adrian.rodriguez@example.com',
  role: 1,
  createdAt: '2026-10-03T00:00:00+00:00',
}

export const john: User = {
  id: 'a0000000-0000-0000-0000-000000000002',
  name: 'John Doe',
  email: 'john.doe@example.com',
  role: 0,
  createdAt: '2026-10-03T00:00:00+00:00',
}

export function ticket(overrides: Partial<Ticket> = {}): Ticket {
  return {
    id: 't1',
    title: 'Printer offline',
    description: 'Floor 2 printer shows offline',
    priority: 1,
    status: 0,
    assignedToUserId: null,
    createdByUserId: john.id,
    createdAt: '2026-10-20T14:00:00+00:00',
    resolvedAt: null,
    deletedAt: null,
    resolutionSummary: null,
    ...overrides,
  }
}
