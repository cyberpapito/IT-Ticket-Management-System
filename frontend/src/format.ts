const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

export function formatDateTime(iso: string): string {
  return dateTime.format(new Date(iso))
}

// "Unknown (pre-users)" from SeedUsers: the creator of tickets made before users existed.
// It stays visible on those tickets but isn't offered as a choice for new ones.
export const UNKNOWN_USER_ID = 'a0000000-0000-0000-0000-000000000000'
