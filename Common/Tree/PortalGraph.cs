using System.Numerics;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public class Portal<THull, TContent> where THull : class, IHull<THull>
{
    public BspNode<THull, TContent> Splitter;
    public THull? Bounds;
    public Side Side;
    public TContent Flags = default!;
    public bool Pass = true;
    public Portal(BspNode<THull, TContent> splitter, Side side)
    {
        Splitter = splitter;
        Side = side;
    }
    public Portal<THull, TContent> CutOffFront(THull splitter)
    {
        if (Bounds == null || Bounds.Empty) return this;
        var (b, f) = Bounds.Split(splitter);
        Bounds = b;
        return new Portal<THull, TContent>(Splitter, Side)
        {
            Bounds = f,
        };
    }
    public Portal<THull, TContent> Dual => new(Splitter, (Side)(-(int)Side))
    {
        Bounds = Bounds,
        Flags = Flags,
    };
    public Portal<THull, TContent> Intersect(Portal<THull, TContent> other)
    {
        if (Splitter != other.Splitter) throw new ArgumentException("Splitters are different");
        if (Bounds == null || Bounds.Empty)
        {
            return this;
        }
        if (other.Bounds == null || other.Bounds.Empty)
            return other;
        return new Portal<THull, TContent>(Splitter, Side) { Bounds = Bounds.Intersect(other.Bounds) };
    }
}

public interface IAreaBuilder<TContent>
{
    TContent OuterContent { get; }
    TContent Aggregate(TContent portalContent, TContent portalLeafContent);
    bool PassCondition(TContent portalContent, Side side);
}

