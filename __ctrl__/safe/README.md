# safe/ — keys, addresses, prod env (local only)

| Pattern | Purpose |
|---------|---------|
| `*-privatekey.pem` | SSH private key |
| `*-address.txt` | VM IP / hostname (first line) |
| `*-env.env` | Production secrets → uploaded as `~/projects/dotnet-svelte/.env` |

| Files | Server id |
|-------|-----------|
| `ar-dotnet-svelte-bamdad-*` | `dotnet-svelte` |

Copy the `*.example` stubs, drop the `.example` suffix, and fill real values.

`*.pem`, `*.env`, `*-address.txt` are gitignored.

Upload env to VM:

```bat
dotnet-svelte-ctrl.bat env
```

That copies `safe/ar-dotnet-svelte-bamdad-env.env` → `~/projects/dotnet-svelte/.env`.
