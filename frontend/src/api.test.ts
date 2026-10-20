import { describe, expect, it } from 'vitest'
import { ApiError, api, availableSteps, TicketStatus } from './api'
import { adrian, fakeApi, ticket } from './test/fakeApi'

describe('availableSteps', () => {
  it('offers only assignment on an unassigned open ticket', () => {
    expect(availableSteps(ticket())).toEqual({
      assign: true,
      start: false,
      resolve: false,
      close: false,
    })
  })

  it('offers reassign, start and resolve once a technician is assigned', () => {
    expect(availableSteps(ticket({ assignedToUserId: adrian.id }))).toEqual({
      assign: true,
      start: true,
      resolve: true,
      close: false,
    })
  })

  it('offers only resolve while in progress', () => {
    const t = ticket({ assignedToUserId: adrian.id, status: TicketStatus.InProgress })
    expect(availableSteps(t)).toEqual({ assign: false, start: false, resolve: true, close: false })
  })

  it('offers only close on a resolved ticket, and nothing on a closed one', () => {
    const resolved = ticket({ assignedToUserId: adrian.id, status: TicketStatus.Resolved })
    expect(availableSteps(resolved)).toEqual({
      assign: false,
      start: false,
      resolve: false,
      close: true,
    })
    expect(Object.values(availableSteps({ ...resolved, status: TicketStatus.Closed }))).toEqual([
      false,
      false,
      false,
      false,
    ])
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
