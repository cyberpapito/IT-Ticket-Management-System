import { useState, type FormEvent } from 'react'
import {
  api,
  isStaff,
  NAME_MAX_LENGTH,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  roleLabels,
  UserRole,
  type User,
} from '../api'
import { useMe } from '../authContext'
import { ErrorMessage } from '../components/ErrorMessage'
import { formatDateTime } from '../format'
import { useUsers } from '../useUsers'

// Technicians see the list; admins also add users, change roles and reset passwords (the API
// allows those to admins only).
export function UsersPage() {
  const me = useMe()
  const admin = me.role === UserRole.Admin
  const { users, error: loadError, reload } = useUsers()
  const [rowError, setRowError] = useState<string | null>(null)
  const [resettingId, setResettingId] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  async function changeRole(user: User, role: UserRole) {
    setRowError(null)
    setNotice(null)
    try {
      await api.changeRole(user.id, role)
      await reload()
      setNotice(`${user.name}'s role is now ${roleLabels[role]}. Their open sessions were signed out.`)
    } catch (err) {
      setRowError((err as Error).message)
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>Users</h1>
      </div>

      <ErrorMessage message={loadError ?? rowError} />
      {notice !== null && <p className="notice">{notice}</p>}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Added</th>
              {admin && <th>Password</th>}
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.name}</td>
                <td>{u.email}</td>
                <td>
                  {admin ? (
                    <select
                      aria-label={`Role for ${u.name}`}
                      value={u.role}
                      onChange={(e) => void changeRole(u, Number(e.target.value) as UserRole)}
                    >
                      {Object.values(UserRole).map((r) => (
                        <option key={r} value={r}>
                          {roleLabels[r]}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <span className={isStaff(u) ? 'badge role-tech' : 'badge'}>{roleLabels[u.role]}</span>
                  )}
                </td>
                <td className="nowrap">{formatDateTime(u.createdAt)}</td>
                {admin && (
                  <td>
                    {resettingId === u.id ? (
                      <ResetPassword
                        user={u}
                        onDone={(message) => {
                          setResettingId(null)
                          setNotice(message)
                        }}
                      />
                    ) : (
                      <button type="button" onClick={() => setResettingId(u.id)}>
                        Set password
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {admin && <AddUserForm onAdded={reload} />}
    </>
  )
}

function ResetPassword({ user, onDone }: { user: User; onDone: (message: string | null) => void }) {
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await api.setPassword(user.id, password)
      onDone(`New password set for ${user.name}. Their open sessions were signed out.`)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <form className="inline-form" onSubmit={submit} aria-label={`New password for ${user.name}`}>
      <input
        type="password"
        autoComplete="new-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        minLength={PASSWORD_MIN_LENGTH}
        maxLength={PASSWORD_MAX_LENGTH}
        placeholder={`At least ${PASSWORD_MIN_LENGTH} characters`}
        required
        autoFocus
      />
      <button type="submit" className="primary">
        Save
      </button>
      <button type="button" onClick={() => onDone(null)}>
        Cancel
      </button>
      <ErrorMessage message={error} />
    </form>
  )
}

function AddUserForm({ onAdded }: { onAdded: () => Promise<void> }) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>(UserRole.User)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      await api.createUser({ name, email, role, password })
      setName('')
      setEmail('')
      setRole(UserRole.User)
      setPassword('')
      await onAdded()
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setSaving(false)
    }
  }

  return (
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
        <span className="hint">Technicians and admins can be assigned tickets; admins also manage users.</span>
      </label>
      <label>
        Password
        <input
          type="password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          minLength={PASSWORD_MIN_LENGTH}
          maxLength={PASSWORD_MAX_LENGTH}
          required
        />
        <span className="hint">At least {PASSWORD_MIN_LENGTH} characters. Give it to them privately.</span>
      </label>
      <ErrorMessage message={error} />
      <div className="actions">
        <button type="submit" className="primary" disabled={saving}>
          {saving ? 'Adding…' : 'Add user'}
        </button>
      </div>
    </form>
  )
}
