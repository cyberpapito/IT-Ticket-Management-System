import { useEffect, useState, type FormEvent } from 'react'
import {
  api,
  COMMENT_MAX_LENGTH,
  TicketActivityType,
  type TicketActivity,
} from '../api'
import { formatDateTime } from '../format'
import { ErrorMessage } from './ErrorMessage'

interface TimelineProps {
  ticketId: string
  // Bumped by the page after each workflow step, so the new entry shows up.
  refreshKey: number
  nameOf: (id: string | null) => string | null
}

// "by Adrian Rodriguez" for steps someone took; nothing on entries from before sign-in, which
// didn't record who did them.
function by(a: TicketActivity, nameOf: TimelineProps['nameOf']): string {
  const name = nameOf(a.actorUserId)
  return name === null ? '' : ` by ${name}`
}

function describe(a: TicketActivity, nameOf: TimelineProps['nameOf']): string {
  switch (a.type) {
    case TicketActivityType.Created:
      return `${nameOf(a.userId) ?? 'Someone'} reported the ticket`
    case TicketActivityType.Assigned:
      // Say who assigned only when it wasn't the technician taking it themselves.
      return `Assigned to ${nameOf(a.userId) ?? 'a technician'}${a.actorUserId === a.userId ? '' : by(a, nameOf)}`
    case TicketActivityType.Started:
      return `Work started${by(a, nameOf)}`
    case TicketActivityType.Resolved:
      return `Resolved${by(a, nameOf)}`
    case TicketActivityType.Closed:
      return `Closed${by(a, nameOf)}`
    case TicketActivityType.Deleted:
      return `Deleted${by(a, nameOf)}`
    case TicketActivityType.Comment:
      return nameOf(a.userId) ?? 'Someone'
  }
}

const kind: Record<TicketActivityType, string> = {
  0: 'created',
  1: 'assigned',
  2: 'started',
  3: 'resolved',
  4: 'closed',
  5: 'deleted',
  6: 'comment',
}

// The comment form posts as the signed-in user.
export function Timeline({ ticketId, refreshKey, nameOf }: TimelineProps) {
  const [entries, setEntries] = useState<TicketActivity[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [commentError, setCommentError] = useState<string | null>(null)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api
      .getActivity(ticketId)
      .then((list) => {
        setEntries(list)
        setLoadError(null)
      })
      .catch((e: Error) => setLoadError(e.message))
  }, [ticketId, refreshKey])

  async function addComment(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setCommentError(null)
    try {
      const comment = await api.addComment(ticketId, text)
      setEntries((list) => [...(list ?? []), comment])
      setText('')
    } catch (err) {
      setCommentError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="panel timeline" aria-label="Activity">
      <h2>Activity</h2>
      <ErrorMessage message={loadError} />

      {entries === null && loadError === null && <p className="muted">Loading activity…</p>}

      {entries !== null && entries.length === 0 && (
        <p className="muted">No history recorded for this ticket.</p>
      )}

      {entries !== null && entries.length > 0 && (
        <ol className="events">
          {entries.map((a) => (
            <li key={a.id} className={`event ${kind[a.type]}`}>
              <div className="event-head">
                <strong>{describe(a, nameOf)}</strong>
                <time dateTime={a.at}>{formatDateTime(a.at)}</time>
              </div>
              {a.note !== null && <p className="event-note">{a.note}</p>}
            </li>
          ))}
        </ol>
      )}

      <form onSubmit={addComment} className="form comment-form">
        <label>
          Add a comment
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            rows={3}
            maxLength={COMMENT_MAX_LENGTH}
            placeholder="Ask for details, post an update…"
            required
          />
        </label>
        <div className="actions">
          <button type="submit" className="primary" disabled={busy}>
            Comment
          </button>
        </div>
        <ErrorMessage message={commentError} />
      </form>
    </section>
  )
}
