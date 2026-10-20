import { priorityLabels, statusLabels, type TicketPriority, type TicketStatus } from '../api'

const statusClass: Record<TicketStatus, string> = {
  0: 'open',
  1: 'in-progress',
  2: 'resolved',
  3: 'closed',
}

const priorityClass: Record<TicketPriority, string> = {
  0: 'low',
  1: 'medium',
  2: 'high',
  3: 'critical',
}

export function StatusBadge({ status }: { status: TicketStatus }) {
  return <span className={`badge status-${statusClass[status]}`}>{statusLabels[status]}</span>
}

export function PriorityBadge({ priority }: { priority: TicketPriority }) {
  return (
    <span className={`priority priority-${priorityClass[priority]}`}>
      {priorityLabels[priority]}
    </span>
  )
}
