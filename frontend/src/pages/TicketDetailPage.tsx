import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, api, availableSteps, UserRole, type Ticket } from '../api'
import { PriorityBadge, StatusBadge } from '../components/Badges'
import { ErrorMessage } from '../components/ErrorMessage'
import { Timeline } from '../components/Timeline'
import { formatDateTime } from '../format'
import { useUsers } from '../useUsers'

export function TicketDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const { users, nameOf } = useUsers()

  const [ticket, setTicket] = useState<Ticket | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [stepError, setStepError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [stepsTaken, setStepsTaken] = useState(0)

  const [technicianId, setTechnicianId] = useState('')
  const [summary, setSummary] = useState('')

  useEffect(() => {
    api
      .getTicket(id)
      .then(setTicket)
      .catch((e: Error) =>
        setLoadError(
          e instanceof ApiError && e.status === 404
            ? 'This ticket does not exist or was deleted.'
            : e.message,
        ),
      )
  }, [id])

  // Runs one workflow step. The API's answer replaces the ticket on screen; a refusal (400/409)
  // shows its reason and leaves the ticket as it was, since the API saved nothing.
  async function run(step: () => Promise<Ticket>) {
    setBusy(true)
    setStepError(null)
    try {
      setTicket(await step())
      setSummary('')
      setStepsTaken((n) => n + 1)
    } catch (e) {
      setStepError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function remove() {
    if (!confirm('Delete this ticket? It will move to the Deleted list and can no longer be changed.')) return
    setBusy(true)
    try {
      await api.deleteTicket(id)
      navigate('/')
    } catch (e) {
      setStepError((e as Error).message)
      setBusy(false)
    }
  }

  if (loadError !== null) {
    return (
      <>
        <Link to="/" className="back">
          ← All tickets
        </Link>
        <ErrorMessage message={loadError} />
      </>
    )
  }

  if (ticket === null) return <p className="muted">Loading ticket…</p>

  const steps = availableSteps(ticket)
  const technicians = users.filter((u) => u.role === UserRole.Technician)
  const chosenTechnician =
    technicianId || ticket.assignedToUserId || technicians[0]?.id || ''

  const assign = (e: FormEvent) => {
    e.preventDefault()
    void run(() => api.assignTicket(ticket.id, chosenTechnician))
  }
  const resolve = (e: FormEvent) => {
    e.preventDefault()
    void run(() => api.resolveTicket(ticket.id, summary))
  }

  return (
    <>
      <Link to="/" className="back">
        ← All tickets
      </Link>

      <div className="detail">
        <div className="detail-main">
          <article className="panel">
            <div className="detail-head">
              <StatusBadge status={ticket.status} />
              <PriorityBadge priority={ticket.priority} />
            </div>
            <h1>{ticket.title}</h1>
            {ticket.description ? (
              <p className="description">{ticket.description}</p>
            ) : (
              <p className="muted">No description.</p>
            )}

            {ticket.resolutionSummary !== null && (
              <section className="resolution">
                <h2>Resolution</h2>
                <p>{ticket.resolutionSummary}</p>
              </section>
            )}

            <dl className="facts">
              <dt>Reported by</dt>
              <dd>{nameOf(ticket.createdByUserId)}</dd>
              <dt>Technician</dt>
              <dd>{nameOf(ticket.assignedToUserId) ?? 'Unassigned'}</dd>
              <dt>Created</dt>
              <dd>{formatDateTime(ticket.createdAt)}</dd>
              {ticket.resolvedAt !== null && (
                <>
                  <dt>Resolved</dt>
                  <dd>{formatDateTime(ticket.resolvedAt)}</dd>
                </>
              )}
            </dl>
          </article>

          <Timeline
            ticketId={ticket.id}
            refreshKey={stepsTaken}
            users={users}
            nameOf={nameOf}
            defaultAuthorId={ticket.createdByUserId}
          />
        </div>

        <aside className="panel workflow" aria-label="Workflow">
          <h2>Next steps</h2>
          <ErrorMessage message={stepError} />

          {steps.assign && (
            <form onSubmit={assign} className="form">
              <label>
                {ticket.assignedToUserId === null ? 'Assign a technician' : 'Reassign'}
                <select
                  value={chosenTechnician}
                  onChange={(e) => setTechnicianId(e.target.value)}
                >
                  {technicians.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
              </label>
              <button type="submit" disabled={busy || chosenTechnician === ''}>
                Assign
              </button>
            </form>
          )}

          {steps.start && (
            <button
              className="primary"
              disabled={busy}
              onClick={() => run(() => api.startWork(ticket.id))}
            >
              Start work
            </button>
          )}

          {steps.resolve && (
            <form onSubmit={resolve} className="form">
              <label>
                Resolution summary
                <textarea
                  value={summary}
                  onChange={(e) => setSummary(e.target.value)}
                  rows={3}
                  placeholder="What fixed it?"
                  required
                />
              </label>
              <button type="submit" className="primary" disabled={busy}>
                Resolve
              </button>
            </form>
          )}

          {steps.close && (
            <button
              className="primary"
              disabled={busy}
              onClick={() => run(() => api.closeTicket(ticket.id))}
            >
              Close ticket
            </button>
          )}

          {!steps.assign && !steps.start && !steps.resolve && !steps.close && (
            <p className="muted">This ticket is closed. Nothing left to do.</p>
          )}

          {steps.assign && ticket.assignedToUserId === null && (
            <p className="hint">Assign a technician to start or resolve this ticket.</p>
          )}

          <hr />
          <button className="danger" disabled={busy} onClick={remove}>
            Delete ticket
          </button>
        </aside>
      </div>
    </>
  )
}
