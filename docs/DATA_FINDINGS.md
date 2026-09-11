# StarSyncUniverse – Datenfunde und Provenance

Stand: 2026-09-04

## Aktuelle lokale Primärquellen

- StarBreaker: `%STARSYNC_STARBREAKER%` (tested with StarBreaker 0.3.7 selfextended)
- LIVE: `%STARSYNC_DATA_P4K%`
- Stanton System SOC: `Data/ObjectContainers/PU/system/stanton/stantonsystem.socpak`
- Aaron Halo SOC: `Data/ObjectContainers/PU/system/stanton/aaronhalo.socpak`
- Sekundärdaten: `database/<scunpacked-dataset>/starmap_positions.json`

## System ObjectContainers

`stantonsystem.xml` führt Child ObjectContainers mit:
- `pos`
- `rot`
- `guid`
- `entityName`
- `class`
- `starMapRecord`
- nested `ChildObjectContainers`

Beispiel microTech (CURRENT LIVE Data.p4k):

- class: `OrbitingObjectContainer`
- source: `Data/objectcontainers/pu/system/stanton/stanton4.socpak`
- starMapRecord: `5a529db7-1a4e-45b0-bdcb-a5a0c7a7fc57`
- pos: `(22462085252.484795, 37185744964.830101, 0)` m
- rot: `(0,0,0,1)`

Calliope ist direkt darunter ein weiterer `OrbitingObjectContainer`:

- pos relative microTech: `(63715944.237567902, 16904285.818809509, 0)` m
- rot: `(0,0,0,-1)`
- starMapRecord: `3304263b-f133-41f8-85e7-79693e5bb2de`

## Parent/Child Translation Proof

Für CURRENT LIVE System-/Body-ObjectContainer ist die bisherige Translation-Semantik belastbar: `Child.pos` ist parent-relativ, aber in gemeinsamen Systemachsen ausgedrückt. Die Welttranslation wird deshalb durch einfache Vektoraddition gebildet und **nicht** durch die Parent-Quaternion rotiert:

`WorldXYZ(child) = WorldXYZ(parent) + Child.pos`

Dieser Ansatz reproduziert die bekannten SCUnpacked-Vergleichsplacements in Stanton/Pyro/Nyx ohne Meter-Abweichungen in den geprüften Systemgraphen. Die Quaternion bleibt reine Orientierungsinformation, bis active/passive-Komposition endgültig bewiesen ist.

### CURRENT LIVE Stanton Orbitalstationen

Die planetaren Root-SOCs enthalten die großen Orbitalstationen direkt als Body-Child-ObjectContainer. CURRENT LIVE liefert damit deren Body-local XYZ, `starMapRecord` UUID und SourcePath direkt:

- Hurston -> Everus Harbor: UUID `ab29f65e-c792-4b1f-b23d-5810cb0ef416`, local `(-507742.3125,-903464.4375,496489.0625)` m
- Crusader -> Seraphim Station: UUID `45d2d370-9fab-4e6b-b84b-e9c913bc316a`, local `(5876098.201785,-1384206.723274,5565883.231907)` m
- ArcCorp -> Baijini Point: UUID `164ca676-83eb-448d-ab0b-1ddde8a9c0bd`, local `(-771960.5625,-321347.21875,-359509.34375)` m
- microTech -> Port Tressler: UUID `233238ee-adeb-4405-8045-49fa07370f37`, local `(561471.046151,545853.017029,808831.762531)` m

`BodyWorldXYZ + station local XYZ` reproduziert die vorhandenen SCUnpacked-Weltpositionen für diese vier Stationen bei der aktuellen Prüfung bis auf numerisches Rundungsniveau. SCUnpacked ist wegen P4-Mismatch weiterhin nur Identitäts-/Vergleichsreferenz; die Stationsgeometrie stammt aus CURRENT LIVE Data.p4k.

Die gleichen Body-SOCs zeigen außerdem weitere Orbitalinfrastruktur wie Grim HEX und mehrere OLPs. Diese Objekte werden ab 0.4.0 in den normalen Universe-Katalog hochgezogen.

### Stationsmodell-Export-Proof (Port Tressler, 2026-09-04)

Der CURRENT-LIVE `stanton4.socpak`-Body-Container verweist Port Tressler über die bekannte starMap UUID `233238ee-adeb-4405-8045-49fa07370f37` direkt auf:

