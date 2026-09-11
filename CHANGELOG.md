# Changelog

## 0.7.77 - 2026-09-11

Public repository refresh for the current StarSync Universe map baseline.

- synchronized the public source tree with the current 0.7.77 application code while retaining the existing public documentation, licensing, contribution and publication files
- added the current orbital-context camera/visibility behavior so a focused station or Comm Array keeps its parent planet/moon visible while the camera is rotated
- added Stellar Lighting and Survey Lighting controls directly below the zoom control
- added the optional persistent Survey night-side projection filter with cyan/teal scan tint and progressive anti-stellar depth shading while preserving readable surface detail
- improved body/location interaction so semantic body-summary locations can be selected and double-clicked for direct camera focus
- suppress unavailable population/atmosphere rows from normal body details instead of showing internal source placeholders
- improved map HUD layout so measurement scale/labels avoid the bottom instruction and map-legend areas
- added dual metric and astronomical-unit distance presentation for map measurement and route totals
- retained the transparent, borderless bottom map legend presentation
- refreshed the public README into a full product introduction with feature overview, architecture/data-source summary and a prepared five-screenshot gallery plan
- updated the public release/build tooling and version references to 0.7.77

## 0.7.55 - 2026-09-06

StarSyncUniverse application branding integration.

- integrated the dedicated StarSyncUniverse galaxy branding into the main application shell
- replaced the old top-left blue circle with the StarSyncUniverse galaxy application emblem
- added the galaxy-background StarSyncUniverse artwork as the centered startup/loading presentation
- added the StarSyncUniverse logo artwork as the About workspace background while keeping the community/legal information readable above it
- configured the StarSyncUniverse galaxy emblem as the WPF window icon and executable application icon
- package all Branding assets into Release builds and expose the About artwork to the WebView through the local `starsync-branding` virtual host; no external request is required for application branding

## 0.7.54 - 2026-09-06

Persistent map/display preferences.

- added a dedicated persistent `display-settings.json` under `%LOCALAPPDATA%/StarSyncUniverse` so map visibility choices survive application restarts and system switches
- persist Stars, Planets & Moons, station classes, Racing Tracks, Jump Points, Orbits, Asteroid Fields, Labels, Region Boundaries, Show Hidden and Background Nebula together with the internal WebGL/orbit-plane/guide switches
- display changes are saved automatically when a checkbox is changed, including changes made through the Settings workspace mirror controls; no separate Apply step is required
- renderer startup applies the saved display state before the first visibility calculation, so settings such as `Labels = off` no longer flash on and then need to be disabled again after every launch
- kept data-source/API/SyncHost settings separate from display preferences so changing map presentation never reloads Data.p4k or external enrichment sources

## 0.7.53 - 2026-09-06

Small-body texture visibility and persistent local location presentation overrides.

- keep the synthetic planet/moon frame removed, but render overview-scale textured bodies without directional dark-side lighting and with a modest brightness lift so small moons remain visible instead of disappearing when the old cyan rim is absent
- added a persistent local location/object override store under `%LOCALAPPDATA%/StarSyncUniverse/LocationOverrides` with separate user-authored description and image fields
- every selected entity or surface target now exposes `Add/Change image`, `Delete image`, `Add/change local description` and `Delete local description` controls even when no online/bundled image or description exists
- local user image and description have highest presentation precedence; online enrichment and packaged community metadata remain available underneath and automatically become visible again when the local override is deleted
- local images are copied into the StarSyncUniverse override cache and served through a dedicated WebView2 virtual host; supported formats are PNG, JPEG and WebP with a bounded 24 MiB file size
- local overrides also work for objects without a canonical SCUnpacked knowledge record by using stable placement/surface identifiers, while canonical UUID-backed objects use the UUID-based override key
- online enrichment cannot overwrite an active local user image/description; deleting only one local field preserves the other

## 0.7.52 - 2026-09-06

Racing-track semantic integration, body-rim cleanup, focus-camera travel and bookmark-group sharing foundation.

- added a packaged Racing Track semantic baseline derived from SCUnpacked contract `LocationPools/ResolvedLocations`, while retaining Data.p4k as the sole spatial authority
- resolve the known Stanton racing ObjectContainers to readable identities including Lorville Outskirts, Caplan Circuit, Yadar Valley, The Snake Pit, The Sky Scraper, The Icebreaker and Miner's Lament, with bundled description, operator, length and feature metadata
- normalized racing anchors to `Category = RacingTrack` and made the Racing Tracks visibility toggle apply consistently to body overlays, Surface Locations, body views and lists instead of only normal entity markers
- removed the synthetic cyan/green outline drawn over already-textured planets and moons; the WebGL texture silhouette is now the visible body edge
- replaced the previous three-stage focus animation that zoomed out, translated sideways and zoomed back in with target-tracking travel: the selected object moves smoothly to screen center and stays centered during the approach; when a 3D target is behind the current view, the camera first rotates toward it and then travels in
- added persistent bookmark creator identity and content fingerprints, local bookmark groups with stable GUIDs, multi-selection assignment/move/delete/update operations and signed group-package export/import with stable-ID de-duplication
- added a dedicated StarSync SyncHost settings card and a Universe SyncHost client identity/request-signing foundation compatible with the StarSync WPF client's ECDSA-P256/SHA-256 client identity and signed request model
- retained the 0.7.51 system overview baseline and completed the pending Pyro reference-data correction

## 0.7.51 - 2026-09-05

System overview overlay sourced from the bundled starcitizen.tools reference set.

- expanded the top-left system header below `CURRENT LIVE SYSTEM · AUTHORITATIVE PLACEMENTS` with a compact system description, affiliation, jurisdiction, size and star type
- added six at-a-glance astronomical/location counters for star, planets, moons, belts, stations and jump points in the same map-overlay visual language
- added `Assets/CommunityBaseline/System/system-overviews.json` as a bundled no-network system reference layer with explicit `starcitizen.tools` provenance and last-checked timestamp
- populated Stanton with the requested reference facts: United Empire of Earth / UEE, 4.85 AU, G-type main sequence, 1 star, 4 planets, 12 moons, 2 belts, 24 stations and 4 jump points
- added corresponding reference records for Pyro and Nyx so the overlay follows the selected system instead of being Stanton-specific
- the overview remains available offline and does not trigger external HTTP requests; future consented online enrichment can refresh the bundled reference independently of Data.p4k geometry

## 0.7.50 - 2026-09-05

Packaged canonical location knowledge and true no-tool first start.

- froze the current Stanton/Pyro/Nyx spatial snapshots into `Assets/CommunityBaseline/Universe` so a fresh installation can start without StarBreaker, Data.p4k, SCUnpacked or any previous `%LOCALAPPDATA%` snapshot cache
- froze 2,056 canonical location records, 74 factions, 22 service types and the resolved location descriptions/relationships into `Assets/CommunityBaseline/Knowledge/location-knowledge.json`; known objects therefore keep readable names and descriptions even when the SCUnpacked adapter is disabled or absent
- raw Data.p4k names remain only as technical provenance; normal map labels, Surface Locations, detail cards, search and navigation resolve against the packaged semantic baseline first
- added `--headless-export-community-baseline` as the development refresh command so a later verified Data.p4k/SCUnpacked import can be deliberately frozen into the next community build instead of becoming a runtime prerequisite
- changed optional SCUnpacked import from "required semantic source" to "refresh/extend packaged semantic baseline" and kept geometry authority separate
- generic AI landing/drop-off helper ObjectContainers are hidden from the normal Surface Locations catalogue and remain available only through Show Hidden/technical provenance, preventing non-location helper nodes from appearing beside real named locations
- expanded consented Online API enrichment so a successful location lookup can refresh description/details and cache an available image; packaged names/descriptions remain the offline fallback and external access is still hard-gated by explicit consent
- first-start validation with local settings, snapshots, extracts, knowledge cache and image cache temporarily removed: Stanton loaded 470 entities / 16 bodies with WebGL2 active, no StarBreaker process, and the packaged 2,056-location knowledge baseline present

## 0.7.49 - 2026-09-05

Always-visible body textures with memory-bounded overview LOD.

- added a packaged 512px overview texture for every available Stanton/Pyro planet and moon so the system map can render all visible bodies with recognizable surface imagery instead of limiting textured rendering to two high-resolution bodies
- split body texture residency into two tiers: overview textures remain resident for all renderable bodies while at most two full-resolution surfaces plus one cloud and one normal layer are promoted for selected/focused/large bodies
- LIVE Data.p4k refreshes now regenerate the corresponding overview texture automatically, while Standalone/Snapshot mode reads the same overview assets from the packaged or cached presentation baseline
- renderer payload now exposes overview texture URLs independently from full-resolution URLs; user-installed surface overrides retain priority
- added runtime texture diagnostics at `%LOCALAPPDATA%/StarSyncUniverse/renderer-texture-status.txt`; validation on Stanton reports `16/16` renderable bodies textured, 16 overview textures resident, WebGL2 active, and no full-resolution body required for the zoomed-out system view
- retained the 0.7.45 memory safety model for large 4K Pyro surfaces while restoring the visual requirement that textures are shown regardless of StarBreaker, SCUnpacked, API or Snapshot/LIVE mode

## 0.7.48 - 2026-09-05

Packaged body-presentation baseline and mode-independent texture rendering.

- copied the validated Stanton/Pyro presentation texture layers into `Assets/BodyTextures` so the active community build ships its body textures instead of depending on a previous local Data.p4k extraction
- body presentation is now a renderer baseline independent of the selected data mode: Standalone/Snapshot, SCUnpacked enrichment, Online enrichment and LIVE Data.p4k all keep planet/moon textures available
- first start installs the packaged baseline into the normal local presentation cache; a user-authorized LIVE Data.p4k refresh may replace/update local assets, while a failed or partial refresh falls back to the packaged/last-known-good presentation set instead of blank bodies
- retained the validated equirectangular projection/orientation contract so body-fixed target geometry and visible texture geography remain aligned
- grouped the optional Data.p4k/StarBreaker adapter, SCUnpacked import and Online API/consent controls into one Settings card with three collapsible subcards; these adapters are explicitly described as data/enrichment sources rather than texture switches
- added `Assets/BodyTextures/README.md` documenting the packaged presentation-layer contract and future replaceability of the texture asset set without changing spatial/navigation logic

## 0.7.47 - 2026-09-05

Renderer recovery for the 0.7.46 encoding regression and standalone cached body-texture restoration.

- restored the 0.7.45 renderer text baseline before reapplying the 0.7.46 community/settings additions, eliminating the mojibake/UTF-8 corruption visible throughout the map UI
- standalone snapshot mode now loads the already-generated local body-presentation cache without invoking StarBreaker, so planets and moons remain textured when the Local Data.p4k update adapter is disabled
- added a cache-only texture reconstruction path that preserves the existing presentation PNGs, cloud layers, normal layers, render strengths and Pyro presentation tint metadata from the local audit files
- fresh installations now default both the Local Data.p4k adapter and SCUnpacked import to OFF; all external/local enrichment sources remain explicit opt-ins
- retained the online enrichment consent hard gate and re-applied the dedicated Racing Tracks visibility toggle on the repaired renderer baseline
- validation: standalone Stanton rendered 470 entities / 16 bodies with WebGL2 active, 34 local texture resource references, zero renderer mojibake markers and no StarBreaker child process

## 0.7.46 - 2026-09-05

Community-build identity, explicit external-source controls, offline snapshot runtime and Racing Track visibility.

