using System.Numerics;
using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public interface ICopyable<T>
{
    public T Copy();
}

public class BspTree0D : ICopyable<BspTree0D>
{
    public Vector2 hull;

    public BspTree0D Copy() => new BspTree0D() { hull = hull };

    public override string ToString() => hull.ToString();
}

public static class NodePrinter
{
    public static string Print<E, C>(BspNode<E, C> root) where E : ICopyable<E>
    {
        var s = new Stack<(BspNode<E, C> Node, int Depth)>();
        var res = new StringBuilder();
        var nodes = new List<(long, BspNode<E, C>)>();
        s.Push((root, 1));
        while (s.TryPop(out var n))
        {
            res.Append($"{String.Join("  ", Enumerable.Repeat("", n.Depth))}- {n.Node.ToString().Split('\n', 2)[0]}\n");
            nodes.Add((n.Node.numeration, n.Node));
            if (n.Node.edge != null)
            {
                s.Push((n.Node.front!, n.Depth + 1));
                s.Push((n.Node.back!, n.Depth + 1));
            }
        }
        // foreach (var i in nodes.OrderBy(x => x.Item1))
        // {
        //     res.Append($"- {i.Item2.ToString().ReplaceLineEndings("\n  ").TrimEnd()}\n");
        // }
        return res.ToString();
    }
}