`Data/objectcontainers/pu/loc/mod/stanton/station/ser/reststop_ext/rs_ext_mic-leo1.socpak`

Attribute des direkten Body-Child-Eintrags:

- `entityName=ObjectContainer_RestStop`
- `class=LocationObjectContainer`
- `pos=(561471.046151,545853.017029,808831.762531)` m
- `rot=(0.88813943,0.19666909,0.15115008,0.38688919)`
- `guid=4b4a2a58-ebb4-b6aa-9a70-856e38a11698`

Damit ist bewiesen, dass dieselbe autoritative Placement-Quelle, die StarSyncUniverse für Position/Identität nutzt, zugleich den konkreten Stations-ObjectContainer für einen optionalen Visual-Export liefert.

Der aktuelle StarBreaker-`socpak export` kann diesen Container direkt als GLB exportieren. Ein LOD2/Materials-none-Proof erzeugte erfolgreich:

- 3,037 Nodes
- 272 Meshes / 846 Primitives
- 3,106,603 Vertices
- 197 Materialdefinitionen
- 0 Texturen (absichtlich `materials=none`)
- ca. 18.3 MB GLB-Dateigröße

StarBreaker meldet für mehrere lokale `Brush/designer_*.cgf`-Referenzen fehlende `.cgfm`-Begleitdateien. Der GLB-Export wird trotzdem erfolgreich abgeschlossen. Diese Warnungen müssen vor einem als vollständig deklarierten Produktions-Modelpack noch separat bewertet werden; der technische Pfad `authoritative placement -> source SOC -> optional GLB override` ist jedoch nachgewiesen.

Für StarSyncUniverse werden solche Modelle ausschließlich als Präsentationsassets behandelt. Das Modell darf Canonical ID, Position, Routing oder andere Universe-Authority niemals überschreiben.

Für die Visualisierung wird eine **simulierte Kreisbahn** erzeugt, die durch die autoritative aktuelle Position verläuft. Der Radius entspricht dem direkten Parent-Abstand; die Ebene wird so gewählt, dass sie mit der Parent-Rotationsachse möglichst geringe Inklination besitzt. Diese Bahn ist ausdrücklich `SIMULATED_ORBIT_GEOMETRY_NOT_DYNAMIC_TRUTH` und keine Behauptung über aktive Ingame-Orbitdynamik.

## Body Physical Parameters

Direkt aus aktuellen Body-SOCs gefunden:

- Hurston: radius 1,000,000 m; rotation raw 2.48 h; axis `(0,0,1)`
- Daymar: radius 295,000 m; rotation raw 2.48 h; axis `(0,0,1)`
- microTech: radius 1,000,000 m; rotation raw 4.1199999 h; axis `(0,0,1)`
- Clio: radius 337,170 m; rotation raw 3.25 h; axis `(0,0,1)`
- ArcCorp: radius 800,000 m; rotation raw 3.1099999 h; axis `(0,0,1)`

`planetRotationSpeed` ist semantisch irreführend benannt. Die beobachteten Werte entsprechen der bekannten Rotationsperiodendauer in Stunden; StarSyncUniverse speichert deshalb künftig Raw-Wert + abgeleitete `PeriodSeconds` und behält Provenance.

## Aaron Halo – CURRENT LIVE direct proof

P4K path:

`Data/ObjectContainers/PU/system/stanton/aaronhalo.socpak`

Das SOC wird vom Stanton-System als `OrbitingObjectContainer` `OOC_AaronHalo` bei `(0,0,0)` eingebunden.

`aaronhalo_editor.xml` enthält:

- `GoTo_AaronHalo_InnerEdge`
- `GoTo_AaronHalo_OuterEdge`
- `GoTo_AaronHalo_Middle`
- `AsteroidRing_AaronHalo`
- type `AsteroidRing`

`aaronhalo.soc` enthält `SAsteroidRingComponentParams` und folgende CURRENT LIVE Werte:

- composition: `AsteroidField_AaronHalo`
- densityScale: `0.0099999998`
- innerRadiusKm: `19597600`
- outerRadiusKm: `21392800`
- depthKm: `10000`

Daraus lokal direkt:

- inner radius = 19,597,600 km
- outer radius = 21,392,800 km
- radial belt width = 1,795,200 km
- thickness/depth = 10,000 km

Root bounds in `aaronhalo.xml`:

