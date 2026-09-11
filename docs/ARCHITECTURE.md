# StarSyncUniverse architecture

## Purpose

StarSyncUniverse is a local-first WPF application with an embedded WebView2 map renderer. The application separates authoritative game-derived spatial data from optional semantic/presentation enrichment. This separation is a core design rule: presentation code may improve readability, but must not silently invent or replace canonical Star Citizen data.

## High-level layers

### 1. WPF host (`MainWindow.xaml`, `MainWindow.xaml.cs`)

Responsibilities:

- native application shell and top navigation;
- application lifecycle and cancellation;
- loading/reloading datasets;
- data-source discovery and settings persistence;
- WebView2 initialization and trusted host-to-renderer bridge;
- galaxy-wide native search and system switching;
- orchestration of bookmarks, routes, diagnostics, SyncHost and data adapters.

The WPF host is the privileged process boundary. Messages received from WebView2 can trigger settings changes, bookmark writes, diagnostics and route operations. Therefore `MapView_WebMessageReceived` accepts messages only from generated files below the local StarSyncUniverse `RendererCache` directory.

### 2. Domain and data import (`Domain/`, `Data/`, `Transforms/`)

Responsibilities:

- canonical internal universe representation;
- placement/body/surface coordinate records;
- StarBreaker/Data.p4k import;
- DataCore queries;
- source authority and data-status preservation;
- coordinate transforms.

Game-derived names and game object content remain native. Localization must never modify these values.

### 3. Services (`Services/`)

Services contain reusable application behavior such as:

- SCUnpacked knowledge enrichment;
- SyncHost identity/transport;
- bookmarks and bookmark groups;
- location image cache and local user overrides;
- display/application settings;
- support diagnostics;
- body texture caches and presentation assets.

I/O ownership belongs in services rather than the renderer. External network access is performed by the host services and is gated by the application settings/consent model.

### 4. Localization (`Localization/UiLocalizationCatalog.cs`)

The localization catalog is the sole source of application UI translations.

Rules:

- English (`en`) is the canonical fallback language.
- German (`de`) is the second built-in language.
- Only application chrome, controls, help text and application-generated field captions are translated.
- Star Citizen object names, descriptions, factions, services, commodities, locations, source paths, technical identifiers and any other game-provided values stay native.
- New languages can be added by extending the catalog and language normalization without touching game data.

See `docs/LOCALIZATION.md` for the extension procedure.

### 5. Web renderer (`Renderer/MapHtmlRenderer.cs`)

`MapHtmlRenderer` produces a self-contained local HTML document for WebView2. It serializes the current immutable renderer payload and provides:

- Canvas/WebGL universe rendering;
- tactical labels and overlays;
- left/right panels;
- workspaces (Database, Tools, Settings, About, Bookmarks);
- route interaction;
- bookmarks;
- body/surface views.

The generated page is protected by a restrictive Content Security Policy. Direct network connections from renderer JavaScript are disabled (`connect-src 'none'`). Images/scripts are limited to StarSyncUniverse virtual hosts. Optional online enrichment happens in C#, not in renderer JavaScript.

`MapHtmlRenderer.cs` is still intentionally kept as a single generation boundary because its CSS/HTML/JS are tightly coupled. The file is large; future structural work should extract static CSS/JS into versioned application assets without changing the renderer-to-host trust model.

## Data authority

Preferred authority order:

1. CURRENT LIVE Data.p4k / StarBreaker: geometry, placements and live game records when the adapter is enabled.
2. Packaged community baseline: stable offline readable knowledge/presentation baseline.
3. SCUnpacked: semantic enrichment and refreshed readable metadata.
4. Online enrichment: optional descriptions/media only, with explicit user consent.
5. User overrides: local presentation overrides such as image/description; they never alter canonical geometry.

Every feature that combines sources should preserve provenance/status rather than silently merging away source authority.

## Application settings

`StarSyncUniverseSettings` is persisted under `%LOCALAPPDATA%\StarSyncUniverse\settings.json` using an atomic temp-file replacement pattern.

Current settings include:

- Data.p4k adapter enablement/path;
- StarBreaker path;
- SCUnpacked enablement/root;
- online enrichment enablement + explicit consent;
- SyncHost configuration;
- UI language (`en` / `de`).

## Lifecycle and resource ownership

- `MainWindow` owns the reload `CancellationTokenSource` and disposes it on replacement/window close.
- StarBreaker child processes are disposed and killed as a process tree on cancellation.
- WebView navigation completion handlers are removed in `finally` blocks.
- WebView host-message handlers are explicitly detached on window close.
- renderer MutationObservers and WebGL resources are disposed/disconnected on `beforeunload`.
- image response/stream/file handles use `using`/`await using`.
- the location image HTTP client is intentionally static to avoid socket exhaustion.

## Change discipline

When adding a UI feature:

1. add UI wording to `UiLocalizationCatalog`;
2. keep game/object values unmodified;
3. HTML-escape any game/user/external string inserted through `innerHTML`;
4. keep privileged operations in the WPF host/services;
5. add cancellation/timeout handling for external work;
6. update relevant documentation;
7. build Debug and Release with zero warnings/errors.