- added a prominent **Built for the Community** About card: StarSyncUniverse is presented as a free community build intended to remain usable without optional online enrichment, with an explicit unofficial/not-affiliated notice
- added persistent application settings under `%LOCALAPPDATA%/StarSyncUniverse/settings.json` for the local Data.p4k update adapter, optional SCUnpacked import, online location enrichment consent/activation and user-configurable StarBreaker/Data.p4k/SCUnpacked paths
- online location enrichment is now hard-gated twice: it is disabled by default, requires explicit consent, and the renderer does not request remote location imagery unless both consent and enrichment are active; the host independently refuses requests when the gate is closed
- Settings now explains the normal HTTP privacy implication of external enrichment, exposes the configured provider (`Star-Citizen.wiki API / starcitizen.tools media`) and provides a local online-cache clear action
- added `UniverseSnapshotReader`: when the local Data.p4k adapter is disabled, the application loads cached per-system StarSyncUniverse snapshots instead of invoking StarBreaker; snapshot writing now preserves galaxy-position fields for future standalone baselines
- SCUnpacked enrichment can now be disabled independently; the spatial Data.p4k/snapshot map, navigation, routing and bookmarks remain available without the semantic database
- added `RacingTrack` as a first-class visual class and a dedicated **Racing Tracks** display/settings toggle; current race-specific Data.p4k containers such as `rctrk`/`racetrack`/`racing static` objects can be hidden without affecting ordinary stations or other surface targets
- retained the 0.7.45 bounded WebGL texture residency model; Settings documentation now describes demand-loaded textures rather than the removed system-wide prewarm behavior
## 0.7.45 - 2026-09-05

Planet-surface renderer residency and WebView2 memory-pressure repair.

- removed eager prewarming of every 4096x4096 body texture in the active system; textures are now loaded only when the WebGL body renderer actually needs them
- reduced the live renderer working set to a strict LRU of two base-surface textures plus one cloud and one normal texture, with selected/focused bodies retaining priority so close body inspection remains fully textured
- added explicit WebGL texture release on LRU eviction; dropping an `Image` from the JavaScript cache now also calls `gl.deleteTexture`, preventing GPU textures and mipmaps from accumulating for the entire session
- the regression was measured directly on Stanton: the renderer process had grown to about 1.29 GiB working set while idle after all body textures were resident; the new bounded residency removes that system-wide texture accumulation instead of masking it with UI throttling

## 0.7.44 - 2026-09-05

SCUnpacked semantic location database, exact Data.p4k correlation, commerce relations and lazy presentation imagery.

- added a read-only `ScUnpackedKnowledgeDatabase` over the checked-in master data: 2,056 readable starmap locations, faction/jurisdiction relations, Amenities/services, trade profiles and the 21,909-item catalog are indexed without replacing CURRENT LIVE Data.p4k geometry
- correlate normal placements and body-local anchors to SCUnpacked primarily by exact UUID and write `%LOCALAPPDATA%/StarSyncUniverse/KnowledgeDatabase/scunpacked-data-p4k-correlation.tsv`; the current validation run resolves 947 Data.p4k objects/anchors, all 947 through `DIRECT_UUID`, including 478 raw technical names that project to different readable location names
- replace technical location labels in normal map, Surface Locations, galaxy search and details with correlated readable identities such as Orison platform-cluster names and Pyro RASTAR/outpost names while retaining raw IDs/classes/source paths inside collapsed provenance sections
- add conservative commodity commerce relations from `resources/commodity_trade_locations.json`: a commodity is projected only when both `StarmapObjectUUID` and the matched trade tag identify that exact commodity, with metadata from `resources/commodities.json`; current master resolves 138 locations with exact commodity-tag relations across 25 commodities and distinguishes commodities sold here from commodities bought here
- enrich details with faction identity/classification/relations, services, retail-service categories, trade profiles, exact commodity lists and conservative related-item links; generic Amenities are not misrepresented as a physical storefront count
- add semantic planet/moon hierarchy summaries for cities/landing zones, stations, outposts and known locations, while radius/diameter, rotation and system coordinates remain sourced from Data.p4k; atmosphere/population remain explicitly unavailable where no proven authoritative field mapping exists
- add lazy cached location imagery: Star-Citizen.wiki location metadata resolves presentation images (including starcitizen.tools media), images are downloaded on demand into `%LOCALAPPDATA%/StarSyncUniverse/LocationImages`, source attribution is retained and WebView serves only the local cache; list imagery is intersection-observer lazy-loaded instead of requesting every location at once
- compact the renderer knowledge payload by serializing each semantic location once and sending placement/anchor matches as lightweight UUID references; Stanton renderer HTML dropped from roughly 17.9 MB during development to roughly 4.0 MB while preserving the same semantic detail surface
- persist the latest renderer-ready/error state in `%LOCALAPPDATA%/StarSyncUniverse/renderer-runtime-status.txt` so JavaScript startup regressions can be verified without relying only on the visible status bar
- add `--headless-location-image-smoke` and extend `--headless-import` diagnostics; Release validation passes Stanton, Pyro and Nyx with zero structural errors, and the Orison image smoke resolves/caches a real starcitizen.tools image via the Star-Citizen.wiki API
- document source authority, correlation order, UI projection and deliberate environment/shop-count limitations in `docs/LOCATION_KNOWLEDGE_MODEL.md`

## 0.7.43 - 2026-09-05

Reference-calibrated Pyro surfaces and high-visibility body-local target rendering.

- calibrated presentation colors for every Pyro planet and moon against current Alpha 4.0/PTU orbital reference images from starcitizen.tools; only derived RGB palette statistics and source links are stored in the project, not the third-party reference images
- generalized the SPLAT-as-material-mask plus DISPLACEMENT-as-relief reconstruction from the Monox/Fuego/Terminus prototypes to every rocky Pyro body, with 4096x4096 presentation output where the required CURRENT LIVE control maps are available
- retained the separate gas-giant pipeline for Pyro V while reference-calibrating its visible color range and preventing an additional renderer-side grade on already derived body textures
- kept Monox, Fuego and Terminus cloud-free in map presentation; cloud source assets remain separate and auditable and are never baked into the base terrain texture
- increased surface-location readability: front-hemisphere targets use a high-contrast gold ring with green core; opposite-side targets remain visible in blue with dashed outlines and leader lines, including close body-local views
- added HiddenOnMap propagation for body-local targets so hidden locations follow the existing Show Hidden setting instead of disappearing for unrelated perspective reasons
- added a generated body-reference-colors.tsv audit beside the Pyro presentation cache, recording the measured palette and exact starcitizen.tools reference source per body
- bumped the presentation cache algorithm to v17 so all affected Pyro body textures are regenerated from the same CURRENT LIVE source geometry/control maps with the corrected reference colors
## 0.7.42 - 2026-09-05

Corrected Pyro terrain-control semantics and body-specific surface palettes.

- fixed the core reconstruction error: grayscale SPLAT values are now treated as material-region masks instead of visible brightness, while DISPLACEMENT supplies local relief/tone; this prevents compact high SPLAT values on Terminus from appearing as fake white cloud-like surface dots
- Monox, Fuego and Terminus now use deliberately distinct body palettes: Monox blue/lavender/rose, Fuego rust/orange/ochre, Terminus charcoal/rust/taupe/blue-gray/cream; calibrated bodies bypass the old generic warm/cool blending path
- Terminus SPLAT is low-pass filtered more strongly before material classification and its WebGL normal/relief strengths are reduced to 0.04 so high-frequency technical-control detail cannot reappear as cloud-like highlights
- extended CURRENT-LIVE material-hint parsing for FrozenOcean materials: CoastTransitionColor and DistanceOceanColor are now recognized, preserving Terminus' actual blue/ice material cues instead of silently discarding them
- cloud textures remain fully separate and disabled for Monox/Fuego/Terminus; the v16 cache key forces a complete rebuild of the three 4096x4096 presentation surfaces without changing UVs, BodyFixed coordinates, rotation or geometry

## 0.7.41 - 2026-09-05

Renderer-startup repair for the native galaxy search cutover.

- fixed the blank-map regression caused by JavaScript still dereferencing removed Search Catalog compatibility controls during renderer initialization
- retained hidden compatibility controls for the existing object-list pipeline while keeping the obsolete Search Catalog UI removed
- neutralized the old catalog-highlight predicate so native galaxy-wide search does not depend on removed in-map search state
- preserved the intended Enter flow: cross-system result -> switch system -> renderer ready -> focus/zoom exact object or body-local target

## 0.7.40 - 2026-09-05

Galaxy-wide instant search, single-pass body color grading and perspective-correct Terminus rings.

- added an always-visible centered Galaxy search field to the native top navigation bar; predictive results search placements and body-local targets across every loaded system, support keyboard up/down selection, Enter navigation, mouse selection and an inline reset X
- cross-system search results automatically switch the active system and focus the exact placement or surface anchor after the destination renderer reports ready; the redundant left-side Search Catalog panel was removed while the collapsible object catalog remains available
- fixed a color-pipeline defect that applied a second Pyro body grade in WebGL after the 4096x4096 derived surface had already been body-graded; derived terrain and true starmap diffuse assets now pass through the renderer without another palette remap, while lighting/atmosphere remain independent
- kept Monox, Fuego and Terminus cloud compositing disabled; Terminus highlight range, normal strength and relief strength were reduced so bright terrain-control speckle is no longer presented like invented cloud cover
- changed the Terminus ring/halo compositor so the far ring is removed inside the solid body disc and only the near half is allowed to cross the foreground, preventing the renderer from showing ring content through the planet
- bumped the derived body presentation cache to v14 so affected surfaces are rebuilt from the same archived CURRENT-LIVE source maps on next load; source DDS/PNG archives and BodyFixed geometry remain unchanged

## 0.7.39 - 2026-09-05

High-resolution splat/displacement-first reconstruction for Monox, Fuego and Terminus.

- removed elevation from the dominant visible-surface role for the three calibrated Pyro examples (`pyro2`, `pyro5e`, `pyro6`); their presentation is now driven by splat first and displacement second, with climate and elevation reduced to weak supporting cues only
- preserved substantially more splat/displacement source frequency for these bodies and emit their geometry-preserving `surface-updated.png` at 4096x4096 for close inspection and presentation; UV topology, rotation, flip state, crop state and BodyFixed location mapping remain unchanged
- recalibrated body-specific color grades so Monox, Fuego and Terminus no longer collapse toward the same generic brown surface: Monox is darker carbon/iron, Fuego warmer ochre/ash, Terminus cream/taupe/rose-gray
- retained cloud coverage as an independent WebGL layer and excluded cloud maps from the base surface texture entirely
- bumped the derived cache algorithm key so the three updated textures are rebuilt from the archived CURRENT-LIVE source controls on the next load/export

## 0.7.38 - 2026-09-05

Surface-first Pyro texture reconstruction and UI encoding repair.

- compared the archived CURRENT-LIVE original layers for Monox (`pyro2`), Fuego (`pyro5e`) and Terminus (`pyro6`) directly in the StarBreaker `BodyTextures/PYRO/.../original` folders; confirmed that Pyro terrain bodies provide climate/splat/elevation/displacement/normal controls rather than a final body-specific starmap albedo, while Monox and Terminus additionally provide separate global cloud coverage
- changed derived rocky-body presentation generation to be geometry-first: elevation is now the dominant macro structure, displacement adds restrained surface detail, climate/splat are reduced to low-frequency material modulation, and cloud maps remain completely outside the base surface synthesis
- added reference-only color/tone profiles for Monox, Fuego and Terminus so these visually distinct bodies no longer collapse toward the same generic brown fallback palette; profiles change presentation color only and do not rotate, flip, crop, resample or move UV/location geometry
- reduced Monox and Terminus cloud opacity because their CURRENT-LIVE cloud coverage is now rendered only as a separate WebGL shell instead of being allowed to obscure the surface presentation
- bumped the derived presentation algorithm cache key and regenerated `updated/surface-updated.png` in the StarBreaker archive; originals and SHA-256 provenance remain untouched
- repaired UTF-8 mojibake in `MapHtmlRenderer.cs` by converting the corrupted Windows-1252-decoded symbol sequences back to their intended Unicode characters; `Ã`, `Â` and `â...` artifacts no longer remain in StarSyncUniverse renderer source

## 0.7.37 - 2026-09-05

StarBreaker-side original/updated body-texture archive with explicit orientation preservation.

