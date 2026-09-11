# Privacy

StarSyncUniverse is designed as a local desktop application.

## Local data

Runtime settings, caches, bookmarks, snapshots and generated diagnostics are stored locally on the Windows account. These runtime files are not part of the public source tree and should not be committed.

## Optional external access

Network activity is feature-dependent. Optional functions may access community providers such as the Star Citizen Wiki API or StarCitizen.Tools for metadata/media enrichment, or a user-configured SyncHost endpoint.

Provider access is not required for the packaged baseline map.

## Local Star Citizen data

When the local `Data.p4k` adapter is enabled, StarSyncUniverse invokes the separately supplied StarBreaker executable against the configured local Star Citizen installation. The application does not require account credentials and does not read or store login secrets.

## SyncHost identity [TBD in Future Release]

If SyncHost support is enabled, the application can create a local client identity and signing key. The private key is protected for the current Windows user and is not part of this repository. Do not publish generated runtime identity files.

## Public repository hygiene

The prepared public tree intentionally excludes:

- Windows usernames and profile paths;
- developer repository paths;
- API keys, passwords and tokens;
- user bookmarks/settings;
- runtime caches and logs;
- local `Data.p4k` files;
- extracted personal/game-installation state.
