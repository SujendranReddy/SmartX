
using SmartX.Api.Collections;
using Xunit;

namespace SmartX.Tests;

public class AvlTreeTests
{
    [Fact]
    public void EmptyTreeReturnsNoValues()
    {
        var tree = new AvlTree<int, string>();

        Assert.Equal(0, tree.Count);
        Assert.Empty(tree.GetAll());
        Assert.Empty(tree.GetRange(1, 10));
    }

    [Fact]
    public void InsertedValuesAreReturnedInSortedOrder()
    {
        var tree = new AvlTree<int, string>();

        tree.Add(3, "Three");
        tree.Add(1, "One");
        tree.Add(2, "Two");

        Assert.Equal(
            new[] { "One", "Two", "Three" },
            tree.GetAll());

        Assert.Equal(3, tree.Count);
    }

    [Fact]
    public void DuplicateKeysPreserveAllValues()
    {
        var tree = new AvlTree<int, string>();

        tree.Add(1, "First");
        tree.Add(1, "Second");

        Assert.Equal(2, tree.Count);
        Assert.Equal(
            new[] { "First", "Second" },
            tree.GetAll());
    }

    [Fact]
    public void RangeQueryReturnsOnlyMatchingValues()
    {
        var tree = new AvlTree<int, int>();

        for (var i = 1; i <= 10; i++)
        {
            tree.Add(i, i);
        }

        var results = tree.GetRange(3, 6);

        Assert.Equal(new[] { 3, 4, 5, 6 }, results);
    }

    [Fact]
    public void TreeRemainsBalancedAfterSequentialInsertions()
    {
        var tree = new AvlTree<int, int>();

        for (var i = 1; i <= 1000; i++)
        {
            tree.Add(i, i);
        }

        Assert.Equal(1000, tree.Count);
        Assert.True(tree.Height <= 16);
    }

    [Fact]
    public void InvalidRangeThrowsException()
    {
        var tree = new AvlTree<int, int>();

        Assert.Throws<ArgumentException>(
            () => tree.GetRange(10, 1));
    }
}