- added `BodyTextureArchiveService` and export every imported planet/moon into `StarBreaker/BodyTextures/<SYSTEM>/<BodyName>/` with separate `original/` and `updated/` directories
- `original/` preserves exact CURRENT-LIVE Data.p4k DDS sources plus StarBreaker-decoded PNG inspection copies for direct starmap diffuse, climate, splat, elevation, DDN normal, cloud and gas-giant tint layers where present; no original asset is rotated, flipped, cropped, resampled or color-adjusted
- `updated/` stores the StarSync presentation surface plus separate cloud/normal presentation files, keeping the same spherical UV geometry so BodyFixed targets continue to resolve to the same UV coordinates
- every body receives `orientation.json` documenting zero physical rotation/flip/crop of the source, the body-local XYZ -> equirectangular UV contract and the remaining geodetic-zero-meridian uncertainty; `original.sha256` records hashes for every archived original/chunk/decoded preview, and each system receives `texture-index.tsv` plus a README explaining direct versus derived presentation authority
- archive location follows the active StarBreaker executable: normal installs write beside `starbreaker.exe` under `BodyTextures`, while a repository `target/release/starbreaker.exe` resolves to the StarBreaker project root instead of polluting `target/release`

## 0.7.36 - 2026-09-05

Presentation-quality pass for planetary surfaces, route results and camera-relative station visuals.

- changed derived CURRENT-LIVE terrain presentation generation to low-pass climate/splat control fields before color synthesis; technical control-map frequency is no longer exposed as noisy pseudo-albedo when no baked body starmap diffuse exists
- bumped the derived texture algorithm cache key so Pyro/Stanton body presentation textures are rebuilt automatically from the same Data.p4k sources; CURRENT-LIVE geometry and BodyFixed targets remain untouched
- recalibrated the Terminus/Pyro VI presentation grade toward the public visual reference and replaced the two-line placeholder ring cue with a broader multi-band presentation ring; this is visual calibration only, not a geometry source
- redesigned route results around colored Start/Destination endpoints, compact numbered gold segment rows and a separated Total Distance / Segments / Interstellar Transits summary; coordinate-frame diagnostics and internal route-status tokens are no longer mixed into the normal result list
- procedural station/gateway presentation now receives camera yaw/pitch and visibly changes orientation while middle-drag orbiting the system camera instead of remaining a permanently screen-facing 2D silhouette
- public Star Citizen Wiki system/body imagery may be used as a presentation QA/reference target, while all canonical placement, physical dimensions, routing and body-local geometry remain sourced from CURRENT LIVE Data.p4k and validated enrichment

## 0.7.35 - 2026-09-05

Jump-point gateway infrastructure, staged cinematic focus, full visible-body texture prewarm and navigable Pyro asteroid markers.

- added `JumpPointInfrastructureImporter`: CURRENT-LIVE jump-point SOC roots are opened and identity-bearing nested `LocationObjectContainer` station/rest-stop children are promoted at child-local XYZ composed with the authoritative jump-point world XYZ; SCUnpacked contributes name/type/QT/hidden metadata only after strict UUID + composed world-position agreement within 1 m
- verified the gateway relationship against CURRENT LIVE for all imported endpoints: Stanton now promotes Nyx/Pyro/Terra Gateway, Pyro promotes Nyx/Stanton Gateway and Nyx promotes Pyro/Stanton Gateway; diagnostic max reference delta is 0.000000 m in the current import
- fixed duplicate filename extraction ambiguity by selecting the extracted SOC whose full relative path matches the authoritative entity `SourcePath`, avoiding the unrelated `EA/PyroJump` container when a `PU/system/.../jumppoints` SOC has the same filename
- classified QT-valid asteroid placements (`Asteroid_ValidQT`) as user-facing `AsteroidCluster` navigation markers while ordinary non-QT asteroid population remains minor detail; the `Asteroid Fields` switch now participates in entity visibility invalidation, making Pyro RAB/PYAM/Cluster targets visible without enabling every raw asteroid
- replaced one-step search focus with a three-stage cinematic sequence: ease out to SYS zoom 0.75, glide at that overview scale to the target XYZ, then ease in to the computed target framing; each stage honors the configured camera-travel duration
- double-click now bypasses the staging sequence and performs a direct configured-speed glide/zoom to the clicked object, matching the expected interaction for an already selected target
- base surface textures are prewarmed for every current-system body and the resident base-map budget is raised dynamically above the body count; Stanton 16/16 and Pyro 12/12 texture descriptors are present, preventing newly visible moons from appearing as white placeholders while cloud/normal maps remain LOD-lazy
- all left/right UI sections now start collapsed and expand on a single header click; side-panel body/control/muted/value typography is normalized to the same 10.5 px UI grade
- gateway stations receive an explicit larger presentation radius plus a dedicated WebGL gateway silhouette; the generic station shader was also replaced with a more structured hub/ring/truss/pod silhouette. These remain StarSync standard presentation meshes, not claims of exact game mesh geometry; canonical Data.p4k model overrides remain the path for exact station appearance

## 0.7.34 - 2026-09-05

Zoom/navigation reliability, station-role separation and Nyx field presentation correction.

- replaced the focus-only zoom control with one absolute logarithmic SYS zoom slider that remains usable with or without a focused object; without focus it changes scale around the current view, while an active focus keeps the selected object as the camera center
- added `Reset camera` directly beside the top zoom slider and synchronized the slider continuously with wheel/camera-travel zoom state
- reworked cinematic focus travel so camera translation to the actual target XYZ and logarithmic zoom use one continuous eased timeline; parent/orbit information is used only to determine framing, never as the camera destination
- introduced separate `System Stations` and `Mission / Event Stations` visibility roles. Classification is derived systemically from CURRENT-LIVE ObjectContainer hierarchy/source topology, entity class/type and hidden/interaction semantics; SCUnpacked remains identity/QT/visibility enrichment only
- Nyx `QV` Breaker/Extraction families are classified as mission/event infrastructure and are hidden by default from the clean system overview; when enabled they remain at their real CURRENT-LIVE system positions instead of being pulled into an illustrated star cluster
- fixed Levski classification: the actual CURRENT-LIVE hierarchical `Levski` child under `glaciemring segment levski` is now a Station, while the Glaciem/Delamar host remains its own ring/host object
- restricted illustrated station spacing/orbit cues to stations parented by a Planet or Moon; star/ring/segment-parented infrastructure always uses true projected system coordinates
- rebuilt the Nyx asteroid presentation from the CURRENT-LIVE `glaciemring bg` volume instead of the narrow derived placement envelope: a central clear zone, outward-increasing patchy density and a soft inner/outer fade remove the hard circular edge without changing the authoritative volume
- moved Map Legend out of the left navigation into a bottom map dock and added a persistent Settings switch for it
- added a per-system planet quick-selection panel with symbols in the left navigation; clicking a planet uses the same camera focus pipeline

## 0.7.33 - 2026-09-05

Cinematic system overview, persistent navigation symbols and Pyro presentation-reference pass.

- Pyro body rendering now applies presentation-only reference grades per major planet while preserving the CURRENT-LIVE terrain/control geometry: Pyro I shifts toward the expected cyan/blue appearance, Monox toward muted rose/brown, Bloom toward icy blue/white, Pyro V keeps its green/yellow gas-giant presentation, and Terminus receives the expected muted cold-brown grade plus a restrained presentation halo/ring cue
- WebGL body shader gained explicit reference-grade tint/strength, saturation and brightness controls; these affect only visual presentation and never body-fixed coordinates, route geometry or source authority
- spatial station symbols are now persistent in SYS whenever Stations is enabled, including hidden-but-operational station placements; Levski/People-named Nyx objects are protected from overview LOD/major-only suppression
- procedural station silhouettes are enlarged by a further 3.5x presentation factor with a larger readable screen-space floor while their authoritative placements remain untouched
- added `Illustrated system overview` in Settings: at low SYS zoom, moons and station clusters are screen-space separated around their real parent direction with compact local orbit cues, then continuously blend back to true projected spacing as zoom increases
- all moons become available to illustrated SYS immediately around an on-screen parent, while true-scale projection automatically resumes during approach
- replaced the previous narrow Nyx interior aggregation with a broad, interrupted Glaciem presentation field derived from the CURRENT-LIVE placement-envelope center/radius/thickness; density is deterministic presentation-only and does not modify the region envelope
- persistent asteroid-field labels remain visible in SYS when Labels and Asteroid Fields are enabled
- focus travel now biases camera translation slightly ahead of zoom so Enter/double-click visibly pans toward the destination while approaching instead of appearing to zoom first and re-center afterwards
- added a top-center focused-object zoom slider that smoothly adjusts the active focus distance without changing the selected object or its coordinates
- moved the SYS/GLX control to bottom center and added an accelerating zoom-out / zoom-in transition between system and galaxy views instead of an instantaneous mode swap

## 0.7.32 - 2026-09-05

System-overview visibility and camera-travel pass.

- station placements keep their persistent 2D map symbol even when a larger procedural WebGL station silhouette is rendered at the same authoritative position, so overview navigation no longer loses distributed station markers
- object focus now performs a smooth logarithmic zoom/pan flight instead of an immediate camera jump; search + Enter and normal focus/double-click share this behavior
- Settings now exposes `Smooth focus travel` and selectable travel durations from 0.5 s to 3.5 s; smooth travel can be disabled completely for immediate focus jumps and is persisted locally
- Nyx gets an additional patchy asteroid aggregation derived only from CURRENT-LIVE `RingSegmentVolume` placement bounds: deterministic presentation asteroids are scattered inside each real segment volume, producing a broad interrupted inner-system field instead of presenting the data only as a narrow annular line
- the Nyx aggregation changes presentation density only; segment centers, bounds, system placements and all route/measurement geometry remain unchanged

## 0.7.31 - 2026-09-05

Navigation/search polish, body-relative surface routing, stronger station silhouettes and asteroid-belt aggregation.

- replaced the long cross-system destination dropdown with a full-text predictive destination search; up/down selects suggestions, Enter confirms the target, and clearing the field also clears/resets the active destination; placement and body-fixed surface targets participate in the prediction list
- moved the generated map document from WebView2 `NavigateToString` to a local file-backed renderer cache, removing the WebView2 HTML-size ceiling that became visible once the searchable cross-system target set included surface locations
- all text/search inputs now receive the same compact clear-X behavior, including dynamically created workspace fields
- added a same-body station/orbit-to-surface radial route mode: when one endpoint is a body-fixed surface/atmospheric anchor and the other is a placement orbiting the same physical body, distance is measured from placement altitude above the physical body radius to the anchor altitude instead of incorrectly measuring to the body center; the result is explicitly labelled `BODY_RELATIVE_RADIAL_ALTITUDE_ONLY_ABSOLUTE_SURFACE_PHASE_UNRESOLVED`
- increased procedural station presentation scale by approximately 5x and raised the minimum overview silhouette size while preserving canonical placement coordinates
- added a deterministic diffuse asteroid-belt aggregation layer on top of the existing WebGL asteroid primitives so large known ring regions such as Aaron Halo remain visibly recognizable at system overview scale; individual particles remain presentation-only while ring geometry remains authoritative
- removed the diagnostic surface-authority paragraph and the obsolete `Open body view` / `System view` buttons from the visible Body Targets panel while preserving the underlying controls for compatibility
- no canonical positions, body radii, orbit radii or region geometry are altered by these presentation changes

## 0.7.30 - 2026-09-05

Initial render reliability, search-focus and procedural 3D context pass.

- fixed the stale visibility-cache defect that caused moons/stations to appear only after toggling `Planets & Moons` or `Stations`; visibility/LOD is now recalculated on every map draw so zoom, pan, focus and system-load state are reflected immediately
- body texture payloads are now built for every physical body, not only bodies that currently have surface anchors; every visible moon/planet can therefore receive its presentation texture as soon as it enters the LOD, while moons remain hidden as symbols before their cluster becomes relevant
- increased the bounded resident body-texture set to 12 and kept lazy loading so visible planet/moon clusters can remain fully textured without returning to eager all-body loading
- Search Catalog matches now receive an amber/orange holographic glow; pressing Enter focuses/zooms the best exact/prefix/current match directly on the map
- added a dedicated `Asteroid Fields` display layer and deterministic WebGL procedural asteroid instances for authoritative annular regions and placed asteroid/ring-segment volumes; Stanton, Pyro and Nyx use different restrained material tones while canonical region/volume geometry remains unchanged
- added WebGL procedural station silhouettes at the exact placement coordinates, with approximate class-based physical presentation sizes plus a small visibility floor; this is a presentation LOD and does not alter station coordinates/distances
- retained permanent fine planet-orbit guides in SYS and proximity-gated moon/station detail/orbit behavior

