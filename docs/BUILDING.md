# Building StarSyncUniverse

## Toolchain

The public project targets **.NET 10 / Windows** and uses WPF plus Microsoft WebView2.

Tested public-preparation environment:

- .NET SDK 10.0.400
- MSBuild 18.9
- RID `win-x64`
- Windows 10/11 x64

The exact SDK patch version is not a hard runtime requirement, but a compatible .NET 10 SDK is recommended for source builds.

## Clone and build

```powershell
git clone [GIT_REPOSITORY_URL]
cd StarSync-Universe
dotnet restore
dotnet build StarSyncUniverse.slnx -c Release
```

The main project is:

```text
src\StarSyncUniverse\StarSyncUniverse.csproj
```

and it references:

```text
src\StarSyncUniverse.Contracts\StarSyncUniverse.Contracts.csproj
```

## Publish a copyable Windows build

Use:

```powershell
.\scripts\publish-win-x64.ps1
```

The script creates a framework-dependent `win-x64` publish directory and a ZIP under `release\`. A compatible .NET 10 Desktop Runtime and Microsoft Edge WebView2 Runtime must be present on the target machine.

## Optional local Star Citizen data refresh

StarSyncUniverse can run from its packaged baseline. Local game-data refreshes are optional.

Configure local tools/data either in the application UI or with environment variables:

```powershell
$env:STARSYNC_STARBREAKER = 'C:\path\to\starbreaker.exe'
$env:STARSYNC_DATA_P4K = 'X:\path\to\StarCitizen\LIVE\Data.p4k'
$env:STARSYNC_SCUNPACKED = 'C:\path\to\scunpacked-data'
```

Do not commit local `Data.p4k`, extracted game archives, account-specific configuration or private cache files to the public repository.

## Community baseline updates

For development-only baseline generation, place a SCUnpacked dataset under the repository `database\` directory or set `STARSYNC_SCUNPACKED`. The baseline exporter and headless import modes are intended for maintainers who understand the provenance of generated data.

## Build verification

Before publishing:

```powershell
dotnet build StarSyncUniverse.slnx -c Release
```

The prepared baseline was required to build with **0 warnings and 0 errors** before publication.

