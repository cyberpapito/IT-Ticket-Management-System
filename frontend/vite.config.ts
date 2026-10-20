/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API (dotnet run --launch-profile http) listens on 5137. The dev server forwards /api
// to it, so the browser only ever talks to one origin and the API needs no CORS setup.
const apiUrl = process.env.API_URL ?? 'http://localhost:5137'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': apiUrl,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
