import { useCallback, useEffect, useState } from 'react'
import { api, type User } from './api'

// Every user, for showing names in place of the ids tickets carry.
export function useUsers() {
  const [users, setUsers] = useState<User[]>([])
  const [error, setError] = useState<string | null>(null)

  const reload = useCallback(
    () =>
      api
        .getUsers()
        .then((list) => {
          setUsers(list)
          setError(null)
        })
        .catch((e: Error) => setError(e.message)),
    [],
  )

  useEffect(() => {
    reload()
  }, [reload])

  const nameOf = useCallback(
    (id: string | null) => {
      if (id === null) return null
      return users.find((u) => u.id === id)?.name ?? 'Unknown user'
    },
    [users],
  )

  return { users, error, reload, nameOf }
}
