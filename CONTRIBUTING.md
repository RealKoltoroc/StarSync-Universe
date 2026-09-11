# Contributing

Contributions are welcome if they preserve the project's core data-integrity rules.

## Development rules

1. Do not commit personal paths, usernames, credentials, API keys, logs, caches or game-installation paths.
2. Keep authoritative local game data, optional enrichment and derived presentation data clearly separated.
3. Do not invent spatial units, positions, relationships or service availability when the source is unresolved.
4. Keep external network providers optional.
5. Preserve source/provenance fields where practical.
6. Keep game-content names/text native; localize only the application UI.
7. Do not add Cloud Imperium Games assets unless redistribution is clearly permitted. Prefer generated/project-owned presentation assets.

## Before submitting a change

```powershell
dotnet build StarSyncUniverse.slnx -c Release
```

The build should complete with zero warnings and zero errors.

If a change touches import, transforms, routing or source authority, update the appropriate file in `docs/`.

## Public issue reports

Please remove account names, filesystem usernames, organization-private URLs, private SyncHost endpoints and other personal information before attaching logs or screenshots.

