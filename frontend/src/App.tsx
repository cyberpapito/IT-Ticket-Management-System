import { Link, NavLink, Route, Routes } from 'react-router-dom'
import { isStaff, roleLabels, UserRole } from './api'
import { useAuth } from './authContext'
import { DeletedTicketsPage } from './pages/DeletedTicketsPage'
import { SignInPage } from './pages/SignInPage'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'
import { UsersPage } from './pages/UsersPage'

// The pages each role gets. The API enforces the same rules; hiding a page only spares a 403.
export default function App() {
  const { me, signOut } = useAuth()
  if (me === null) return <SignInPage />

  const admin = me.role === UserRole.Admin

  return (
    <>
      <header className="topbar">
        <Link to="/" className="brand">
          IT Tickets
        </Link>
        <nav>
          <NavLink to="/" end>
            {isStaff(me) ? 'Tickets' : 'My tickets'}
          </NavLink>
          {admin && <NavLink to="/deleted">Deleted</NavLink>}
          {isStaff(me) && <NavLink to="/users">Users</NavLink>}
        </nav>
        <div className="whoami">
          <span>
            {me.name} <span className="hint">· {roleLabels[me.role]}</span>
          </span>
          <button type="button" onClick={() => void signOut()}>
            Sign out
          </button>
        </div>
      </header>

      <main>
        <Routes>
          <Route path="/" element={<TicketsPage />} />
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
          {admin && <Route path="/deleted" element={<DeletedTicketsPage />} />}
          {isStaff(me) && <Route path="/users" element={<UsersPage />} />}
          <Route
            path="*"
            element={
              <p className="empty">
                Page not found. <Link to="/">Back to tickets</Link>
              </p>
            }
          />
        </Routes>
      </main>
    </>
  )
}
