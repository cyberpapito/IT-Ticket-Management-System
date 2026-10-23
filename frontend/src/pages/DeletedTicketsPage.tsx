import { useEffect, useState } from 'react'
import { api, type Ticket } from '../api'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { ErrorMessage } from '../components/ErrorMessage'
import { formatDateTime } from '../format'
import { useUsers } from '../useUsers'

// The audit history: soft-deleted tickets as they stood when deleted, newest deletion first (the
// API's order). Read-only, and the titles aren't links, because the API treats a deleted ticket
// as gone (GET /api/tickets/{id} is a 404) and there is no undelete.
export function DeletedTicketsPage() {
  const { error: usersError, nameOf } = useUsers()
  const [tickets, setTickets] = useState<Ticket[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api
      .getDeletedTickets()
      .then(setTickets)
      .catch((e: Error) => setError(e.message))
  }, [])

  return (
    <>
      <div className="page-head">
        <h1>Deleted tickets</h1>
      </div>
      <p className="muted">
        Deleting a ticket only stamps when it happened, so its record stays here for the audit
        trail. The status is where it was in its workflow when it was deleted.
      </p>

      <ErrorMessage message={error ?? usersError} />

      {tickets === null && error === null && <p className="muted">Loading deleted tickets…</p>}

      {tickets !== null && tickets.length === 0 && (
        <p className="empty">No tickets have been deleted.</p>
      )}

      {tickets !== null && tickets.length > 0 && (
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
                <th>Deleted</th>
              </tr>
            </thead>
            <tbody>
              {tickets.map((t) => (
                <tr key={t.id}>
                  <td>
                    {t.title}
                    {t.resolutionSummary && (
                      <div className="hint">Resolution: {t.resolutionSummary}</div>
                    )}
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
                  <td className="nowrap">{t.deletedAt && formatDateTime(t.deletedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  )
}
