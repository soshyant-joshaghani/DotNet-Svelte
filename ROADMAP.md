# Roadmap

DotNet-Svelte is the enterprise-tier DotNet launchpad of the FoxG family. It stays aligned with the folder contract and the wire contract in [foxg-kit](../../../README.md).

- Keep `apps/sample`, `base`, and `system` in step with Fast-Svelte
- Keep the wire contract in [CONTRACT.md](../../../CONTRACT.md) identical, so a project can change backend without changing the UI or the data
- Keep the Python `__ctrl__` command surface (dev, test, app, prod, flatten, remote)
- Grow the enterprise surface inside `base/` and `system/` (audit, tenancy, policy) only when a product needs it