## 0.7.29 - 2026-09-05

System-context visibility, functional workspaces and body-presentation quality pass.

- restored the user-facing `Show Hidden` toggle in Display while keeping the reference-style visibility controls
- fixed missing planet orbit guides at the data layer: `SimulatedOrbitBuilder` now also emits presentation-only circular current-radius guides for Planet and Moon placements; when a physical parent rotation axis is unavailable (notably star parents), the visual guide uses an explicitly labelled system-plane fallback without changing any canonical position or distance
- planet orbit guides remain continuously visible in SYS whenever Orbits is enabled; moon/station orbit/detail layers remain proximity/LOD gated
- relaxed moon LOD from the overly restrictive 0.7.28 threshold so moons become visible as soon as their true projected separation is resolvable; no satellite position is visually displaced or rescaled
- added renderer-ready telemetry for visible planet/moon counts and available planet-orbit guides so blank/missing-layer regressions are detectable immediately from the host status line
- replaced the decorative top-menu behavior with functional in-map workspaces: Database provides a searchable CURRENT LIVE placement table with double-click focus, Tools exposes route/navigation/bookmark actions, Settings controls the actual renderer visibility toggles, and About reports authority/runtime scope; Starmap closes the workspace and returns to the system map
- CURRENT-LIVE derived terrain presentation now uses elevation as the dominant large-scale luminance cue while climate/splat channels remain restrained material-distribution controls, reducing the previous quilt/noise appearance
- derived terrain textures are rebuilt at 2048x2048 with bilinear control sampling; WebGL surface/normal relief is attenuated when a body is magnified beyond the source texture footprint instead of exaggerating source pixels/control noise
- body textures are loaded lazily with bounded resident image sets rather than eagerly loading every body texture in the current system
- no new geographic claims are introduced: Pyro bodies without a true body-specific starmap diffuse still use explicitly marked derived presentation textures, not synthetic authoritative terrain

## 0.7.28 - 2026-09-05

Renderer recovery and reference-GUI convergence pass.

- fixed the 0.7.27 blank-map regression caused by a JavaScript statement boundary error in the system draw path; the renderer now reports runtime JavaScript failures to the WPF status bar instead of failing silently
- added an explicit renderer-ready handshake so the host confirms that the WebGL/Canvas map completed initialization after every system load or switch
- removed the duplicate in-WebView application header so the actual WPF application bar is the single top-level chrome, matching the approved reference layout more closely
- rebuilt the WPF header with functional `Starmap`, `Database`, `Tools`, `Settings`, and `About` actions; Database opens the real object/search surface, Tools opens the real route work area, Settings exposes the real display controls, and About reports the running StarSyncUniverse build/authority scope
- left/right panels now use the same responsive width and flatter technical section headers with per-section glyphs; the visible Display controls now match the approved functional set: Stars, Planets & Moons, Stations, Jump Points, Orbits, Labels, Region Boundaries, Background Nebula
- those Display controls are wired to actual visibility/rendering behavior rather than decorative switches; planet orbits remain true placement-derived presentation geometry and smaller moon/station clusters remain LOD gated
- Data Status, Map Legend, Search Catalog and Objects were restructured toward the reference UI; Objects includes live class counts while the detailed result list retains the established collapse behavior
- Selected Object now exposes functional `Set as Origin` and `Add Bookmark` actions; Measurement/Route adds an explicit functional Calculate Route action on top of the existing route engine
- system overview framing now derives its initial scale from star/planet/jump-point extents instead of distant infrastructure, improving system readability without changing any world coordinate or measured distance
- distant planets use a larger screen-space presentation marker only at system-overview scale; physical radius takes over continuously as zoom increases, so deep surface zoom and BodyFixed projection remain true-scale
- the central map gained a deterministic presentation-only nebula/starfield, system title, hierarchy navigation, scale ruler and compass while authoritative objects, routes, orbits and POIs remain data-driven
- side-panel search and map hover presentation were tightened toward the approved cyan/orange holographic language, including gold object/cursor inspection overlays

## 0.7.27 - 2026-09-05

Starmap-style GUI, system influence plane and material-aware body-depth pass.

- rebuilt the map chrome toward the approved StarSyncUniverse starmap reference: SSU / Universe Map branding, compact cyan/orange technical header, functional `GLX`, `SYS` and `OBJ` tabs, cleaner glass panels and cyan object typography
- `GLX` is functional rather than decorative: it renders the existing authority-labelled `GalaxySchematicBuilder` topology, supports pan/zoom, hover inspection and double-click system switching through the host; it explicitly remains unit-edge topology and does not claim interstellar metric distance
- `SYS` returns to the authoritative current-system coordinate map; `OBJ` opens the catalog/search work surface and focuses the object search field
- added a diffuse system influence/aggregation disc centered on the system star, derived only from the resolved planet-orbit extent and clearly treated as presentation; it does not alter any coordinates or distances
- planet simulated-orbit paths are now always drawn as extremely fine continuous system-context lines; the existing parent-distance guide toggle only controls secondary guides
- small moons/stations remain cluster-LOD gated so system overview stays readable while planet orbit context remains visible
- upgraded presentation-texture reconstruction to use CURRENT LIVE elevation plus decoded planet/ocean material color parameters where available; warm/cool material signals now influence derived terrain presentation instead of the old generic rocky palette alone
- added CURRENT LIVE `*_ddn` normal-map discovery and a lazy WebGL normal-map layer; DDN normals now perturb body lighting without modifying canonical body geometry
- WebGL relief lighting now perturbs the lighting normal instead of only multiplying luminance, producing stronger local depth while keeping authoritative coordinates untouched
- selected/filter-highlighted objects now use cyan/class glows rather than hard white rings; system labels use the starmap-style upper-case cyan name + smaller object-class line
- system star rendering now uses a multi-stage radial glow instead of a flat marker
- WPF host chrome was restyled to the same SSU dark technical/cyan/orange visual language while retaining the existing system selector and LIVE reload function

## 0.7.26 - 2026-09-05

WebGL atmosphere/cloud/relief pass and system-context LOD.

- added presentation-only atmospheric scattering around textured bodies with a soft sun-facing rim, wider terminator transition and subtle limb reflections; true body geometry and BodyFixed coordinates are unchanged
- WebGL body shading now derives a restrained relief response from neighboring texture samples so low-resolution presentation maps read more plastically without inventing terrain geometry
- CURRENT-LIVE `*_clouds_global` / `*_cloud_global` assets are discovered separately from base surface color and used only as cloud coverage/luminance; their encoded RGB values are never treated as final albedo
- bodies with explicit cloud fields receive a lightweight cloud veil; bodies without cloud fields get only a restrained rim/haze presentation and no synthetic geographic cloud pattern
- surface POIs now use ring-and-dot holographic markers, selected POIs use amber emphasis, and labels are collision-limited with leader lines instead of rendering every name on top of every other name
- system context now keeps planetary orbit guides visible while moon/station-scale details use parent-cluster proximity/zoom LOD so the overview stays readable and the smaller objects appear as their orbit center becomes locally relevant
- added optional presentation starfield and a subtle system-orbit-plane reference layer; both are presentation aids and do not claim real astronomical background-star or ephemeris data
- grid, orbit and body-local guide lines were reduced in visual weight to support the requested holographic starmap hierarchy

## 0.7.25 - 2026-09-05

Systemic body-presentation resolver and Pyro false-color correction.

- replaced the unsafe direct climate-map fallback: climate/splat/cloud control textures are no longer exposed as visible RGB surface color
- body texture discovery now accepts both CURRENT-LIVE naming conventions (`*_global_starmap_diff` and `*_starmap_diff`), restoring direct starmap textures for ArcCorp and microTech while keeping all Stanton moons/planets on body-specific presentation assets
- added a centralized presentation-safe resolver with explicit priority: exact body starmap diffuse -> derived terrain presentation -> renderer fallback
- added `BodyPresentationTextureBuilder`: terrain climate+splat controls are converted into subdued tone/structure and optionally graded through the matching CURRENT-LIVE per-body CCH instead of rendering encoded control channels as false color
- added a gas-giant presentation path: Pyro V is detected from its body cloud field plus supplied cloud tint gradient and is reconstructed from those sources rather than forcing a rocky-body fallback
- Pyro audit now resolves all 12 physical bodies without exposing any raw climate/cloud/control RGB map; each body records strategy/source/CCH/status in `body-visual-audit.tsv`
- CURRENT-LIVE Pyro `*_starmap.mtl` placeholder references to Stanton/Hurston are deliberately ignored; only body-specific actual texture sources are eligible
- presentation cache is unified per system so direct and derived textures share one WebView virtual-host root and remain precomputed/cacheable at startup
- startup presentation precomputation now compacts temporary LOH/image-decoder buffers before WebView creation; measured full process-tree memory fell from the 0.7.24 Stanton baseline of ~583.7 MB working set / 410.6 MB private to ~531.6 MB / 383.0 MB in the 0.7.25 Stanton test, with Pyro after switch at ~565.8 MB / 358.5 MB

## 0.7.24 - 2026-09-04

Visual-quality pass for body textures, hover layering, selection glow and orbit guides.

- CURRENT-LIVE body texture discovery now falls back to each body's global climate texture when a system ships no unique `*_global_starmap_diff` asset; this specifically covers Pyro-style body containers without inventing coordinates or geometry
- fallback climate textures remain explicitly tagged as terrain-presentation data rather than authoritative starmap diffuse assets
- object/surface/bookmark hover cards are now forced into the top HUD layer so planets and WebGL bodies can never cover them
- active map selection no longer uses a hard white ring; it uses a class-colored holographic glow and a faint secondary halo
- simulated orbits and moon parent-distance guides now render much finer, lower-alpha and proximity-sensitive, with only a subtle cyan emphasis for the active/hovered orbit
- increased orbit curve sampling to keep very thin lines visually smooth at deep zoom

## 0.7.23 - 2026-09-04

Pointer HUD, search reset, persistent body textures and star-relative illumination.

- coordinate HUD is now a slimmer gold technical readout and automatically moves below an active object/bookmark/surface tooltip instead of covering it
- catalog, cross-system route, selected-surface and bookmark filters now use native search fields with an inline clear/reset control
- CURRENT-LIVE body textures are prepared for all imported systems during startup instead of only on first system selection
- all visible planets/moons with an available CURRENT-LIVE/override texture are rendered through the WebGL body layer, not only the currently focused surface body
- current-system texture images are preloaded and retained so zooming/focusing does not wait for per-body decode/load
- WebGL texture filtering now uses mipmapping plus available anisotropic filtering; native source resolution is preserved rather than inventing synthetic detail
- added star-relative sphere illumination computed only from authoritative system-space star/body positions and the current camera transform; this gives a geometrically meaningful lit/dark hemisphere without claiming a calibrated terrain day/night phase
- absolute terrain longitude registration, absolute spin phase and time-resolved orbital motion remain explicitly unresolved and are not fabricated

## 0.7.22 - 2026-09-04

Sidebar collapse-grid correction.

- fixed CSS Grid auto-placement when the left navigation panel is hidden
- left navigation, center map, and right detail panel now have explicit grid-column/grid-row assignments so hiding either sidebar cannot move the remaining panels into the wrong columns
- the center map remains the center grid item and expands into the zero-width hidden sidebar column as intended
- no navigation, system-data, camera, WebGL, bookmark, coordinate, or routing behavior changed

## 0.7.21 - 2026-09-04

Map-shell layout and system-switch reliability pass.

- left menu and right detail sidebar now use the same fixed width
- both sidebars can be independently hidden/restored from persistent bidirectional arrow buttons on the map edge
- all sidebar sections can be collapsed/expanded with a double-click on their header; existing explicit list-collapse buttons continue to work
- added strict overflow/wrapping rules so long status, authority, coordinate and source strings remain inside their panel surfaces
- system-switch rendering is generation-guarded so a slower previous texture/cache render can no longer overwrite a newer selected system
- initial/reset camera scale is now derived from non-hidden major navigation objects relative to the system star, avoiding hidden/outlier placements making a switched system appear empty
- no universe coordinates, body radii, true-scale rendering, route geometry or bookmark data semantics were changed

## 0.7.20 - 2026-09-04

