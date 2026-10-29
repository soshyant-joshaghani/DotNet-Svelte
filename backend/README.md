# DotnetSvelte backend

ASP.NET Core (net10.0) implementation of the FoxG wire contract (`foxg-kit/CONTRACT.md`).

```text
src/core/                    config, db (EF Core + SQL migrator), cache, jobs, security, errors, openapi
src/modules/apps/sample/     notes CRUD with Redis read-cache
src/modules/base/auth|users/ login, /me, admin user management
src/modules/system/          health-check, private (local only)
worker/                      BRPOP worker for the foxg:jobs Redis list
migrations/                  plain SQL, applied at startup into schema_migrations
```

Flow: router (minimal API group) -> service -> repository (interface, EF Core implementation) -> Postgres.

```bash
dotnet run --project backend/src        # API on APP_HOST:APP_PORT (default 0.0.0.0:8000)
dotnet run --project backend/worker     # job worker
dotnet test backend/DotnetSvelte.sln    # tests use in-memory fakes, no Postgres/Redis needed
```

Configuration comes from environment variables, then the first `.env` found in `.`, `..`, `../..`.
Docs: `/docs` (Swagger UI), `/sdoc` (Scalar), `/api/v1/openapi.json`.

Swap a store by registering another implementation of `IUserRepository`, `INoteRepository`, `ICache` or `IJobQueue` in `Bootstrap.AddFoxg`.