- minBounds `(-21392799744,-21392799744,-5000000)` m
- maxBounds `(21392799744,21392799744,5000000)` m
- radius `3.0267306e+10` m

Damit ist Aaron Halo kein illustrativer Ring, sondern eine lokal direkt definierte räumliche Region.

## SCUnpacked Stanton Snapshot

`starmap_positions.json` enthält aktuell 805 Stanton Placements, u.a.:

- 455 Outpost
- 254 Asteroid
- 26 Manmade
- 21 Manmade_VisibleOnInteraction
- 12 Moon
- 4 Planet
- 4 PointOfInterest
- 4 LandingZone
- 2 Anomaly
- 2 JumpPoint
- 2 NavPoint
- 1 Star

Es enthält außerdem viele Mining Claims/Bases und Lagrange-Asteroid-Objekte. Diese Daten werden nicht als Primary Authority verwendet, sind aber wertvoll für Name/Type/UUID/visibility/QT enrichment und als Validation Snapshot.

## Orbit Model

DataCore bestätigt `EntityClassDefinition.OrbitingObjectContainer` mit `SOrbitComponentParams`:

- OrbitalRadius
- OrbitalSpeed
- OrbitalAngle
- parentGUID

Planeten und Monde sind tatsächlich als `OrbitingObjectContainer` instanziert. Aktive non-zero planet/moon orbit instance parameters wurden noch nicht nachgewiesen. Deshalb:

- orbit capability: LOCAL_DIRECT
- current placements: LOCAL_DIRECT STATIC snapshot
- dynamic orbital movement: NOT_PROVEN

Parent-distance guides im Viewer sind ausdrücklich keine Behauptung aktiver Umlaufbahnen.

## CURRENT-LIVE body presentation / cloud-layer finding (0.7.26)

Direct P4K inspection confirms that body-global cloud fields are stored separately from body presentation/base-color data. Examples include `*_clouds_global.dds` and `*_cloud_global.dds`; Pyro additionally ships humidity/temperature/type/wind control maps for several bodies. These files are therefore treated as cloud/atmospheric coverage inputs only, never as RGB surface albedo. Pyro bodies without an explicit global cloud field (for example Fuego / `pyro5e` in the current LIVE set) must not receive a fabricated geographic cloud map. They may receive only a restrained presentation rim/haze until a direct cloud source is proven.

## Rotation Model

Lokal direkt:
- period raw
- axis
- radius

Noch nicht lokal statisch gefunden:
- absolute spin phase
- epoch/alignment
- definitive direction/handedness

Die Foundation muss Phase/Alignment separat speichern und darf externe `rotationCorrection`-Werte nicht als permanente Authority behandeln.

## Multi-system G1 findings

Der direkte Import ist inzwischen auf Stanton, Pyro und Nyx generalisiert.

Aktueller reproduzierbarer Snapshot:

- Stanton: 440 spatial entities, 16 physical bodies, 1 direct spatial region
- Pyro: 177 spatial entities, 12 physical bodies
- Nyx: 232 spatial entities, 3 physical bodies

Alle drei Systemgraphen bestehen die strukturelle Parent/Placement/Canonical-Validierung.

`planetRotationSpeed = 0` ist in den aktuellen LIVE-Daten ein tatsächlich gespeicherter Wert und darf nicht automatisch als Importfehler interpretiert werden. Direkt bestätigt:

- Pyro / Fuego (`pyro5e`): radius 465670 m, rotation 0, axis 0,0,1
- Nyx / `nyx2`: radius 578600 m, rotation 0, axis 0,0,1
- Nyx / `nyx3`: radius 578600 m, rotation 0, axis 0,0,1

Der Import bewahrt diese Werte als LOCAL_DIRECT. Eine spätere semantische Interpretation als bewusst statisch, synchron gebunden oder anderweitig runtime-gesteuert muss separat erfolgen.

## Jump graph

Lokale Jump-Point-/Anomaly-Endpunkte ergeben aktuell fünf logische Verbindungen:

- Stanton <-> Pyro: beide Endpunkte lokal vorhanden, LOCAL_DIRECT_PAIRED
- Pyro <-> Nyx: beide Endpunkte lokal vorhanden, LOCAL_DIRECT_PAIRED
- Stanton -> Magnus: lokaler Stanton-Endpunkt vorhanden, Gegenende nicht im importierten Systembestand
- Stanton -> Terra: lokaler Stanton-Endpunkt vorhanden, Gegenende nicht im importierten Systembestand
- Nyx -> Castra: lokaler Nyx-Endpunkt vorhanden, Gegenende nicht im importierten Systembestand

