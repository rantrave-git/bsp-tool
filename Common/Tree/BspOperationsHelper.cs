using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public class BspOperationsHelper<THull, TContent>
    where THull : class, IHull<THull>
{
    public static long Numerate(BspNode<THull, TContent> root)
    {
        var i = 0L;
        var q = new Queue<BspNode<THull, TContent>>();
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
    public static Dictionary<long, Dictionary<long, Side>> Order(BspNode<THull, TContent> root)
    {
        var result = new Dictionary<long, Dictionary<long, Side>>();
        var q = new Stack<(BspNode<THull, TContent> Node, long Parent, Side Side)>();
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

    public static bool TryAdd(ref BspNode<THull, TContent>? root, BspNode<THull, TContent> node, Dictionary<long, Dictionary<long, Side>> order)
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

    public static BspNode<THull, TContent> MakeLeaf(TContent flags) => new BspNode<THull, TContent>(flags);
    public static BspNode<THull, TContent> MakeNode(IBspTree<THull, TContent> plane) => new BspNode<THull, TContent>(plane);
    public static BspNode<THull, TContent> MakeNode(IBspTree<THull, TContent> plane, BspNode<THull, TContent> back, BspNode<THull, TContent> front) => new BspNode<THull, TContent>(plane)
    {
        back = back,
        front = front,
    };
}
