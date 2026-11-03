
using SmartX.Api.Services;
using SmartX.Shared.Models;
using SmartX.Shared.Requests;
using Xunit;

namespace SmartX.Tests;

public class MeshFailureRecoveryTests
{
    [Fact]
    public void FailedSensorTriggersAlternativeRouting()
    {
        var topology = CreateTopology();

        topology.AddConnection("SMARTX-GATEWAY", "SENSOR-A", 2);
        topology.AddConnection("SENSOR-A", "SENSOR-B", 3);
        topology.AddConnection("SMARTX-GATEWAY", "SENSOR-B", 8);

        var original = topology.GetSensorRoute("SENSOR-B");

        Assert.True(original.IsReachable);
        Assert.Equal(5d, original.TotalCost);
        Assert.Equal(
            new[] { "SENSOR-B", "SENSOR-A", "SMARTX-GATEWAY" },
            original.Path);

        var snapshot = topology.SetSensorAvailability("SENSOR-A", false);

        var failedSensor = Assert.Single(
            snapshot.Nodes.Where(node => node.Id == "SENSOR-A"));

        Assert.False(failedSensor.IsOnline);
        Assert.False(failedSensor.IsReachable);

        var alternative = topology.GetSensorRoute("SENSOR-B");

        Assert.True(alternative.IsReachable);
        Assert.Equal(8d, alternative.TotalCost);
        Assert.Equal(
            new[] { "SENSOR-B", "SMARTX-GATEWAY" },
            alternative.Path);

        Assert.Equal(8d, snapshot.SpanningTree.TotalCost);
    }

    [Fact]
    public void RestoredSensorRecoversOriginalConnections()
    {
        var topology = CreateTopology();

        topology.AddConnection("SMARTX-GATEWAY", "SENSOR-A", 2);
        topology.AddConnection("SENSOR-A", "SENSOR-B", 3);
        topology.AddConnection("SMARTX-GATEWAY", "SENSOR-B", 8);

        topology.SetSensorAvailability("SENSOR-A", false);

        var restored = topology.SetSensorAvailability("SENSOR-A", true);

        var sensor = Assert.Single(
            restored.Nodes.Where(node => node.Id == "SENSOR-A"));

        Assert.True(sensor.IsOnline);
        Assert.True(sensor.IsReachable);

        Assert.Equal(3, restored.Connections.Length);
        Assert.Equal(5d, restored.SpanningTree.TotalCost);

        var route = topology.GetSensorRoute("SENSOR-B");

        Assert.True(route.IsReachable);
        Assert.Equal(5d, route.TotalCost);
        Assert.Equal(
            new[] { "SENSOR-B", "SENSOR-A", "SMARTX-GATEWAY" },
            route.Path);
    }

    [Fact]
    public void SensorBecomesDisconnectedWithoutAlternativeRoute()
    {
        var topology = CreateTopology();

        topology.AddConnection("SMARTX-GATEWAY", "SENSOR-A", 2);
        topology.AddConnection("SENSOR-A", "SENSOR-B", 3);

        var original = topology.GetSensorRoute("SENSOR-B");

        Assert.True(original.IsReachable);
        Assert.Equal(5d, original.TotalCost);

        var failed = topology.SetSensorAvailability("SENSOR-A", false);

        var sensorB = Assert.Single(
            failed.Nodes.Where(node => node.Id == "SENSOR-B"));

        Assert.True(sensorB.IsOnline);
        Assert.False(sensorB.IsReachable);

        var disconnected = topology.GetSensorRoute("SENSOR-B");

        Assert.False(disconnected.IsReachable);
        Assert.Empty(disconnected.Path);
        Assert.Null(disconnected.TotalCost);

        topology.SetSensorAvailability("SENSOR-A", true);

        var recovered = topology.GetSensorRoute("SENSOR-B");

        Assert.True(recovered.IsReachable);
        Assert.Equal(5d, recovered.TotalCost);
    }

    [Fact]
    public void GatewayAndUnknownSensorsCannotBeTakenOffline()
    {
        var topology = CreateTopology();

        Assert.Throws<KeyNotFoundException>(() =>
            topology.SetSensorAvailability("SMARTX-GATEWAY", false));

        Assert.Throws<KeyNotFoundException>(() =>
            topology.SetSensorAvailability("UNKNOWN-SENSOR", false));

        var snapshot = topology.GetSnapshot();

        Assert.Equal(3, snapshot.Nodes.Length);
        Assert.All(snapshot.Nodes, node => Assert.True(node.IsOnline));
    }

    private static MeshTopologyService CreateTopology()
    {
        var registry = new SensorRegistry();

        RegisterSensor(registry, "SENSOR-A");
        RegisterSensor(registry, "SENSOR-B");

        return new MeshTopologyService(registry);
    }

    private static void RegisterSensor(
        SensorRegistry registry,
        string identifier)
    {
        var sensor = registry.Register(new RegisterSensorRequest
        {
            DeviceIdentifier = identifier,
            DeploymentLocation = "Greenhouse A",
            Category = SensorCategory.Environmental,
            DataType = TelemetryDataType.Float
        });

        Assert.NotNull(sensor);
    }
}
