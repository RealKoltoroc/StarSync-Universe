# Code guide

This document is the short orientation map for the public StarSyncUniverse source tree.

## Application shell

- `App.xaml` / `App.xaml.cs` — WPF startup, optional headless diagnostics/import modes and application bootstrap.
- `MainWindow.xaml` / `MainWindow.xaml.cs` — primary application window, settings, WebView2 host, reload/import orchestration and UI bridge.

## Domain

`src/StarSyncUniverse/Domain/`

Contains immutable/transport-oriented models for universe entities, render snapshots, overlays, bookmarks, galaxy schematics, orbital simulation and map visuals. Domain models should remain independent from WPF controls and external providers.

## Data import

`src/StarSyncUniverse/Data/`

Responsibilities include:

- StarBreaker process integration.
- `Data.p4k`/ObjectContainer extraction contracts.
- SCUnpacked catalog correlation.
- body/system/station/jump-point imports.
- hierarchy and placement resolution.
- source identity/provenance retention.

The import layer deliberately preserves unresolved semantics instead of converting unknown units/frames into invented values.

## Spatial transforms

`src/StarSyncUniverse/Transforms/`

Contains body/world frame math, body rotation, spatial transforms and surface coordinate conversion. This is the core mathematical boundary and should not depend on presentation code.

## Services

`src/StarSyncUniverse/Services/`

Contains application/domain services such as:

- universe catalog and index builders;
- routing and jump graph construction;
- bookmarks and bookmark groups;
- diagnostics/validation;
- location and system knowledge;
- presentation texture caches;
- settings and user overrides;
- optional SyncHost client support;
- optional external enrichment.

Provider/network code should remain optional and must not silently become the authoritative source for spatial game data.

## Renderer

`src/StarSyncUniverse/Renderer/`

- `RenderFrameSnapshotBuilder.cs` converts domain state into a renderer-friendly snapshot.
- `MapHtmlRenderer.cs` creates the isolated HTML/CSS/JavaScript surface loaded in WebView2.

The renderer uses custom `https://starsync-*` virtual host mappings for packaged/local assets. These are internal WebView2 host names, not public internet endpoints.

## WebGL helper

`src/StarSyncUniverse/Assets/MapVisuals/WebGL/starsync-webgl.js`

Optional GPU presentation helper used by the embedded map surface.

## Community baseline

`src/StarSyncUniverse/Assets/CommunityBaseline/`

Packaged data used for immediate standalone operation. It contains generated/curated project data and source/provenance fields. Machine-specific paths are removed from the public copy.

## Body textures and visuals

`src/StarSyncUniverse/Assets/BodyTextures/` and `Assets/MapVisuals/`

Presentation assets used by the map. See the README files in those directories and `THIRD_PARTY_NOTICES.md` before redistributing or replacing them.

## Contracts project

`src/StarSyncUniverse.Contracts/`

Contains catalog/adapter contracts intended to keep the standalone universe module separable from other StarSync applications.

## Design rules

1. Preserve source provenance.
2. Distinguish authoritative game data from enrichment and presentation.
3. Do not infer spatial units/frames when evidence is missing.
4. Keep network enrichment optional.
5. Keep user-specific paths/settings outside the repository.
6. Keep game-content text/names native; application UI localization is a separate concern.
