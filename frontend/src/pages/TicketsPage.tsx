import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, isStaff, priorityLabels, statusLabels, TicketPriority, TicketStatus, type Ticket } from '../api'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { ErrorMessage } from '../components/ErrorMessage'
import { NewTicketForm } from '../components/NewTicketForm'
import { formatDateTime } from '../format'
import { useMe } from '../authContext'
import { useUsers } from '../useUsers'

// "active" is everything not yet closed, the default view for a support queue
type StatusFilter = 'active' | 'all' | `${TicketStatus}`
type PriorityFilter = 'all' | `${TicketPriority}`

export function TicketsPage() {
  const navigate = useNavigate()
  const me = useMe()
  const { error: usersError, nameOf } = useUsers()

  const [tickets, setTickets] = useState<Ticket[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)

  const [statusFilter, setStatusFilter] = useState<StatusFilter>('active')
  const [priorityFilter, setPriorityFilter] = useState<PriorityFilter>('all')
  const [search, setSearch] = useState('')

  useEffect(() => {
    api
      .getTickets()
      .then(setTickets)
      .catch((e: Error) => setError(e.message))
  }, [])

  const shown = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (tickets ?? [])
      .filter((t) => {
        if (statusFilter === 'active') return t.status !== TicketStatus.Closed
        return statusFilter === 'all' || t.status === Number(statusFilter)
      })
      .filter((t) => priorityFilter === 'all' || t.priority === Number(priorityFilter))
      .filter(
        (t) =>
          term === '' ||
          t.title.toLowerCase().includes(term) ||
          t.description.toLowerCase().includes(term),
      )
      // Most urgent first, then newest
      .sort((a, b) => b.priority - a.priority || b.createdAt.localeCompare(a.createdAt))
  }, [tickets, statusFilter, priorityFilter, search])

  const counts = useMemo(() => {
    const byStatus = { 0: 0, 1: 0, 2: 0, 3: 0 } as Record<TicketStatus, number>
    for (const t of tickets ?? []) byStatus[t.status]++
    return byStatus
  }, [tickets])

  return (
    <>
      <div className="page-head">
        <h1>{isStaff(me) ? 'Tickets' : 'My tickets'}</h1>
        {!showForm && (
          <button className="primary" onClick={() => setShowForm(true)}>
            New ticket
          </button>
        )}
      </div>

      {showForm && (
        <NewTicketForm
          onCreated={(t) => navigate(`/tickets/${t.id}`)}
          onCancel={() => setShowForm(false)}
        />
      )}

      <ErrorMessage message={error ?? usersError} />

      {tickets !== null && (
        <ul className="summary" aria-label="Tickets by status">
          {Object.values(TicketStatus).map((s) => (
            <li key={s}>
              <button
                className={statusFilter === String(s) ? 'selected' : undefined}
                onClick={() => setStatusFilter(statusFilter === String(s) ? 'active' : `${s}`)}
              >
                <strong>{counts[s]}</strong> {statusLabels[s]}
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="filters">
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search title or description"
          aria-label="Search tickets"
        />
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
          aria-label="Status"
        >
          <option value="active">Not closed</option>
          <option value="all">Any status</option>
          {Object.values(TicketStatus).map((s) => (
            <option key={s} value={s}>
              {statusLabels[s]}
            </option>
          ))}
        </select>
        <select
          value={priorityFilter}
          onChange={(e) => setPriorityFilter(e.target.value as PriorityFilter)}
          aria-label="Priority"
        >
          <option value="all">Any priority</option>
          {Object.values(TicketPriority).map((p) => (
            <option key={p} value={p}>
              {priorityLabels[p]}
            </option>
          ))}
        </select>
      </div>

      {tickets === null && error === null && <p className="muted">Loading tickets…</p>}

      {tickets !== null && shown.length === 0 && (
        <p className="empty">
          {tickets.length === 0 ? 'No tickets yet.' : 'No tickets match these filters.'}
        </p>
      )}

      {shown.length > 0 && (
        <div className="table-wrap">
          <table className="tickets">
            <thead>
              <tr>
                <th>Title</th>
                <th>Status</th>
                <th>Priority</th>
                <th>Reported by</th>
                <th>Technician</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((t) => (
                <tr key={t.id}>
                  <td>
                    <Link to={`/tickets/${t.id}`}>{t.title}</Link>
                  </td>
                  <td>
                    <StatusBadge status={t.status} />
                  </td>
                  <td>
                    <PriorityBadge priority={t.priority} />
                  </td>
                  <td>{nameOf(t.createdByUserId)}</td>
                  <td>{nameOf(t.assignedToUserId) ?? <span className="muted">Unassigned</span>}</td>
                  <td className="nowrap">{formatDateTime(t.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
