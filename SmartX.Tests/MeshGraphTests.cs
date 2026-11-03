
using SmartX.Api.Collections;
using Xunit;

namespace SmartX.Tests;

public class MeshGraphTests
{
    [Fact]
    public void EmptyGraphHasNoNodesOrConnections()
    {
        var graph = new MeshGraph();

        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.ConnectionCount);
        Assert.Empty(graph.GetNodes());
        Assert.Empty(graph.GetConnections());
    }

    [Fact]
    public void AddingNodesAndConnectionsCreatesMesh()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("Sensor-A");
        graph.AddNode("Sensor-B");

        graph.AddConnection("Gateway", "Sensor-A", 2);
        graph.AddConnection("Sensor-A", "Sensor-B", 4);

        Assert.Equal(3, graph.NodeCount);
        Assert.Equal(2, graph.ConnectionCount);
        Assert.Equal(2, graph.GetConnections().Length);
    }

    [Fact]
    public void NodeIdentifiersAreCaseInsensitive()
    {
        var graph = new MeshGraph();

        Assert.True(graph.AddNode("Gateway"));
        Assert.False(graph.AddNode("gateway"));

        Assert.Equal(1, graph.NodeCount);
        Assert.True(graph.ContainsNode("GATEWAY"));
    }

    [Fact]
    public void BreadthFirstSearchVisitsConnectedNodes()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");
        graph.AddNode("D");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("Gateway", "C", 2);
        graph.AddConnection("A", "B", 3);

        var result = graph.BreadthFirstSearch("Gateway");

        Assert.Equal(
            new[] { "Gateway", "A", "C", "B" },
            result);

        Assert.DoesNotContain("D", result);
    }

    [Fact]
    public void UnreachableNodesAreDetected()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");
        graph.AddNode("C");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("B", "C", 2);

        var unreachable = graph.GetUnreachableNodes("Gateway");

        Assert.Equal(new[] { "B", "C" }, unreachable);
    }

    [Fact]
    public void RemovingNodeRemovesItsConnections()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");
        graph.AddNode("B");

        graph.AddConnection("Gateway", "A", 1);
        graph.AddConnection("A", "B", 2);
        graph.AddConnection("Gateway", "B", 3);

        Assert.True(graph.RemoveNode("A"));

        Assert.Equal(2, graph.NodeCount);
        Assert.Equal(1, graph.ConnectionCount);
        Assert.False(graph.ContainsNode("A"));
        Assert.False(graph.RemoveNode("A"));
    }

    [Fact]
    public void UpdatingConnectionDoesNotDuplicateEdge()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");

        graph.AddConnection("Gateway", "A", 2);
        graph.AddConnection("Gateway", "A", 5);

        Assert.Equal(1, graph.ConnectionCount);
        Assert.Equal(5, graph.GetConnections()[0].Cost);
    }

    [Fact]
    public void InvalidConnectionCostsAreRejected()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");
        graph.AddNode("A");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => graph.AddConnection("Gateway", "A", 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => graph.AddConnection("Gateway", "A", -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => graph.AddConnection("Gateway", "A", double.NaN));

        Assert.Throws<KeyNotFoundException>(
            () => graph.AddConnection("Gateway", "Unknown", 1));
    }

    [Fact]
    public void SearchingFromUnknownNodeThrowsException()
    {
        var graph = new MeshGraph();

        graph.AddNode("Gateway");

        Assert.Throws<KeyNotFoundException>(
            () => graph.BreadthFirstSearch("Unknown"));
    }
}
