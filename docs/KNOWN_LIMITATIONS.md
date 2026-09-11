# Known limitations

The public 0.7.77 baseline is intentionally conservative about unresolved Star Citizen data semantics.

- Inter-system jump topology is represented, but jump-distance metric semantics are not invented when the source does not establish them.
- Galactic position values are retained with raw/current source semantics where unit conversion is unresolved.
- Some advanced orbit/resource/environment layers remain gated unless a locally verifiable source is available.
- Optional local `Data.p4k` refresh depends on a compatible external StarBreaker build and the current Star Citizen data layout.
- Optional SCUnpacked enrichment may lag the currently installed game build; same-build checks are used where implemented to avoid stale enrichment overriding current local data.
- External media/description providers are optional and may change availability or response formats independently of this project.
- WebView2 Runtime is required for the map host.
- Packaged community-baseline data is a snapshot and should not be assumed to represent future Star Citizen patches without refresh/revalidation.

The project generally prefers an explicit "unresolved" state over presenting a guessed value as authoritative.