public class BspTree<THull, TPlane, TEdgeTree, TPoint, TContent> : IBspTree<BspTree<THull, TPlane, TEdgeTree, TPoint, TContent>, TContent>, ICopyable<BspTree<THull, TPlane, TEdgeTree, TPoint, TContent>>
    where TEdgeTree : ICopyable<TEdgeTree>
    where TPoint : struct
    where THull : IHull<THull, TPlane, TPoint, BspNode<TEdgeTree, TContent>, TContent>
{
    public THull Hull;
    public BspNode<TEdgeTree, TContent> Root;
    public BspTree(BspNode<TEdgeTree, TContent> root, THull hull)
    {
        Root = root;
        Hull = hull;
    }

    public BspTree<THull, TPlane, TEdgeTree, TPoint, TContent> Separate(THull newHull, THull targetHull)
    {
        Hull = newHull;
        return new BspTree<THull, TPlane, TEdgeTree, TPoint, TContent>(Root, targetHull);
    }

    public void Numerate() => BspOperationsHelper<TEdgeTree, TContent>.Numerate(Root);
    public BspTree<THull, TPlane, TEdgeTree, TPoint, TContent> Copy() => new BspTree<THull, TPlane, TEdgeTree, TPoint, TContent>(Root.DeepCopy(), Hull.Copy());

    public BspTree<THull, TPlane, TEdgeTree, TPoint, TContent> Csg(
        BspTree<THull, TPlane, TEdgeTree, TPoint, TContent> other,
        ISpaceContentOperation<TContent> operation,
        bool inplace = false)
    {
        var splitterQueue = new Queue<(BspNode<TEdgeTree, TContent> Splitter, BspNode<TEdgeTree, TContent> Subtree, BspNode<TEdgeTree, TContent> Target)>();
        var copy = other.Root.DeepCopy();
        BspOperationsHelper<TEdgeTree, TContent>.Numerate(copy);
        var order = BspOperationsHelper<TEdgeTree, TContent>.Order(copy);
        var root = Root;
        if (!inplace)
        {
            root = root.Copy();
        }
        splitterQueue.Enqueue((Root, copy, root));
        while (splitterQueue.TryDequeue(out var current))
        {
            // current.Splitter vs current.Subtree
            if (current.Splitter.edge == null)
            {
                if (current.Subtree.edge == null)
                {
                    current.Target.SetLeaf(operation.Apply(current.Splitter.flags, current.Subtree.flags));
                    continue;
                }
                current.Subtree.ApplyLeft(operation, current.Splitter.flags);
                current.Target.SetNode(current.Subtree.back!, current.Subtree.front!, current.Subtree.edge);
                continue;
            }
            if (current.Subtree.edge == null)
            {
                // apply operation on leafs
                var rest = inplace ? current.Splitter : current.Splitter.DeepCopy();
                rest.ApplyRight(operation, current.Subtree.flags);
                current.Target.SetChild(Side.Incident, rest);
                continue;
            }
            var subtreeStack = new Stack<(BspNode<TEdgeTree, TContent> Subtree, Side Side)>();
            BspNode<TEdgeTree, TContent>? back = null;
            BspNode<TEdgeTree, TContent>? front = null;
            subtreeStack.Push((current.Subtree, 0));

            while (subtreeStack.TryPop(out var el))
            {
                if (el.Subtree.edge == null)
                {
                    if (el.Side < 0)
                    {
                        BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref back, el.Subtree, order);
                        continue;
                    }
                    if (el.Side > 0)
                    {
                        BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref front, el.Subtree, order);
                        continue;
                    }
                    if (BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref back, el.Subtree, order))
                    {
                        BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref front, el.Subtree.Copy(), order);
                    }
                    else
                    {
                        BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref front, el.Subtree, order);
                    }
                    continue;
                }
                var (b, f) = Hull.SplitNode(current.Splitter, el.Subtree, operation.EdgeOperation, out var flip);
                var relfront = flip ? el.Subtree.back! : el.Subtree.front!; // el.Subtree.GetChild(flip ? -1 : 1)!;
                var relback = !flip ? el.Subtree.back! : el.Subtree.front!; // el.Subtree.GetChild(flip ? 1 : -1)!;
                if (f == b)
                {
                    // incident
                    if (el.Side != 0) throw new AssertionException("Can't be incident");
                    subtreeStack.Push((relfront, Side.Front));
                    subtreeStack.Push((relback, Side.Back));
                    continue;
                }
                switch (el.Side)
                {
                    case Side.Front:
                        subtreeStack.Push((relback, Side.Front));
                        subtreeStack.Push((relfront, Side.Front));
                        break;
                    case Side.Back:
                        subtreeStack.Push((relback, Side.Back));
                        subtreeStack.Push((relfront, Side.Back));
                        break;
                    case Side.Incident:
                        subtreeStack.Push((relback, f == null ? Side.Back : Side.Incident));
                        subtreeStack.Push((relfront, b == null ? Side.Front : Side.Incident));
                        break;
                }

                if (f != null)
                {
                    if (el.Side < 0) throw new AssertionException("Wrong side determined");
                    f.Detach();
                    BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref front, f, order);
                }
                if (b != null)
                {
                    if (el.Side > 0) throw new AssertionException("Wrong side determined");
                    b.Detach();
                    BspOperationsHelper<TEdgeTree, TContent>.TryAdd(ref back, b, order);
                }
            }

            var bc = inplace ? current.Splitter.back! : current.Splitter.back!.Copy();
            var fc = inplace ? current.Splitter.front! : current.Splitter.front!.Copy();
            // var fstr = front?.Print(64);
            // var bstr = back?.Print(64);

            current.Target.SetChildren(bc, fc);

            if (back != null) splitterQueue.Enqueue((current.Splitter.back!, back, bc));
            if (front != null) splitterQueue.Enqueue((current.Splitter.front!, front, fc));
        }
        return new BspTree<THull, TPlane, TEdgeTree, TPoint, TContent>(root, Hull.Copy());
    }

    public BspNode<TEdgeTree, TContent> Search(TPoint point) => Hull.GetSide(Root, point);

    public override string ToString()
    {
        var s = new Stack<(BspNode<TEdgeTree, TContent> Node, int Depth)>();
        var res = new StringBuilder();
        var nodes = new List<(long, BspNode<TEdgeTree, TContent>)>();
        s.Push((Root, 1));
        res.Append($"{Hull}\n");
        while (s.TryPop(out var n))
        {
            res.Append($"{String.Join("  ", Enumerable.Repeat("", n.Depth))}- {n.Node.ToString().Split('\n', 2)[0]}\n");
            nodes.Add((n.Node.numeration, n.Node));
            if (n.Node.edge != null)
            {
                s.Push((n.Node.front!, n.Depth + 1));
                s.Push((n.Node.back!, n.Depth + 1));
            }
        }
        foreach (var i in nodes.OrderBy(x => x.Item1))
        {
            res.Append($"- {i.Item2.ToString().ReplaceLineEndings("\n  ").TrimEnd()}\n");
        }
        return res.ToString();
    }
}
