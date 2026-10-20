import { useState, type FormEvent } from 'react'
import { api, NAME_MAX_LENGTH, roleLabels, UserRole } from '../api'
import { ErrorMessage } from '../components/ErrorMessage'
import { formatDateTime } from '../format'
import { useUsers } from '../useUsers'

export function UsersPage() {
  const { users, error: loadError, reload } = useUsers()

  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>(UserRole.User)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createUser({ name, email, role })
      setName('')
      setEmail('')
      setRole(UserRole.User)
      await reload()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Users</h1>
      </div>

      <ErrorMessage message={loadError} />

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Added</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.name}</td>
                <td>{u.email}</td>
                <td>
                  <span className={u.role === UserRole.Technician ? 'badge role-tech' : 'badge'}>
                    {roleLabels[u.role]}
                  </span>
                </td>
                <td className="nowrap">{formatDateTime(u.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <form className="panel form narrow" onSubmit={submit} aria-label="Add user">
        <h2>Add a user</h2>
        <label>
          Name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={NAME_MAX_LENGTH}
            required
          />
        </label>
        <label>
          Email
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>
        <label>
          Role
          <select value={role} onChange={(e) => setRole(Number(e.target.value) as UserRole)}>
            {Object.values(UserRole).map((r) => (
              <option key={r} value={r}>
                {roleLabels[r]}
              </option>
            ))}
          </select>
          <span className="hint">Only technicians can be assigned tickets.</span>
        </label>
        <ErrorMessage message={error} />
        <div className="actions">
          <button type="submit" className="primary" disabled={saving}>
            {saving ? 'Adding…' : 'Add user'}
          </button>
        </div>
      </form>
    </>
  )
}
