import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { TicketStatus } from '../api'
import { adrian, fakeApi, john, ticket } from '../test/fakeApi'
import { signedInAs } from '../test/signedIn'
import { TicketsPage } from './TicketsPage'

const tickets = [
  ticket({ id: 'a', title: 'Printer offline', assignedToUserId: adrian.id }),
  ticket({ id: 'b', title: 'VPN drops', priority: 3, description: 'Every hour' }),
  ticket({ id: 'c', title: 'Old laptop', status: TicketStatus.Closed, assignedToUserId: adrian.id }),
]

function renderPage() {
  fakeApi({ 'GET /api/tickets': [200, tickets], 'GET /api/users': [200, [adrian, john]] })
  render(signedInAs(adrian, <MemoryRouter><TicketsPage /></MemoryRouter>))
}

const titles = () =>
  screen
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getByRole('link').textContent)

describe('TicketsPage', () => {
  it('lists tickets that are not closed, most urgent first, with user names', async () => {
    renderPage()

    expect(await screen.findByText('VPN drops')).toBeInTheDocument()
    expect(titles()).toEqual(['VPN drops', 'Printer offline'])

    const printerRow = screen.getByText('Printer offline').closest('tr')!
    expect(await within(printerRow).findByText('Adrian Rodriguez')).toBeInTheDocument()
    expect(within(printerRow).getByText('John Doe')).toBeInTheDocument()
  })

  it('shows closed tickets when asked, and filters by search', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('VPN drops')

    await user.selectOptions(screen.getByLabelText('Status'), 'all')
    expect(titles()).toHaveLength(3)

    await user.type(screen.getByLabelText('Search tickets'), 'every hour')
    expect(titles()).toEqual(['VPN drops'])

    await user.clear(screen.getByLabelText('Search tickets'))
    await user.type(screen.getByLabelText('Search tickets'), 'nothing like this')
    expect(screen.getByText('No tickets match these filters.')).toBeInTheDocument()
  })
})
