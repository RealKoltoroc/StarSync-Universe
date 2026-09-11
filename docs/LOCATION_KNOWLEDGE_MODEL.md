# StarSyncUniverse Location Knowledge Model

## Purpose

StarSyncUniverse uses CURRENT LIVE `Data.p4k` as the spatial authority and the checked-in same-release SCUnpacked master dataset as the readable semantic layer. The objective is to present locations as player-facing objects (`Empyrean Park`, `Rustville`, `Lazarus Complex Phoenix-I`, etc.) while retaining the exact placement/body-local coordinates imported from the game data.

## Authority rules

1. **Geometry, placement, body-local XYZ, routes and transforms:** CURRENT LIVE `Data.p4k` only.
2. **Readable location identity and description:** SCUnpacked `starmap.json`, correlated primarily by exact UUID.
3. **Faction/jurisdiction:** SCUnpacked faction and starmap relations. Affiliation UUID is preferred when it resolves directly; jurisdiction UUID/name are fallbacks.
4. **Services:** `starmap.json` Amenities.
5. **Commodity trading:** `resources/commodity_trade_locations.json`, joined only when the endpoint has the exact `StarmapObjectUUID` **and** its `MatchedTagName` resolves to that exact commodity key/name; commodity metadata comes from `resources/commodities.json`. Broad tags such as `Commodity`, `Metal` or `Food` remain category hints and are not presented as proof that every matching commodity is actually traded.
6. **Legacy trade profiles/tags:** `trade_locations.json`. These remain secondary semantic hints and are not promoted to geometry or canonical identity.
7. **Items:** `items.json`, used for readable item metadata and conservative trade-tag-to-item enrichment only.
8. **Images:** presentation-only lazy cache. The Star-Citizen.wiki location API is used as the resolver; returned media may originate from starcitizen.tools. Image provenance is retained in cache metadata. Images never affect identity or placement.

## Correlation

`ScUnpackedKnowledgeDatabase.MatchLocation` uses the following order:

- `DIRECT_UUID` (confidence 1.00)
- normalized name + parent UUID in the same system
- normalized name + matching SC container context in the same system
- unique normalized name in the same system

Every match exposes its link authority and confidence. A name-derived semantic relation can therefore never silently become positional truth.

A correlation audit is generated at:

`%LOCALAPPDATA%/StarSyncUniverse/KnowledgeDatabase/scunpacked-data-p4k-correlation.tsv`

It records the Data.p4k object/anchor, raw technical name, resolved SCUnpacked UUID/name and correlation authority.

## UI projection

Normal object and Surface Location presentation is semantic-first:

- cached image when available
- readable name
- parent body/location and coordinates
- description
- jurisdiction and faction
- services and retail-service categories
- exact commodities sold/bought when a `StarmapObjectUUID` relation exists
- trade profile information and conservative related-item hints

Raw placement IDs, classes, UUIDs, source paths and status tokens remain available under collapsed technical/source sections rather than replacing player-facing information.

For planets and moons the detail view also aggregates the SCUnpacked location hierarchy to show landing zones/cities, stations, outposts and the known-location list, while Data.p4k supplies physical radius/diameter, rotation and system coordinates.

## Deliberate limitations

Atmospheric composition/pressure, gravity, temperature and population are not currently projected as authoritative physical facts because no proven CURRENT LIVE Data.p4k/DataCore field mapping for these values exists in the StarSyncUniverse importer, and the current SCUnpacked starmap master does not expose them as reliable body fields. The UI explicitly marks these fields as unavailable rather than inventing or silently importing unrelated web values.

Physical shop counts must not be inferred from generic Amenities. The UI therefore distinguishes **retail service categories**, **trade profiles**, and exact commodity relations instead of claiming an unproven number of storefronts.

## Diagnostics

- `StarSyncUniverse.exe --headless-import` validates Stanton/Pyro/Nyx imports and writes the semantic correlation audit.
- `StarSyncUniverse.exe --headless-location-image-smoke` resolves and caches the Orison presentation image as an end-to-end image-pipeline smoke test.
