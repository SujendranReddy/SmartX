
using SmartX.Api.Collections;
using Xunit;

namespace SmartX.Tests;

public class MinimumSpanningTreeTests
{
    [Fact]
    public void MinimumSpanningTreeSelectsLowestCostConnections()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");

        graph.AddConnection("Gateway", "A", 4);
        graph.AddConnection("Gateway", "B", 1);
        graph.AddConnection("A", "B", 2);
        graph.AddConnection("A", "C", 3);
        graph.AddConnection("B", "C", 10);

        var result = graph.GetMinimumSpanningTree("Gateway");

        Assert.Equal(3, result.Connections.Length);
        Assert.Equal(6d, result.TotalCost);
        Assert.Equal(4, result.ReachableNodes.Length);
        Assert.Empty(result.UnreachableNodes);

        Assert.Equal(
            new[] { 1d, 2d, 3d },
            result.Connections
                .Select(connection => connection.Cost)
                .OrderBy(cost => cost)
                .ToArray());
    }

    [Fact]
    public void DisconnectedSensorsAreIdentified()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");

        graph.AddConnection("Gateway", "A", 4);
        graph.AddConnection("B", "C", 1);

        var result = graph.GetMinimumSpanningTree("Gateway");

        Assert.Single(result.Connections);
        Assert.Equal(4d, result.TotalCost);

        Assert.Equal(
            new[] { "A", "Gateway" },
            result.ReachableNodes);

        Assert.Equal(
            new[] { "B", "C" },
            result.UnreachableNodes);
    }

    [Fact]
    public void SingleGatewayProducesEmptySpanningTree()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");

        var result = graph.GetMinimumSpanningTree("Gateway");

        Assert.Empty(result.Connections);
        Assert.Equal(0d, result.TotalCost);
        Assert.Single(result.ReachableNodes);
        Assert.Empty(result.UnreachableNodes);
    }

    [Fact]
    public void MissingGatewayThrowsException()
    {
        var graph = new MeshGraph();

        graph.AddNode("A");

        Assert.Throws<KeyNotFoundException>(
            () => graph.GetMinimumSpanningTree("Gateway"));
    }

    [Fact]
    public void RemovingNodeRecalculatesAvailableConnections()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 1);
        graph.AddConnection("Gateway", "B", 5);

        var original = graph.GetMinimumSpanningTree("Gateway");

        Assert.Equal(2d, original.TotalCost);
        Assert.Equal(2, original.Connections.Length);

        graph.RemoveNode("A");

        var updated = graph.GetMinimumSpanningTree("Gateway");

        Assert.Equal(5d, updated.TotalCost);
        Assert.Single(updated.Connections);
        Assert.Equal(2, updated.ReachableNodes.Length);
        Assert.Empty(updated.UnreachableNodes);
    }

    [Fact]
    public void CyclicNetworkProducesMinimumSpanningTree()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 2);
        graph.AddConnection("B", "C", 3);
        graph.AddConnection("C", "Gateway", 4);
        graph.AddConnection("A", "C", 5);

        var result = graph.GetMinimumSpanningTree("Gateway");

        Assert.Equal(3, result.Connections.Length);
        Assert.Equal(6d, result.TotalCost);
        Assert.Empty(result.UnreachableNodes);
    }

    [Fact]
    public void GatewayLookupIsCaseInsensitive()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("Sensor-A");

        graph.AddConnection("Gateway", "Sensor-A", 3);

        var result = graph.GetMinimumSpanningTree("gateway");

        Assert.Equal(3d, result.TotalCost);
        Assert.Equal(2, result.ReachableNodes.Length);
        Assert.Single(result.Connections);
    }
}
