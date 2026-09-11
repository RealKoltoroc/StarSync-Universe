using System.Globalization;
using System.Text;
using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed record ScKnowledgeNamedRef(string Uuid, string Name);

public sealed record ScKnowledgeCommodityRef(
    string Uuid,
    string Key,
    string Name,
    string Description,
    string LinkAuthority);

public sealed record ScKnowledgeFaction(
    string Uuid,
    string Name,
    string Description,
    string FactionType,
    string Classification,
    string? Headquarters,
    IReadOnlyList<ScKnowledgeNamedRef> Relations,
    string SourcePath);

public sealed record ScKnowledgeTradeProfile(
    string Uuid,
    string ClassName,
    string DisplayName,
    bool Disabled,
    IReadOnlyList<ScKnowledgeNamedRef> Produces,
    IReadOnlyList<ScKnowledgeNamedRef> Consumes,
    string SourcePath);

public sealed record ScKnowledgeItem(
    string Reference,
    string ClassName,
    string Name,
    string Type,
    string SubType,
    string Description,
    string Manufacturer,
    string Size,
    string Grade,
    string Classification,
    IReadOnlyList<string> Tags,
    string SourcePath);

public sealed record ScKnowledgeItemLink(
    string Reference,
    string ClassName,
    string Name,
    string Type,
    string SubType,
    string Relation,
    string LinkAuthority);

public sealed record ScKnowledgeLocation(
    string Uuid,
    string Name,
    string Description,
    string Type,
    string Subtype,
    string System,
    string? ParentUuid,
    string? ParentName,
    string? ContainerName,
    string? JurisdictionUuid,
    string? JurisdictionName,
    string? FactionUuid,
    string? FactionName,
    string FactionLinkAuthority,
    string? FactionDescription,
    string? FactionType,
    string? FactionClassification,
    IReadOnlyList<ScKnowledgeNamedRef> FactionRelations,
    IReadOnlyList<ScKnowledgeNamedRef> Services,
    IReadOnlyList<ScKnowledgeNamedRef> Tags,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ScKnowledgeTradeProfile> TradeProfiles,
    IReadOnlyList<ScKnowledgeNamedRef> TradeProduces,
    IReadOnlyList<ScKnowledgeNamedRef> TradeConsumes,
    IReadOnlyList<ScKnowledgeCommodityRef> CommoditiesSold,
    IReadOnlyList<ScKnowledgeCommodityRef> CommoditiesBought,
    IReadOnlyList<ScKnowledgeItemLink> RelatedItems,
    bool Hidden,
    bool QuantumTravelValid,
    double? PositionX,
    double? PositionY,
    double? PositionZ,
    string SourcePath);

public sealed record ScKnowledgeMatch(
    ScKnowledgeLocation Location,
    string LinkAuthority,
    double Confidence,
    string Evidence);

public sealed record ScKnowledgeSummary(
    int LocationCount,
    int FactionCount,
    int ServiceCount,
    int TradeLocationCount,
    int CommodityRelationLocationCount,
    int CommodityCount,
    int ItemCount,
    string SourceRoot);

/// <summary>
/// Read-only semantic index over SCUnpacked-derived location knowledge. The normal client loads a
/// frozen packaged baseline; an optional user-supplied SCUnpacked dataset may refresh/extend it.
/// Data.p4k remains authoritative for geometry/placement. This database contributes readable names,
/// descriptions and semantic relationships only. Every correlation carries explicit link authority
/// so a name-derived relation can never silently become geometric truth.
/// </summary>
public sealed class ScUnpackedKnowledgeDatabase
{
    private readonly Dictionary<string, ScKnowledgeLocation> _locationsByUuid;
    private readonly Dictionary<string, List<ScKnowledgeLocation>> _locationsByNormalizedName;
    private readonly Dictionary<string, ScKnowledgeFaction> _factionsByUuid;
    private readonly Dictionary<string, ScKnowledgeFaction> _factionsByNormalizedName;
    private readonly Dictionary<string, ScKnowledgeItem> _itemsByReference;

