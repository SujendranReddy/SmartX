
namespace SmartX.Api.Collections;

public sealed record MeshRouteResult(
    string SensorId,
    string GatewayId,
    string[] Path,
    double? TotalCost,
    bool IsReachable);

public static class MeshRouting
{
    public static MeshRouteResult GetRoute(
        MeshGraph graph,
        string gatewayId,
        string sensorId)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.ContainsNode(gatewayId))
        {
            throw new KeyNotFoundException(
                "The gateway node does not exist.");
        }

        if (!graph.ContainsNode(sensorId))
        {
            throw new KeyNotFoundException(
                "The sensor node does not exist.");
        }

        var nodes = graph.GetNodes();

        var gateway = nodes.First(node =>
            string.Equals(
                node,
                gatewayId.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var sensor = nodes.First(node =>
            string.Equals(
                node,
                sensorId.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var tree = graph.GetMinimumSpanningTree(gateway);

        var reachable = tree.ReachableNodes.Contains(
            sensor,
            StringComparer.OrdinalIgnoreCase);

        if (!reachable)
        {
            return new MeshRouteResult(
                sensor,
                gateway,
                [],
                null,
                false);
        }

        if (string.Equals(
            sensor,
            gateway,
            StringComparison.OrdinalIgnoreCase))
        {
            return new MeshRouteResult(
                sensor,
                gateway,
                [gateway],
                0,
                true);
        }
        //keep track of the conection leading back to the gateway
        var parents = new Dictionary<string, MeshConnection>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var connection in tree.Connections)
        {
            parents[connection.Target] = connection;
        }

        var path = new List<string>();
        var current = sensor;
        var totalCost = 0d;

        path.Add(current);
        //follow the route to the gateway and add up the connection costs
        while (!string.Equals(
            current,
            gateway,
            StringComparison.OrdinalIgnoreCase))
        {
            if (!parents.TryGetValue(current, out var connection))
            {
                return new MeshRouteResult(
                    sensor,
                    gateway,
                    [],
                    null,
                    false);
            }

            totalCost += connection.Cost;
            current = connection.Source;
            path.Add(current);
        }

        return new MeshRouteResult(
            sensor,
            gateway,
            path.ToArray(),
            totalCost,
            true);
    }
}
