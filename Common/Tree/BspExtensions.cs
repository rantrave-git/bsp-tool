using System.Numerics;
using Bsp.Common.Geometry;
using System.Runtime.Intrinsics;

namespace Bsp.Common.Tree;



public static class BspExtensions
{
    public static Vector2 MakePoint(float x) => new(x, -1);

    public static Hull2D CreateHull(this Vector4 plane, IList<Vector3> points)
    {
        var hull = new Hull2D(plane);
        for (int i = 0; i < points.Count; ++i)
        {
            var edge = new Hull1D(hull.Local.Plane2D(points[i], points[(i + 1) % points.Count], out var t0, out var t1));
            edge.AddBound(new Vector2(-1.0f, MathF.Min(t0, t1)));
            edge.AddBound(new Vector2(1.0f, MathF.Max(t0, t1)));
            hull.Bounds.Add(edge);
        }
        return hull;
    }
    private static BspNode<Hull1D, TContent> BuildSplit<TContent>(Hull1D edge, IContentProvider<TContent> edgeContent, TContent back, TContent front)
    {
        return BspOperationsHelper<Hull1D, TContent>.MakeNode(
            edge.BuildTree(edgeContent),
            BspOperationsHelper<Hull1D, TContent>.MakeLeaf(back),
            BspOperationsHelper<Hull1D, TContent>.MakeLeaf(front)
        );
    }
    public static PortalGraph<Hull2D, Hull1D, VisibilityContent>? ToBsp(this Face face, ISpaceContentOperation<VisibilityContent> operation, IAreaBuilder<VisibilityContent> areaBuilder)
    {
        Span<Vector3> points = stackalloc Vector3[face.Indices.Length];
        var innerContent = new VisibilityContent(face.Flags, VisibilityFlags.BackToFront);
        var outerContent = new VisibilityContent(0, VisibilityFlags.Open);

        // var hull = new Hull2D(face.Normal.Plane(points[0]));
        for (int i = 0; i < face.Indices.Length; ++i)
        {
            points[i] = face.Mesh.Vertices[face.Indices[i]].Pos;
        }
        var normal = face.Normal;
        if (normal.LengthSquared() < Linealg.EpsSquared) return null;
        var hull = Hull2D.ConvexHull(face.Normal.Plane(points[0]), points);
        var root = new BspTree<Hull2D, Hull1D, VisibilityContent>(
            BspOperationsHelper<Hull1D, VisibilityContent>.MakeLeaf(outerContent), hull);
        for (int i = 0; i < points.Length; ++i)
        {
            var v0 = points[i];
            var v1 = points[(i + 1) % points.Length];
            var locp = hull.Local.Plane2D(v0, v1, out var t0, out var t1);
            var loch = new Hull1D(locp, t0, t1);
            root.Add(loch.BuildTree(
                new SolidContent(
                    new VisibilityContent() { Flags = 1, Visibility = VisibilityFlags.BackToFront })),
                operation.EdgeOperation);
        }
        var gr = PortalGraph<Hull2D, Hull1D, VisibilityContent>.Build(root, areaBuilder);
        gr.BuildParity(true, innerContent, outerContent);
        return gr;
    }

    public static PortalGraph<Hull3D, Hull2D, VisibilityContent> ToBsp(this Mesh mesh, long content, ISpaceContentOperation<VisibilityContent> operation, IAreaBuilder<VisibilityContent> edgeAreaBuilder, IAreaBuilder<VisibilityContent> areaBuilder)
    {
        var innerContent = new VisibilityContent(content, VisibilityFlags.BackToFront);
        var outerContent = new VisibilityContent(0, VisibilityFlags.Open);
        var hull = Hull3D.Coplanar(new(), false);
        var root = new BspTree<Hull3D, Hull2D, VisibilityContent>(
            BspOperationsHelper<Hull2D, VisibilityContent>.MakeLeaf(outerContent), hull
        );
        for (int i = 0; i < mesh.Faces.Count; ++i)
        {
            var face = mesh.Faces[i];
            Console.WriteLine($"{i * 1.0 / mesh.Faces.Count}");
            var bsp = face.ToBsp(operation.EdgeOperation, edgeAreaBuilder);
            if (bsp == null) continue;
            root.Add(bsp.Tree, operation.EdgeOperation);
        }
        var v = 0;
        var d = root.Visit(x => v += 1);
        Console.WriteLine($"Depth: {d}, Nodes: {v}");

        Console.WriteLine($"Building graph");

        var gr = PortalGraph<Hull3D, Hull2D, VisibilityContent>.Build(root, areaBuilder);
        Console.WriteLine($"Building pairity");
        gr.BuildParity(true, innerContent, outerContent);
        return gr;
    }
    public static Mesh ToMesh(this PortalGraph<Hull3D, Hull2D, VisibilityContent> graph, int leaf)
    {
        var points = new List<Vector3>();
        var faces = new List<(int[] Indices, long Flags)>();
        foreach (var edge in graph.LeafEdges[leaf])
        {
            // if (edge.Empty) continue;
            var start = points.Count;
            edge.Bounds!.Points(points);
            faces.Add((Enumerable.Range(start, points.Count - start).ToArray(), (int)edge.Flags.Flags));
        }
        return new Mesh(points, faces, graph.Leafs[leaf].flags.Flags);
    }
    public static List<Vector3> Rect(this Matrix4x4 transform, float halfwidth, float halfheight) => new() {
        Vector3.Transform(new Vector3(halfwidth, halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(-halfwidth, halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(-halfwidth, -halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(halfwidth, -halfheight, 0.0f), transform),
    };

    public static BspNode<Hull0D, TContent> Search<TContent>(this IBspTree<Hull1D, TContent> tree, float x)
    {
        if (tree is BspTree<Hull1D, Hull0D, TContent> t) return t.Search(x.AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
    public static BspNode<Hull1D, TContent> Search<TContent>(this IBspTree<Hull2D, TContent> tree, Vector2 x)
    {
        if (tree is BspTree<Hull2D, Hull1D, TContent> t) return t.Search(x.AsVector128().AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
    public static BspNode<Hull2D, TContent> Search<TContent>(this IBspTree<Hull3D, TContent> tree, Vector3 x)
    {
        if (tree is BspTree<Hull3D, Hull2D, TContent> t) return t.Search(x.AsVector128().AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
}