Unbekannte Gegenenden werden nicht synthetisch ergänzt.

## Canonical identity vs placement

Seit G1 werden kanonische Objekte und räumliche Placements getrennt persistiert:

- `CanonicalSpatialNode`: Identität / Source UUID / Typ / Name
- `SpatialPlacementRecord`: Parent Frame, lokaler Transform, Welt-Snapshot, Placement Mode

Damit kann dieselbe kanonische UUID zukünftig mehrere Placements besitzen, ohne dass eine globale XYZ fälschlich als Objektidentität behandelt wird.

## Pyro body-presentation texture findings (0.7.25)

CURRENT LIVE Pyro unterscheidet sich strukturell von Stanton bei den Map-/Presentation-Assets:

- die Pyro-Body-Ordner enthalten fuer die felsigen Bodies `*_clim`, `*_splat`, `*_elev` sowie weitere technische Maps, aber keine eigene body-spezifische `*_starmap_diff`-Datei
- `*_clim` und `*_splat` sind PlanetTerrain-Control-Inputs und duerfen nicht als RGB-Albedo/Starmap-Oberflaeche dargestellt werden; die zuvor sichtbaren Cyan/Magenta/Green-Falschfarben waren genau diese Fehlinterpretation
- die Pyro-`*_starmap.mtl`-Dateien verweisen im aktuellen LIVE-Bestand auf `Textures/planets/global/stanton/stanton1/stanton1_global_starmap_diff.tif`; diese systemfremde Hurston-Referenz wird als Placeholder/ungeeignete Presentation-Quelle abgelehnt
- Pyro V (`pyro5`) besitzt im Material `%GAS_GIANT` und statt Climate/Splat eine `pyro5_cloud_global` plus `pyro5_cloud_tint_gradient`; diese Kombination ist ein eigener Gas-Giant-Presentation-Pfad und darf nicht mit den Rocky-Body-Regeln vermischt werden
- fuer Pyro existieren body-spezifische CCHs unter `Data/Textures/colorcharts/Systems/Pyro/`; sie werden nur als Presentation-Grade verwendet und sind keine Geometrie-/POI-Authority
- Audit 0.7.25: alle 12 physischen Pyro-Bodies erhalten einen sicheren Presentation-Pfad: 11 Terrain-Derivationen und 1 Gas-Giant-Derivation; keine raw Climate/Cloud-Control-Map wird direkt sichtbar gerendert

Stanton verwendet weiterhin die echten Starmap-Diffuse-Assets. Der Resolver muss beide dort vorkommenden Namensschemata erkennen: `*_global_starmap_diff` sowie `*_starmap_diff` (insbesondere ArcCorp `stanton3_starmap_diff` und microTech `stanton4_starmap_diff`).

### Material-/Relief-Erweiterung 0.7.27

Direkte CURRENT-LIVE Prüfung von Monox (`pyro2`) und Fuego (`pyro5e`) zeigt, dass die Body-Materialien zusätzliche verwendbare Presentation-Hinweise liefern, die bisher ignoriert wurden:

- `pyro2_planet.mtl` referenziert `pyro2_splat`, `pyro2_clim`, `pyro2_elev`; `pyro2_ocean.mtl` liefert eine dunkle teal-farbene Ocean-Diffuse sowie Shoreline-/Surface-Variation-Parameter
- `pyro5e.mtl` referenziert `pyro5e_splat`, `pyro5e_clim`, `pyro5e_elev` und enthält `WetEdgeColor`; `pyro5e_ocean.mtl` liefert eine klar warm rot/orange geprägte Ocean-/Shoreline-Farbpalette
- beide Body-Ordner enthalten `*_ddn.dds`; diese Map wird ab 0.7.27 als separater CURRENT-LIVE Normal-Layer an WebGL übergeben und niemals als sichtbare RGB-Albedo interpretiert
- die rekonstruierten Pyro-Terrain-Presentation-Texturen verwenden ab 0.7.27 zusätzlich `*_elev` für großskaliges Relief-Shading sowie Material-Farbsignale. Das verbessert Tiefe und body-spezifische Farbcharakteristik, bleibt aber ausdrücklich Presentation-only, solange CIG keine echte body-spezifische Pyro-Starmap-Diffuse liefert
- Fuego besitzt im aktuellen Body-Ordner weiterhin keinen expliziten globalen Cloud-Coverage-Layer; deshalb wird keine geographische Wolkenverteilung erfunden. Monox besitzt `pyro2_cloud_global` und kann einen echten separaten Cloud-Layer verwenden.

