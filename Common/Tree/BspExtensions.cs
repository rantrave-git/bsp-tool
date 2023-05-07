global using BspNode1D = Bsp.Common.Tree.BspNode<Bsp.Common.Tree.Hull0D, long>;
global using BspNode2D = Bsp.Common.Tree.BspNode<Bsp.Common.Tree.Hull1D, long>;
global using BspNode3D = Bsp.Common.Tree.BspNode<Bsp.Common.Tree.Hull2D, long>;
global using BspTree1D = Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull1D, Bsp.Common.Tree.Hull0D, long>;
global using BspTree2D = Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull2D, Bsp.Common.Tree.Hull1D, long>;
global using BspTree3D = Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull3D, Bsp.Common.Tree.Hull2D, long>;
// global using BspNode2D = Bsp.Common.Tree.BspNode<Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull1D, System.Numerics.Vector2, Bsp.Common.Tree.BspTree0D, float, long>, long>;
// global using BspNode3D = Bsp.Common.Tree.BspNode<Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull2D, System.Numerics.Vector3, Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull1D, System.Numerics.Vector2, Bsp.Common.Tree.BspTree0D, float, long>, System.Numerics.Vector2, long>, long>;

global using BspOperationsHelper1D = Bsp.Common.Tree.BspOperationsHelper<Bsp.Common.Tree.Hull0D, long>;
global using BspOperationsHelper2D = Bsp.Common.Tree.BspOperationsHelper<Bsp.Common.Tree.Hull1D, long>;
global using BspOperationsHelper3D = Bsp.Common.Tree.BspOperationsHelper<Bsp.Common.Tree.Hull2D, long>;
// global using BspOperationsHelper3D = Bsp.Common.Tree.BspOperationsHelper<Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull2D, System.Numerics.Vector3, Bsp.Common.Tree.BspTree<Bsp.Common.Tree.Hull1D, System.Numerics.Vector2, Bsp.Common.Tree.BspTree0D, float, long>, System.Numerics.Vector2, long>, long>;

using System.Numerics;
using Bsp.Common.Geometry;
using System.Runtime.Intrinsics;

namespace Bsp.Common.Tree;

public static class BspExtensions
{
    public static BspTree1D CreateBspTree(this Hull1D plane, long flags)
    {
        if (flags == 0) return new BspTree1D(BspOperationsHelper1D.MakeLeaf(flags), plane);
        return new BspTree1D(
            BspOperationsHelper1D.MakeNode(
                new BspTree0D<long>(new Vector2(-1.0f, -plane.min)),
                BspOperationsHelper1D.MakeNode(
                    new BspTree0D<long>(new Vector2(1.0f, plane.max)),
                    BspOperationsHelper1D.MakeLeaf(flags),
                    BspOperationsHelper1D.MakeLeaf(0)
                ),
                BspOperationsHelper1D.MakeLeaf(0)
            ), plane
        );
    }
    public static BspTree1D CreateBspTree1D(this Hull1D plane, long flags) =>
        new BspTree1D(BspOperationsHelper1D.MakeLeaf(flags), plane);
    public static Vector2 MakePoint(float x) => new Vector2(x, -1);

    public static BspTree2D CreateBspTree(this Hull2D plane, long flags)
    {
        if (flags == 0) return new BspTree2D(BspOperationsHelper2D.MakeLeaf(flags), plane);
        // var root = BspOperationsHelper2D.MakeNode()
        var hulls = plane.Bounds.Select(x => x.Coplanar()).ToArray();
        for (int i = 0; i < hulls.Length; ++i)
        {
            var hi = hulls[i];
            if (hi == null || hi.Empty) continue;
            for (int j = i + 1; j < hulls.Length; ++j)
            {
                var hj = hulls[j];
                if (hj == null || hj.Empty) continue;
                (hj, _) = hj!.Split(hi);
                hulls[j] = hj;
            }
        }
        var root = BspOperationsHelper2D.MakeLeaf(flags);
        for (int i = hulls.Length; i-- > 0;)
        {
            if (hulls[i] == null || hulls[i].Empty) continue;
            root = BspOperationsHelper2D.MakeNode(hulls[i].CreateBspTree(1), root, BspOperationsHelper2D.MakeLeaf(0));
        }
        return new BspTree2D(root, plane.Coplanar(new List<Hull1D>(), false));
    }
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
    public static List<Vector3> Rect(this Matrix4x4 transform, float halfwidth, float halfheight) => new List<Vector3>() {
        Vector3.Transform(new Vector3(halfwidth, halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(-halfwidth, halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(-halfwidth, -halfheight, 0.0f), transform),
        Vector3.Transform(new Vector3(halfwidth, -halfheight, 0.0f), transform),
    };

    public static BspNode1D Search<TContent>(this IBspTree<Hull1D, TContent> tree, float x)
    {
        if (tree is BspTree1D t) return t.Search(x.AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
    public static BspNode2D Search<TContent>(this IBspTree<Hull2D, TContent> tree, Vector2 x)
    {
        if (tree is BspTree2D t) return t.Search(x.AsVector128().AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
    public static BspNode3D Search<TContent>(this IBspTree<Hull3D, TContent> tree, Vector3 x)
    {
        if (tree is BspTree3D t) return t.Search(x.AsVector128().AsVector());
        throw new ArgumentException("Wrong dimension", nameof(tree));
    }
    // public static BspTree3D CreateBspTree(this Mesh mesh)
}