First production WebGL2 body layer and modern map-shell pass.

- added a fully local/offline WebGL2 renderer bridge under `Assets/MapVisuals/WebGL/starsync-webgl.js`; no CDN/runtime network dependency
- the hybrid production migration now renders a focused body texture on the GPU while retaining the proven Canvas2D interaction, labels, guides, surface-targets and fallback path
- WebGL sphere sampling uses the same yaw/pitch BodyFixed projection convention as surface targets and applies neutral limb shading only; no synthetic day/night direction is implied
- present optional equirectangular surface assets can be projected onto the visible planet/moon disc without altering body radius, placement, SurfaceTarget coordinates or routing geometry
- added CURRENT-LIVE `*_global_starmap_diff.dds` discovery/cache keyed strictly by the imported physical body container; Stanton currently resolves 14 body textures and missing systems/bodies remain a valid fallback state
- CURRENT-LIVE starmap DDS files are batch-decoded through the existing StarBreaker per-build extraction cache and exposed only through a local WebView2 virtual host
- body textures are loaded lazily by the WebView and bounded to a small resident image cache instead of eagerly decoding every available planet/moon texture
- added a runtime WebGL2 status indicator and user-toggle; unsupported devices automatically remain on the Canvas fallback
- added `Export-SurfaceTextureOverride.ps1` to decode an explicitly identified CURRENT-LIVE DDS through StarBreaker into the canonical per-body surface override path with SHA-256/provenance manifest; the tool never guesses a source DDS
- refreshed the map shell with a more compact professional glass/technical UI while preserving existing interaction contracts
- retained the authority gate: texture registration is still presentation-only until body-axis/longitude calibration is proven, and POIs are never moved to match an unregistered image

## 0.7.19 - 2026-09-04

Sync-overlay conflict telemetry and convergence hardening.

- added explicit `OverlayConflictRecord` telemetry for record, tombstone and tombstone-vs-record merge decisions
- merge results now expose deterministic conflict-resolution details without changing the winning-record algorithm
- extended post-import integration diagnostics with a three-client merge-order convergence proof
- existing higher-revision tombstone delete and newer-record resurrection proofs remain active
- no SyncHost write is performed; transport/auth remain separate integration work

## 0.7.18 - 2026-09-04

Renderer/navigation performance pass without changing universe geometry.

- pre-indexed SurfaceCoverage and surface targets by body name/source UUID instead of repeatedly scanning the complete lists during draw, hover and surface selection
- cached physical bodies by name in addition to UUID to remove repeated linear body lookup during seamless surface projection
- cached the active seamless body overlay per rendered frame; individual entity draws no longer rebuild the same body/surface association repeatedly
- retained projected entity coordinates from the last draw for hit testing, avoiding full world-to-screen reprojection on every mouse-move event
- retained projected bookmark coordinates from the last draw for bookmark hit testing
- inter-system topology results are cached inside the route planner and JumpConnection lookup is indexed by connection ID
- no change to true-scale body rendering, coordinate frames, distances, bookmark semantics or authority gates

## 0.7.17 - 2026-09-04

Bookmark navigation-context completion.

- unified context labels to `Bookmark › Save`, `Bookmark › Edit`, and `Bookmark › Delete`
- right-clicking a bookmark now keeps the route/direct-line actions available in the same context menu while adding bookmark Edit/Delete actions
- bookmarks can now be used directly as route/direct-line Start or Destination targets
- system-space bookmarks resolve as system XYZ navigation targets; BodyFixed bookmarks remain body-local navigation targets and never masquerade as system XYZ
- direct-line distance is computed only when both endpoints share the same proven coordinate frame; cross-frame direct distance remains explicitly undefined
- BodyFixed bookmark routing is routed through the existing Surface/OM authority gate and remains blocked until proven OM plus BodyFixed-to-System phase data are available
- existing right-side bookmark list Focus/Edit/Delete behavior remains unchanged

## 0.7.16 - 2026-09-04

Cursor bookmark selection fix.

- right-clicking an empty map coordinate now clears the active object/surface selection while preserving the current camera focus
- `Save > Bookmark` on an empty map coordinate now always captures the cursor coordinate instead of falling back to a previously selected object
- bookmark right-click `Bookmark › Ändern` / `Bookmark › Löschen` behavior from 0.7.15 remains unchanged

## 0.7.15 - 2026-09-04

Bookmark context-menu refinement.

- right-clicking directly on a rendered bookmark now switches the context menu into bookmark mode instead of showing generic navigation actions
- bookmark context menu exposes `Bookmark › Ändern`, reopening the same full bookmark editor used during creation with all current details prefilled
- bookmark context menu exposes `Bookmark › Löschen`, deleting the clicked bookmark immediately through the existing host/store mutation path
- existing right-side bookmark list Focus/Edit/Delete behavior remains unchanged
- bookmark geometry, categories, colors, visibility and true-scale universe rendering are unchanged


## 0.7.14 - 2026-09-04

Context-driven bookmark workflow and map bookmark presentation.

- moved bookmark creation to the map context menu: `Save > Bookmark` captures either the exact object/surface target under the cursor, the active selected object, a front-hemisphere body-local surface point, or the current cursor point on the camera focus plane
- added persistent bookmark scope classification `SURFACE` / `ORBIT` / `SPACE` without changing Universe coordinates or distances; body-local orbital anchors remain BodyFixed and are tagged ORBIT
- added always-on cursor coordinate readout in the system viewer; object and surface-target hits expose their exact coordinates, arbitrary surface points expose BodyFixed XYZ, and empty-space cursor points are explicitly derived on the current view focus plane
- bookmark save/edit now uses an in-view overlay with name, category (`Mineralien`, `Infos`, `Material`, `Gefahr`, `Piraten`, `Basis`, `Sammelpunkt`), free-text note, map color, map visibility and read-only captured coordinates
- bookmark records now persist category, note, color, map visibility and scope; existing 0.7.12/0.7.13 bookmark files are normalized with safe defaults on load
- added arbitrary BodyFixed bookmark creation for cursor-selected surface points while requiring an already proven BodyFixed frame for that physical body
- visible bookmarks render as small narrow crosses in their configured color; mouse-over shows name, category, scope and XYZ; right-side bookmark entries support Focus, Edit and Delete
- map visibility changes redraw immediately; no bookmark metadata mutates base Universe geometry, placement, routing or authority
- documented the future Bookmark Manager scope: grouping, categorization, multi-selection, bulk delete/move/copy/edit and later Corporation/Alliance sharing over SyncHost

## 0.7.13 - 2026-09-04

Bookmark editing and free-coordinate authoring.

- extended the local bookmark contract with metadata updates while preserving immutable frame/coordinate identity for each bookmark
- Bookmarks panel can now edit name, comma-separated tags and visibility; updates increment the existing local revision through the atomic bookmark store
- added free System XYZ bookmark creation from the viewer with finite-number validation and explicit system frame ownership
- bookmark filtering now includes visibility and tags in addition to name/system/coordinate kind
- all bookmark authoring remains a mutable user overlay; no authoritative universe entity, placement, body radius, distance or routing coordinate is modified
- retained the 0.7.10 true-scale body baseline and continuous system-to-surface behavior unchanged

## 0.7.12 - 2026-09-04

Bookmark UI and persistent local user-navigation integration.

- wired the existing frame-aware `BookmarkService`/`LocalBookmarkStore` into the standalone viewer without changing base-universe data
- added a right-side Bookmarks panel with live filtering, count, focus and delete actions
- selected system placements can be saved directly as placement-snapshot bookmarks; selected surface targets can be saved as BodyFixed bookmarks
- bookmark mutations are handled by the WPF host and pushed back to the WebView as an updated bookmark state without reimporting the universe
- placement/system bookmarks focus using their stored frame-relative coordinates; BodyFixed bookmarks resolve back to the matching CURRENT-LIVE surface target when available and deep-focus through the existing continuous system-to-surface path
- bookmark persistence remains local under the existing user-data store and does not modify, override or contaminate authoritative Data.p4k universe geometry
- retained the 0.7.10 true-scale body baseline and 0.7.11 surface presentation behavior unchanged

## 0.7.11 - 2026-09-04

Surface presentation/runtime usability pass following the 0.7.10 true-scale body baseline.

- kept the 0.7.10 true-scale body/system-distance behavior unchanged as the renderer baseline
- added WebView2 virtual-host mapping for `Assets/MapVisuals`, allowing optional local surface presentation assets to be consumed without CDN or external web dependencies
- optional per-body surface textures are now loaded through the stable canonical-ID asset path and rendered as the base layer of the diagnostic equirectangular 2D surface map; the 3D/system body remains untextured until geographic axis/registration calibration is authoritative
- surface texture descriptors now read and expose pixel width/height when the local asset is decodable
- expanded the selected-body `Surface locations` panel with live filtering and match counts while preserving click-to-highlight and double-click deep focus
- selected surface-location details now provide `Set route start`, `Set route destination` and `Focus` actions; surface navigation stays BodyFixed and continues to fail closed until a proven CURRENT-LIVE OM and BodyFixed->System phase are available
- no synthetic OMs, no synthetic surface world coordinates, no scale or distance modification

## 0.7.10 - 2026-09-04

True-scale body approach and surface-focus rendering.

- removed the fixed 70x body-radius presentation magnification from deep/system projection; physical body radius now projects at true scale once it is larger than a small screen-space visibility floor
- retained only a tiny minimum on-screen marker radius for distant planets/moons so system navigation remains usable without falsifying body-to-body distance relationships
- continuous surface targets remain attached to the same displayed body radius, so body-local surface placement stays geometrically consistent while zooming from system scale down toward the surface
- non-focused stars/planets/moons are de-emphasized once the selected body enters continuous surface rendering, reducing visual overlap without changing any system coordinates, distances, routing geometry or orbital data
- deep zoom remains available: increasing camera zoom enlarges the true projected body radius naturally until the surface fills and exceeds the viewport

## 0.7.9 - 2026-09-04

Deep surface zoom, selected-body surface list and guarded surface-route foundation.

- removed the 180 px apparent-body clamp that prevented system-view zoom from ever reaching the surface; body discs and their continuous surface overlays can now grow far beyond the viewport with only a large safety cap
- raised the interactive zoom ceiling and made wheel zoom remain cursor-anchored even while an object is focused
- added a right-side `Surface locations` list for the selected planet/moon with name, category, diagnostic axis lat/lon and altitude; click highlights a location and double-click zooms/recenters toward it in the normal system view
- surface markers can now be picked by the navigation context menu and are represented as BodyFixed surface navigation targets rather than fabricated system XYZ points
- route planner now authority-gates surface routing: a surface leg requires a proven CURRENT-LIVE OM for the same body and a proven BodyFixed->System phase; until those are available it returns an explicit blocked status instead of inventing OM placements or surface world coordinates
- updated the project plan with the deep-zoom acceptance criteria and the required Surface <-> OM <-> system route policy

## 0.7.8 - 2026-09-04

Camera orbit pivot and body-focus interaction fix.

- middle-mouse yaw/pitch no longer implicitly rotates around the system origin when a meaningful local focus exists
- orbit pivot priority is now active selected/focused object -> object under cursor -> existing view focus
- changing the middle-mouse orbit pivot preserves the pivot's current screen position, preventing the visible jump that made close body navigation difficult
- physical planets/moons always use close-body focus on first double-click regardless of the generic parent/orbit focus preference
- repeated double-click on an already focused object zooms further inward instead of recomputing a wider framing
- project plan updated with explicit cursor/selection-relative orbit-pivot requirements for seamless System -> Body -> Surface navigation

## 0.7.7 - 2026-09-04

System-view seamless surface projection fix.

- fixed the body/coverage association used by the continuous system-view surface overlay: orbital body placement UUIDs and physical body/source UUIDs can differ, so the renderer now resolves coverage by direct UUID, physical-body UUID and body name instead of requiring one exact UUID match
- fixed the apparent-body-radius calculation for the seamless overlay so it uses the resolved physical body radius directly; previously a UUID mismatch could collapse the overlay radius to the generic 2.5 px fallback even while the planet was visibly rendered at full size
- lowered the activation threshold slightly so body-local targets appear earlier during approach while remaining hidden in distant system overview
- retained the existing body-local projection, front/back hemisphere treatment, labels and click-through details; no synthetic surface targets or coordinate assumptions were introduced