## Pyro asteroid/volume discovery

Aktuelle LIVE Data.p4k enthält unter `Data/ObjectContainers/PU/system/pyro/` u.a.:

- `akirocluster.socpak`
- `pyro_asteroidcluster.socpak`

`akirocluster.socpak` enthält einen `AsteroidGasCloud` mit `AsteroidFieldComponent`, `SAsteroidGasCloudComponentParams`, Composition `AsteroidField_RoundRough` und `densityScale=0.69999999`. Der ObjectContainer besitzt direkte Bounds und Radius. Dies ist ein klarer Beleg dafür, dass neben Ringregionen wie Aaron Halo auch echte volumetrische Asteroid-/Gas-Regionen modelliert werden müssen. Die endgültige `SpatialVolume`-Repräsentation wird deshalb als eigener G1/G2-Datentyp vorgesehen und nicht in das annular Aaron-Halo-Schema gezwängt.

## Translation-space proof

Ein zentraler Transform-Fund wurde mit allen drei importierten Systemen bestätigt: Die verschachtelten `Child.pos`-Vektoren aus den System-SOCs sind parent-relative Offsets, werden für die aktuelle Welt-/Systemposition aber ohne Anwendung der Parent-Quaternion rekursiv addiert.

Direkter Cross-Check gegen `starmap_positions.json`:

- Stanton: 112 vergleichbare Placements, max delta 0.000 m
- Pyro: 171 vergleichbare Placements, max delta 0.000 m
- Nyx: 89 vergleichbare Placements, max delta 0.000 m

Daher wird im Datenmodell `TranslationSpace = PARENT_RELATIVE_SYSTEM_AXES` gespeichert. `Child.rot` bleibt eine eigene Orientation-Information und darf nicht ohne gesonderten Beweis auf den Child-Translationsvektor angewendet werden. Das ist für die spätere Transform Engine verbindlich.

## Pyro placed asteroid-cluster volumes

Im aktuellen `pyrosystem.socpak` sind 15 platzierte modulare Asteroidencluster vorhanden. Ihre Placement-Zentren kommen direkt aus den jeweiligen `Child.pos`-Einträgen des Pyro-System-SOCs. Die wiederverwendeten Templates liegen unter:

`Data/ObjectContainers/PU/asteroidCluster/clusters_modular/clusters_mod_set/cluster_modular_warm_001..006.socpak`

Alle sechs geprüften Templates liefern direkt:

- minBounds = -30000,-30000,-30000 m
- maxBounds = 30000,30000,30000 m
- radius = 30000 m

Damit werden die 15 Pyro-Cluster als `SpatialVolume` mit direktem Weltzentrum und lokalem Template-Volumen importiert. Beispielnamen sind `Cluster KKE-717`, `Cluster WDH-387`, `Cluster FSN-704`, `Cluster GRP-839` und weitere Region-A/B/C/D-Cluster. Status: LOCAL_DIRECT.

Dies ist ausdrücklich etwas anderes als Aaron Halo: Aaron Halo ist eine annular definierte Ringregion mit inner/outer radius und thickness; die Pyro-Cluster sind platzierte lokale 3D-Volumes.

## Nyx placed gas-cloud / Glaciem-ring volumes

Der CURRENT-LIVE Nyx-Systemgraph enthält zusätzlich zahlreiche platzierte räumliche Container. Zwei direkt geprüfte Templateklassen liefern echte Root-Bounds:

- `Data/ObjectContainers/PU/system/nyx/gascloudsgen/tsg_gascloud_001.socpak`: minBounds `(-30000,-30000,-30000)` m, maxBounds `(30000,30000,30000)` m, root radius `77467.109` m.
- `Data/ObjectContainers/PU/system/nyx/glaciemRing/glaciemring_segment_mission_genrl_002.socpak`: minBounds `(-25677.727131,-28297.859254,-11014.474302)` m, maxBounds `(34322.272869,31702.140746,48985.525698)` m, root radius `78601.578` m.

