import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { TicketStatus, type Ticket } from '../api'
import { adrian, fakeApi, john, ticket } from '../test/fakeApi'
import { DeletedTicketsPage } from './DeletedTicketsPage'

function renderPage(deleted: Ticket[]) {
  fakeApi({ 'GET /api/tickets/deleted': [200, deleted], 'GET /api/users': [200, [adrian, john]] })
  render(
    <MemoryRouter>
      <DeletedTicketsPage />
    </MemoryRouter>,
  )
}

describe('DeletedTicketsPage', () => {
  it("lists deleted tickets in the API's order, with names, the resolution and no links", async () => {
    renderPage([
      ticket({
        id: 'b',
        title: 'Old laptop',
        status: TicketStatus.Resolved,
        assignedToUserId: adrian.id,
        resolutionSummary: 'Replaced the battery',
        deletedAt: '2026-10-21T15:00:00+00:00',
      }),
      ticket({ id: 'a', title: 'Duplicate of VPN ticket', deletedAt: '2026-10-21T09:00:00+00:00' }),
    ])

    expect(await screen.findByText('Old laptop')).toBeInTheDocument()
    const rows = screen.getAllByRole('row').slice(1)
    expect(rows.map((r) => within(r).getAllByRole('cell')[0].firstChild?.textContent)).toEqual([
      'Old laptop',
      'Duplicate of VPN ticket',
    ])
    expect(within(rows[0]).getByText('Resolution: Replaced the battery')).toBeInTheDocument()
    expect(within(rows[0]).getByText('Resolved')).toBeInTheDocument()
    expect(await within(rows[0]).findByText('Adrian Rodriguez')).toBeInTheDocument()
    expect(within(rows[1]).getByText('Unassigned')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('says so when nothing has been deleted', async () => {
    renderPage([])

    expect(await screen.findByText('No tickets have been deleted.')).toBeInTheDocument()
  })
})