## 0.7.6 - 2026-09-04

Surface texture asset contract and runtime discovery foundation.

- added `SurfaceTextureDescriptor` and `SurfaceTextureAssetResolver` for optional per-body surface presentation assets keyed by the same canonical body identity used by the Universe graph
- stable install path: `Assets/MapVisuals/Surfaces/Overrides/by-canonical-id/<safeCanonicalId>.png|jpg|jpeg|webp`
- renderer payload now exposes per-body texture availability/status without treating a presentation texture as geometry or coordinate authority
- body-target UI reports whether a surface texture asset is present while keeping day/night explicitly disabled until absolute spin phase/orientation is proven
- missing textures remain fully valid and fall back to the technical continuous body/grid projection introduced in 0.7.5

## 0.7.5 - 2026-09-04

Continuous body/surface projection in the standalone StarSyncUniverse viewer.

- focused/selected physical bodies now expose CURRENT-LIVE body-local surface targets directly in the normal system zoom once the rendered body reaches a useful apparent size; no explicit body-view switch is required
- surface targets are projected onto the same visually magnified body disc used by the renderer, so marker placement stays attached to the visible planet/moon instead of shrinking to un-magnified physical scale
- front/back hemispheres are distinguished from the current camera projection; back-side markers are de-emphasized while front-side labels appear progressively at higher zoom
- higher body zoom adds a technical surface-grid overlay as a transition layer for future registered planet/moon textures without claiming geographic calibration
- focusing a physical body automatically selects its Body Targets dataset; clicking a seamlessly projected marker opens the existing authoritative surface-target detail view
- bodies with no CURRENT-LIVE surface anchors remain marker-free placeholders even at high zoom
- project plan updated with the mandatory continuous System -> Orbit -> Body -> Surface transition and the rule that orbital infrastructure must remain visible during body approach

## 0.7.4 - 2026-09-04

Optional Data.p4k station-model extraction proof and reproducible override-pack tooling.

- proved the Port Tressler presentation model source from its authoritative CURRENT LIVE body-child placement: `rs_ext_mic-leo1.socpak`
- successfully exported the station ObjectContainer through StarBreaker `socpak export` to GLB and inspected the result: 3,037 nodes, 272 meshes, 846 primitives and 3,106,603 vertices in the LOD2/materials-none proof
- added `tools/Export-MapVisualOverride.ps1` to produce canonical-ID-named GLB overrides with SHA-256/provenance manifests
- added diagnostics that verify a canonical-ID GLB override wins over class/default fallbacks
- documented exporter warnings for unresolved local designer-brush companion files; these remain a completeness item before a production model pack is declared complete
- model assets remain presentation-only and cannot alter canonical identity, placement, routing or Universe authority

## 0.7.3 - 2026-09-04

Technical surface-map view and placeholder-body inspection.

- renderer payload now carries deterministic visual classes for system entities and surface targets
- added a switchable body-local 3D / diagnostic 2D equirectangular surface view
- surface targets expose normalized map coordinates and explicit uncalibrated projection status
- placeholder/definition-only bodies can now be opened even with zero CURRENT LIVE surface targets, so Nyx body definitions remain inspectable without fabricating POIs
- body coverage carries the available physical reference radius for empty surface views
- surface view explicitly reports that texture registration and absolute day/night phase are unresolved rather than inventing geographic truth
- details expose visual class and projection provenance

## 0.7.2 - 2026-09-04

Map visual override contract and surface-map projection foundation.

- added `MapVisualClass` and representation contracts for stations, cities, landing zones, outposts, mining/research bases, facilities, bunkers, caves, settlements, comm arrays, relays, jump points, surface POIs, celestial bodies and spatial regions
- added `MapVisualAssetResolver` with deterministic priority: canonical-ID override -> class override -> StarSync standard 3D model -> StarSync standard icon -> built-in fallback
- optional Data.p4k model packs are isolated below `Assets/MapVisuals/Overrides` and can automatically replace default visuals without changing canonical universe identity or geometry
- added a stable asset-directory contract and project output-copy rule so visual packs can be installed independently of the core binary
- added equirectangular surface-map projection and geometric day/night terminator generation from an explicitly supplied sub-solar direction
- terminator rendering remains authority-gated: no automatic current-time day/night boundary is produced until absolute body orientation/spin phase is proven
- updated the total project plan with system/orbit/body/surface/POI view hierarchy, Nyx placeholder behavior, optional real-model overrides, standard visual requirements and surface texture/POI/media requirements

## 0.7.1 - 2026-09-04

Surface-target diagnostic view foundation and explicit CURRENT LIVE body-definition-only state.

- added a body-target catalog built only from CURRENT LIVE body-local anchors classified as near-surface/elevated; no SCUnpacked geometry is introduced
- added a technical body-local viewer that projects real body-local XYZ anchors around the selected physical body and exposes category, scope, altitude, raw axis-spherical projection, source and data status
- body-target lists are filtered/collapsible and do not materialize unavailable targets
- bodies with a physical CURRENT LIVE definition but zero body-local surface anchors remain visible in the body selector as `CURRENT_LIVE_BODY_DEFINITION_ONLY_NO_SURFACE_ANCHORS`; opening a false surface view is disabled
- this makes Nyx I/II/III explicit as current physical/body placeholders with no current surface-target dataset, while Levski/People's Service/other orbital infrastructure remain in the system-level catalog where they belong
- added `GetSurfaceTargets` / `SearchSurfaceTargets` to the internal Universe service boundary for later Alpha/UI integration
- no geographic longitude convention or absolute spin phase is promoted; body-local XYZ remains the authoritative surface-target coordinate and derived lat/lon stays diagnostic only

## 0.7.0 - 2026-09-04

Routing runtime/cost-analysis milestone.

- added `NavigationRouteAnalyzer` as a separate operational layer over geometry/routing; it reports total segments, in-system segments, interstellar transits and measurable local distance without assigning a fabricated metric distance to transit legs
- added the user-supplied current gameplay rule `25% available quantum fuel per interstellar transit` as an explicit cost-policy datum, separate from Universe geometry and with aggregation semantics intentionally left unresolved
- route result payload now carries route-summary metadata so the viewer does not need to re-derive authoritative segment/transit counts
- route panel can show the per-transit QT fuel rule when an interstellar transit is present while keeping local measurable distance separate
- `UniverseRoutePlanner` now caches one `InSystemVisibilityRouter` per system instead of rebuilding it for every route leg
- `InSystemVisibilityRouter` now pre-indexes/de-duplicates QT/navigation candidates once per imported dataset; repeated route requests reuse those candidates and the obstacle set
- `UniverseService` and the standalone WPF host reuse a persistent route planner, reducing allocations and repeated candidate/obstacle setup during interactive routing
- strengthened G6 regression proof for Terminus (Pyro) -> Baijini Point (Stanton): segment count, interstellar-transit count, undefined transit metric and 25%-per-transit cost-policy metadata must all remain consistent

## 0.6.4 - 2026-09-04

Catalog result-density and route terminology refinement.

- object-result list is now collapsible; with category `All` it starts collapsed by default to avoid materializing hundreds of result rows unnecessarily
- explicit user expand/collapse choice is persisted locally and overrides the automatic `All` default on later sessions
- collapsed result lists still compute and show the match count but skip DOM row construction, reducing unnecessary WebView layout work
- category-specific searches auto-expand only while no explicit user preference exists
- reciprocal jump legs are now presented as `Interstellar Transit` instead of `JUMP (metric unresolved)`; measurable local distance remains strictly separate from non-metric interstellar transitions
- route summary now labels the transition count as `Interstellar transits` while retaining segment-by-segment accounting

## 0.6.3 - 2026-09-04

JumpPoint operational-visibility fix.

- fixed Pyro/Stanton/Nyx jump-point catalog visibility: CURRENT LIVE jump placements can carry `Hidden=true` reference metadata while still being required operational navigation endpoints
- JumpPoint and Gateway entities now remain visible/searchable in the technical map/catalog even when `Show hidden` is disabled
- global cross-system target catalog now retains JumpPoint/Gateway entries instead of dropping them with generic hidden filtering
- added regression diagnostics proving every source jump placement is classified as `JumpPoint`; hidden source metadata is no longer allowed to suppress operational navigation objects
- no geometry or jump coordinates were changed; routing continues to use the existing CURRENT LIVE placements and paired jump graph


## 0.6.2 - 2026-09-04

Cross-system Set Start/Destination persistence, route-segmentation summary, and zoom/focus interaction correction.

- `Set > Start` and `Set > Destination` now persist in the WPF host across system switches; a start selected in Pyro and a destination selected later in Stanton resolve through the same authoritative `UniverseRoutePlanner`
- when start and destination belong to different system frames, navigation mode is forced automatically to `Route`; `Direct line` is not allowed to imply a metric distance across independent system frames
- route results now expose the requested operational summary in the viewer: measurable total local distance, number of route segments, number of jump transitions, and each segment with its own distance or explicit unresolved jump metric
- route rendering remains system-frame safe: only non-jump legs belonging to the currently displayed system are projected
- single-click selection no longer changes wheel zoom anchoring; wheel zoom remains cursor-centric for selected-but-unfocused objects
- double-click remains the only object-focus action and centers/zooms to the active object according to the configured focus mode/margin
- added `Unset zoom / focus` to the right-click menu and Escape-key handling; leaving object focus preserves the current visual viewport while returning wheel zoom to cursor-centric behavior

## 0.6.1 - 2026-09-04

Cross-system route selection and route-regression hardening.

- added an explicit regression proof for the requested `Terminus (Pyro) -> Baijini Point (Stanton)` route; the route must contain one reciprocal jump leg, keeps the jump metric distance unresolved and validates every measurable local leg against proven planet/moon obstacles
- extended the diagnostic WebView route bridge so start and destination may belong to different imported systems
- added a lightweight global route-target catalog to the diagnostic viewer; a current-system start can select/search a destination in Stanton, Pyro or Nyx without fabricating shared XYZ coordinates between system frames
- cross-system direct distance is explicitly reported as undefined; route output instead reports measurable local legs plus a non-metric JUMP leg
- route drawing renders only the local legs belonging to the currently displayed system and no longer tries to project a destination from another system into the active system frame
- continued CURRENT LIVE OM/star-obstruction discovery; old SCUnpacked `starmap.json` exposes an OM-1 template and star QT obstruction metadata, but no current-build placement/source has yet been proven, so neither is promoted to authority

## 0.6.0 - 2026-09-04

Nyx structural-completion, local galaxy-position and obstruction-aware navigation milestone.

- generalized CURRENT LIVE Nyx hierarchical ObjectContainer resolution with bounded/cached recursion and semantic promotion only; anonymous decorative/internal containers are traversed when needed but are not dumped into the normal catalog
- locally resolves Levski plus People's Service Station Alpha, Delta, Theta and Lambda and their PSS clinics; cross-build SCUnpacked contributes display identity/QT metadata only after strict UUID + locally derived world-XYZ agreement
- Nyx hierarchical proof currently resolves 96 carrier/child containers and promotes 9 relevant nested placements with max SCUnpacked world-position delta 0.000000 m; geometry remains Data.p4k authority
- Nyx I/II/III physical-body identity is recovered from CURRENT LIVE body containers and exposed as planets without copying old reference geometry
- added a locally derived Glaciem ring placement envelope from CURRENT LIVE segment centers/bounds; the huge `glaciemring_bg` AABB is retained as technical data but no longer presented as the ring itself in the diagnostic map
- added CURRENT LIVE `SSolarSystem.galacticPosition` import for Stanton, Pyro and Nyx; raw coordinates are preserved with unit semantics explicitly unresolved instead of being converted to invented astronomical distances
- added `InSystemVisibilityRouter`: local route legs now test proven planet/moon physical-body spheres and can use locally available QT/navigation placements as intermediate nodes rather than always drawing a single Euclidean segment through a body
- inter-system routing keeps local visibility legs separated by reciprocal jump legs; jump traversal distance remains null/unknown rather than being folded into metric system distance
- connected WebView navigation requests to the C# Universe routing service so the diagnostic viewer can consume the same route-leg model as the runtime core
- expanded catalog/navigation classification for Nyx transit points, gateways, breaker/extraction/logistics stations and outposts without changing source authority
- versioned Release build and full headless import validated with zero build warnings/errors and structural Validation PASS for Stanton, Pyro and Nyx
- orbital-marker placement source and authoritative stellar obstruction radius remain intentionally unresolved; no synthetic OM-1..OM-6 or star radius is generated

