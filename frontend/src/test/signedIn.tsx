import type { ReactElement } from 'react'
import type { User } from '../api'
import { AuthContext } from '../authContext'

// A page as this user sees it, without going through the sign-in page.
export function signedInAs(me: User, ui: ReactElement, signOut = async () => {}) {
  return <AuthContext.Provider value={{ me, signIn: async () => {}, signOut }}>{ui}</AuthContext.Provider>
}