Die Bounds werden nach CryXML-Konvertierung direkt aus den CURRENT-LIVE SOC-Root-XMLs gelesen. Der Import gruppiert identische Source-SOCs, sodass jedes wiederverwendete Template nur einmal extrahiert und geparst wird. Aktueller Snapshot:

- Nyx GasCloudVolume: 40 Placements
- Nyx RingSegmentVolume: 52 Placements
- insgesamt Nyx SpatialVolumes: 92

Weltzentrum und Parent-Hierarchie stammen aus dem aktuellen Nyx-Systemgraphen; Bounds/Radius stammen aus dem jeweiligen aktuellen Root-SOC. Status: `LOCAL_DIRECT`. Es werden keine Dichte, Wolkenform außerhalb der Root-Bounds oder dynamische Bewegung erfunden.

## Body-local anchors / New Babbage proof

Der aktuelle `stanton4.socpak` enthält `NewBabbage_LOC` direkt als `LocationObjectContainer` mit:

- body-local position = `520722.992113,419364.313162,743654.685314 m`
- starmapRecord = `122e8057-831d-4517-a23b-0eb8382b90a2`
- body = microTech
- microTech local reference radius = `1,000,000 m`

Daraus folgt lokal abgeleitet:

- radial distance = ca. `1,000,020.576 m`
- geometric altitude above reference radius = ca. `20.576 m`
- axis-spherical latitude = ca. `48.042365°`
- axis-spherical longitude = ca. `38.846235°`

Wichtig: XYZ und Radius sind lokal direkt. Radius/Altitude und die sphärischen Winkel sind LOCAL_DERIVED. Die sphärischen Winkel werden **nicht** als bestätigte Star-Citizen-geographische Lat/Lon-Konvention ausgegeben, solange Achsorientierung, Nullmeridian und Handedness nicht bewiesen sind.

Der Body-OC-Importer liefert aktuell:
- Stanton: 521 body-local anchors, davon 486 `NEAR_SURFACE`
- Pyro: 82 body-local anchors
- Nyx: aktuell 0 aus den importierten body OCs

Ein Anchor wird nicht automatisch als Surface-POI behandelt. Hohe positive Altitude wird als `BODY_LOCAL_SPACE_ORBITAL` klassifiziert, wodurch z.B. Grim HEX oder Seraphim Station nicht mit Boden-POIs vermischt werden.

## G2 transform math proof

Neue Double-Precision-Transformdiagnostik bestätigt für die aktuellen drei Systeme:

- parent+local placement reconstruction: max delta `0.000000000 m`
- spherical XYZ -> Lat/Lon/Alt -> XYZ round-trip: numerisch im Sub-Nanometer-/Nanometerbereich
- full-cycle body rotation: Rückkehr auf Ausgangspunkt innerhalb numerischer Rundung
- half-cycle und inverse rotation: ebenfalls innerhalb numerischer Rundung

Quaternion component-order evidence aus aktuellen LIVE System-SOCs:
- Stanton: 440 `rot` Werte, davon 397 exakt `1,0,0,0`; nur 2 exakt `0,0,0,1`
- Pyro: 177 `rot` Werte, davon 175 exakt `1,0,0,0`
- Nyx: ebenfalls mehrere `1,0,0,0` Identitäts-/Default-Placements

Zusätzlich behandelt der vorhandene StarBreaker-Blender-Importer das vierteilige Scene-Quaternion direkt als Blender `Quaternion(rotation)` und verwendet `(1,0,0,0)` als Identity. Das unterstützt stark `Q0=W` / WXYZ. Handedness sowie active/passive orientation semantics bleiben trotzdem UNRESOLVED und werden noch nicht für Child-Translations verwendet.

Absolute body spin phase bleibt bewusst unresolved. Relative Rotation ist mathematisch implementiert; absolute zeitbezogene Surface->System-Projektion wird erst aktiviert, wenn Phase/Epoch lokal bewiesen oder explizit kalibriert ist.

## Spatial query index

Pro System existiert jetzt ein Query-Index für:
- Source UUID
- Type
- Parent/Children
- Radius queries über X-sortierte Kandidatenreduktion
- nearest-neighbor lookup