public class PortalGraph<THull, TEdgeHull, TContent>
    where TEdgeHull : class, IHull<TEdgeHull>
    where THull : class, IHull<THull>, IBoxable, IContentHull<THull, TEdgeHull>
{
    public class LeafContents
    {
        public int Area = 0; // negative value corresponds solid
        public AABB Box;
        public THull Hull;
        public LeafContents(THull hull, AABB volume)
        {
            Hull = hull;
            Box = hull.Box().FluentIntersect(volume);
        }
    }
    public List<BspNode<TEdgeHull, TContent>> Leafs = default!;
    public List<List<Portal<TEdgeHull, TContent>>> LeafEdges = default!;
    public List<Dictionary<int, Portal<TEdgeHull, TContent>>> Portals = default!;
    public List<LeafContents> LeafData = default!;
    public List<Dictionary<int, Classification>> AreaGraph = default!;
    public AABB Box = default!;
    public BspTree<THull, TEdgeHull, TContent> Tree = default!;
    public List<Brush>? Brushes;
    private PortalGraph() { }
    public static PortalGraph<THull, TEdgeHull, TContent> Build(BspTree<THull, TEdgeHull, TContent> tree, IAreaBuilder<TContent> areaBuilder)
    {
        // BspOperationsHelper<TEdgeHull, TContent>.Numerate(root);
        // tree.Numerate();
        var q = new Queue<(BspNode<TEdgeHull, TContent> Node, List<Portal<TEdgeHull, TContent>> Portals)>();
        var leafs = new List<BspNode<TEdgeHull, TContent>>();
        var nodes = new List<BspNode<TEdgeHull, TContent>>();
        var leafEdges = new List<List<Portal<TEdgeHull, TContent>>>();
        q.Enqueue((tree.Root, new List<Portal<TEdgeHull, TContent>>()));
        int leafNumeration = 0;
        int nodeNumeration = 0;
        // build bounds
        while (q.TryDequeue(out var e))
        {
            if (e.Node.edge == null) // leaf
            {
                e.Node.numeration = leafNumeration++;
                leafs.Add(e.Node);
                leafEdges.Add(e.Portals);
                continue;
            }
            e.Node.numeration = nodeNumeration++;
            nodes.Add(e.Node);
            var back = new List<Portal<TEdgeHull, TContent>>();
            var front = new List<Portal<TEdgeHull, TContent>>();

            var p = new Portal<TEdgeHull, TContent>(e.Node, Side.Back)
            {
                Bounds = e.Node.edge.Hull.Coplanar(),
            };
            foreach (var i in e.Portals)
            {
                if (i.Bounds != null)
                {
                    if (i.Side == Side.Back)
                    {
                        p.CutOffFront(i.Bounds);
                    }
                    else
                    {
                        p = p.CutOffFront(i.Bounds);
                    }
                }
                var f = i.CutOffFront(e.Node.edge.Hull);
                if (i.Bounds != null && !i.Bounds.Empty)
                {
                    back.Add(i);
                }
                if (f.Bounds != null && !f.Bounds.Empty)
                {
                    front.Add(f);
                }
            }
            back.Add(p);
            front.Add(p.Dual);
            q.Enqueue((e.Node.back!, back));
            q.Enqueue((e.Node.front!, front));
        }
        if (leafs.Count == 0) throw new InvalidOperationException("Unable to build ajacency for empty tree");
        // preevaluate adjacent leafs
        var portalAdjacency = new Dictionary<long, List<(int Edge, int Leaf)>>(); // portal.numeration -> (leaf edge, leaf)
        for (int i = 0; i < leafs.Count; ++i)
        {
            for (var p = 0; p < leafEdges[i].Count; ++p)
            {
                if (!portalAdjacency.TryGetValue(leafEdges[i][p].Splitter.numeration, out var l))
                {
                    l = portalAdjacency[leafEdges[i][p].Splitter.numeration] = new List<(int, int)>();
                }
                l.Add((p, i));
            }
        }
        var portals = leafs.Select(x => new Dictionary<int, Portal<TEdgeHull, TContent>>()).ToList();
        foreach (var portal in portalAdjacency)
        {
            for (int i = 0; i < portal.Value.Count; ++i)
            {
                var (iedge, ileaf) = portal.Value[i];
                for (int j = i + 1; j < portal.Value.Count; ++j)
                {
                    var (jedge, jleaf) = portal.Value[j];
                    var p = leafEdges[ileaf][iedge].Intersect(leafEdges[jleaf][jedge]);
                    if (p.Bounds != null && !p.Bounds.Empty)
                    {
                        p.Flags = p.Splitter.edge!.Leafs(p.Bounds).Aggregate(areaBuilder.Aggregate);
                        p.Pass = areaBuilder.PassCondition(p.Flags, p.Side);
                        var dual = p.Dual;
                        dual.Pass = areaBuilder.PassCondition(dual.Flags, dual.Side);

                        portals[ileaf][jleaf] = p;
                        portals[jleaf][ileaf] = dual;
                    }
                }
            }
        }
        var bigbox = tree.Hull.Box().FluentExpand(Linealg.DecentVolume);
        var leafData = leafs.Select((x, i) => new LeafContents(tree.Hull.Coplanar(leafEdges[i].Select(y => y.Splitter.edge!.Hull).ToList()), bigbox)).ToList();

        var visitQueue = new Queue<int>();
        Span<bool> visited = stackalloc bool[leafs.Count];
        visited.Clear();
        var lastArea = 1;
        // clusterize graph
        for (int i = 0; i < leafs.Count; ++i)
        {
            if (leafData[i].Area != 0) continue; // already visited
            visitQueue.Enqueue(i);
            while (visitQueue.TryDequeue(out var leaf))
            {
                leafData[leaf].Area = lastArea;
                foreach (var j in portals[leaf])
                {
                    if (visited[j.Key]) continue;
                    visited[j.Key] = true;
                    if (j.Value.Pass && j.Value.Dual.Pass)
                    {
                        visitQueue.Enqueue(j.Key);
                    }
                }
            }
            lastArea++;
        }
        var areaGraph = Enumerable.Range(0, lastArea).Select(x => new Dictionary<int, Classification>()).ToList();
        for (int i = 0; i < portals.Count; ++i)
        {
            var leaf = leafData[i];
            foreach (var portal in portals[i])
            {
                if (portal.Key <= i) continue;
                var v = (Classification)((Convert.ToInt32(portal.Value.Pass) * (int)Classification.Front) | (Convert.ToInt32(portal.Value.Dual.Pass) * (int)Classification.Back));
                var cv = areaGraph[leaf.Area].GetValueOrDefault(leafData[portal.Key].Area, Classification.Coincident);
                areaGraph[leaf.Area][leafData[portal.Key].Area] = cv | v;
            }
        }

        return new PortalGraph<THull, TEdgeHull, TContent>()
        {
            Leafs = leafs,
            LeafEdges = leafEdges,
            Portals = portals,
            LeafData = leafData,
            AreaGraph = areaGraph,
            Box = bigbox,
            Tree = tree,
        };
    }
    public void BuildParity(bool evenIsExternal, TContent @internal, TContent @external)
    {
        Span<int> pool = stackalloc int[Leafs.Count];
        Span<bool> visited = stackalloc bool[Leafs.Count];
        visited.Clear();
        int curStart = 0;
        int curStep = 1;
        int curEnd = 1;
        int nxtStart = Leafs.Count - 1;
        int nxtStep = -1;
        int nxtEnd = Leafs.Count - 1;
        pool[0] = Tree.Search(Box.Min.AsVector()).numeration;
        visited[pool[0]] = true;
        int ind = 0;
        // while (nxtStep * (nxtEnd - nxtStart) > 0)
        // {
        while (curStep * (curEnd - curStart) > 0)
        {
            var leaf = pool[curStart];
            LeafData[leaf].Area = ind;
            foreach (var n in Portals[leaf])
            {
                if (visited[n.Key]) continue;
                visited[n.Key] = true;
                if (n.Value.Pass && Portals[n.Key][leaf].Pass)
                {
                    pool[curEnd] = n.Key;
                    curEnd += curStep;
                }
                else
                {
                    pool[nxtEnd] = n.Key;
                    nxtEnd += nxtStep;
                }
            }
            curStart += curStep;
            if (curStep * (curEnd - curStart) <= 0)
            {
                (curEnd, curStart, curStep, nxtEnd, nxtStart, nxtStep) = (nxtEnd, nxtStart, nxtStep, curEnd, curStart, curStep);
                ind += 1;
            }
        }
        Span<bool> externality = stackalloc bool[ind];
        for (int i = 0; i < Leafs.Count; ++i)
        {
            Leafs[i].flags = (LeafData[i].Area % 2) == 0 == evenIsExternal ? @external : @internal;
        }
    }
    public void GetBrushes(int leaf)
    {

    }
    void BuildPassGraph(List<Vector<float>> anchors)
    {

    }
}