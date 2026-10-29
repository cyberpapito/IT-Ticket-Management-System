import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { ApiError, api, whenSignedOut, type User } from './api'
import { AuthContext } from './authContext'

// Asks the API who is signed in (the cookie survives a reload), then keeps that answer current:
// signing in or out, or any later 401 (the session expired or an admin reset the password).
export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<User | null>(null)
  const [checked, setChecked] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    whenSignedOut(() => setMe(null))
    api
      .me()
      .then(setMe)
      .catch((e: Error) => {
        if (!(e instanceof ApiError && e.status === 401)) setError(e.message)
      })
      .finally(() => setChecked(true))
    return () => whenSignedOut(null)
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    setMe(await api.login(email, password))
  }, [])

  const signOut = useCallback(async () => {
    try {
      await api.logout()
    } finally {
      setMe(null)
    }
  }, [])

  if (!checked) return <p className="muted center">Loading…</p>
  if (error !== null) return <p className="error center">{error}</p>

  return <AuthContext.Provider value={{ me, signIn, signOut }}>{children}</AuthContext.Provider>
}
