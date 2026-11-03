
using SmartX.Api.Collections;
using Xunit;

namespace SmartX.Tests;

public class MeshRoutingTests
{
    [Fact]
    public void SensorFindsMultiHopRouteToGateway()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 2);
        graph.AddConnection("B", "C", 3);
        graph.AddConnection("Gateway", "C", 10);

        var result = MeshRouting.GetRoute(graph, "Gateway", "C");

        Assert.True(result.IsReachable);
        Assert.Equal(6d, result.TotalCost);
        Assert.Equal(
            new[] { "C", "B", "A", "Gateway" },
            result.Path);
    }

    [Fact]
    public void DisconnectedSensorHasNoRoute()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");

        graph.AddConnection("Gateway", "A", 2);

        var result = MeshRouting.GetRoute(graph, "Gateway", "B");

        Assert.False(result.IsReachable);
        Assert.Empty(result.Path);
        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void GatewayCanRouteToItself()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");

        var result = MeshRouting.GetRoute(
            graph,
            "Gateway",
            "Gateway");

        Assert.True(result.IsReachable);
        Assert.Equal(0d, result.TotalCost);
        Assert.Equal(new[] { "Gateway" }, result.Path);
    }

    [Fact]
    public void MissingGatewayThrowsException()
    {
        var graph = new MeshGraph();

        graph.AddNode("A");

        Assert.Throws<KeyNotFoundException>(
            () => MeshRouting.GetRoute(
                graph,
                "Gateway",
                "A"));
    }

    [Fact]
    public void MissingSensorThrowsException()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");

        Assert.Throws<KeyNotFoundException>(
            () => MeshRouting.GetRoute(
                graph,
                "Gateway",
                "Unknown"));
    }

    [Fact]
    public void SensorUsesAlternativeRouteAfterNodeFailure()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 1);
        graph.AddConnection("Gateway", "B", 5);

        var original = MeshRouting.GetRoute(
            graph,
            "Gateway",
            "B");

        Assert.True(original.IsReachable);
        Assert.Equal(2d, original.TotalCost);
        Assert.Equal(
            new[] { "B", "A", "Gateway" },
            original.Path);

        graph.RemoveNode("A");

        var updated = MeshRouting.GetRoute(
            graph,
            "Gateway",
            "B");

        Assert.True(updated.IsReachable);
        Assert.Equal(5d, updated.TotalCost);
        Assert.Equal(
            new[] { "B", "Gateway" },
            updated.Path);
    }

    [Fact]
    public void SensorBecomesDisconnectedWhenNoAlternativeExists()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 2);

        var original = MeshRouting.GetRoute(
            graph,
            "Gateway",
            "B");

        Assert.True(original.IsReachable);

        graph.RemoveNode("A");

        var updated = MeshRouting.GetRoute(
            graph,
            "Gateway",
            "B");

        Assert.False(updated.IsReachable);
        Assert.Empty(updated.Path);
        Assert.Null(updated.TotalCost);
    }

    [Fact]
    public void RoutingUsesCaseInsensitiveNodeIdentifiers()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("Sensor-A");

        graph.AddConnection("Gateway", "Sensor-A", 3);

        var result = MeshRouting.GetRoute(
            graph,
            "gateway",
            "SENSOR-A");

        Assert.True(result.IsReachable);
        Assert.Equal(3d, result.TotalCost);
        Assert.Equal(
            new[] { "Sensor-A", "Gateway" },
            result.Path);
    }
}
