
using SmartX.Api.Collections;

namespace SmartX.Api.Services;

public sealed record MeshTopologyNode(
    string Id,
    Guid? SensorId,
    string DeploymentLocation,
    bool IsGateway,
    bool IsReachable,
    bool IsOnline);

public sealed record MeshTopologySnapshot(
    string GatewayId,
    MeshTopologyNode[] Nodes,
    MeshConnection[] Connections,
    MeshSpanningTree SpanningTree);

public sealed class MeshTopologyService
{
    public const string GatewayId = "SMARTX-GATEWAY";

    private readonly SensorRegistry registry;
    private readonly NetworkPersistenceStore? persistence;

    private readonly MeshGraph graph = new();

    private readonly HashSet<string> offlineNodes =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object syncRoot = new();

    public MeshTopologyService(SensorRegistry registry)
    {
        this.registry = registry;

        graph.AddNode(GatewayId);
    }

    public MeshTopologyService(
        SensorRegistry registry,
        NetworkPersistenceStore persistence)
        : this(registry)
    {
        this.persistence = persistence;

        SynchronizeSensors();

        foreach (var connection in persistence.GetConnections())
        {
            if (!graph.ContainsNode(connection.Source) ||
                !graph.ContainsNode(connection.Target))
            {
                throw new InvalidDataException(
                    "A saved network connection references an unknown sensor.");
            }

            graph.AddConnection(
                connection.Source,
                connection.Target,
                connection.Cost);
        }
    }

    public MeshTopologySnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            SynchronizeSensors();

            return CreateSnapshot();
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

            if (persistence is not null &&
                persistence.IsPersistentNode(source) &&
                persistence.IsPersistentNode(target))
            {
                persistence.SaveConnection(
                    new MeshConnection(source, target, cost));
            }
        }
    }

    public MeshRouteResult GetSensorRoute(string sensorId)
    {
        lock (syncRoot)
        {
            SynchronizeSensors();

            if (string.IsNullOrWhiteSpace(sensorId) ||
                !graph.ContainsNode(sensorId))
            {
                throw new KeyNotFoundException(
                    "The sensor node does not exist.");
            }

            var sensor = graph.GetNodes().First(node =>
                string.Equals(
                    node,
                    sensorId.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (offlineNodes.Contains(sensor))
            {
                return new MeshRouteResult(
                    sensor,
                    GatewayId,
                    [],
                    null,
                    false);
            }

            return MeshRouting.GetRoute(
                BuildActiveGraph(),
                GatewayId,
                sensor);
        }
    }

    public MeshTopologySnapshot SetSensorAvailability(
        string sensorId,
        bool isOnline)
    {
        lock (syncRoot)
        {
            SynchronizeSensors();

            if (string.IsNullOrWhiteSpace(sensorId))
            {
                throw new ArgumentException(
                    "A sensor identifier is required.",
                    nameof(sensorId));
            }

            var sensor = graph.GetNodes().FirstOrDefault(node =>
                string.Equals(
                    node,
                    sensorId.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (sensor is null ||
                string.Equals(
                    sensor,
                    GatewayId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException(
                    "The requested sensor does not exist.");
            }

            if (isOnline)
            {
                offlineNodes.Remove(sensor);
            }
            else
            {
                offlineNodes.Add(sensor);
            }

            return CreateSnapshot();
        }
    }

    private MeshTopologySnapshot CreateSnapshot()
    {
        var sensors = registry.GetAll();

        var sensorLookup = sensors.ToDictionary(
            sensor => sensor.DeviceIdentifier,
            StringComparer.OrdinalIgnoreCase);

        var activeGraph = BuildActiveGraph();

        var spanningTree =
            activeGraph.GetMinimumSpanningTree(GatewayId);

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
                        true,
                        true);
                }

                var sensor = sensorLookup[id];

                return new MeshTopologyNode(
                    id,
                    sensor.Id,
                    sensor.DeploymentLocation,
                    false,
                    reachable.Contains(id),
                    !offlineNodes.Contains(id));
            })
            .ToArray();

        return new MeshTopologySnapshot(
            GatewayId,
            nodes,
            activeGraph.GetConnections(),
            spanningTree);
    }

    private MeshGraph BuildActiveGraph()
    {
        var activeGraph = new MeshGraph();

        foreach (var node in graph.GetNodes())
        {
            if (!offlineNodes.Contains(node))
            {
                activeGraph.AddNode(node);
            }
        }

        foreach (var connection in graph.GetConnections())
        {
            if (activeGraph.ContainsNode(connection.Source) &&
                activeGraph.ContainsNode(connection.Target))
            {
                activeGraph.AddConnection(
                    connection.Source,
                    connection.Target,
                    connection.Cost);
            }
        }

        return activeGraph;
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
