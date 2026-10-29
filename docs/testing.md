# Testing

`test backend` runs `dotnet test backend` from the kit root.

xUnit tests live in `tests/backend/`. `backend/*.sln` includes that project, so `dotnet test backend` runs them.

The tests inject in-memory repositories, cache, and job queue, so they need neither Postgres nor Redis. They cover auth, the superuser routes, notes isolation and caching, and the local-only private routes.
