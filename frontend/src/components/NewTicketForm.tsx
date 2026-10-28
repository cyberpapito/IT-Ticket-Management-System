import { useState, type FormEvent } from 'react'
import {
  api,
  DESCRIPTION_MAX_LENGTH,
  priorityLabels,
  TicketPriority,
  TITLE_MAX_LENGTH,
  type Ticket,
} from '../api'
import { ErrorMessage } from './ErrorMessage'

interface Props {
  onCreated: (ticket: Ticket) => void
  onCancel: () => void
}

// The reporter is whoever is signed in; the API takes it from the session.
export function NewTicketForm({ onCreated, onCancel }: Props) {
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState<TicketPriority>(TicketPriority.Medium)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function submit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      onCreated(await api.createTicket({ title, description, priority }))
    } catch (err) {
      setError((err as Error).message)
      setSaving(false)
    }
  }

  return (
    <form className="panel form" onSubmit={submit} aria-label="New ticket">
      <h2>New ticket</h2>

      <label>
        Title
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          maxLength={TITLE_MAX_LENGTH}
          placeholder="e.g. Printer on floor 2 is offline"
          required
          autoFocus
        />
      </label>

      <label>
        Description
        <textarea
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          maxLength={DESCRIPTION_MAX_LENGTH}
          rows={4}
          placeholder="What happened, and what have you tried?"
        />
        <span className="hint">
          {description.length} / {DESCRIPTION_MAX_LENGTH}
        </span>
      </label>

      <div className="row">
        <label>
          Priority
          <select
            value={priority}
            onChange={(e) => setPriority(Number(e.target.value) as TicketPriority)}
          >
            {Object.values(TicketPriority).map((p) => (
              <option key={p} value={p}>
                {priorityLabels[p]}
              </option>
            ))}
          </select>
        </label>
      </div>

      <ErrorMessage message={error} />

      <div className="actions">
        <button type="submit" className="primary" disabled={saving}>
          {saving ? 'Creating…' : 'Create ticket'}
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  )
}
