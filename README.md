# Food Journal

A React + TypeScript food journaling app with a .NET API. The interface carries forward the approved Dashboard, My Journal, Insights and Settings experience. Users can create accounts, log and edit meals and water, search/filter history, and view summaries of their entered nutrition data.

## Run locally

Requirements: Node.js 20+ and .NET 10 SDK.

```sh
# Terminal 1 — API
cd server
dotnet run --urls http://localhost:5080

# Terminal 2 — client
cd client
npm install
npm run dev
```

Open http://localhost:5173. Accounts and journal state are persisted in `server/bin/.../food-journal-data.json` (keep the server's data directory backed up; do not commit its data file). The API's in-memory bearer sessions reset when the server restarts; sign in again after a restart. For production, replace the demo session store and JSON repository with a vetted identity provider and durable database, configure HTTPS/CORS and secret management, and add rate limiting and password recovery. Photo selection remains disabled until a storage service is configured.

## Verify

```sh
cd client && npm test && npm run build
cd ../server && dotnet build
```
