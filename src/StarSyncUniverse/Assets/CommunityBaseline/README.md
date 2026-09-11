# StarSyncUniverse Community Baseline

This directory is the no-tool runtime baseline shipped with StarSyncUniverse.

- `Universe/*.current.json` contains the frozen spatial/system snapshots used on first start when no Local Data.p4k update adapter is enabled.
- `Knowledge/location-knowledge.json` contains the frozen canonical readable location knowledge used by the normal UI: readable names, descriptions, parent relationships, jurisdiction/faction metadata, services and exact trade relationships known at the time of the freeze.
- `manifest.json` records the source builds and counts used for the freeze.

The raw Data.p4k object/container names remain in the spatial snapshots as technical provenance. They are not the normal presentation identity. The renderer resolves known objects through the packaged location-knowledge baseline and shows the readable canonical name/description. Optional SCUnpacked import refreshes this semantic layer at runtime; it is not required for first start. Optional online enrichment may add/refresh remote descriptions/details/images only after explicit user consent.

Refresh this directory during development with:

`StarSyncUniverse.exe --headless-export-community-baseline`

The command freezes the current local StarSyncUniverse system snapshots and the latest repository SCUnpacked semantic dataset into these project assets.
