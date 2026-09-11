# StarSyncUniverse Map Visual Assets

This directory is the stable asset contract for the production map renderer.

Resolution priority:
1. `Overrides/by-canonical-id/<canonicalId>.glb|gltf|obj`
2. `Overrides/by-class/<VisualClass>.glb|gltf|obj`
3. `Standard/Models/<VisualClass>.glb|gltf|obj`
4. `Standard/Icons/<VisualClass>.png|webp|svg`
5. built-in icon fallback (`builtin://map-visual/<VisualClass>`)

Optional Data.p4k model packs MUST install only into `Overrides`; they never replace canonical universe data and are never required for routing, search, surface coordinates, or object identity.

Supported visual classes are defined in `Domain/MapVisualModels.cs`.

The final Standard visual pack must use a coherent technical sci-fi design language. Primitive cubes/spheres are allowed only as temporary diagnostics, not as production standard visuals.

Surface textures are separate presentation assets. Their projection/registration must carry source/build/provenance and must not be treated as authoritative geometry. Day/night rendering must remain disabled until a caller can provide a time-resolved, proven sub-solar direction.

Surface texture contract:
- optional per-body textures install into `Surfaces/Overrides/by-canonical-id/<safeCanonicalId>.png|jpg|jpeg|webp`
- canonical IDs use the same deterministic safe-file-name conversion as model overrides (`:` becomes `_`)
- textures are treated as `EQUIRECTANGULAR` presentation assets and remain `PRESENT_UNREGISTERED_UNTIL_AXIS_CALIBRATION` until the Star Citizen surface axis/longitude convention is proven
- missing textures are valid and must fall back to the technical body/grid rendering; they never create or move POIs
- `tools/Export-SurfaceTextureOverride.ps1` can decode a specifically identified CURRENT-LIVE DDS through StarBreaker and write the canonical override plus SHA-256/provenance manifest; source DDS discovery remains explicit so the tool never guesses which game texture represents a body

WebGL renderer contract (0.7.20+):
- `WebGL/starsync-webgl.js` is a local, offline WebGL2 body layer loaded through the existing `starsync-assets` WebView2 virtual host; there is no CDN dependency
- the current production migration is hybrid: WebGL2 draws the focused textured body while Canvas2D remains the interaction/label/orbit/surface-target overlay and fallback path
- the sphere shader uses the same yaw/pitch body-local transform convention as the existing surface target projection and samples equirectangular UVs without changing any canonical coordinates
- only neutral limb shading is applied. No directional sunlight/day-night implication is rendered while absolute spin phase/sub-solar authority remains unresolved
- a present but unregistered texture is presentation-only; POIs remain attached to BodyFixed geometry and are never moved to match the image
- later GLB loading/instancing must remain a presentation layer: placement supplies position/orientation/identity, model assets supply only mesh/material/pivot/scale
