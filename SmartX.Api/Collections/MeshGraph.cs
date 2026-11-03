
namespace SmartX.Api.Collections;

public sealed record MeshConnection(
    string Source,
    string Target,
    double Cost);

public sealed record MeshSpanningTree(
    MeshConnection[] Connections,
    string[] ReachableNodes,
    string[] UnreachableNodes,
    double TotalCost);

public sealed class MeshGraph
{
    private readonly Dictionary<string, Dictionary<string, double>> adjacency =
        new(StringComparer.OrdinalIgnoreCase);

    public int NodeCount => adjacency.Count;

    public int ConnectionCount =>
        adjacency.Sum(node => node.Value.Count) / 2;

    public bool AddNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException(
                "A node identifier is required.",
                nameof(nodeId));
        }

        return adjacency.TryAdd(
            nodeId.Trim(),
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase));
    }

    public void AddConnection(
        string source,
        string target,
        double cost)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException(
                "Both node identifiers are required.");
        }

        source = source.Trim();
        target = target.Trim();

        if (string.Equals(
            source,
            target,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A node cannot connect to itself.");
        }

        if (!double.IsFinite(cost) || cost <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cost),
                "Connection cost must be a positive finite number.");
        }

        if (!adjacency.ContainsKey(source) ||
            !adjacency.ContainsKey(target))
        {
            throw new KeyNotFoundException(
                "Both nodes must exist before creating a connection.");
        }

        adjacency[source][target] = cost;
        adjacency[target][source] = cost;
    }

    public bool RemoveNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) ||
            !adjacency.TryGetValue(nodeId.Trim(), out var connections))
        {
            return false;
        }

        foreach (var neighbour in connections.Keys.ToArray())
        {
            adjacency[neighbour].Remove(nodeId.Trim());
        }

        return adjacency.Remove(nodeId.Trim());
    }

    public bool ContainsNode(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) &&
            adjacency.ContainsKey(nodeId.Trim());
    }

    public string[] GetNodes()
    {
        return adjacency.Keys
            .OrderBy(node => node, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public MeshConnection[] GetConnections()
    {
        var connections = new List<MeshConnection>();

        foreach (var node in adjacency)
        {
            foreach (var neighbour in node.Value)
            {
                if (StringComparer.OrdinalIgnoreCase.Compare(
                    node.Key,
                    neighbour.Key) < 0)
                {
                    connections.Add(new MeshConnection(
                        node.Key,
                        neighbour.Key,
                        neighbour.Value));
                }
            }
        }

        return connections
            .OrderBy(connection => connection.Source,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(connection => connection.Target,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string[] BreadthFirstSearch(string startNode)
    {
        if (string.IsNullOrWhiteSpace(startNode) ||
            !adjacency.ContainsKey(startNode.Trim()))
        {
            throw new KeyNotFoundException(
                "The starting node does not exist.");
        }

        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<string>();
        var result = new List<string>();

        startNode = startNode.Trim();

        visited.Add(startNode);
        queue.Enqueue(startNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var neighbour in adjacency[current].Keys
                .OrderBy(node => node, StringComparer.OrdinalIgnoreCase))
            {
                if (visited.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return result.ToArray();
    }

    public string[] GetUnreachableNodes(string gatewayId)
    {
        var reachable = new HashSet<string>(
            BreadthFirstSearch(gatewayId),
            StringComparer.OrdinalIgnoreCase);

        return adjacency.Keys
            .Where(node => !reachable.Contains(node))
            .OrderBy(node => node, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public MeshSpanningTree GetMinimumSpanningTree(string gatewayId)
    {
        if (string.IsNullOrWhiteSpace(gatewayId) ||
            !adjacency.ContainsKey(gatewayId.Trim()))
        {
            throw new KeyNotFoundException(
                "The gateway node does not exist.");
        }

        var gateway = adjacency.Keys.First(node =>
            string.Equals(
                node,
                gatewayId.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var selectedConnections = new List<MeshConnection>();

        var frontier = new PriorityQueue<
            MeshConnection,
            (double Cost, string Source, string Target)>();

        visited.Add(gateway);
        AddCandidateConnections(gateway);

        while (frontier.TryDequeue(out var connection, out _))
        {
            if (!visited.Add(connection.Target))
            {
                continue;
            }

            selectedConnections.Add(connection);
            AddCandidateConnections(connection.Target);
        }

        var reachableNodes = visited
            .OrderBy(node => node, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unreachableNodes = adjacency.Keys
            .Where(node => !visited.Contains(node))
            .OrderBy(node => node, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MeshSpanningTree(
            selectedConnections.ToArray(),
            reachableNodes,
            unreachableNodes,
            selectedConnections.Sum(connection => connection.Cost));

        void AddCandidateConnections(string source)
        {
            foreach (var neighbour in adjacency[source]
                .OrderBy(node => node.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (visited.Contains(neighbour.Key))
                {
                    continue;
                }

                var connection = new MeshConnection(
                    source,
                    neighbour.Key,
                    neighbour.Value);

                frontier.Enqueue(
                    connection,
                    (
                        neighbour.Value,
                        source.ToUpperInvariant(),
                        neighbour.Key.ToUpperInvariant()
                    ));
            }
        }
    }
}