## 0.5.3 - 2026-09-04

Navigation interaction and projected measurement-label milestone.

- unfocused/unselected map zoom is now cursor-centric: wheel zoom preserves the world point under the mouse cursor instead of always zooming toward screen center
- added right-click navigation context menu with `Set > Start`, `Set > Destination`, `Mode > Direct line`, `Mode > Route`, and clear action
- added a dedicated `Measurement / Route` result panel showing start/destination, direct 3D distance, horizontal distance, delta XYZ, azimuth, elevation and coordinate frame
- direct measurement line now carries a viewport-clamped projected distance label that follows the 3D projection and chooses an above/below offset based on the current view
- route mode is visually distinct and explicitly reports same-system direct-leg status; jump/gate segmentation remains reserved for cross-system/Galaxy context instead of fabricating legs inside a single-system view
- reset camera now also clears focused/selected state so cursor-centric zoom behavior is restored deterministically

## 0.5.2 - 2026-09-04

Runtime spatial-query/index milestone.

- added immutable `SystemVolumeIndex` per system; spatial-volume containment no longer performs a full volume scan for every query
- added transport-neutral `SpatialPositionSnapshot`/`GetPosition(...)` runtime access using the explicit `SYSTEM_AXES_METERS` frame
- bumped `StarSyncUniverse.Contracts` to API 1.2 with `GetPositionAsync(...)` so StarSync Alpha can consume position/frame authority without duplicating coordinate logic
- strengthened post-import regression diagnostics: position/measurement frame consistency is verified and every imported Pyro/Nyx volume center must resolve through the new volume index
- diagnostic viewer now supports an explicit measurement origin: select an object, set it as origin, then select another object to see 3D distance, delta XYZ, azimuth and elevation; a thin diagnostic line connects the pair
- measurement remains derived from current authoritative placements; no QT/ship performance assumptions are introduced

## 0.5.1 - 2026-09-04

Spatial measurement/API integration milestone.

- added cached per-system catalog materialization inside `UniverseService`; repeated runtime/API searches no longer rebuild/sort the full catalog on every query
- added `SpatialMeasurement` with system-frame delta XYZ, direct 3D distance, XY-plane distance, elevation delta, system-XY azimuth and elevation angle
- added runtime `Measure(...)` and `FindVolumesContaining(...)` services; volume containment is evaluated against authoritative local bounds without generating synthetic region geometry
- bumped the open `StarSyncUniverse.Contracts` catalog API to 1.1 with transport-neutral spatial measurement and volume-query contracts for later StarSync Alpha integration
- API capabilities now advertise spatial measurements and spatial-volume queries explicitly
- runtime selected-object diagnostics now show distance/elevation relative to the system star and report any containing proven spatial volumes
- added regression diagnostics proving Port Tressler measurement math and Pyro volume-center containment
- no flight-time/QT-time values are fabricated yet; those remain a later drive/ship performance layer over the verified geometry

## 0.5.0 - 2026-09-04

Spatial-volume foundation milestone for asteroid/gas/ring infrastructure.

- extended `StarBreakerClient.ExtractAsync` with optional CryXML conversion while preserving build/filter-aware extraction caching
- added `PlacedSpatialVolumeImporter`, grouping candidates by shared source SOC so each unique template is extracted and parsed only once
- CURRENT LIVE Nyx gas-cloud and Glaciem-ring segment placements can now be promoted to `SpatialVolume` using direct system placement XYZ plus direct root min/max bounds and radius
- volume authority remains `Data.p4k/System ObjectContainer placement + CURRENT LIVE ObjectContainer root bounds`; no inferred density, shape or dynamic movement is fabricated
- added `GasCloud` and `RingSegment` catalog categories alongside `AsteroidCluster`/`AsteroidField`
- diagnostic renderer keeps these spatial classes searchable/highlightable and renders proven volume bounds without introducing a production GPU renderer

## 0.4.4 - 2026-09-04

Catalog context/highlight behavior and asteroid-volume visualization milestone.

- central system stars now remain visible in system-map mode even when catalog filters are active, preserving the system reference point
- added configurable catalog map behavior: `Highlight matches` (default) keeps normal context and highlights search/category hits, while `Filter map to matches` reduces the map but still keeps the system star visible
- search/category matches are forced visible even when `Major objects only` would normally hide their category, so catalog lookup remains useful for deep objects
- added `AsteroidCluster` and `AsteroidField` catalog classifications and technical marker/legend support
- CURRENT LIVE spatial volumes are now supplied to the diagnostic renderer; Pyro's proven asteroid-cluster volumes render as projected 3D AABB wireframes using their LOCAL_DIRECT bounds
- data-status panel now exposes spatial-volume count and the regions panel lists volume type, bound radius and authority status
- fixed region/volume renderer payload casing by projecting explicit browser DTOs instead of relying on record-property casing
- no GPU/OpenGL/WebGL dependency was introduced; all work remains foundation/diagnostic oriented

## 0.4.3 - 2026-09-04

Catalog initialization regression fix.

- fixed WebView2 `NavigateToString` catalog initialization being aborted by direct `localStorage` access on an opaque/unsupported document origin
- focus preference persistence now uses exception-safe storage access and gracefully falls back to session-only values when browser storage is unavailable
- catalog category population, object-list initialization and search/filter wiring can no longer be prevented by unavailable browser storage
- no Universe import, catalog classification or authoritative spatial data behavior was changed

## 0.4.2 - 2026-09-04

Viewer filter/detail regression fix.

- fixed a JavaScript regression where the simulated-orbit lookup map had been renamed during renderer optimization while detail/focus/hover code still referenced the old identifier; this prevented selected-object details and could interrupt focus/hover behavior
- catalog text/category filters now drive both the object list and the map-visible entity set through the same cached predicate
- changing search text or category invalidates the visible-entity cache and redraws the map immediately
- selected-object details now also show system, direct parent name and placement ID in addition to category/type/UUID/XYZ/source/orbit data
- kept the optimized cached visibility/projection path; no new renderer dependency or large in-memory index was introduced

## 0.4.1 - 2026-09-04

Hierarchical focus/navigation and renderer-efficiency milestone.

- object-list single click now selects/details only; double click performs camera focus, matching map interaction semantics
- added persistent focus framing settings: `Object + parent / orbit` and `Object close`, plus configurable focus margin presets stored in browser localStorage
- parent framing centers the actual parent body and chooses zoom from the direct parent distance; close framing uses body radius or a bounded fraction of the object's orbital radius/distance
- focus remains a camera-only operation and does not alter authoritative Universe coordinates or placements
- cached visible-entity filtering to avoid repeated full scans on every draw when display filters are unchanged
- cached per-frame trigonometric/projection state and precomputes projected depth once per visible entity before sorting, reducing redundant math during pan/tilt/zoom
- object-list DOM updates now use a DocumentFragment to reduce incremental layout work
- all optimizations preserve the current Canvas2D diagnostic renderer; no GPU/OpenGL/WebGL dependency has been introduced
- added `LagrangeInfrastructureImporter`: direct rest-stop/station children of CURRENT LIVE Lagrange-point SOCs are promoted into the normal system graph using parent-relative local XYZ
- Stanton gains 18 direct Lagrange station placements; Pyro gains 9; Nyx currently exposes none through the same rule
- cross-build SCUnpacked names are accepted only when the same source UUID also reproduces the locally derived world XYZ within 1 meter; geometry remains CURRENT LIVE Data.p4k authority
- added a dedicated Lagrange infrastructure transform diagnostic so these promotions remain regression-testable independently of body-orbital stations

## 0.4.0 - 2026-09-04

Orbital-infrastructure catalog milestone and persisted simulated-orbit foundation.

- promoted direct body-orbital infrastructure from CURRENT LIVE body SOC roots into the normal system entity/canonical/frame/placement graph
- Stanton now resolves 9 additional direct body-orbital infrastructure placements; the major stations Everus Harbor, Seraphim Station, Baijini Point and Port Tressler are all present with CURRENT LIVE body-local geometry and derived system XYZ
- station display identity may use the older SCUnpacked catalog only when both starMap UUID and direct parent UUID match; geometry remains CURRENT LIVE Data.p4k authority and is explicitly marked `LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY`
- added a permanent cross-build transform proof for promoted infrastructure; all 9 matched reference placements currently reproduce world XYZ with zero reported delta at snapshot precision
- fixed placement identity for repeated body-SOC template GUIDs by scoping placement IDs to parent body UUID plus starMap UUID instead of treating raw `Child.guid` as globally unique
- added persisted `SimulatedOrbitRecord` data to every system snapshot
- circular visualization orbits pass through the current authoritative station position and choose the minimum-inclination plane compatible with the parent rotation axis; they are explicitly tagged `SIMULATED_ORBIT_GEOMETRY_NOT_DYNAMIC_TRUTH`
- Stanton currently produces 27 simulated body-relative visualization orbits, including Port Tressler, Baijini Point, Everus Harbor, Seraphim Station and Grim HEX; Pyro currently produces 2
- simulated orbit lines are rendered as true 3D sampled circles, highlight on selection or after two seconds hover, and expose parent/radius/status in the selected-object panel
- added `--headless-import` deterministic import/validation mode for repeatable milestone tests without WebView/UI startup
- all three systems continue to pass structural validation after infrastructure promotion

## 0.3.5 - 2026-09-04

Orbital-guide interaction foundation.

- body-orbiting stations, comm arrays, landing zones/nav points and moons now receive thin parent-distance reference guides when a direct physical-body parent is known
- selecting an eligible object highlights its guide immediately
- hovering an eligible object for two seconds highlights the same guide without changing selection
- guide styling is deliberately marked as a measured parent-distance reference, not a proven physical orbit; no orbital plane, inclination or period is fabricated from a single snapshot position
- this interaction/state layer is intentionally reusable by a future LOCAL_DIRECT dynamic-orbit renderer once plane/period parameters are proven

## 0.3.4 - 2026-09-04

Catalog-aware technical map markers and navigation-object visibility milestone.

- map rendering now uses the catalog category for marker semantics instead of drawing every non-body object as the same dot
- added distinct technical marker shapes for stations, security stations, shipping hubs, comm arrays, Lagrange points, jump points, anomalies, landing zones and nav points
- physical stars/planets/moons keep radius-aware rendering while non-body navigation objects use stable screen-space symbols
- `Major objects only` now includes catalog-classified navigation infrastructure, so comm arrays/Lagrange points/stations are visible without disabling the major-object filter
- added a live map legend using the same category/color mapping as the Canvas renderer
- selection details and hover tooltips now show the catalog category
- hit testing uses the actual marker radius for the category

## 0.3.3 - 2026-09-04

Open catalog API contract for StarSync Alpha/database enrichment integration.

- added standalone `StarSyncUniverse.Contracts` class library with no WPF dependency
- added versioned `IUniverseCatalogApi` contract (`1.0`) for search, placement lookup, source-UUID lookup and capability discovery
- added transport-neutral `UniverseCatalogObject` DTO preserving system, XYZ, parent references, UUID, QT state, source provenance and data status
- added `IUniverseCatalogEnrichmentProvider` extension point so StarSync Database Foundation can attach structured details without changing or replacing LOCAL_DIRECT spatial truth
- enrichment payloads are provider/schema/build scoped JSON documents, allowing missions, locations, shops, factions, media or database metadata to evolve independently
- added `UniverseCatalogApiAdapter` bridging the standalone Universe core to the open contract; zero enrichment providers remains a valid pure-local mode
- added the Contracts project to the local solution and referenced it from StarSyncUniverse

## 0.3.2 - 2026-09-04

Navigation catalog and schematic galaxy-layout foundation.

