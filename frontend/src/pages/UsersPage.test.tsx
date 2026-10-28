import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { UserRole, type User } from '../api'
import { ada, adrian, CSRF_TOKEN, fakeApi, john, type Routes } from '../test/fakeApi'
import { signedInAs } from '../test/signedIn'
import { UsersPage } from './UsersPage'

function renderPage(me: User, routes: Routes = {}) {
  const fetchMock = fakeApi({ 'GET /api/users': [200, [ada, adrian, john]], ...routes })
  render(signedInAs(me, <MemoryRouter><UsersPage /></MemoryRouter>))
  return fetchMock
}

const body = (fetchMock: ReturnType<typeof fakeApi>, key: string) => {
  const [, init] = fetchMock.mock.calls.find(([url, i]) => `${i?.method ?? 'GET'} ${url}` === key)!
  return { json: JSON.parse(init!.body as string), headers: init!.headers }
}

describe('UsersPage', () => {
  it('shows a technician the list only', async () => {
    renderPage(adrian)

    expect(await screen.findByText('john.doe@example.com')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Add user' })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Set password' })).not.toBeInTheDocument()
  })

  it('lets an admin add a user with a password', async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ada, { 'POST /api/users': [201, { ...john, id: 'new', name: 'Jane Smith' }] })

    const form = await screen.findByRole('form', { name: 'Add user' })
    await user.type(within(form).getByLabelText('Name'), 'Jane Smith')
    await user.type(within(form).getByLabelText('Email'), 'jane@example.com')
    await user.selectOptions(within(form).getByLabelText(/^Role/), 'Technician')
    await user.type(within(form).getByLabelText(/^Password/), 'a long enough password')
    await user.click(within(form).getByRole('button', { name: 'Add user' }))

    await screen.findByText('john.doe@example.com')
    const sent = body(fetchMock, 'POST /api/users')
    expect(sent.json).toEqual({
      name: 'Jane Smith', email: 'jane@example.com', role: UserRole.Technician, password: 'a long enough password',
    })
    expect(sent.headers).toEqual({ 'Content-Type': 'application/json', 'X-XSRF-TOKEN': CSRF_TOKEN })
  })

  it("lets an admin change a role and shows the API's refusal", async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ada, {
      'PUT /api/users/a0000000-0000-0000-0000-000000000002/role': [200, { ...john, role: UserRole.Technician }],
      [`PUT /api/users/${ada.id}/role`]: [409, { detail: 'This is the only admin who can sign in. Make someone else an admin first.' }],
    })

    await user.selectOptions(await screen.findByLabelText('Role for John Doe'), 'Technician')
    expect(await screen.findByText(/John Doe's role is now Technician/)).toBeInTheDocument()
    expect(body(fetchMock, `PUT /api/users/${john.id}/role`).json).toEqual({ role: UserRole.Technician })

    await user.selectOptions(screen.getByLabelText('Role for Ada Admin'), 'User')
    expect(await screen.findByRole('alert')).toHaveTextContent('This is the only admin who can sign in.')
  })

  it("lets an admin set someone's password", async () => {
    const user = userEvent.setup()
    const fetchMock = renderPage(ada, { [`PUT /api/users/${john.id}/password`]: [204] })

    const row = (await screen.findByText('john.doe@example.com')).closest('tr')!
    await user.click(within(row).getByRole('button', { name: 'Set password' }))
    await user.type(within(row).getByPlaceholderText(/At least 12 characters/), 'a brand new passphrase')
    await user.click(within(row).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText(/New password set for John Doe/)).toBeInTheDocument()
    expect(body(fetchMock, `PUT /api/users/${john.id}/password`).json).toEqual({ password: 'a brand new passphrase' })
  })
})
