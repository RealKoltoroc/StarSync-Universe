using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class SurfaceTargetCatalogBuilder
{
    public static IReadOnlyList<SurfaceTargetCatalogEntry> Build(UniverseDataset dataset)
    {
        var hiddenSourceUuids = dataset.Entities
            .Where(e => e.Hidden && !string.IsNullOrWhiteSpace(e.SourceUuid))
            .Select(e => e.SourceUuid!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hiddenIds = dataset.Entities
            .Where(e => e.Hidden && !string.IsNullOrWhiteSpace(e.Id))
            .Select(e => e.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return dataset.BodyAnchors
            .Where(a => a.AnchorScope is "NEAR_SURFACE" or "BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED")
            .Select(a =>
            {
                var racing = RacingTrackCatalog.Resolve(a);
                return new SurfaceTargetCatalogEntry(
                    a.AnchorId,
                    a.System,
                    a.BodyName,
                    racing?.Name ?? ResolveDisplayName(a),
                    racing is not null || RacingTrackCatalog.LooksLikeRacingAnchor(a) ? "RacingTrack" : Classify(a),
                    a.AnchorScope,
                    a.SourceUuid,
                    a.EntityClass,
                    a.BodyLocalX,
                    a.BodyLocalY,
                    a.BodyLocalZ,
                    a.ReferenceRadiusMeters,
                    a.AxisLatitudeDegrees,
                    a.AxisLongitudeDegrees,
                    a.AltitudeMeters,
                    a.CoordinateConvention,
                    a.SourcePath,
                    a.SourceAuthority,
                    a.DataStatus,
                    IsTechnicalHelperAnchor(a) ||
                    (!string.IsNullOrWhiteSpace(a.SourceUuid) && hiddenSourceUuids.Contains(a.SourceUuid)) ||
                    hiddenIds.Contains(a.AnchorId));
            })
            .OrderBy(x => x.BodyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveDisplayName(BodyLocalAnchorRecord anchor)
    {
        var raw = anchor.Name?.Trim() ?? string.Empty;
        var path = (anchor.SourcePath ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        var normalized = raw.ToLowerInvariant();

        // Data.p4k contains a number of player-relevant surface object containers whose internal
        // object-container names are compact production abbreviations rather than display names.
        // Resolve only when the source path proves the semantic meaning; keep the original raw
        // name available in technical provenance through the underlying anchor record.
        if (normalized.Contains("drlct") &&
            (normalized.Contains("ctplr") || path.Contains("/drak/caterpillar/")) &&
            (normalized.Contains("sfce") || path.Contains("/surface/")) &&
            path.Contains("/crashsite/"))
        {
            var suffix = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var variant = !string.IsNullOrWhiteSpace(suffix) && suffix.All(char.IsDigit)
                ? $" {suffix}"
                : string.Empty;
            var puzzle = normalized.Contains("puz") ? " · Puzzle" : string.Empty;
            return $"Derelict Caterpillar Crash Site{puzzle}{variant}";
        }

        // AI landing/drop-off containers are technical navigation helpers, not evidence of a
        // crashsite or another player-facing POI by themselves. Keep them hidden by default and
        // present a readable technical label only when Show Hidden is enabled. A canonical
        // SCUnpacked location matched by UUID/name/body-local position takes precedence later in
        // the renderer and replaces this fallback label.
        if (path.Contains("/setup/ai_landing_area/") || normalized.Contains("ai landing area dropoff"))
        {
            var suffix = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var variant = !string.IsNullOrWhiteSpace(suffix) &&
                          !suffix.Equals("dropoff", StringComparison.OrdinalIgnoreCase)
                ? $" · {suffix.ToUpperInvariant()}"
                : string.Empty;
            return $"AI Landing Area · Drop-off{variant}";
        }

        return raw;
    }

    private static bool IsTechnicalHelperAnchor(BodyLocalAnchorRecord anchor)
    {
        var name = anchor.Name ?? string.Empty;
        var path = anchor.SourcePath ?? string.Empty;
        var text = (name + " " + path).ToLowerInvariant();

        // These are landing/drop-off helper object containers used by AI/navigation setup, not
        // canonical player-facing locations. Keep them available through Show Hidden/technical
        // provenance, but do not mix them into the normal Surface Locations catalogue.
        return text.Contains("/setup/ai_landing_area/") ||
               text.Contains("ai landing area dropoff") ||
               text.StartsWith("dropship landing", StringComparison.Ordinal);
    }

    private static string Classify(BodyLocalAnchorRecord anchor)
    {
        var name = anchor.Name ?? string.Empty;
        var path = anchor.SourcePath ?? string.Empty;
        var text = (name + " " + path).ToLowerInvariant();

        if (text.Contains("landingzone") || text.Contains("city") || text.Contains("area18") || text.Contains("lorville") || text.Contains("new babbage") || text.Contains("orison")) return "LandingZone";
        if (text.Contains("research lab") || text.Contains("researchlab")) return "Research";
        if (text.Contains("shelter")) return "Shelter";
        if (text.Contains("mining") || text.Contains(" mng ") || text.Contains("mine")) return "Mining";
        if (text.Contains("trade") || text.Contains("trdp") || text.Contains("depot")) return "Trade";
        if (text.Contains("scrap") || text.Contains("scrp")) return "Scrapyard";
        if (text.Contains("derelict") || text.Contains("drlct") || text.Contains("crashsite")) return "Derelict";
        if (text.Contains("prison")) return "Prison";
        if (text.Contains("facility") || text.Contains("borehole")) return "Facility";
        if (text.Contains("outpost") || text.Contains("otpst") || text.Contains("homestead") || text.Contains("hmstd")) return "Outpost";
        if (text.Contains("shuttle") || text.Contains("transit")) return "Transit";
        return "SurfaceTarget";
    }
}
