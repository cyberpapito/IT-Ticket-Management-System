import { useState, type FormEvent } from 'react'
import { useAuth } from '../authContext'
import { ErrorMessage } from '../components/ErrorMessage'

export function SignInPage() {
  const { signIn } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await signIn(email, password)
    } catch (err) {
      setError((err as Error).message)
      setPassword('')
      setBusy(false)
    }
  }

  return (
    <main className="signin">
      <form className="panel form" onSubmit={submit} aria-label="Sign in">
        <h1>IT Tickets</h1>
        <p className="muted">Sign in to see and report tickets.</p>
        <label>
          Email
          <input
            type="email"
            autoComplete="username"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoFocus
          />
        </label>
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        <ErrorMessage message={error} />
        <div className="actions">
          <button type="submit" className="primary" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </div>
        <p className="hint">No account? Ask an admin to add you.</p>
      </form>
    </main>
  )
}
