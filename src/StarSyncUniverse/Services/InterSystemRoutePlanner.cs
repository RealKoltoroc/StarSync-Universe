using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class InterSystemRoutePlanner
{
    public static SystemRouteResult FindRoute(
        IReadOnlyList<JumpConnection> connections,
        string startSystem,
        string destinationSystem)
    {
        startSystem = Normalize(startSystem);
        destinationSystem = Normalize(destinationSystem);

        if (startSystem.Equals(destinationSystem, StringComparison.OrdinalIgnoreCase))
            return new SystemRouteResult(startSystem, destinationSystem, true, [startSystem], [], "LOCAL_TOPOLOGICAL");

        var traversable = connections.Where(c => c.Traversable && c.ReciprocalEndpointsPresent).ToArray();
        var adjacency = new Dictionary<string, List<(string Next, string ConnectionId)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in traversable)
        {
            Add(adjacency, c.SystemA, c.SystemB, c.ConnectionId);
            Add(adjacency, c.SystemB, c.SystemA, c.ConnectionId);
        }

        var queue = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startSystem };
        var previous = new Dictionary<string, (string Previous, string ConnectionId)>(StringComparer.OrdinalIgnoreCase);
        queue.Enqueue(startSystem);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var edges)) continue;
            foreach (var edge in edges)
            {
                if (!visited.Add(edge.Next)) continue;
                previous[edge.Next] = (current, edge.ConnectionId);
                if (edge.Next.Equals(destinationSystem, StringComparison.OrdinalIgnoreCase))
                    return Reconstruct(startSystem, destinationSystem, previous);
                queue.Enqueue(edge.Next);
            }
        }

        return new SystemRouteResult(startSystem, destinationSystem, false, [], [], "NO_RECIPROCAL_ROUTE");
    }

    private static SystemRouteResult Reconstruct(
        string start,
        string destination,
        Dictionary<string, (string Previous, string ConnectionId)> previous)
    {
        var systems = new List<string> { destination };
        var connections = new List<string>();
        var current = destination;
        while (!current.Equals(start, StringComparison.OrdinalIgnoreCase))
        {
            var step = previous[current];
            connections.Add(step.ConnectionId);
            current = step.Previous;
            systems.Add(current);
        }
        systems.Reverse();
        connections.Reverse();
        return new SystemRouteResult(start, destination, true, systems, connections, "LOCAL_TOPOLOGICAL_RECIPROCAL_JUMPS");
    }

    private static void Add(Dictionary<string, List<(string Next, string ConnectionId)>> map, string from, string to, string id)
    {
        if (!map.TryGetValue(from, out var list)) map[from] = list = [];
        list.Add((to, id));
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
