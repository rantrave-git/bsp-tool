using System.Numerics;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

class Portal<THull, TContent> where THull : class, IHull<THull>
{
    public BspNode<THull, TContent> Splitter;
    public THull? Bounds;
    public Side Side;
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
    public Portal<THull, TContent> Dual => new Portal<THull, TContent>(Splitter, (Side)(-(int)Side)) { Bounds = Bounds };
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

class PortalGraph<THull, TContent> where THull : class, IHull<THull>
{
    public List<BspNode<THull, TContent>> Leafs = new List<BspNode<THull, TContent>>();
    public List<List<Portal<THull, TContent>>> LeafEdges = new List<List<Portal<THull, TContent>>>();
    public List<Dictionary<int, Portal<THull, TContent>>> Portals = new List<Dictionary<int, Portal<THull, TContent>>>();
    private PortalGraph() { }
    public static PortalGraph<THull, TContent> Build(BspNode<THull, TContent> root)
    {
        BspOperationsHelper<THull, TContent>.Numerate(root);
        var q = new Queue<(BspNode<THull, TContent> Node, List<Portal<THull, TContent>> Portals)>();
        var leafs = new List<BspNode<THull, TContent>>();
        var leafEdges = new List<List<Portal<THull, TContent>>>();
        q.Enqueue((root, new List<Portal<THull, TContent>>()));
        // build bounds
        while (q.TryDequeue(out var e))
        {
            if (e.Node.edge == null) // leaf
            {
                leafs.Add(e.Node);
                leafEdges.Add(e.Portals);
                continue;
            }
            var back = new List<Portal<THull, TContent>>();
            var front = new List<Portal<THull, TContent>>();
            foreach (var i in e.Portals)
            {
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
            back.Add(new Portal<THull, TContent>(e.Node, Side.Back));
            front.Add(new Portal<THull, TContent>(e.Node, Side.Front));
            q.Enqueue((e.Node.back!, back));
            q.Enqueue((e.Node.front!, front));
        }
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
        var portals = leafs.Select(x => new Dictionary<int, Portal<THull, TContent>>()).ToList();
        foreach (var portal in portalAdjacency)
        {
            for (int i = 0; i < portal.Value.Count; ++i)
            {
                var il = portal.Value[i];
                for (int j = i; j < portal.Value.Count; ++j)
                {
                    var jl = portal.Value[j];
                    var p = leafEdges[il.Leaf][il.Edge].Intersect(leafEdges[jl.Leaf][jl.Edge]);

                    portals[i][j] = p;
                    portals[j][i] = p.Dual;
                }
            }
        }
        return new PortalGraph<THull, TContent>()
        {
            Leafs = leafs,
            LeafEdges = leafEdges,
            Portals = portals,
        };
    }
}