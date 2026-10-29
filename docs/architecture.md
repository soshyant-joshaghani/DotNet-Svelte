# Architecture

DotNet-Svelte follows the FoxG folder contract. The Svelte site lives in `frontend/`. It is a copy of Fast-Svelte's UI.

```text
dotnet-svelte/
├── frontend/
├── backend/                 `backend/src/modules/{apps,base,system}`, `backend/src/core`, and `backend/worker`
├── tests/
├── traefik/
├── docs/
├── __plans__/
└── __ctrl__/                Python CLI
```

Request flow: Router (minimal API group) → Service → Repository (interface, EF Core) → PostgreSQL. The API is `backend/src`. The worker is `backend/worker` and reuses the API's `core`.

Routes speak the [wire contract](../../../../CONTRACT.md): `/api/v1`, `snake_case` JSON, `{"detail": "..."}` errors, form login, JWT HS256. Data is PostgreSQL with the Fast schema. Redis is the cache and the job queue. Both degrade softly: a missing Redis never fails a request.
