using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;


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

    public IEnumerable<TContent> Leafs(Hull0D hull) { yield break; }
    public override string ToString() => Hull?.ToString() ?? "";

    public IBspTree<Hull0D, TContent> CloneProjected(Hull0D hull) => new BspTree0D<TContent>() { Hull = hull };
}

public class BspTree<THull, TEdgeHull, TContent> : IBspTree<THull, TContent>
    where THull : class, IHull<THull>, IContentHull<THull, TEdgeHull>
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
    private static BspNode<TEdgeHull, TContent> SplitLeaf(BspNode<TEdgeHull, TContent> splitter, BspNode<TEdgeHull, TContent> leaf)
    {
        var res = splitter;
        res.back = leaf;
        res.front = leaf.Copy();
        return res;
    }
    public IEnumerable<TContent> Leafs(THull hull)
    {
        // var bnds = hull.Boundaries();
        var q = new Queue<(BspNode<TEdgeHull, TContent> Node, THull Hull)>();
        if (hull.Empty) yield break;
        q.Enqueue((Root, hull));
        while (q.TryDequeue(out var current))
        {
            if (current.Node.edge == null)
            {
                yield return current.Node.flags;
                continue;
            }
            var (b, f) = current.Hull.Split(current.Node.edge.Hull);
            if (b != null && !b.Empty)
            {
                q.Enqueue((current.Node.back!, b));
            }
            if (f != null && !f.Empty)
            {
                q.Enqueue((current.Node.front!, f));
            }
        }
    }

    public void SplitLeafs() {
        
    }

    private void AddToSubtree(BspNode<TEdgeHull, TContent> subtree, IBspTree<TEdgeHull, TContent> edge, ISpaceContentOperation<TContent> edgeOperation)
    {

        var queue = new Queue<(BspNode<TEdgeHull, TContent> Splitter, BspNode<TEdgeHull, TContent> Node)>();
        queue.Enqueue((subtree, new BspNode<TEdgeHull, TContent>(edge)));
        while (queue.TryDequeue(out var current))
        {
            var (b, f) = current.Node.Split(current.Splitter, edgeOperation, out var flip);
            // if (flip) (b, f) = (f, b);

            if (b == f) continue;
            if (b != null)
            {
                if (current.Splitter.back!.edge != null)
                {
                    queue.Enqueue((current.Splitter.back, b));
                }
                else
                {
                    current.Splitter.back = BspTree<THull, TEdgeHull, TContent>.SplitLeaf(b, current.Splitter.back);
                }
            }
            if (f != null)
            {
                if (current.Splitter.front!.edge != null)
                {
                    queue.Enqueue((current.Splitter.front, f));
                }
                else
                {
                    current.Splitter.front = BspTree<THull, TEdgeHull, TContent>.SplitLeaf(f, current.Splitter.front);
                }
            }
        }
    }

    public void Add(IBspTree<TEdgeHull, TContent> edge, ISpaceContentOperation<TContent> edgeOperation)
    {
        if (Root.edge == null)
        {
            // empty tree
            Root = BspTree<THull, TEdgeHull, TContent>.SplitLeaf(new BspNode<TEdgeHull, TContent>(edge), Root);
            return;
        }
        AddToSubtree(Root, edge, edgeOperation);
    }
    private void AddSplitterNode(BspNode<TEdgeHull, TContent> subtree, BspNode<TEdgeHull, TContent> node,
                     ISpaceContentOperation<TContent> edgeOperation, Dictionary<long, Dictionary<long, Side>> order,
                     Dictionary<(long, Side), BspNode<TEdgeHull, TContent>> leafNodes)
    {
        var queue = new Stack<(BspNode<TEdgeHull, TContent> Splitter, BspNode<TEdgeHull, TContent> Node)>();
        queue.Push((subtree, node));
        while (queue.TryPop(out var current))
        {
            // if (order.TryGetValue(current.Node.numeration, out var ancestors) && ancestors.TryGetValue(current.Splitter.numeration, out var side))
            // {
            //     var child = current.Splitter.GetChild(side)!;
            //     if (child == null)
            //     {
            //         current.Splitter.SetChild(side, current.Node);
            //         continue;
            //     }
            //     if (child.edge == null)
            //     {
            //         leafNodes[current.Node.numeration] = child;
            //         current.Splitter.SetChild(side, current.Node);
            //     }
            //     else
            //     {
            //         queue.Enqueue((child, current.Node));
            //     }
            //     continue;
            // }
            var (b, f) = current.Node.Split(current.Splitter, edgeOperation, out var _);
            if (b == f) continue;
            if (b != null)
            {
                if (current.Splitter.back == null)
                {
                    current.Splitter.back = current.Node;
                }
                else if (current.Splitter.back.edge == null)
                {
                    leafNodes[(current.Splitter.numeration, Side.Back)] = current.Splitter.back;
                    current.Splitter.back = current.Node;
                }
                else
                {
                    queue.Push((current.Splitter.back, b));
                }
            }
            if (f != null)
            {
                if (current.Splitter.front == null)
                {
                    current.Splitter.front = current.Node;
                }
                else if (current.Splitter.front.edge == null)
                {
                    leafNodes[(current.Splitter.numeration, Side.Front)] = current.Splitter.front;
                    current.Splitter.front = current.Node;
                }
                else
                {
                    queue.Push((current.Splitter.front, f));
                }
            }
        }
    }

    public void Optimize()
    {
        if (Root == null) return;
        Root.Optimize();
    }

    private void SetLeaf(BspTree<THull, TEdgeHull, TContent> other, ISpaceContentOperation<TContent> operation,
        bool isinverse,
        BspNode<TEdgeHull, TContent> node, BspNode<TEdgeHull, TContent> leaf, Side side,
        Dictionary<long, BspNode<TEdgeHull, TContent>?> parents,
        Stack<BspNode<TEdgeHull, TContent>> leafPool, ISpaceTransformer<TEdgeHull> transformer)
    {
        var p = parents[node.numeration];
        var hull = node.edge!.Hull;
        float distance = 1e30f;
        while (p != null)
        {
            distance = hull.LimitDistance(side, distance, p.edge!.Hull);
            p = parents[p.numeration];
        }
        var point = transformer.Transform(node.edge!.Hull.PointByDistance(side, distance * 0.9f));
        var otherleaf = other.Search(point);
        var f = operation.Apply(leaf!.flags, isinverse ? operation.Invert(otherleaf.flags) : otherleaf.flags);

        if (leafPool.TryPop(out var newNode))
        {
            newNode.SetLeaf(f);
        }
        else
        {
            newNode = new BspNode<TEdgeHull, TContent>(f);
        }
        node.SetChild(side, newNode);
    }
    private BspTree<THull, TEdgeHull, TContent> DoCsg(BspTree<THull, TEdgeHull, TContent> other, ISpaceContentOperation<TContent> operation, bool inplace = false)
    {
        // var otherHull = Hull.IsFlip(otherTree.Hull) ? otherTree.Hull.Flipped : otherTree.Hull;
        var copy = other.CloneProjectedInstance(Hull).Root;
        var (_, transform) = Hull.Transform(other.Hull);
        var isinverse = Hull.IsFlip(other.Hull);
        var root = Root;
        if (Root.edge == null)
        {
            var f = root.flags;
            if (isinverse) copy.Invert(operation);
            Root.Apply(operation, f);
            if (inplace)
            {
                Root = copy;
                Hull = Hull.Union(other.Hull);
                return this;
            }
            return new BspTree<THull, TEdgeHull, TContent>(copy, Hull.Union(other.Hull));
        }
        if (!inplace)
        {
            root = root.DeepCopy();
        }
        var size = BspOperationsHelper<TEdgeHull, TContent>.Numerate(copy);
        BspOperationsHelper<TEdgeHull, TContent>.Numerate(root, size);
        var order = BspOperationsHelper<TEdgeHull, TContent>.Order(copy);

        var queue = new Queue<BspNode<TEdgeHull, TContent>>();
        var leafBindings = new Dictionary<(long, Side), BspNode<TEdgeHull, TContent>>();
        var leafPool = new Stack<BspNode<TEdgeHull, TContent>>();
        queue.Enqueue(copy);
        while (queue.TryDequeue(out var current))
        {
            if (current.edge == null)
            {
                leafPool.Push(current);
                continue;
            }
            queue.Enqueue(current.back!);
            queue.Enqueue(current.front!);
            current.Detach();
            AddSplitterNode(root, current, operation.EdgeOperation, order, leafBindings);
        }
        var leafStack = new Stack<(BspNode<TEdgeHull, TContent> Node, BspNode<TEdgeHull, TContent>? Leaf)>();
        var parents = new Dictionary<long, BspNode<TEdgeHull, TContent>?>();
        parents[root.numeration] = null;
        leafStack.Push((root, null));
        while (leafStack.TryPop(out var current))
        {
            var leaf = current.Leaf;
            // if (leaf == null && leafBindings.TryGetValue(current.Node.numeration, out var newLeaf))
            // {
            //     leaf = newLeaf;
            // }
            if (current.Node.back == null)
            { // leaf placeholder
                if (leaf == null) throw new AssertionException("Broken tree: no leaf");
                SetLeaf(other, operation, isinverse, current.Node, leaf, Side.Back, parents, leafPool, transform);
            }
            else if (current.Node.back.edge == null)
            {
                SetLeaf(other, operation, isinverse, current.Node, current.Node.back, Side.Back, parents, leafPool, transform);
            }
            else
            {
                if (leafBindings.TryGetValue((current.Node.numeration, Side.Back), out var currentLeaf)) leaf = currentLeaf;
                parents[current.Node.back.numeration] = current.Node;
                leafStack.Push((current.Node.back, leaf));
            }
            if (current.Node.front == null)
            { // leaf placeholder
                if (leaf == null) throw new AssertionException("Broken tree: no leaf");
                SetLeaf(other, operation, isinverse, current.Node, leaf, Side.Front, parents, leafPool, transform);
            }
            else if (current.Node.front.edge == null)
            {
                SetLeaf(other, operation, isinverse, current.Node, current.Node.front, Side.Front, parents, leafPool, transform);
            }
            else
            {
                // if (leafBindings.TryGetValue((current.Node.numeration, Side.Front), out var leaf)) currentLeaf = leaf;
                if (leafBindings.TryGetValue((current.Node.numeration, Side.Front), out var currentLeaf)) leaf = currentLeaf;
                parents[current.Node.front.numeration] = current.Node;
                leafStack.Push((current.Node.front, leaf));
            }
        }
        if (inplace)
        {
            Hull = Hull.Union(other.Hull);
            return this;
        }
        return new BspTree<THull, TEdgeHull, TContent>(root, Hull.Union(other.Hull));
    }

    public IBspTree<THull, TContent> CloneProjected(THull hull) => CloneProjectedInstance(hull);

    public BspTree<THull, TEdgeHull, TContent> CloneProjectedInstance(THull hull)
    {
        var (h, t) = hull.Transform(Hull);
        return new(Root.DeepCopy(t), h);
    }

    public IBspTree<THull, TContent> Csg(
        IBspTree<THull, TContent> otherTree,
        ISpaceContentOperation<TContent> operation,
        bool inplace = false)
    {
        if (otherTree is not BspTree<THull, TEdgeHull, TContent> other)
        {
            throw new NotImplementedException("Invalid bsp tree type");
        }
        return DoCsg(other, operation, inplace);
    }
    private BspTree<THull, TEdgeHull, TContent> Csg(
        BspTree<THull, TEdgeHull, TContent> other,
        ISpaceContentOperation<TContent> operation,
        bool inplace = false)
    {
        // var otherHull = Hull.IsFlip(otherTree.Hull) ? otherTree.Hull.Flipped : otherTree.Hull;
        var splitterQueue = new Queue<(BspNode<TEdgeHull, TContent> Splitter, BspNode<TEdgeHull, TContent> Subtree, BspNode<TEdgeHull, TContent> Target)>();
        var copy = other.CloneProjectedInstance(Hull).Root;
        var size = BspOperationsHelper<TEdgeHull, TContent>.Numerate(copy);
        var isinverse = Hull.IsFlip(other.Hull);
        var order = BspOperationsHelper<TEdgeHull, TContent>.Order(copy);
        var root = Root;
        BspOperationsHelper<TEdgeHull, TContent>.Numerate(root, size);
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
                    var f = isinverse ? operation.Invert(current.Subtree.flags) : current.Subtree.flags;
                    current.Target.SetLeaf(operation.Apply(current.Splitter.flags, f));
                    continue;
                }
                if (isinverse) current.Subtree.Invert(operation);
                current.Subtree.Apply(operation, current.Splitter.flags);
                current.Target.SetNode(current.Subtree.back!, current.Subtree.front!, current.Subtree.edge);
                continue;
            }
            if (current.Subtree.edge == null)
            {
                // apply operation on leafs
                var rest = inplace ? current.Splitter : current.Splitter.DeepCopy();
                var f = isinverse ? operation.Invert(current.Subtree.flags) : current.Subtree.flags;
                rest.Apply(operation.Reverse, f);
                current.Target.SetChild(Side.Incident, rest);
                continue;
            }
            var subtreeStack = new Stack<BspNode<TEdgeHull, TContent>>();
            BspNode<TEdgeHull, TContent>? back = null;
            BspNode<TEdgeHull, TContent>? front = null;
            subtreeStack.Push(current.Subtree);

            while (subtreeStack.TryPop(out var el))
            {
                if (el.edge == null)
                {
                    if (BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref back, el, order))
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, el.Copy(), order);
                    }
                    else
                    {
                        BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, el, order);
                    }
                    continue;
                }
                var (b, f) = el.Split(current.Target, operation.EdgeOperation, out var flip);
                subtreeStack.Push(el.back!);
                subtreeStack.Push(el.front!);
                if (f != null)
                {
                    f.Detach();
                    BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref front, f, order);
                }
                if (b != null)
                {
                    b.Detach();
                    BspOperationsHelper<TEdgeHull, TContent>.TryAdd(ref back, b, order);

                }
            }

            var bc = inplace ? current.Splitter.back! : current.Splitter.back!.Copy();
            var fc = inplace ? current.Splitter.front! : current.Splitter.front!.Copy();

            current.Target.SetChildren(bc, fc);

            if (back != null) splitterQueue.Enqueue((current.Splitter.back!, back, bc));
            if (front != null) splitterQueue.Enqueue((current.Splitter.front!, front, fc));
        }
        if (inplace)
        {
            Hull = Hull.Union(other.Hull);
            return this;
        }
        return new BspTree<THull, TEdgeHull, TContent>(root, Hull.Union(other.Hull));
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
            res.Append($"{string.Join("  ", Enumerable.Repeat("", n.Depth))}- {n.Node.ToString().Split('\n', 2)[0]}\n");
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

    public int Visit(Action<BspNode<TEdgeHull, TContent>> action)
    {
        var s = new Stack<(BspNode<TEdgeHull, TContent> Node, int Depth)>();
        var nodes = new List<(long, BspNode<TEdgeHull, TContent>)>();
        s.Push((Root, 1));
        var maxd = 0;
        while (s.TryPop(out var n))
        {
            action(n.Node);
            maxd = Math.Max(maxd, n.Depth);
            nodes.Add((n.Node.numeration, n.Node));
            if (n.Node.edge != null)
            {
                s.Push((n.Node.front!, n.Depth + 1));
                s.Push((n.Node.back!, n.Depth + 1));
            }
        }
        return maxd;
    }
}
