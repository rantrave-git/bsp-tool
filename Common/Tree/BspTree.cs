using System.Numerics;
using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;


// public class BspTree0D : ICopyable<BspTree0D>, IHulled<Vector2>
// {
//     public Vector2 Hull { get; set; }

//     public BspTree0D Copy() => new BspTree0D() { Hull = Hull };

//     public override string ToString() => Hull.ToString();
// }

public static class NodePrinter
{
    public static string Print<H, C>(BspNode<H, C> root) where H : class, IHull<H>
    {
        var s = new Stack<(BspNode<H, C> Node, int Depth)>();
        var res = new StringBuilder();
        var nodes = new List<(long, BspNode<H, C>)>();
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

public class BspTree0D<TContent> : IBspTree<Hull0D, TContent>
{
    public Hull0D Hull { get; private set; } = default!;

    private BspTree0D() { }
    public BspTree0D(Vector2 plane) => Hull = new Hull0D(plane);
    public IBspTree<Hull0D, TContent> Copy() => new BspTree0D<TContent>() { Hull = Hull };
    public IBspTree<Hull0D, TContent> Csg(IBspTree<Hull0D, TContent> other, ISpaceContentOperation<TContent> operation, bool inplace = false) => throw new NotImplementedException();
    public IBspTree<Hull0D, TContent> Separate(Hull0D newHull, Hull0D targetHull) => throw new NotImplementedException();
}

public class BspTree<THull, TEdgeHull, TContent> : IBspTree<THull, TContent>
    // where TEdgeTree : ICopyable<TEdgeTree>
    // where TPoint : struct
    where THull : class, IHull<THull>
    where TEdgeHull : class, IHull<TEdgeHull>
{
    public THull Hull { get; private set; }
    public BspNode<TEdgeHull, TContent> Root;
    public BspTree(BspNode<TEdgeHull, TContent> root, THull hull)
    {
        Root = root;
        Hull = hull;
    }

    public IBspTree<THull, TContent> Separate(THull newHull, THull targetHull)
    {
        Hull = newHull;
        return new BspTree<THull, TEdgeHull, TContent>(Root, targetHull);
    }

    public void Numerate() => BspOperationsHelper<TEdgeHull, TContent>.Numerate(Root);
    public IBspTree<THull, TContent> Copy() => new BspTree<THull, TEdgeHull, TContent>(Root.DeepCopy(), Hull.Copy());

    public IBspTree<THull, TContent> Csg(
        IBspTree<THull, TContent> otherTree,
        ISpaceContentOperation<TContent> operation,
        bool inplace = false)
    {
        var other = otherTree as BspTree<THull, TEdgeHull, TContent>;
        if (other == null) return this;
        var splitterQueue = new Queue<(BspNode<TEdgeHull, TContent> Splitter, BspNode<TEdgeHull, TContent> Subtree, BspNode<TEdgeHull, TContent> Target)>();
        var copy = other.Root.DeepCopy();
        BspOperationsHelper<TEdgeHull, TContent>.Numerate(copy);
        var order = BspOperationsHelper<TEdgeHull, TContent>.Order(copy);
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
            var subtreeStack = new Stack<(BspNode<TEdgeHull, TContent> Subtree, Side Side)>();
            BspNode<TEdgeHull, TContent>? back = null;
            BspNode<TEdgeHull, TContent>? front = null;
            subtreeStack.Push((current.Subtree, 0));

            while (subtreeStack.TryPop(out var el))
            {
                if (el.Subtree.edge == null)
                {
                    if (el.Side < 0)
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref back, el.Subtree, order);
                        continue;
                    }
                    if (el.Side > 0)
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, el.Subtree, order);
                        continue;
                    }
                    if (BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref back, el.Subtree, order))
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, el.Subtree.Copy(), order);
                    }
                    else
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, el.Subtree, order);
                    }
                    continue;
                }
                var (b, f) = el.Subtree.Split(current.Splitter, operation.EdgeOperation, out var flip);
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
                    BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, f, order);
                }
                if (b != null)
                {
                    if (el.Side > 0) throw new AssertionException("Wrong side determined");
                    b.Detach();
                    BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref back, b, order);
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
        return new BspTree<THull, TEdgeHull, TContent>(root, Hull.Copy());
    }

    public BspNode<TEdgeHull, TContent> Search(Vector<float> point) => Root.Search(point);

    public override string ToString()
    {
        var s = new Stack<(BspNode<TEdgeHull, TContent> Node, int Depth)>();
        var res = new StringBuilder();
        var nodes = new List<(long, BspNode<TEdgeHull, TContent>)>();
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
