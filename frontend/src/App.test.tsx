import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import App from './App'
import { AuthProvider } from './auth'
import { ada, adrian, fakeApi, john, type Routes } from './test/fakeApi'

function renderApp(routes: Routes) {
  const fetchMock = fakeApi({
    'GET /api/tickets': [200, []],
    'GET /api/users': [200, [ada, adrian, john]],
    ...routes,
  })
  render(
    <MemoryRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  )
  return fetchMock
}

const nav = () => screen.getByRole('navigation')

describe('sign-in', () => {
  it('shows the sign-in page when nobody is signed in, then the app once they are', async () => {
    const user = userEvent.setup()
    const fetchMock = renderApp({ 'GET /api/auth/me': [401], 'POST /api/auth/login': [200, john] })

    const form = await screen.findByRole('form', { name: 'Sign in' })
    await user.type(within(form).getByLabelText('Email'), 'john.doe@example.com')
    await user.type(within(form).getByLabelText('Password'), 'correct horse battery')
    await user.click(within(form).getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { name: 'My tickets' })).toBeInTheDocument()
    expect(screen.getByText('John Doe')).toBeInTheDocument()
    const [, init] = fetchMock.mock.calls.find(([url]) => url === '/api/auth/login')!
    expect(JSON.parse(init!.body as string)).toEqual({ email: 'john.doe@example.com', password: 'correct horse battery' })
  })

  it("shows the API's reason for a wrong password and clears the field", async () => {
    const user = userEvent.setup()
    renderApp({
      'GET /api/auth/me': [401],
      'POST /api/auth/login': [401, { title: 'Sign-in failed', detail: 'Wrong email or password.' }],
    })

    const form = await screen.findByRole('form', { name: 'Sign in' })
    await user.type(within(form).getByLabelText('Email'), 'john.doe@example.com')
    await user.type(within(form).getByLabelText('Password'), 'nope nope nope')
    await user.click(within(form).getByRole('button', { name: 'Sign in' }))

    expect(await within(form).findByRole('alert')).toHaveTextContent('Wrong email or password.')
    expect(within(form).getByLabelText('Password')).toHaveValue('')
  })

  it('keeps a signed-in user signed in across a reload, and signs out', async () => {
    const user = userEvent.setup()
    renderApp({ 'GET /api/auth/me': [200, adrian], 'POST /api/auth/logout': [204] })

    expect(await screen.findByRole('heading', { name: 'Tickets' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Sign out' }))

    expect(await screen.findByRole('form', { name: 'Sign in' })).toBeInTheDocument()
  })
})

describe('navigation by role', () => {
  it('gives a plain user only their tickets', async () => {
    renderApp({ 'GET /api/auth/me': [200, john] })
    await screen.findByRole('heading', { name: 'My tickets' })
    expect(within(nav()).getAllByRole('link').map((a) => a.textContent)).toEqual(['My tickets'])
  })

  it('gives a technician the user list but not the deleted tickets', async () => {
    renderApp({ 'GET /api/auth/me': [200, adrian] })
    await screen.findByRole('heading', { name: 'Tickets' })
    expect(within(nav()).getAllByRole('link').map((a) => a.textContent)).toEqual(['Tickets', 'Users'])
  })

  it('gives an admin everything', async () => {
    renderApp({ 'GET /api/auth/me': [200, ada] })
    await screen.findByRole('heading', { name: 'Tickets' })
    expect(within(nav()).getAllByRole('link').map((a) => a.textContent)).toEqual(['Tickets', 'Deleted', 'Users'])
  })
})
