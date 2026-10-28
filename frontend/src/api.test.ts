import { describe, expect, it } from 'vitest'
import { ApiError, api, availableSteps, TicketStatus, whenSignedOut } from './api'
import { ada, adrian, CSRF_TOKEN, fakeApi, john, ticket } from './test/fakeApi'

const none = { assign: false, start: false, resolve: false, close: false, delete: false }

describe('availableSteps', () => {
  it('offers a technician only assignment on an unassigned open ticket', () => {
    expect(availableSteps(ticket(), adrian)).toEqual({ ...none, assign: true })
  })

  it('offers the assigned technician reassign, start and resolve', () => {
    expect(availableSteps(ticket({ assignedToUserId: adrian.id }), adrian)).toEqual({
      ...none,
      assign: true,
      start: true,
      resolve: true,
    })
  })

  it('offers a technician who is not assigned only reassignment', () => {
    const other = { ...adrian, id: 'someone-else' }
    expect(availableSteps(ticket({ assignedToUserId: adrian.id }), other)).toEqual({ ...none, assign: true })
  })

  it('offers only resolve while in progress', () => {
    const t = ticket({ assignedToUserId: adrian.id, status: TicketStatus.InProgress })
    expect(availableSteps(t, adrian)).toEqual({ ...none, resolve: true })
  })

  it('lets the reporter close their resolved ticket and nothing else', () => {
    const resolved = ticket({ assignedToUserId: adrian.id, status: TicketStatus.Resolved })
    expect(availableSteps(resolved, john)).toEqual({ ...none, close: true })
    expect(availableSteps(ticket(), john)).toEqual(none)
    expect(availableSteps({ ...resolved, status: TicketStatus.Closed }, john)).toEqual(none)
  })

  it('lets an admin take any step and delete', () => {
    expect(availableSteps(ticket({ assignedToUserId: adrian.id }), ada)).toEqual({
      assign: true,
      start: true,
      resolve: true,
      close: false,
      delete: true,
    })
  })
})

describe('session', () => {
  it('sends the antiforgery token on writes, fetching it once', async () => {
    const fetchMock = fakeApi({
      'POST /api/tickets/t1/start': [200, ticket()],
      'POST /api/tickets/t1/close': [200, ticket()],
      'GET /api/tickets/t1': [200, ticket()],
    })

    await api.startWork('t1')
    await api.closeTicket('t1')
    await api.getTicket('t1')

    const calls = fetchMock.mock.calls.map(([url, init]) => [`${init?.method ?? 'GET'} ${url}`, init?.headers])
    expect(calls.filter(([key]) => key === 'GET /api/auth/csrf')).toHaveLength(1)
    expect(calls.find(([key]) => key === 'POST /api/tickets/t1/close')![1]).toEqual({ 'X-XSRF-TOKEN': CSRF_TOKEN })
    expect(calls.find(([key]) => key === 'GET /api/tickets/t1')![1]).toBeUndefined()
  })

  it('fetches a new token after signing in, since the old one was for nobody', async () => {
    const fetchMock = fakeApi({ 'POST /api/auth/login': [200, john], 'POST /api/tickets/t1/start': [200, ticket()] })

    await api.login('john.doe@example.com', 'correct horse battery')
    await api.startWork('t1')

    expect(fetchMock.mock.calls.filter(([url]) => url === '/api/auth/csrf')).toHaveLength(2)
  })

  it('tells the app when a signed-in call comes back 401', async () => {
    fakeApi({ 'GET /api/tickets': [401] })
    let signedOut = false
    whenSignedOut(() => (signedOut = true))

    await expect(api.getTickets()).rejects.toBeInstanceOf(ApiError)
    expect(signedOut).toBe(true)
  })
})

describe('api errors', () => {
  it("uses the API's ProblemDetails reason as the message", async () => {
    fakeApi({
      'POST /api/tickets/t1/close': [
        409,
        { title: 'Not allowed', detail: 'Cannot close a ticket that is Open.' },
      ],
    })

    const error = await api.closeTicket('t1').catch((e) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(409)
    expect(error.message).toBe('Cannot close a ticket that is Open.')
  })

  it('joins model-binding errors when there is no detail', async () => {
    fakeApi({
      'GET /api/tickets/nope': [
        400,
        { title: 'One or more validation errors occurred.', errors: { id: ['The value is not valid.'] } },
      ],
    })

    await expect(api.getTicket('nope')).rejects.toThrow('The value is not valid.')
  })

  it('says not found for a bare 404', async () => {
    fakeApi({})
    await expect(api.getTicket('t1')).rejects.toThrow('Not found.')
  })

  it('resolves a 204 delete with no body', async () => {
    fakeApi({ 'DELETE /api/tickets/t1': [204] })
    await expect(api.deleteTicket('t1')).resolves.toBeUndefined()
  })
})
