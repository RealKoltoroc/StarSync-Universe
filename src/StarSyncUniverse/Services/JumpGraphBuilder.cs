using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class JumpGraphBuilder
{
    private static readonly string[] KnownSystems = ["stanton", "pyro", "nyx", "magnus", "terra", "castra"];

    public static IReadOnlyList<JumpConnection> Build(IReadOnlyDictionary<string, UniverseDataset> datasets)
    {
        var endpoints = new List<JumpEndpoint>();

        foreach (var dataset in datasets.Values)
        {
            foreach (var entity in dataset.Entities)
            {
                var destination = TryParseDestination(dataset.System, entity);
                if (destination is null) continue;

                endpoints.Add(new JumpEndpoint(
                    EndpointId: entity.SourceUuid ?? entity.Id,
                    System: dataset.System,
                    DestinationSystem: destination,
                    Name: entity.Name,
                    EndpointType: entity.Type,
                    SourceUuid: entity.SourceUuid,
                    PlacementId: entity.Id,
                    X: entity.X,
                    Y: entity.Y,
                    Z: entity.Z,
                    SourceAuthority: entity.SourceAuthority,
                    DataStatus: entity.DataStatus));
            }
        }

        var keys = endpoints
            .Select(e => CanonicalPair(e.System, e.DestinationSystem))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        var result = new List<JumpConnection>();
        foreach (var key in keys)
        {
            var parts = key.Split('|');
            var a = parts[0];
            var b = parts[1];
            var endpointA = endpoints.FirstOrDefault(e =>
                e.System.Equals(a, StringComparison.OrdinalIgnoreCase) &&
                e.DestinationSystem.Equals(b, StringComparison.OrdinalIgnoreCase));
            var endpointB = endpoints.FirstOrDefault(e =>
                e.System.Equals(b, StringComparison.OrdinalIgnoreCase) &&
                e.DestinationSystem.Equals(a, StringComparison.OrdinalIgnoreCase));

            var reciprocal = endpointA is not null && endpointB is not null;
            var endpointTypes = new[] { endpointA?.EndpointType, endpointB?.EndpointType }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var connectionType = endpointTypes.Any(x => x!.Contains("Anomaly", StringComparison.OrdinalIgnoreCase)) ? "JUMP_POINT_ANOMALY" : "JUMP_POINT";
            result.Add(new JumpConnection(
                ConnectionId: $"jump:{a}:{b}",
                SystemA: a,
                SystemB: b,
                EndpointA: endpointA,
                EndpointB: endpointB,
                ReciprocalEndpointsPresent: reciprocal,
                ConnectionType: connectionType,
                Availability: reciprocal ? "LOCAL_TRAVERSABLE" : "LOCAL_ENDPOINT_ONLY",
                Traversable: reciprocal,
                SourceAuthority: "Data.p4k system ObjectContainer jump endpoints",
                DataStatus: reciprocal ? "LOCAL_DIRECT_PAIRED" : "LOCAL_DIRECT_UNPAIRED"));
        }

        return result;
    }

    private static string? TryParseDestination(string system, UniverseEntity entity)
    {
        var candidates = new[]
        {
            entity.Name,
            entity.SourcePath ?? string.Empty,
            entity.EntityClass ?? string.Empty
        };

        foreach (var candidate in candidates)
        {
            var normalized = candidate
                .Replace('\\', '/')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .ToLowerInvariant();
            if (!normalized.Contains("jump point", StringComparison.Ordinal) &&
                !normalized.Contains("jumppoint", StringComparison.Ordinal))
                continue;

            var found = KnownSystems
                .Where(x => normalized.Contains(x, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!found.Any(x => x.Equals(system, StringComparison.OrdinalIgnoreCase)))
                continue;

            var destination = found.FirstOrDefault(x => !x.Equals(system, StringComparison.OrdinalIgnoreCase));
            if (destination is not null) return destination;
        }

        return null;
    }

    private static string CanonicalPair(string a, string b)
    {
        var values = new[] { a.ToLowerInvariant(), b.ToLowerInvariant() };
        Array.Sort(values, StringComparer.OrdinalIgnoreCase);
        return values[0] + "|" + values[1];
    }

}
