import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { resetSession } from '../api'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  resetSession()
})