- added `UniverseCatalogEntry` plus `UniverseCatalogBuilder` to classify local LIVE objects into stars, planets, moons, jump points, Lagrange points, comm arrays, security stations, shipping hubs, stations, landing zones, nav points, anomalies, manmade objects and asteroids
- extended `IUniverseService` with bounded catalog search over name, category, type, class, UUID and source path
- technical viewer now includes a searchable object catalog with category filter and direct focus-on-result behavior
- catalog entries retain current system XYZ, source UUID/path, parent references, QT flag, authority and data status
- verified current LIVE top-level Stanton station/navigation data includes comm arrays, Covalex Shipping Hub, Security Post Kareah and all Stanton L1-L5 placements present in the current system SOC; Pyro exposes its current Lagrange placements directly
- added `GalaxySchematicBuilder`: Pyro is used as a schematic anchor when present and reciprocal neighboring systems are placed along normalized local jump-endpoint directions
- schematic edge length is explicitly unit/stylized only and is never interpreted as physical interstellar distance
- physical `GalaxySystemRecord.GalaxyX/Y/Z` remains unresolved/null; schematic display coordinates are stored separately to prevent accidental promotion to astronomical truth

## 0.3.1 - 2026-09-04

Technical 3D viewer foundation and physical body-scale rendering.

- status line now shows the assembly version continuously
- technical map now preserves X/Y/Z in camera projection instead of flattening Z away
- middle mouse drag orbits/tilts the camera; left drag pans
- double-click focuses an object and moves the floating viewer origin to that object
- wheel zoom range expanded substantially for close inspection
- physical-body display radius now derives from the LOCAL_DIRECT body radius with one common display magnification, preserving relative body-size ratios (for example Crusader versus moons)
- parent-distance guides and asteroid-belt guides tilt with the camera rather than remaining screen-plane circles
- object hit testing now accounts for displayed body radius
- renderer remains a technical Canvas diagnostic stage; production WebGL2/Three.js migration remains planned

## 0.3.0 - 2026-09-04

Universe service, routing and frame-relative bookmark foundation expanded.

- added `UniverseRoutePlanner` with explicit in-system static legs and reciprocal jump legs
- route distance sums only locally measurable in-system Euclidean legs; jump traversal distance/cost is intentionally left unresolved
- extended `IUniverseService` with typed region/volume access and navigation-route planning
- added relative BodyFixed <-> System projection methods using proven period/axis while keeping absolute phase unresolved
- added `BookmarkFactory` for system-point, placement-snapshot and BodyFixed surface-anchor bookmarks
- surface-anchor bookmark proof keeps BodyFixed XYZ authoritative and lat/lon/alt as metadata only
- added `GalaxySystemRecord` and global `galaxy:local` frame metadata; system metric galaxy coordinates remain explicitly unresolved instead of fabricated
- added post-import integration diagnostics exercising G4/G5/G6/G7/G8 paths, including New Babbage frame round-trip and Stanton->Nyx navigation planning
- added a double-precision floating-origin render snapshot/LOD bridge for the later WebGL2 production renderer
- added richer jump endpoint/connection type, availability, traversability and source-authority metadata
- added deterministic revision/timestamp merge contracts for mutable bookmarks/routes/notes/media overlays without writing to SyncHost
- added `BookmarkService` and concurrent-safe atomic local bookmark writes
- added guarded external ObjectContainer recursion; the current New Babbage proof resolves 8 current LIVE containers and 165 raw child nodes with no unresolved reference while leaving arbitrary deep transforms deliberately uncomposed
- added strict SCUnpacked P4 compatibility checks: local P4 12519617 is now reference/validation-only against current LIVE P4 12545750 and cannot override LIVE names/types/visibility/QT fields
- added direct local name/type derivation so disabling stale SCUnpacked enrichment does not destroy current LIVE body/location identity
- added advanced-layer availability gates for resources, environment and dynamic orbit data; stale/unproven sources remain blocked instead of being silently promoted to authority
- added G8 tombstone regression proof: a higher-revision delete wins, while a still newer record may intentionally supersede the tombstone
- hardened jump endpoint discovery to use current LIVE ObjectContainer names/source paths (`jumppoint_<system>_<destination>.socpak`) instead of depending on stale SCUnpacked display names; five logical connections and two reciprocal pairs are restored from local data
- final per-system snapshots are refreshed after global G4-G9 diagnostics so persisted snapshots contain routing/render/bookmark/overlay proof results
- coverage report now includes G4 through G9 proof diagnostics rather than stopping at G3
- no automatic persistent bookmark, SyncHost write or fabricated galaxy coordinate is created by diagnostics

## 0.2.1 - 2026-09-04

G2/G3 evidence hardening and multi-anchor surface coverage.

- added persisted `OrientationConventionEvidence`, `SurfaceCoverageSummaryRecord` and `RotationPhaseEvidenceRecord` datasets
- WXYZ quaternion component order is now strongly supported by current LIVE placement distributions and local StarBreaker/Blender handling; quaternion norms remain within ~3.4e-8 of unit length across current systems
- right-handed orientation treatment is strongly supported by StarBreaker's local scene-axis conversion matrix, which is a proper rotation with determinant +1; active/passive semantics remain unresolved
- parent/child translation semantics remain unchanged and proven: child positions are parent-relative offsets expressed in common system axes and are not rotated by parent `Child.rot`
- added real per-import scan of current body root ObjectContainer XML for absolute rotation phase/epoch/alignment candidates; current roots expose `planetRotationSpeed` and `planetAxis` but no locally proven absolute phase field
- expanded G3 direct surface proofs from New Babbage to Lorville, Area18 and Orison
- surface anchors now distinguish `NEAR_SURFACE`, `BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED`, `BODY_LOCAL_SPACE_ORBITAL` and `BODY_LOCAL_SUBSURFACE`
- Orison is therefore represented as an elevated/atmospheric body-local location rather than incorrectly being treated as a ground site or orbital station
- added per-body coverage summaries with min/max altitude and scope counts; coverage integrity is validated structurally
- snapshots and global universe index now persist orientation evidence, rotation-phase evidence and per-body surface coverage
- known-proof diagnostics now report New Babbage, Lorville, Area18 and Orison from current LIVE body ObjectContainers
- no synthetic geographic convention, rotation epoch or dynamic orbital parameters are introduced when local proof is absent

## 0.2.0 - 2026-09-04

Transform/time and body-local coordinate foundation.

- added double-precision `Vector3D`, `QuaternionD` and rigid-transform math
- added relative body-spin engine with explicit unresolved absolute phase/alignment state
- added `BodyFixed` frames and persisted `TemporalTransformRecord` body-spin transforms
- added reversible spherical body-local XYZ <-> axis latitude/longitude/altitude conversion
- added automated G2 diagnostics for nested placement translation, surface-coordinate round-trip and body rotation/inverse rotation
- direct G2 math proofs remain at sub-nanometer numerical error in current diagnostics
- generalized body-local anchor discovery from current body ObjectContainers
- anchors are separated into `NEAR_SURFACE` and `BODY_LOCAL_SPACE_ORBITAL`; no orbital station is silently treated as a surface location
- Stanton currently exposes 521 body-local anchors, of which 486 are near-surface candidates; Pyro exposes 82 body-local anchors; Nyx currently exposes none from the imported body OCs
- New Babbage is now directly resolved from `stanton4.socpak` at body-local XYZ `(520722.992, 419364.313, 743654.685) m`; geometric altitude ~20.576 m above the local 1,000,000 m microTech reference radius
- axis-spherical New Babbage coordinates are derived but explicitly marked as not yet proven to match the game's geographic lat/lon convention
- Nyx body identity mapping hardened for current dummy/test OC placement paths; Nyx I/II/III now receive proper body-fixed frames and temporal records
- added known proof diagnostics for New Babbage, Port Tressler, Aaron Halo and Pyro asteroid-cluster volumes
- added reciprocal inter-system topological route planner; Stanton -> Pyro -> Nyx resolves, while unpaired Stanton -> Terra remains non-traversable by design
- added per-system spatial query index with UUID/type/parent lookup plus X-sorted radius and nearest-neighbor queries
- spatial-index diagnostics compare indexed radius results against brute force to prevent silent query loss
- current system-SOC quaternion distribution strongly supports Q0=W / WXYZ component order (`1,0,0,0` dominates identity placements); handedness/active-passive semantics remain explicitly unresolved
- reads current LIVE `build_manifest.id`; current tested build is `sc-alpha-4.10.0-hotfix`, P4 `12545750`, BuildId `0ca4083d-bfca-4099-b4e8-312b2f6c1772`, client version `1.0.191.28374`
- adds explicit source provenance: Data.p4k quick fingerprint, build-manifest SHA256, extracted system-SOC SHA256 and SCUnpacked SHA256
- adds generated `coverage.current.txt` with per-system coverage, proof diagnostics, provenance and intentionally unresolved items
- technical viewer can switch between already-imported Stanton, Pyro and Nyx without re-importing Data.p4k
- adds initial local frame-relative `BookmarkRecord` contract and atomic JSON `LocalBookmarkStore`; base-universe data remains separate from mutable user overlays
- per-system snapshots and global universe index now persist temporal transforms, body-local anchors, source provenance and route proofs
- structural validation expanded to body-fixed frames, temporal transforms and body-local anchors

## 0.1.3 - 2026-09-04

Asteroid-cluster volume modeling for Pyro.

- added `SpatialVolume` domain type separate from annular `SpatialRegion`
- generalized direct extraction of shared modular asteroid-cluster ObjectContainer templates
- Pyro now resolves 15 placed asteroid-cluster volumes from current LIVE Data.p4k
- all six used `cluster_modular_warm_00x` templates expose direct local bounds `-30000..30000 m` and bounding radius `30000 m`
- volume centers remain the direct system-world placement from `pyrosystem.socpak`; template extents remain local to each placement
- volume integrity validation added
- Stanton, Pyro and Nyx remain structurally valid after volume import
- technical viewer status now reports ring-region and volume-region counts separately
- clean Debug build and smoke test

## 0.1.2 - 2026-09-04

Spatial frame graph and transform-semantics hardening.

- added explicit `SpatialFrameRecord` graph for System, CelestialBodyCenter and ObjectContainer frames
- placements now reference parent frames instead of only parent placement IDs
- frame/canonical/placement references are validated structurally
- snapshots and global universe index now persist frame counts and frame graph data
- added explicit `TranslationSpace` to placements
- direct cross-check proves the current system-SOC child translation offsets must be accumulated in common system axes: simple recursive translation matches SCUnpacked exactly for 112 Stanton, 171 Pyro and 89 Nyx placements (0.000 m max delta in all three current comparisons)
- this prevents a future transform engine from incorrectly rotating child translation vectors with `Child.rot`
- clean Debug build and smoke test; Stanton, Pyro and Nyx all pass validation

## 0.1.1 - 2026-09-04

Foundation expansion from Stanton-only G0 into multi-system G1.

- generalized system SOC import for Stanton, Pyro and Nyx
- generalized body physical import for all three systems
- added canonical node / placement separation to prevent UUID identity from being conflated with spatial placement
- added structural validator for parent, canonical, placement, body and region integrity
- zero `planetRotationSpeed` is preserved as a valid LOCAL_DIRECT static/non-rotating value instead of being treated as corruption
- added global import coordinator and reproducible per-system snapshots
- added reciprocal jump graph with Stanton-Pyro and Pyro-Nyx paired directly from local endpoints
- preserves unpaired local exits toward Magnus, Terra and Castra without inventing unavailable remote endpoints
- added global `universe.current.json` index
- all three imported systems pass structural validation
- clean Debug build with 0 warnings / 0 errors and successful smoke test

## 0.1.0 - 2026-09-04

Initial standalone StarSyncUniverse foundation.

- added independent WPF project to `the development solution`
- direct LIVE Data.p4k extraction through StarBreaker
- recursive Stanton system ObjectContainer importer
- SCUnpacked UUID/name/type/visibility/QT enrichment and validation
- direct body physical import for all 16 Stanton planets/moons
- direct Aaron Halo `AsteroidRing` region import
- source fingerprint extraction cache
- reproducible current Stanton JSON snapshot
- interactive WebView2 map with pan/zoom, filters, selection and parent-distance guides
- project planning and data-provenance documentation
- clean Debug build with 0 warnings / 0 errors
