import { Link, NavLink, Route, Routes } from 'react-router-dom'
import { TicketDetailPage } from './pages/TicketDetailPage'
import { TicketsPage } from './pages/TicketsPage'
import { UsersPage } from './pages/UsersPage'

export default function App() {
  return (
    <>
      <header className="topbar">
        <Link to="/" className="brand">
          IT Tickets
        </Link>
        <nav>
          <NavLink to="/" end>
            Tickets
          </NavLink>
          <NavLink to="/users">Users</NavLink>
        </nav>
      </header>

      <main>
        <Routes>
          <Route path="/" element={<TicketsPage />} />
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
          <Route path="/users" element={<UsersPage />} />
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
