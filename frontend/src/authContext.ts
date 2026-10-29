import { createContext, useContext } from 'react'
import type { User } from './api'

// The session, as the pages see it. AuthProvider (auth.tsx) fills it in.
export interface Auth {
  // Who is signed in; null on the sign-in page.
  me: User | null
  signIn: (email: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}

export const AuthContext = createContext<Auth | null>(null)

export function useAuth(): Auth {
  const auth = useContext(AuthContext)
  if (auth === null) throw new Error('useAuth needs an <AuthProvider> (or an AuthContext value in tests)')
  return auth
}

// The signed-in user, for pages that only render once someone is.
export function useMe(): User {
  const { me } = useAuth()
  if (me === null) throw new Error('useMe used on a page shown before sign-in')
  return me
}
