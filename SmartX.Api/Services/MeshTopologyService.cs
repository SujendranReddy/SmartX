
using SmartX.Api.Collections;

namespace SmartX.Api.Services;

public sealed record MeshTopologyNode(
    string Id,
    Guid? SensorId,
    string DeploymentLocation,
    bool IsGateway,
    bool IsReachable);

public sealed record MeshTopologySnapshot(
    string GatewayId,
    MeshTopologyNode[] Nodes,
    MeshConnection[] Connections,
    MeshSpanningTree SpanningTree);

public sealed class MeshTopologyService
{
    public const string GatewayId = "SMARTX-GATEWAY";

    private readonly SensorRegistry registry;
    private readonly MeshGraph graph = new();
    private readonly object syncRoot = new();

    public MeshTopologyService(SensorRegistry registry)
    {
        this.registry = registry;
        graph.AddNode(GatewayId);
    }

    public MeshTopologySnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            SynchronizeSensors();

            var sensors = registry.GetAll();

            var sensorLookup = sensors.ToDictionary(
                sensor => sensor.DeviceIdentifier,
                StringComparer.OrdinalIgnoreCase);

            var spanningTree =
                graph.GetMinimumSpanningTree(GatewayId);

            var reachable = new HashSet<string>(
                spanningTree.ReachableNodes,
                StringComparer.OrdinalIgnoreCase);

            var nodes = graph.GetNodes()
                .Select(id =>
                {
                    if (string.Equals(
                        id,
                        GatewayId,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return new MeshTopologyNode(
                            id,
                            null,
                            "Central Gateway",
                            true,
                            true);
                    }

                    var sensor = sensorLookup[id];

                    return new MeshTopologyNode(
                        id,
                        sensor.Id,
                        sensor.DeploymentLocation,
                        false,
                        reachable.Contains(id));
                })
                .ToArray();

            return new MeshTopologySnapshot(
                GatewayId,
                nodes,
                graph.GetConnections(),
                spanningTree);
        }
    }

    public void AddConnection(
        string source,
        string target,
        double cost)
    {
        lock (syncRoot)
        {
            SynchronizeSensors();
            graph.AddConnection(source, target, cost);
        }
    }

    public MeshRouteResult GetSensorRoute(string sensorId)
    {
        lock (syncRoot)
        {
            SynchronizeSensors();

            return MeshRouting.GetRoute(
                graph,
                GatewayId,
                sensorId);
        }
    }

    private void SynchronizeSensors()
    {
        foreach (var sensor in registry.GetAll())
        {
            if (string.Equals(
                sensor.DeviceIdentifier,
                GatewayId,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A sensor identifier conflicts with the reserved gateway identifier.");
            }

            graph.AddNode(sensor.DeviceIdentifier);
        }
    }
}