Jeder Import vergleicht mindestens eine Radiusabfrage gegen brute-force Euclidean filtering. Unterschiede werden als Diagnostic sichtbar; silent query loss ist nicht akzeptiert.

## G2 orientation evidence (0.2.1)

Current LIVE system-SOC rotation distributions plus the local StarBreaker Blender bridge strongly support quaternion component order `W,X,Y,Z`:

- Stanton: 440 placements, 397 exact identity `(1,0,0,0)`, 43 non-trivial rotations
- Pyro: 177 placements, 176 exact identity `(1,0,0,0)`, 1 non-trivial rotation
- Nyx: 232 placements, 18 exact identity `(1,0,0,0)`, 214 non-trivial rotations
- maximum observed quaternion norm error in the imported placement sets is on the order of `1e-8`

StarBreaker's local Blender bridge constructs `Quaternion(rotation)` directly and uses `(1,0,0,0)` as identity. Its SC-to-Blender scene-axis conversion is the proper rotation matrix `(x,y,z) -> (x,-z,y)` with determinant `+1`, so a handedness reflection is not introduced. The foundation therefore records `WXYZ_STRONGLY_SUPPORTED` and `RIGHT_HANDED_STRONGLY_SUPPORTED`. Active-vs-passive orientation semantics are still `UNRESOLVED` and must not be guessed.

This does not change the separately proven translation rule: current system-SOC `Child.pos` offsets are accumulated by parent-world plus local position in common system axes; parent quaternion rotation is not applied to child translation.

## Rotation phase / epoch discovery (0.2.1)

The importer now performs a reproducible scan of each current body root ObjectContainer XML. It checks the planet entity attributes for explicit phase/epoch/meridian/start-angle/alignment-like fields.

Current result: body roots expose `planetRotationSpeed` and `planetAxis`; no locally proven absolute spin phase/epoch field was found. Therefore relative spin is supported, while absolute body orientation at a UTC timestamp remains `UNRESOLVED`. No synthetic epoch is inserted.

A separate current system-SOC token scan found the class name `OrbitingObjectContainer`, but no direct non-zero orbit radius/speed/angle parameter fields in the system root XML. Static LIVE placements remain authoritative until a dynamic-orbit source is proven.

## G3 multi-anchor surface proof (0.2.1)

The body-local surface chain is now verified with multiple current LIVE Stanton anchors, not only New Babbage:

- New Babbage / microTech: geometric altitude ~20.576 m -> `NEAR_SURFACE`
- Lorville / Hurston: ~856.298 m -> `NEAR_SURFACE`
- Area18 / ArcCorp: ~587.676 m -> `NEAR_SURFACE`
- Orison / Crusader: ~79,241.812 m -> `BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED`

The current classification bands are deliberately geometric and frame-based:

- below -50 km: `BODY_LOCAL_SUBSURFACE`
- -50 km through +50 km: `NEAR_SURFACE`
- +50 km through +250 km: `BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED`
- above +250 km: `BODY_LOCAL_SPACE_ORBITAL`

The derived axis-spherical angles are useful for internal transforms but remain explicitly marked `GEODETIC_CONVENTION_UNPROVEN`; a Star Citizen geographic zero-meridian/sign convention is not yet claimed.

## 0.3.0 source-compatibility guard

Ein bisher wichtiger impliziter Risikopunkt ist jetzt technisch abgesichert: Das lokal vorhandene SCUnpacked-Verzeichnis trägt P4 `12519617`, während CURRENT LIVE laut `build_manifest.id` P4 `12545750` ist. Deshalb gilt im aktuellen Lauf:

- SCUnpacked ist `SCUNPACKED_BUILD_MISMATCH_VALIDATION_ONLY`
- Namen, Typen, Visibility, QT-Flags und Parent-Metadaten werden bei diesem Mismatch nicht mehr aus SCUnpacked übernommen
- direkte Namen/Body-Typen werden aus CURRENT LIVE ObjectContainer-Attributen und Source-Pfaden abgeleitet
- Positionsvergleiche gegen das ältere Dataset bleiben als explizite `cross-build reference comparison` möglich, aber nicht als Authority
- Resource-/Mining-Daten des älteren SCUnpacked-Standes werden nicht in den aktuellen autoritativen Universe-Layer importiert

Damit kann ein älterer Sekundärsnapshot nicht mehr unbemerkt aktuelle LIVE-Daten überschreiben.

## 0.3.0 deep ObjectContainer proof

