
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;
using Xunit;

namespace SmartX.Tests;

public sealed class NetworkPersistenceTests
{
    [Fact]
    public void RegisteredSensorsSurviveRestart()
    {
        using var storage = new TemporaryNetworkStorage();

        var persistence = storage.CreateStore();
        var registry = new SensorRegistry(persistence);

        var sensorA = RegisterSensor(registry, "PERSIST-TEST-A");
        var sensorB = RegisterSensor(registry, "PERSIST-TEST-B");

        var restartedRegistry = new SensorRegistry(
            storage.CreateStore());

        var restoredSensors = restartedRegistry.GetAll();

        Assert.Equal(2, restoredSensors.Length);

        var restoredA = Assert.Single(
            restoredSensors,
            sensor => sensor.DeviceIdentifier == "PERSIST-TEST-A");

        var restoredB = Assert.Single(
            restoredSensors,
            sensor => sensor.DeviceIdentifier == "PERSIST-TEST-B");

        Assert.Equal(sensorA.Id, restoredA.Id);
        Assert.Equal(sensorB.Id, restoredB.Id);
        Assert.Equal("Greenhouse A", restoredA.DeploymentLocation);
        Assert.Equal("Greenhouse A", restoredB.DeploymentLocation);
    }

    [Fact]
    public void NetworkConnectionsSurviveRestart()
    {
        using var storage = new TemporaryNetworkStorage();

        var persistence = storage.CreateStore();
        var registry = new SensorRegistry(persistence);

        RegisterSensor(registry, "PERSIST-TEST-A");
        RegisterSensor(registry, "PERSIST-TEST-B");

        var topology = new MeshTopologyService(
            registry,
            persistence);

        topology.AddConnection(
            MeshTopologyService.GatewayId,
            "PERSIST-TEST-A",
            2);

        topology.AddConnection(
            "PERSIST-TEST-A",
            "PERSIST-TEST-B",
            3);

        var restartedPersistence = storage.CreateStore();

        var restartedRegistry = new SensorRegistry(
            restartedPersistence);

        var restartedTopology = new MeshTopologyService(
            restartedRegistry,
            restartedPersistence);

        var snapshot = restartedTopology.GetSnapshot();

        Assert.Equal(2, snapshot.Connections.Length);
        Assert.Equal(5d, snapshot.SpanningTree.TotalCost);

        var route = restartedTopology.GetSensorRoute(
            "PERSIST-TEST-B");

        Assert.True(route.IsReachable);
        Assert.Equal(5d, route.TotalCost);

        Assert.Equal(
            new[]
            {
                "PERSIST-TEST-B",
                "PERSIST-TEST-A",
                MeshTopologyService.GatewayId
            },
            route.Path);
    }

    [Fact]
    public void TemporaryDemoSensorsAreNotPersisted()
    {
        using var storage = new TemporaryNetworkStorage();

        var persistence = storage.CreateStore();
        var registry = new SensorRegistry(persistence);

        RegisterSensor(registry, "PERSIST-TEST-A");

        RegisterSensor(
            registry,
            "SIM-TEST-POWER-0001",
            persistent: false);

        Assert.Equal(2, registry.GetAll().Length);

        var restartedRegistry = new SensorRegistry(
            storage.CreateStore());

        var restoredSensor = Assert.Single(
            restartedRegistry.GetAll());

        Assert.Equal(
            "PERSIST-TEST-A",
            restoredSensor.DeviceIdentifier);
    }

    [Fact]
    public void ConnectionsToTemporarySensorsAreNotPersisted()
    {
        using var storage = new TemporaryNetworkStorage();

        var persistence = storage.CreateStore();
        var registry = new SensorRegistry(persistence);

        RegisterSensor(registry, "PERSIST-TEST-A");

        RegisterSensor(
            registry,
            "SIM-TEST-POWER-0001",
            persistent: false);

        var topology = new MeshTopologyService(
            registry,
            persistence);

        topology.AddConnection(
            MeshTopologyService.GatewayId,
            "SIM-TEST-POWER-0001",
            2);

        topology.AddConnection(
            "SIM-TEST-POWER-0001",
            "PERSIST-TEST-A",
            3);

        Assert.Equal(
            2,
            topology.GetSnapshot().Connections.Length);

        var restartedPersistence = storage.CreateStore();

        var restartedRegistry = new SensorRegistry(
            restartedPersistence);

        var restartedTopology = new MeshTopologyService(
            restartedRegistry,
            restartedPersistence);

        var snapshot = restartedTopology.GetSnapshot();

        Assert.Empty(snapshot.Connections);

        Assert.DoesNotContain(
            snapshot.Nodes,
            node => node.Id == "SIM-TEST-POWER-0001");

        var route = restartedTopology.GetSensorRoute(
            "PERSIST-TEST-A");

        Assert.False(route.IsReachable);
    }

    [Fact]
    public void SimulatedSensorFailuresResetAfterRestart()
    {
        using var storage = new TemporaryNetworkStorage();

        var persistence = storage.CreateStore();
        var registry = new SensorRegistry(persistence);

        RegisterSensor(registry, "PERSIST-TEST-A");

        var topology = new MeshTopologyService(
            registry,
            persistence);

        topology.AddConnection(
            MeshTopologyService.GatewayId,
            "PERSIST-TEST-A",
            2);

        var failedSnapshot = topology.SetSensorAvailability(
            "PERSIST-TEST-A",
            false);

        var failedSensor = Assert.Single(
            failedSnapshot.Nodes,
            node => node.Id == "PERSIST-TEST-A");

        Assert.False(failedSensor.IsOnline);
        Assert.False(failedSensor.IsReachable);

        var restartedPersistence = storage.CreateStore();

        var restartedRegistry = new SensorRegistry(
            restartedPersistence);

        var restartedTopology = new MeshTopologyService(
            restartedRegistry,
            restartedPersistence);

        var restoredSnapshot = restartedTopology.GetSnapshot();

        var restoredSensor = Assert.Single(
            restoredSnapshot.Nodes,
            node => node.Id == "PERSIST-TEST-A");

        Assert.True(restoredSensor.IsOnline);
        Assert.True(restoredSensor.IsReachable);

        var restoredRoute = restartedTopology.GetSensorRoute(
            "PERSIST-TEST-A");

        Assert.True(restoredRoute.IsReachable);
        Assert.Equal(2d, restoredRoute.TotalCost);
    }

    private static SensorProfile RegisterSensor(
        SensorRegistry registry,
        string identifier,
        bool persistent = true)
    {
        var request = new RegisterSensorRequest
        {
            DeviceIdentifier = identifier,
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Environmental,
            DataType = TelemetryDataType.Float
        };

        return Assert.IsType<SensorProfile>(
            registry.Register(request, persistent));
    }

    private sealed class TemporaryNetworkStorage : IDisposable
    {
        private readonly string directory = Path.Combine(
            Path.GetTempPath(),
            "SmartX-Persistence-Tests",
            Guid.NewGuid().ToString("N"));

        public NetworkPersistenceStore CreateStore()
        {
            return new NetworkPersistenceStore(
                Path.Combine(directory, "network.json"));
        }

        public void Dispose()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