    private ScUnpackedKnowledgeDatabase(
        string sourceRoot,
        IReadOnlyList<ScKnowledgeLocation> locations,
        IReadOnlyList<ScKnowledgeFaction> factions,
        IReadOnlyList<ScKnowledgeItem> items,
        int tradeLocationCount,
        int? itemCountOverride = null)
    {
        SourceRoot = sourceRoot;
        Locations = locations;
        Factions = factions;
        Items = items;
        _locationsByUuid = locations
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid))
            .GroupBy(x => x.Uuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        _locationsByNormalizedName = locations
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => Normalize(x.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        _factionsByUuid = factions
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid))
            .GroupBy(x => x.Uuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        _factionsByNormalizedName = factions
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => Normalize(x.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _itemsByReference = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Reference))
            .GroupBy(x => x.Reference, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Summary = new ScKnowledgeSummary(
            locations.Count,
            factions.Count,
            locations.SelectMany(x => x.Services).Select(x => x.Uuid).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            tradeLocationCount,
            locations.Count(x => x.CommoditiesSold.Count > 0 || x.CommoditiesBought.Count > 0),
            locations.SelectMany(x => x.CommoditiesSold.Concat(x.CommoditiesBought)).Select(x => x.Uuid + "|" + x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            itemCountOverride ?? items.Count,
            sourceRoot);
    }

    public string SourceRoot { get; }
    public IReadOnlyList<ScKnowledgeLocation> Locations { get; }
    public IReadOnlyList<ScKnowledgeFaction> Factions { get; }
    public IReadOnlyList<ScKnowledgeItem> Items { get; }
    public ScKnowledgeSummary Summary { get; }

    public ScKnowledgeLocation? LocationByUuid(string? uuid) =>
        !string.IsNullOrWhiteSpace(uuid) && _locationsByUuid.TryGetValue(uuid, out var value) ? value : null;

    public ScKnowledgeFaction? FactionByUuid(string? uuid) =>
        !string.IsNullOrWhiteSpace(uuid) && _factionsByUuid.TryGetValue(uuid, out var value) ? value : null;

    public ScKnowledgeItem? ItemByReference(string? reference) =>
        !string.IsNullOrWhiteSpace(reference) && _itemsByReference.TryGetValue(reference, out var value) ? value : null;

    public ScKnowledgeMatch? MatchLocation(
        string? sourceUuid,
        string? name,
        string system,
        string? parentSourceUuid = null,
        string? sourcePath = null)
    {
        if (!string.IsNullOrWhiteSpace(sourceUuid) && _locationsByUuid.TryGetValue(sourceUuid, out var direct))
        {
            return new ScKnowledgeMatch(
                direct,
                "DIRECT_UUID",
                1.0,
                $"Data.p4k SourceUuid == SCUnpacked starmap UUID ({sourceUuid})");
        }

        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = Normalize(name);
        if (key.Length == 0 || !_locationsByNormalizedName.TryGetValue(key, out var candidates)) return null;

        var systemMatches = candidates
            .Where(x => string.IsNullOrWhiteSpace(x.System) || x.System.Equals(system, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (systemMatches.Length == 0) return null;

        if (!string.IsNullOrWhiteSpace(parentSourceUuid))
        {
            var parentMatch = systemMatches.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.ParentUuid) && x.ParentUuid.Equals(parentSourceUuid, StringComparison.OrdinalIgnoreCase));
            if (parentMatch is not null)
                return new ScKnowledgeMatch(parentMatch, "NAME_PARENT_UUID_MATCH", 0.98, $"normalized name '{name}' + parent UUID + system");
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            var sourceNorm = Normalize(sourcePath);
            var containerMatch = systemMatches.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.ContainerName) && sourceNorm.Contains(Normalize(x.ContainerName), StringComparison.Ordinal));
            if (containerMatch is not null)
                return new ScKnowledgeMatch(containerMatch, "NAME_CONTAINER_MATCH", 0.96, $"normalized name '{name}' + SC container in Data.p4k source path");
        }

        if (systemMatches.Length == 1)
            return new ScKnowledgeMatch(systemMatches[0], "UNIQUE_NAME_SYSTEM_MATCH", 0.90, $"unique normalized name '{name}' in system '{system}'");

        return null;
    }

    /// <summary>
    /// Correlates a Data.p4k body-local surface anchor with a canonical SCUnpacked starmap location
    /// by geometry. This is intentionally a secondary matcher after UUID/name matching. It only
    /// considers locations whose direct SCUnpacked parent is the requested planet/moon, which keeps
    /// nested shops, terminals and other sub-locations from being mistaken for surface destinations.
    /// </summary>
    public ScKnowledgeMatch? MatchSurfaceLocationByPosition(
        string system,
        string bodyName,
        double bodyLocalX,
        double bodyLocalY,
        double bodyLocalZ,
        double maximumDistanceMeters = 3000d)
    {
        if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(bodyName)) return null;
        if (!double.IsFinite(bodyLocalX) || !double.IsFinite(bodyLocalY) || !double.IsFinite(bodyLocalZ)) return null;

        var bodyKey = Normalize(bodyName);
        if (!_locationsByNormalizedName.TryGetValue(bodyKey, out var bodyCandidates)) return null;
        var body = bodyCandidates.FirstOrDefault(x =>
            x.System.Equals(system, StringComparison.OrdinalIgnoreCase) &&
            x.PositionX is not null && x.PositionY is not null && x.PositionZ is not null &&
            (x.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) ||
             x.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase)));
        if (body is null || string.IsNullOrWhiteSpace(body.Uuid)) return null;

        var candidates = Locations
            .Where(x => x.System.Equals(system, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.ParentUuid, body.Uuid, StringComparison.OrdinalIgnoreCase) &&
                        x.PositionX is not null && x.PositionY is not null && x.PositionZ is not null &&
                        !x.Hidden)
            .Select(x => new
            {
                Location = x,
                Distance = Math.Sqrt(
                    Math.Pow((x.PositionX!.Value - body.PositionX!.Value) - bodyLocalX, 2) +
                    Math.Pow((x.PositionY!.Value - body.PositionY!.Value) - bodyLocalY, 2) +
                    Math.Pow((x.PositionZ!.Value - body.PositionZ!.Value) - bodyLocalZ, 2))
            })
            .OrderBy(x => x.Distance)
            .Take(2)
            .ToArray();

        if (candidates.Length == 0 || candidates[0].Distance > maximumDistanceMeters) return null;
        if (candidates.Length > 1 && candidates[1].Distance <= maximumDistanceMeters &&
            candidates[1].Distance < Math.Max(250d, candidates[0].Distance * 1.8d)) return null;

        var best = candidates[0];
        var confidence = best.Distance <= 50d ? 0.995 : best.Distance <= 500d ? 0.98 : 0.94;
        return new ScKnowledgeMatch(
            best.Location,
            "BODY_LOCAL_POSITION_MATCH",
            confidence,
            $"Data.p4k body-local anchor matched SCUnpacked direct child of '{body.Name}' at {best.Distance:F1} m");
    }

    public IReadOnlyList<ScKnowledgeLocation> LocationsForSystem(string system) =>
        Locations.Where(x => x.System.Equals(system, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record BundledKnowledgePayload(
        int SchemaVersion,
        string SourceBuild,
        DateTimeOffset GeneratedUtc,
        ScKnowledgeLocation[] Locations,
        ScKnowledgeFaction[] Factions,
        int TradeLocationCount,
        int ItemCount);

    private static readonly JsonSerializerOptions BundledJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads the semantic baseline shipped with StarSyncUniverse. This is the default readable-name/
    /// description layer and does not require SCUnpacked, StarBreaker, Data.p4k or network access.
    /// </summary>
    public static ScUnpackedKnowledgeDatabase LoadBundledBaseline(string path)
    {
        using var stream = File.OpenRead(path);
        var payload = JsonSerializer.Deserialize<BundledKnowledgePayload>(stream, BundledJsonOptions)
            ?? throw new InvalidDataException($"Bundled location knowledge baseline is invalid: '{path}'.");
        if (payload.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported bundled location knowledge schema {payload.SchemaVersion} in '{path}'.");

        return new ScUnpackedKnowledgeDatabase(
            $"bundled://community-location-baseline/{payload.SourceBuild}",
            payload.Locations ?? [],
            payload.Factions ?? [],
            [],
            payload.TradeLocationCount,
            payload.ItemCount);
    }

    /// <summary>
    /// Development/update command: freezes the currently resolved SCUnpacked semantic layer into a
    /// redistributable project asset. Raw SCUnpacked files are not required by the resulting client.
    /// </summary>
    public async Task ExportBundledBaselineAsync(string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sourceBuild = Path.GetFileName(Path.GetDirectoryName(SourceRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            ?? "unknown";
        var payload = new BundledKnowledgePayload(
            1,
            sourceBuild,
            DateTimeOffset.UtcNow,
            Locations.ToArray(),
            Factions.ToArray(),
            Summary.TradeLocationCount,
            Summary.ItemCount);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, BundledJsonOptions, cancellationToken);
    }

    public static ScUnpackedKnowledgeDatabase Merge(
        ScUnpackedKnowledgeDatabase baseline,
        ScUnpackedKnowledgeDatabase refresh)
    {
        var locations = baseline.Locations
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid))
            .ToDictionary(x => x.Uuid, StringComparer.OrdinalIgnoreCase);
        foreach (var location in refresh.Locations)
            locations[location.Uuid] = location;

        var factions = baseline.Factions
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid))
            .ToDictionary(x => x.Uuid, StringComparer.OrdinalIgnoreCase);
        foreach (var faction in refresh.Factions.Where(x => !string.IsNullOrWhiteSpace(x.Uuid)))
            factions[faction.Uuid] = faction;

        return new ScUnpackedKnowledgeDatabase(
            baseline.SourceRoot + "+refresh:" + refresh.SourceRoot,
            locations.Values.OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            factions.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            refresh.Items,
            Math.Max(baseline.Summary.TradeLocationCount, refresh.Summary.TradeLocationCount),
            Math.Max(baseline.Summary.ItemCount, refresh.Summary.ItemCount));
    }

    public static ScUnpackedKnowledgeDatabase Load(string sourceRoot)
    {
        sourceRoot = Path.GetFullPath(sourceRoot);
        var starmapPath = Path.Combine(sourceRoot, "starmap.json");
        var positionsPath = Path.Combine(sourceRoot, "starmap_positions.json");
        var tradePath = Path.Combine(sourceRoot, "trade_locations.json");
        var commodityPath = Path.Combine(sourceRoot, "resources", "commodities.json");
        var commodityTradePath = Path.Combine(sourceRoot, "resources", "commodity_trade_locations.json");
        var itemsPath = Path.Combine(sourceRoot, "items.json");
        var factionsRoot = Path.Combine(sourceRoot, "factions");
        if (!File.Exists(starmapPath)) throw new FileNotFoundException("SCUnpacked starmap.json missing.", starmapPath);

        var positionByUuid = LoadPositions(positionsPath);
        var factions = LoadFactions(factionsRoot);
        var factionsByName = new Dictionary<string, ScKnowledgeFaction>(StringComparer.Ordinal);
        var factionsByUuid = factions
            .Where(x => !string.IsNullOrWhiteSpace(x.Uuid))
            .GroupBy(x => x.Uuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var faction in factions)
        {
            AddFactionName(factionsByName, faction.Name, faction);
        }
        var tradeProfiles = LoadTradeProfiles(tradePath);
        var tradeByName = tradeProfiles
            .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName))
            .GroupBy(x => Normalize(x.DisplayName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScKnowledgeTradeProfile>)g.ToArray(), StringComparer.Ordinal);
        var commodityRelationsByStarmapUuid = LoadCommodityRelations(commodityPath, commodityTradePath);
        var items = LoadItems(itemsPath);
        var itemsByIdentity = new Dictionary<string, List<ScKnowledgeItem>>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            AddItemIdentity(itemsByIdentity, item.Name, item);
            AddItemIdentity(itemsByIdentity, item.ClassName, item);
        }

        using var stream = File.OpenRead(starmapPath);
        using var json = JsonDocument.Parse(stream);
        var raw = new List<RawLocation>();
        foreach (var element in json.RootElement.EnumerateArray())
        {
            var uuid = GetString(element, "UUID");
            if (string.IsNullOrWhiteSpace(uuid)) continue;
            var affiliation = GetObject(element, "Affiliation");
            var jurisdiction = GetObject(element, "Jurisdiction");
            var typeObject = GetObject(element, "Type");
            var radarContact = GetObject(element, "RadarContactType");
            var locationHierarchyTag = GetObject(element, "LocationHierarchyTag");
            var quantumTravel = GetObject(element, "QuantumTravel");
            // Amenities carry both Name and DisplayName. For service classification we must preserve
            // the canonical Name because DisplayName can intentionally collapse distinct services
            // (for example "Commodity Trading - Freight Elevator" -> "Commodity Trading").
            var services = ReadNamedRefs(element, "Amenities", preferCanonicalName: true);
            var tags = ReadNamedRefs(element, "Tags");
            var properties = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in ReadRelevantProperties(element)) properties[property.Key] = property.Value;
            AddProperty(properties, "NavIcon", GetString(element, "NavIcon"));
            AddProperty(properties, "RespawnLocationType", GetString(element, "RespawnLocationType"));
            AddProperty(properties, "Size", DisplayJsonProperty(element, "Size"));
            AddProperty(properties, "MinimumDisplaySize", DisplayJsonProperty(element, "MinimumDisplaySize"));
            AddProperty(properties, "TypeClassification", typeObject is { } to ? GetString(to, "Classification") : null);
            AddProperty(properties, "LocationHierarchyTag", locationHierarchyTag is { } lht ? GetString(lht, "Name") : null);
            AddProperty(properties, "RadarContact", radarContact is { } rc ? FirstNonEmpty(GetString(rc, "DisplayName"), GetString(rc, "Name")) : null);
            if (quantumTravel is { } qt)
            {
                AddProperty(properties, "QT.ObstructionRadius", DisplayJsonProperty(qt, "ObstructionRadius"));
                AddProperty(properties, "QT.ArrivalRadius", DisplayJsonProperty(qt, "ArrivalRadius"));
                AddProperty(properties, "QT.AdoptionRadius", DisplayJsonProperty(qt, "AdoptionRadius"));
            }
            positionByUuid.TryGetValue(uuid, out var position);

            var jurisdictionName = jurisdiction is { } j ? GetString(j, "Name") : null;
            var jurisdictionUuid = jurisdiction is { } jUuid ? GetString(jUuid, "UUID") : null;
            ScKnowledgeFaction? faction = null;
            var factionAuthority = "NONE";
            if (affiliation is { } aff)
            {
                var affiliationUuid = GetString(aff, "UUID");
                var affiliationName = GetString(aff, "Name");
                if (!string.IsNullOrWhiteSpace(affiliationUuid) && factionsByUuid.TryGetValue(affiliationUuid, out var affiliationByUuid))
                {
                    faction = affiliationByUuid;
                    factionAuthority = "AFFILIATION_UUID_TO_FACTION_UUID";
                }
                else if (!string.IsNullOrWhiteSpace(affiliationName) && factionsByName.TryGetValue(Normalize(affiliationName), out var affiliationByName))
                {
                    faction = affiliationByName;
                    factionAuthority = "AFFILIATION_NAME_TO_FACTION_NAME";
                }
            }
            if (faction is null && !string.IsNullOrWhiteSpace(jurisdictionUuid) && factionsByUuid.TryGetValue(jurisdictionUuid, out var jurisdictionByUuid))
            {
                faction = jurisdictionByUuid;
                factionAuthority = "JURISDICTION_UUID_TO_FACTION_UUID";
            }
            else if (faction is null && !string.IsNullOrWhiteSpace(jurisdictionName) && factionsByName.TryGetValue(Normalize(jurisdictionName), out var jurisdictionByName))
            {
                faction = jurisdictionByName;
                factionAuthority = "JURISDICTION_NAME_TO_FACTION_NAME";
            }

            var locationName = GetString(element, "Name") ?? "Unknown";
            var trades = tradeByName.TryGetValue(Normalize(locationName), out var matchedTrade)
                ? matchedTrade
                : Array.Empty<ScKnowledgeTradeProfile>();
            var produces = trades.SelectMany(x => x.Produces).GroupBy(x => x.Uuid + "|" + x.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var consumes = trades.SelectMany(x => x.Consumes).GroupBy(x => x.Uuid + "|" + x.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var commodityRelations = commodityRelationsByStarmapUuid.TryGetValue(uuid, out var exactCommodityRelations)
                ? exactCommodityRelations
                : CommodityRelations.Empty;
            var relatedItems = BuildItemLinks(produces, consumes, itemsByIdentity);

            raw.Add(new RawLocation(
                uuid,
                locationName,
                CleanText(GetString(element, "Description")),
                typeObject is { } type ? GetString(type, "Name") ?? string.Empty : string.Empty,
                typeObject is { } type2 ? GetString(type2, "Classification") ?? string.Empty : string.Empty,
                position?.System ?? string.Empty,
                GetString(element, "ParentUUID"),
                locationHierarchyTag is { } hierarchy ? GetString(hierarchy, "Name") : null,
                jurisdiction is { } jj ? GetString(jj, "UUID") : null,
                jurisdictionName,
                faction,
                factionAuthority,
                services,
                tags,
                properties,
                trades,
                produces,
                consumes,
                commodityRelations.Sold,
                commodityRelations.Bought,
                relatedItems,
                position?.Hidden ?? false,
                position?.QtValid ?? false,
                position?.X,
                position?.Y,
                position?.Z));
        }

        var rawByUuid = raw.ToDictionary(x => x.Uuid, StringComparer.OrdinalIgnoreCase);
        var locations = raw.Select(x => new ScKnowledgeLocation(
            x.Uuid,
            x.Name,
            x.Description,
            x.Type,
            x.Subtype,
            x.System,
            x.ParentUuid,
            x.ParentUuid is not null && rawByUuid.TryGetValue(x.ParentUuid, out var parent) ? parent.Name : null,
            x.ContainerName,
            x.JurisdictionUuid,
            x.JurisdictionName,
            x.Faction?.Uuid,
            x.Faction?.Name,
            x.FactionAuthority,
            x.Faction?.Description,
            x.Faction?.FactionType,
            x.Faction?.Classification,
            x.Faction?.Relations ?? [],
            x.Services,
            x.Tags,
            x.Properties,
            x.TradeProfiles,
            x.TradeProduces,
            x.TradeConsumes,
            x.CommoditiesSold,
            x.CommoditiesBought,
            x.RelatedItems,
            x.Hidden,
            x.QtValid,
            x.X,
            x.Y,
            x.Z,
            "scunpacked/starmap.json"))
            .OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ScUnpackedKnowledgeDatabase(sourceRoot, locations, factions, items, tradeProfiles.Count);
    }

    public async Task<string> WriteAuditAsync(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarSyncUniverse", "KnowledgeDatabase");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "scunpacked-data-p4k-correlation.tsv");
        var lines = new List<string>
        {
            "System\tObjectKind\tDataP4kId\tDataP4kSourceUuid\tDataP4kName\tScUuid\tScName\tLinkAuthority\tConfidence\tEvidence"
        };
        foreach (var pair in datasets.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var entity in pair.Value.Entities)
            {
                var match = MatchLocation(entity.SourceUuid, entity.Name, pair.Key, entity.ParentSourceUuid, entity.SourcePath);
                if (match is null) continue;
                lines.Add(Tsv(pair.Key, "PLACEMENT", entity.Id, entity.SourceUuid, entity.Name, match.Location.Uuid, match.Location.Name, match.LinkAuthority, match.Confidence.ToString("0.00", CultureInfo.InvariantCulture), match.Evidence));
            }
            foreach (var anchor in pair.Value.BodyAnchors)
            {
                var match = MatchLocation(anchor.SourceUuid, anchor.Name, pair.Key, anchor.BodySourceUuid, anchor.SourcePath);
                if (match is null) continue;
                lines.Add(Tsv(pair.Key, "BODY_ANCHOR", anchor.AnchorId, anchor.SourceUuid, anchor.Name, match.Location.Uuid, match.Location.Name, match.LinkAuthority, match.Confidence.ToString("0.00", CultureInfo.InvariantCulture), match.Evidence));
            }
        }
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
        return path;
    }

    private static string Tsv(params string?[] values) => string.Join('\t', values.Select(x => (x ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ')));

    private static Dictionary<string, PositionRecord> LoadPositions(string path)
    {
        var result = new Dictionary<string, PositionRecord>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return result;
        using var stream = File.OpenRead(path);
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in entities.EnumerateArray()) AddPosition(result, GetString(e, "uuid"), e);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject()) AddPosition(result, property.Name, property.Value);
        }
        return result;
    }

    private static void AddPosition(Dictionary<string, PositionRecord> target, string? uuid, JsonElement e)
    {
        if (string.IsNullOrWhiteSpace(uuid) || e.ValueKind != JsonValueKind.Object) return;
        target[uuid] = new PositionRecord(
            GetString(e, "system") ?? string.Empty,
            GetBool(e, "hidden"),
            GetBool(e, "qt_valid"),
            GetDouble(e, "x"),
            GetDouble(e, "y"),
            GetDouble(e, "z"));
    }

    private static IReadOnlyList<ScKnowledgeFaction> LoadFactions(string root)
    {
        if (!Directory.Exists(root)) return [];
        var list = new List<ScKnowledgeFaction>();
        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var json = JsonDocument.Parse(stream);
                var e = json.RootElement;
                var uuid = GetString(e, "UUID") ?? string.Empty;
                var name = GetString(e, "Name") ?? Path.GetFileNameWithoutExtension(file);
                var headquarters = ReadDisplayValue(e, "Headquarters");
                list.Add(new ScKnowledgeFaction(
                    uuid,
                    name,
                    CleanText(GetString(e, "Description")),
                    GetString(e, "FactionType") ?? string.Empty,
                    GetString(e, "Classification") ?? string.Empty,
                    headquarters,
                    ReadNamedRefs(e, "Relations"),
                    "scunpacked/factions/" + Path.GetFileName(file)));
            }
            catch (JsonException) { }
        }
        return list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<ScKnowledgeTradeProfile> LoadTradeProfiles(string path)
    {
        if (!File.Exists(path)) return [];
        using var stream = File.OpenRead(path);
        using var json = JsonDocument.Parse(stream);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return [];
        var list = new List<ScKnowledgeTradeProfile>();
        foreach (var e in json.RootElement.EnumerateArray())
        {
            list.Add(new ScKnowledgeTradeProfile(
                GetString(e, "UUID") ?? string.Empty,
                GetString(e, "ClassName") ?? string.Empty,
                GetString(e, "DisplayName") ?? string.Empty,
                GetBool(e, "Disabled"),
                ReadPositiveTags(e, "ProducesTags"),
                ReadPositiveTags(e, "ConsumesTags"),
                "scunpacked/trade_locations.json"));
        }
        return list;
    }

    private static IReadOnlyDictionary<string, CommodityRelations> LoadCommodityRelations(string commodityPath, string tradePath)
    {
        var commodityByUuid = new Dictionary<string, ScKnowledgeCommodityRef>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(commodityPath))
        {
            using var commodityStream = File.OpenRead(commodityPath);
            using var commodityJson = JsonDocument.Parse(commodityStream);
            if (commodityJson.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in commodityJson.RootElement.EnumerateArray())
                {
                    var uuid = GetString(e, "UUID") ?? string.Empty;
                    if (uuid.Length == 0) continue;
                    var key = GetString(e, "Key") ?? string.Empty;
                    var rawName = GetString(e, "Name") ?? string.Empty;
                    var name = rawName.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rawName)
                        ? key
                        : rawName;
                    commodityByUuid[uuid] = new ScKnowledgeCommodityRef(
                        uuid,
                        key,
                        name,
                        CleanText(GetString(e, "Description")),
                        "SCUNPACKED_COMMODITY_UUID");
                }
            }
        }

        var soldByLocation = new Dictionary<string, List<ScKnowledgeCommodityRef>>(StringComparer.OrdinalIgnoreCase);
        var boughtByLocation = new Dictionary<string, List<ScKnowledgeCommodityRef>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(tradePath))
        {
            using var tradeStream = File.OpenRead(tradePath);
            using var tradeJson = JsonDocument.Parse(tradeStream);
            if (tradeJson.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in tradeJson.RootElement.EnumerateArray())
                {
                    var commodityUuid = GetString(e, "CommodityUUID") ?? string.Empty;
                    var commodityKey = GetString(e, "CommodityKey") ?? string.Empty;
                    var commodityName = FirstNonEmpty(GetString(e, "CommodityName"), commodityKey) ?? string.Empty;
                    var commodity = commodityByUuid.TryGetValue(commodityUuid, out var known)
                        ? known with { LinkAuthority = "STARMAP_UUID_COMMODITY_TRADE_LOCATION" }
                        : new ScKnowledgeCommodityRef(commodityUuid, commodityKey, commodityName, string.Empty, "STARMAP_UUID_COMMODITY_TRADE_LOCATION");
                    AddTradeEndpoints(e, "SoldAt", soldByLocation, commodity);
                    AddTradeEndpoints(e, "BoughtAt", boughtByLocation, commodity);
                }
            }
        }

        var keys = soldByLocation.Keys.Concat(boughtByLocation.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        return keys.ToDictionary(
            x => x,
            x => new CommodityRelations(
                DistinctCommodities(soldByLocation.TryGetValue(x, out var sold) ? sold : []),
                DistinctCommodities(boughtByLocation.TryGetValue(x, out var bought) ? bought : [])),
            StringComparer.OrdinalIgnoreCase);

        static void AddTradeEndpoints(JsonElement commodity, string propertyName, Dictionary<string, List<ScKnowledgeCommodityRef>> target, ScKnowledgeCommodityRef item)
        {
            if (!commodity.TryGetProperty(propertyName, out var endpoints) || endpoints.ValueKind != JsonValueKind.Array) return;
            foreach (var endpoint in endpoints.EnumerateArray())
            {
                var starmapUuid = GetString(endpoint, "StarmapObjectUUID");
                var matchedTag = GetString(endpoint, "MatchedTagName");
                if (string.IsNullOrWhiteSpace(starmapUuid) || string.IsNullOrWhiteSpace(matchedTag)) continue;
                var tagKey = Normalize(matchedTag);
                if (!tagKey.Equals(Normalize(item.Key), StringComparison.Ordinal) &&
                    !tagKey.Equals(Normalize(item.Name), StringComparison.Ordinal))
                    continue;
                var exactItem = item with { LinkAuthority = "EXACT_COMMODITY_TAG_AND_STARMAP_UUID" };
                if (!target.TryGetValue(starmapUuid, out var list)) target[starmapUuid] = list = [];
                list.Add(exactItem);
            }
        }

        static IReadOnlyList<ScKnowledgeCommodityRef> DistinctCommodities(IEnumerable<ScKnowledgeCommodityRef> values) =>
            values.GroupBy(x => x.Uuid + "|" + x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private static IReadOnlyList<ScKnowledgeItem> LoadItems(string path)
    {
        if (!File.Exists(path)) return [];
        using var stream = File.OpenRead(path);
        using var json = JsonDocument.Parse(stream);
        if (json.RootElement.ValueKind != JsonValueKind.Array) return [];
        var list = new List<ScKnowledgeItem>();
        foreach (var e in json.RootElement.EnumerateArray())
        {
            var stdItem = GetObject(e, "stdItem");
            var name = FirstNonEmpty(GetString(e, "name"), stdItem is { } si ? GetString(si, "Name") : null) ?? string.Empty;
            var className = FirstNonEmpty(GetString(e, "className"), stdItem is { } si2 ? GetString(si2, "ClassName") : null) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(className)) continue;
            var manufacturerObject = stdItem is { } si3 ? GetObject(si3, "Manufacturer") : null;
            var description = stdItem is { } si4
                ? FirstNonEmpty(GetString(si4, "DescriptionText"), GetString(si4, "Description"))
                : null;
            var tags = ReadStringList(e, "tags");
            if (tags.Count == 0 && stdItem is { } si5) tags = ReadStringList(si5, "Tags");
            list.Add(new ScKnowledgeItem(
                FirstNonEmpty(GetString(e, "reference"), stdItem is { } si6 ? GetString(si6, "UUID") : null) ?? string.Empty,
                className,
                name,
                FirstNonEmpty(GetString(e, "type"), stdItem is { } si7 ? GetString(si7, "Type") : null) ?? string.Empty,
                GetString(e, "subType") ?? string.Empty,
                CleanText(description),
                FirstNonEmpty(manufacturerObject is { } manufacturer ? GetString(manufacturer, "Name") : null, GetString(e, "manufacturer")) ?? string.Empty,
                FirstNonEmpty(DisplayJsonProperty(e, "size"), stdItem is { } si8 ? DisplayJsonProperty(si8, "Size") : null) ?? string.Empty,
                FirstNonEmpty(DisplayJsonProperty(e, "grade"), stdItem is { } si9 ? DisplayJsonProperty(si9, "Grade") : null) ?? string.Empty,
                stdItem is { } si10 ? GetString(si10, "Type") ?? string.Empty : string.Empty,
                tags,
                "scunpacked/items.json"));
        }
        return list;
    }

    private static void AddItemIdentity(Dictionary<string, List<ScKnowledgeItem>> index, string? value, ScKnowledgeItem item)
    {
        var key = Normalize(value);
        if (key.Length == 0) return;
        if (!index.TryGetValue(key, out var list)) index[key] = list = [];
        if (!list.Any(x => x.Reference.Equals(item.Reference, StringComparison.OrdinalIgnoreCase) && x.ClassName.Equals(item.ClassName, StringComparison.OrdinalIgnoreCase)))
            list.Add(item);
    }

    private static IReadOnlyList<ScKnowledgeItemLink> BuildItemLinks(
        IReadOnlyList<ScKnowledgeNamedRef> produces,
        IReadOnlyList<ScKnowledgeNamedRef> consumes,
        IReadOnlyDictionary<string, List<ScKnowledgeItem>> itemsByIdentity)
    {
        var links = new List<ScKnowledgeItemLink>();
        AddLinks(produces, "PRODUCES");
        AddLinks(consumes, "CONSUMES");
        return links
            .GroupBy(x => x.Relation + "|" + x.Reference + "|" + x.ClassName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Relation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        void AddLinks(IEnumerable<ScKnowledgeNamedRef> refs, string relation)
        {
            foreach (var tradeRef in refs)
            {
                var key = Normalize(tradeRef.Name);
                if (key.Length == 0 || !itemsByIdentity.TryGetValue(key, out var matches)) continue;
                foreach (var item in matches.Take(12))
                {
                    links.Add(new ScKnowledgeItemLink(
                        item.Reference,
                        item.ClassName,
                        item.Name,
                        item.Type,
                        item.SubType,
                        relation,
                        "TRADE_TAG_NAME_TO_ITEM_NAME_OR_CLASS"));
                }
            }
        }
    }

    private static string? DisplayJsonProperty(JsonElement e, string propertyName) =>
        e.TryGetProperty(propertyName, out var value) ? DisplayJson(value) : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static void AddProperty(IDictionary<string, string> properties, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) properties[name] = value.Trim();
    }

    private static IReadOnlyList<ScKnowledgeNamedRef> ReadPositiveTags(JsonElement e, string propertyName)
    {
        if (!e.TryGetProperty(propertyName, out var root) || root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("Positive", out var positive) || positive.ValueKind != JsonValueKind.Array) return [];
        return ReadNamedRefs(positive);
    }

    private static IReadOnlyList<ScKnowledgeNamedRef> ReadNamedRefs(JsonElement e, string propertyName, bool preferCanonicalName = false)
    {
        if (!e.TryGetProperty(propertyName, out var value)) return [];
        return ReadNamedRefs(value, preferCanonicalName);
    }

    private static IReadOnlyList<ScKnowledgeNamedRef> ReadNamedRefs(JsonElement value, bool preferCanonicalName = false)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<ScKnowledgeNamedRef>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var uuid = GetString(item, "UUID") ?? GetString(item, "Uuid") ?? string.Empty;
                var name = preferCanonicalName
                    ? FirstNonEmpty(GetString(item, "Name"), GetString(item, "name"), GetString(item, "DisplayName")) ?? string.Empty
                    : FirstNonEmpty(GetString(item, "DisplayName"), GetString(item, "Name"), GetString(item, "name")) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(uuid)) result.Add(new ScKnowledgeNamedRef(uuid, name));
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString() ?? string.Empty;
                if (text.Length > 0) result.Add(new ScKnowledgeNamedRef(string.Empty, text));
            }
        }
        return result.GroupBy(x => x.Uuid + "|" + x.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
    }

    private static IReadOnlyDictionary<string, string> ReadRelevantProperties(JsonElement e)
    {
        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!e.TryGetProperty("Properties", out var properties) || properties.ValueKind != JsonValueKind.Object) return result;
        foreach (var property in properties.EnumerateObject())
        {
            if (!IsRelevantProperty(property.Name)) continue;
            var value = DisplayJson(property.Value);
            if (!string.IsNullOrWhiteSpace(value)) result[property.Name] = value;
        }
        return result;
    }

    private static bool IsRelevantProperty(string name)
    {
        var n = Normalize(name);
        return n.Contains("radius", StringComparison.Ordinal) ||
               n.Contains("rotation", StringComparison.Ordinal) ||
               n.Contains("gravity", StringComparison.Ordinal) ||
               n.Contains("temperature", StringComparison.Ordinal) ||
               n.Contains("atmos", StringComparison.Ordinal) ||
               n.Contains("population", StringComparison.Ordinal) ||
               n.Contains("econom", StringComparison.Ordinal) ||
               n.Contains("danger", StringComparison.Ordinal) ||
               n.Contains("habitable", StringComparison.Ordinal) ||
               n.Contains("water", StringComparison.Ordinal) ||
               n.Contains("security", StringComparison.Ordinal) ||
               n.Contains("size", StringComparison.Ordinal) ||
               n.Contains("mass", StringComparison.Ordinal) ||
               n.Contains("distance", StringComparison.Ordinal);
    }

    private static string? ReadDisplayValue(JsonElement e, string propertyName) =>
        e.TryGetProperty(propertyName, out var value) ? DisplayJson(value) : null;

    private static string DisplayJson(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => GetString(value, "Name") ?? GetString(value, "name") ?? GetString(value, "DisplayName") ?? string.Empty,
            JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(DisplayJson).Where(x => !string.IsNullOrWhiteSpace(x)).Take(24)),
            _ => string.Empty
        };
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement e, string propertyName)
    {
        if (!e.TryGetProperty(propertyName, out var value)) return [];
        if (value.ValueKind == JsonValueKind.String)
            return (value.GetString() ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Select(DisplayJson).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return [];
    }

    private static JsonElement? GetObject(JsonElement e, string propertyName)
    {
        if (e.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object) return value;
        return null;
    }

    private static string? GetString(JsonElement e, string propertyName)
    {
        if (!e.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }

    private static bool GetBool(JsonElement e, string propertyName)
    {
        if (!e.TryGetProperty(propertyName, out var value)) return false;
        return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var result) && result;
    }

    private static double? GetDouble(JsonElement e, string propertyName)
    {
        if (!e.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static string CleanText(string? text) => string.IsNullOrWhiteSpace(text)
        ? string.Empty
        : text.Replace("\\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static void AddFactionName(Dictionary<string, ScKnowledgeFaction> target, string? name, ScKnowledgeFaction faction)
    {
        var key = Normalize(name);
        if (key.Length > 0 && !target.ContainsKey(key)) target[key] = faction;
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private sealed record PositionRecord(string System, bool Hidden, bool QtValid, double? X, double? Y, double? Z);

    private sealed record CommodityRelations(
        IReadOnlyList<ScKnowledgeCommodityRef> Sold,
        IReadOnlyList<ScKnowledgeCommodityRef> Bought)
    {
        public static readonly CommodityRelations Empty = new([], []);
    }

    private sealed record RawLocation(
        string Uuid,
        string Name,
        string Description,
        string Type,
        string Subtype,
        string System,
        string? ParentUuid,
        string? ContainerName,
        string? JurisdictionUuid,
        string? JurisdictionName,
        ScKnowledgeFaction? Faction,
        string FactionAuthority,
        IReadOnlyList<ScKnowledgeNamedRef> Services,
        IReadOnlyList<ScKnowledgeNamedRef> Tags,
        IReadOnlyDictionary<string, string> Properties,
        IReadOnlyList<ScKnowledgeTradeProfile> TradeProfiles,
        IReadOnlyList<ScKnowledgeNamedRef> TradeProduces,
        IReadOnlyList<ScKnowledgeNamedRef> TradeConsumes,
        IReadOnlyList<ScKnowledgeCommodityRef> CommoditiesSold,
        IReadOnlyList<ScKnowledgeCommodityRef> CommoditiesBought,
        IReadOnlyList<ScKnowledgeItemLink> RelatedItems,
        bool Hidden,
        bool QtValid,
        double? X,
        double? Y,
        double? Z);
}
