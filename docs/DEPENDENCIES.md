# Dependencies

StarSyncUniverse intentionally keeps its direct external package surface small.

## Direct NuGet dependency

| Package | Version | Purpose |
|---|---:|---|
| `Microsoft.Web.WebView2` | `1.0.3719.77` | Hosts the embedded HTML/JavaScript map surface inside the WPF desktop application. |

The remaining application stack is provided by the .NET 10 Windows/WPF framework.

## Runtime requirements

- .NET 10 Desktop Runtime for framework-dependent binary releases.
- Microsoft Edge WebView2 Runtime.

## Optional external tools/data

These are not NuGet dependencies and are not bundled into the prepared source repository:

- StarBreaker executable for local `Data.p4k` inspection.
- Star Citizen `Data.p4k` supplied by the user from their own installation.
- SCUnpacked Data for optional enrichment/validation updates.
- Star Citizen Wiki API / StarCitizen.Tools for optional enrichment where enabled.

See `THIRD_PARTY_NOTICES.md` for attribution and licensing boundaries.
