# Frontend

React + TypeScript (Vite) UI for the ticket API. See the main [README](../README.md#frontend) for what it does.

```bash
npm install
npm run dev        # http://localhost:5173, with /api forwarded to the API on :5137
npm test           # Vitest + Testing Library, against a faked fetch
npm run lint       # oxlint
npm run build      # type-check, then build into dist/
```

Start the API first (`dotnet run --launch-profile http` in `backend/TicketSystem`). To point the dev server at an API elsewhere, set `API_URL`, e.g. `API_URL=http://localhost:5000 npm run dev`.