Für New Babbage wurde die externe ObjectContainer-Rekursion erstmals über die reine Body-Root-Liste hinaus durchgeführt. Der aktuelle direkte Pfad ist:

`Data/ObjectContainers/PU/loc/flagship/stanton/newbab/newbab_all.socpak`

Der kontrollierte Proof mit `maxDepth=1` / `maxContainers=8` ergibt aktuell:

- 8 direkt aus CURRENT LIVE aufgelöste Container
- 165 rohe Child-Nodes
- 0 ungelöste `.socpak`-Referenzen

Die Rohtransforms werden bewusst als `RAW_CHILD_LOCAL_UNCOMPOSED` gespeichert. Für beliebige externe OC-Hierarchien wird Translation/Orientation erst dann zu einer Weltmatrix komponiert, wenn deren konkrete Semantik ebenso belastbar bewiesen ist wie die bereits bewiesene System-SOC-Translation.

## 0.3.0 G4-G9 foundation

- G4: `galaxy:local` existiert als Frame-/Topologievertrag. Stanton/Pyro/Nyx besitzen weiterhin keine erfundenen metrischen Galaxy-X/Y/Z-Werte. JumpConnections tragen nun Type, Availability und Traversable.
- G5: Der Double-Core erzeugt floating-origin `RenderFrameSnapshot`-Daten mit Culling und LOD-Bändern. Das ist die Übergabegrenze zum späteren WebGL2/Three.js-Frontend.
- G6: `UniverseRoutePlanner` kombiniert messbare statische In-System-Legs mit lokal gepaarten Jump-Legs. Jump-Distanz und echte QT-Zeitkosten bleiben getrennt/unresolved.
- G7: `BookmarkFactory`/`BookmarkService` erzeugen System-, Placement- und BodyFixed-Surface-Bookmarks. Surface-XYZ bleibt primär; axis-spherical Lat/Lon/Alt bleibt Metadatum, solange die SC-Geokonvention nicht bewiesen ist.
- G8: Mutable Overlay-Verträge für Bookmarks, Routes, Notes und MediaRefs besitzen einen deterministischen Revision/UpdatedUtc-Merge. Der Standalone-Importer schreibt nicht zu SyncHost.
- G9: `AdvancedLayerAvailabilityRecord` blockiert Resource/Environment/Dynamic-Orbit-Layer, solange Quelle/Build/Semantik nicht den Authority-Regeln entsprechen. Aaron Halo und die 15 Pyro-Cluster bleiben dagegen produktiv, weil ihre Geometrie direkt lokal bewiesen ist.

## 0.7.29 Pyro presentation-texture resolution / starmap-material evidence

The current LIVE Pyro global body folders do not generally provide a final body-specific high-resolution starmap albedo. Direct extraction confirms that the available global control layers are often substantially lower resolution than a close-up surface view requires. Example `pyro2` CURRENT LIVE assets decode to 1024x1024 for `clim`, `cloud_global`, `ddn`, `displ`, `elev`, and `splat`; several other Pyro bodies use 256/512px global controls, while Pyro V has a 2048px presentation-capable source and therefore naturally survives closer map zoom much better.

`Data/Textures/planets/global/pyro/pyro2/pyro2_starmap.mtl` was also decoded directly. Its Hologram material references `Textures/planets/global/stanton/stanton1/stanton1_global_starmap_diff.tif`, not a Pyro2 diffuse. This is treated as a placeholder/reference and is explicitly **not** accepted as Pyro2 surface authority.

Therefore the Pyro fallback remains `DERIVED_PRESENTATION_ONLY`: it reconstructs a restrained global view from current LIVE elevation + climate/splat/material cues, but it must never be described as the true in-game high-resolution terrain albedo. From 0.7.29 the derived cache is built at 2048x2048 with bilinear control sampling, elevation-dominant large-scale luminance and reduced relief/normal amplification. The renderer additionally attenuates technical-detail lighting when the displayed body exceeds the source texture footprint, so zooming in does not magnify control-map pixels into false terrain relief.

## Source discipline

Wenn ein Wert aus Data.p4k und SCUnpacked abweicht, gewinnt CURRENT LIVE Data.p4k. Abweichung wird diagnostiziert, nicht still überschrieben. Bei Build-Mismatch darf SCUnpacked nicht als Enrichment-Authority verwendet werden.
