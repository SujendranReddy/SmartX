
namespace SmartX.Api.Collections;

public sealed class AvlTree<TKey, TValue> where TKey : notnull
{
    private readonly IComparer<TKey> comparer = Comparer<TKey>.Default;
    private Node? root;

    public int Count { get; private set; }

    public int Height => GetHeight(root);

    public void Add(TKey key, TValue value)
    {
        root = Insert(root, key, value);
        Count++;
    }

    public TValue[] GetAll()
    {
        var results = new List<TValue>(Count);
        CollectAll(root, results);
        return results.ToArray();
    }

    public TValue[] GetRange(TKey from, TKey to)
    {
        if (comparer.Compare(from, to) > 0)
        {
            throw new ArgumentException("The start must not be after the end.", nameof(from));
        }

        var results = new List<TValue>();
        CollectRange(root, from, to, results);
        return results.ToArray();
    }

    private Node Insert(Node? node, TKey key, TValue value)
    {
        if (node is null)
        {
            return new Node(key, value);
        }

        var comparison = comparer.Compare(key, node.Key);

        if (comparison < 0)
        {
            node.Left = Insert(node.Left, key, value);
        }
        else if (comparison > 0)
        {
            node.Right = Insert(node.Right, key, value);
        }
        else
        {
            node.Values.Add(value);
            return node;
        }

        UpdateHeight(node);
        var balance = GetBalance(node);

        if (balance > 1)
        {
            if (GetBalance(node.Left!) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }

            return RotateRight(node);
        }

        if (balance < -1)
        {
            if (GetBalance(node.Right!) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateLeft(Node node)
    {
        var pivot = node.Right!;
        node.Right = pivot.Left;
        pivot.Left = node;
        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }

    private static Node RotateRight(Node node)
    {
        var pivot = node.Left!;
        node.Left = pivot.Right;
        pivot.Right = node;
        UpdateHeight(node);
        UpdateHeight(pivot);
        return pivot;
    }

    private static int GetHeight(Node? node) => node?.Height ?? 0;

    private static int GetBalance(Node node) =>
        GetHeight(node.Left) - GetHeight(node.Right);

    private static void UpdateHeight(Node node)
    {
        node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));
    }

    private static void CollectAll(Node? node, List<TValue> results)
    {
        if (node is null)
        {
            return;
        }

        CollectAll(node.Left, results);
        results.AddRange(node.Values);
        CollectAll(node.Right, results);
    }

    private void CollectRange(Node? node, TKey from, TKey to, List<TValue> results)
    {
        if (node is null)
        {
            return;
        }

        var fromComparison = comparer.Compare(from, node.Key);
        var toComparison = comparer.Compare(to, node.Key);

        if (fromComparison < 0)
        {
            CollectRange(node.Left, from, to, results);
        }

        if (fromComparison <= 0 && toComparison >= 0)
        {
            results.AddRange(node.Values);
        }

        if (toComparison > 0)
        {
            CollectRange(node.Right, from, to, results);
        }
    }

    private sealed class Node
    {
        public Node(TKey key, TValue value)
        {
            Key = key;
            Values = [value];
        }

        public TKey Key { get; }
        public List<TValue> Values { get; }
        public Node? Left { get; set; }
        public Node? Right { get; set; }
        public int Height { get; set; } = 1;
    }
}
