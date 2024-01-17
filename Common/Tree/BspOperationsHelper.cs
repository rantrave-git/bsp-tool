using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public class BspOperationsHelper<TEdgeHull, TContent>
    where TEdgeHull : class, IHull<TEdgeHull>
{
    public static int Numerate(BspNode<TEdgeHull, TContent> root, int start = 0)
    {
        var i = start;
        var q = new Queue<BspNode<TEdgeHull, TContent>>();
        q.Enqueue(root);
        while (q.TryDequeue(out var n))
        {
            n.numeration = i++;
            if (n.front != null) q.Enqueue(n.front);
            if (n.back != null) q.Enqueue(n.back);
        }
        return i;
    }
    // node to set of its parents
    public static Dictionary<long, Dictionary<long, Side>> Order(BspNode<TEdgeHull, TContent> root)
    {
        var result = new Dictionary<long, Dictionary<long, Side>>();
        var q = new Stack<(BspNode<TEdgeHull, TContent> Node, long Parent, Side Side)>();
        q.Push((root, -1, Side.Back));
        while (q.TryPop(out var n))
        {
            Dictionary<long, Side> set;
            if (!result.TryGetValue(n.Node.numeration, out set!))
            {
                set = result[n.Node.numeration] = new Dictionary<long, Side>();
            }
            if (result.TryGetValue(n.Parent, out var s))
            {
                foreach (var i in s)
                {
                    set[i.Key] = i.Value;
                }
            }
            if (n.Parent >= 0) set[n.Parent] = n.Side;
            if (n.Node.edge == null) continue;
            else
            {
                if (n.Node.back != null) q.Push((n.Node.back, n.Node.numeration, Side.Back));
                if (n.Node.front != null) q.Push((n.Node.front, n.Node.numeration, Side.Front));
            }
        }
        return result;
    }

    public static bool TryAdd(ref BspNode<TEdgeHull, TContent>? root, BspNode<TEdgeHull, TContent> node, Dictionary<long, Dictionary<long, Side>> order)
    {
        if (root == null)
        {
            root = node;
            return true;
        }
        var current = root;
        if (!order.TryGetValue(node.numeration, out var parents)) return false;
        while (parents.TryGetValue(current.numeration, out var side))
        {
            var next = current.GetChild(side);
            if (next == null)
            {
                current.SetChild(side, node);
                return true;
            }
            current = next;
        }
        return false;
    }

    public static BspNode<TEdgeHull, TContent> MakeLeaf(TContent flags) => new BspNode<TEdgeHull, TContent>(flags);
    public static BspNode<TEdgeHull, TContent> MakeNode(IBspTree<TEdgeHull, TContent> plane) => new BspNode<TEdgeHull, TContent>(plane);
    public static BspNode<TEdgeHull, TContent> MakeNode(IBspTree<TEdgeHull, TContent> plane, BspNode<TEdgeHull, TContent> back, BspNode<TEdgeHull, TContent> front)
        => new BspNode<TEdgeHull, TContent>(plane)
        {
            back = back,
            front = front,
        };
}
