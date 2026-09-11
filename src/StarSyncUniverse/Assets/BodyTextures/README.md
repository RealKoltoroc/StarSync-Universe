# StarSyncUniverse packaged body-presentation baseline

This directory is the presentation-texture baseline shipped with the active StarSyncUniverse community build.

Runtime rules:

- Body presentation textures are part of the renderer baseline and are available independently of Data.p4k, SCUnpacked, or online-enrichment settings.
- The optional local Data.p4k / StarBreaker adapter may refresh the user's local presentation cache, but disabling that adapter must never remove body textures.
- SCUnpacked and online APIs enrich metadata only; neither controls planet/moon surface rendering.
- The renderer preserves the equirectangular projection/orientation used by body-fixed target geometry.
- The files under `stanton/`, `pyro/`, and future system directories can be replaced by a different presentation asset set without changing spatial data, navigation, or target coordinates.

The active baseline was captured from the validated presentation cache used by the 0.7.43-0.7.47 renderer line. Any future clean-room/stylized replacement should retain the same projection and orientation contract.
