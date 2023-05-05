namespace Bsp.Common.Tree;

class Portal2D
{
    public BspNode2D Splitter;
    public Hull2D? Bounds;
    public Portal2D(BspNode2D splitter)
    {
        Splitter = splitter;
    }
    public Portal2D CutFront(Hull2D splitter)
    {
        if (Bounds == null || Bounds.empty) return this;
        var (b, f) = Bounds.Split(splitter);
        Bounds = b;
        return new Portal2D(Splitter)
        {
            Bounds = f,
        };
    }
}

class PortalGraph2D
{
    public List<BspNode2D> Leafs = new List<BspNode2D>();
    public List<Dictionary<int, Portal2D>> Portals = new List<Dictionary<int, Portal2D>>();
    public static PortalGraph2D Build(BspNode2D root)
    {
        var q = new Queue<(BspNode2D Node, List<Portal2D> Portals)>();
        q.Enqueue((root, new List<Portal2D>()));
        while (q.TryDequeue(out var e))
        {
            foreach (var i in e.Portals)
            {
                // var b = i.CutFront(e.Node.edge.Hull);
            }
        }
        throw new NotImplementedException();
    }
}