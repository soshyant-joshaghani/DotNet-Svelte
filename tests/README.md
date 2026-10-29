# Tests

```bat
__ctrl__\dotnet-svelte-ctrl.bat test all
__ctrl__\dotnet-svelte-ctrl.bat test backend
__ctrl__\dotnet-svelte-ctrl.bat test frontend
```

`test backend` runs `dotnet test backend` from the kit root. xUnit tests live in `tests/backend/`. `backend/*.sln` includes that project, so `dotnet test backend` runs them.

The backend tests use in-memory fakes for the repositories, the cache, and the job queue, so they need no database. `tests/frontend` holds the Vitest suite shared with Fast-Svelte.
