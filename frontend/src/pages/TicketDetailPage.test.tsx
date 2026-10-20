import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { TicketStatus, type Ticket } from '../api'
import { adrian, fakeApi, john, ticket, type Routes as ApiRoutes } from '../test/fakeApi'
import { TicketDetailPage } from './TicketDetailPage'

function renderPage(t: Ticket, routes: ApiRoutes = {}) {
  const fetchMock = fakeApi({
    'GET /api/tickets/t1': [200, t],
    'GET /api/users': [200, [adrian, john]],
    ...routes,
  })
  render(
    <MemoryRouter initialEntries={['/tickets/t1']}>
      <Routes>
        <Route path="/tickets/:id" element={<TicketDetailPage />} />
      </Routes>
    </MemoryRouter>,
  )
  return fetchMock
}

describe('TicketDetailPage', () => {
  it('only offers assignment, to technicians, on an unassigned ticket', async () => {
    renderPage(ticket())

    expect(await screen.findByRole('heading', { name: 'Printer offline' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assign' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Start work' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Resolve' })).not.toBeInTheDocument()

    const options = await screen.findAllByRole('option')
    expect(options.map((o) => o.textContent)).toEqual(['Adrian Rodriguez'])
  })

  it('assigns the chosen technician and shows the updated ticket', async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ticket(), {
      'POST /api/tickets/t1/assign': [200, ticket({ assignedToUserId: adrian.id })],
    })
    await screen.findByRole('option', { name: 'Adrian Rodriguez' })

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

  it('says so when the ticket does not exist', async () => {
    fakeApi({ 'GET /api/users': [200, []] })
    render(
      <MemoryRouter initialEntries={['/tickets/t1']}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>,
    )
    expect(await screen.findByRole('alert')).toHaveTextContent('does not exist or was deleted')
  })
})
