import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { TicketActivityType, TicketStatus, type Ticket, type TicketActivity, type User } from '../api'
import { ada, adrian, fakeApi, john, ticket, type Routes as ApiRoutes } from '../test/fakeApi'
import { signedInAs } from '../test/signedIn'
import { TicketDetailPage } from './TicketDetailPage'

function entry(overrides: Partial<TicketActivity>): TicketActivity {
  return {
    id: crypto.randomUUID(),
    ticketId: 't1',
    type: TicketActivityType.Created,
    at: '2026-10-20T14:00:00+00:00',
    actorUserId: null,
    userId: null,
    note: null,
    fromStatus: null,
    toStatus: null,
    ...overrides,
  }
}

const workflow = () => screen.findByRole('complementary', { name: 'Workflow' })

const created = entry({ actorUserId: john.id, userId: john.id, toStatus: TicketStatus.Open })

const page = () => (
  <MemoryRouter initialEntries={['/tickets/t1']}>
    <Routes>
      <Route path="/tickets/:id" element={<TicketDetailPage />} />
    </Routes>
  </MemoryRouter>
)

// Signed in as the technician unless a test says otherwise.
function renderPage(t: Ticket, routes: ApiRoutes = {}, me: User = adrian) {
  const fetchMock = fakeApi({
    'GET /api/tickets/t1': [200, t],
    'GET /api/users': [200, [adrian, john]],
    'GET /api/tickets/t1/activity': [200, [created]],
    ...routes,
  })
  render(signedInAs(me, page()))
  return fetchMock
}

describe('TicketDetailPage', () => {
  it('only offers assignment, to technicians, on an unassigned ticket', async () => {
    renderPage(ticket())

    expect(await screen.findByRole('heading', { name: 'Printer offline' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assign' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()

    const options = await within(await workflow()).findAllByRole('option')
    expect(options.map((o) => o.textContent)).toEqual(['Adrian Rodriguez'])
  })

  it('assigns the chosen technician and shows the updated ticket', async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ticket(), {
      'POST /api/tickets/t1/assign': [200, ticket({ assignedToUserId: adrian.id })],
    })
    await within(await workflow()).findByRole('option', { name: 'Adrian Rodriguez' })

    await user.click(screen.getByRole('button', { name: 'Assign' }))

    expect(await screen.findByRole('button', { name: 'Start work' })).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls.find(([url]) => url.endsWith('/assign'))!
    expect(JSON.parse(init!.body as string)).toEqual({ technicianId: adrian.id })
  })

  it("shows the API's reason when it refuses a step, and keeps the ticket as it was", async () => {
    const user = userEvent.setup()
    const resolved = ticket({
      assignedToUserId: adrian.id,
      status: TicketStatus.Resolved,
      resolutionSummary: 'Replaced the toner',
      resolvedAt: '2026-10-20T15:00:00+00:00',
    })
    renderPage(resolved, {
      'POST /api/tickets/t1/close': [
        409,
        { detail: 'Cannot close a ticket that is Closed. Tickets must be resolved before closing.' },
      ],
    })

    expect(await screen.findByText('Replaced the toner')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Close ticket' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot close a ticket that is Closed')
    expect(screen.getByText('Resolved', { selector: '.badge' })).toBeInTheDocument()
  })

  it('shows the history with names, notes and comments', async () => {
    renderPage(
      ticket({
        assignedToUserId: adrian.id,
        status: TicketStatus.Resolved,
        resolutionSummary: 'Replaced the toner',
        resolvedAt: '2026-10-20T15:00:00+00:00',
      }),
      {
        'GET /api/tickets/t1/activity': [
          200,
          [
            created,
            entry({ type: TicketActivityType.Assigned, actorUserId: adrian.id, userId: adrian.id }),
            entry({ type: TicketActivityType.Comment, actorUserId: adrian.id, userId: adrian.id, note: 'Toner is on order' }),
            entry({ type: TicketActivityType.Resolved, actorUserId: adrian.id, note: 'Replaced the toner' }),
          ],
        ],
      },
    )

    const activity = await screen.findByRole('region', { name: 'Activity' })
    expect(await within(activity).findByText('John Doe reported the ticket')).toBeInTheDocument()
    // Took it themselves, so no "by"; the steps say who took them.
    expect(within(activity).getByText('Assigned to Adrian Rodriguez')).toBeInTheDocument()
    expect(within(activity).getByText('Resolved by Adrian Rodriguez')).toBeInTheDocument()
    expect(within(activity).getByText('Toner is on order')).toBeInTheDocument()
    expect(within(activity).getAllByRole('listitem')).toHaveLength(4)
  })

  it('posts a comment as the signed-in user and adds it to the history', async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ticket(), {
      'POST /api/tickets/t1/comments': [
        201,
        entry({ type: TicketActivityType.Comment, userId: adrian.id, note: 'Coming by at 3' }),
      ],
    })
    const activity = await screen.findByRole('region', { name: 'Activity' })
    await within(activity).findByText('John Doe reported the ticket')

    expect(within(activity).queryByRole('combobox')).not.toBeInTheDocument()  // no "Comment as" picker
    await user.type(within(activity).getByRole('textbox'), 'Coming by at 3')
    await user.click(within(activity).getByRole('button', { name: 'Comment' }))

    expect(await within(activity).findByText('Coming by at 3')).toBeInTheDocument()
    expect(within(activity).getByRole('textbox')).toHaveValue('')
    const [, init] = fetchMock.mock.calls.find(([url]) => url.endsWith('/comments'))!
    expect(JSON.parse(init!.body as string)).toEqual({ text: 'Coming by at 3' })
  })

  it('reloads the history after a workflow step', async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ticket(), {
      'POST /api/tickets/t1/assign': [200, ticket({ assignedToUserId: adrian.id })],
    })
    await within(await workflow()).findByRole('option', { name: 'Adrian Rodriguez' })
    const activityCalls = () => fetchMock.mock.calls.filter(([url]) => url.endsWith('/activity')).length
    const before = activityCalls()

    await user.click(screen.getByRole('button', { name: 'Assign' }))

    await screen.findByRole('button', { name: 'Start work' })
    expect(activityCalls()).toBe(before + 1)
  })

  it('offers the reporter no steps on an open ticket, and Close once it is resolved', async () => {
    renderPage(ticket({ assignedToUserId: adrian.id }), {}, john)
    expect(await within(await workflow()).findByText('A technician is working on it.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Delete ticket' })).not.toBeInTheDocument()
  })

  it('lets the reporter close their resolved ticket', async () => {
    renderPage(ticket({ assignedToUserId: adrian.id, status: TicketStatus.Resolved }), {}, john)
    expect(await screen.findByRole('button', { name: 'Close ticket' })).toBeInTheDocument()
  })

  it('shows Delete to admins only', async () => {
    renderPage(ticket())
    await workflow()
    expect(screen.queryByRole('button', { name: 'Delete ticket' })).not.toBeInTheDocument()

    renderPage(ticket(), {}, ada)
    expect(await screen.findByRole('button', { name: 'Delete ticket' })).toBeInTheDocument()
  })

  it('says so when the ticket does not exist', async () => {
    fakeApi({ 'GET /api/users': [200, []] })
    render(signedInAs(adrian, page()))
    expect(await screen.findByRole('alert')).toHaveTextContent('does not exist or was deleted')
  })
})
