using System.Numerics;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

using BspNode1D = BspNode<BspTree0D, long>;
using BspNode2D = BspNode<BspTree<Hull1D, Vector2, BspTree0D, float, long>, long>;
using BspNode3D = BspNode<BspTree<Hull2D, Vector3, BspTree<Hull1D, Vector2, BspTree0D, float, long>, Vector2, long>, long>;

public interface IHull<THull, TPlane, TPoint, TBspNode, TContent>
    where TBspNode : IBspNode<TBspNode, TContent>
    where TPoint : struct
{
    (TBspNode? Back, TBspNode? Front) SplitNode(TBspNode splitter, TBspNode node, ISpaceContentOperation<TContent> operation, out bool flip);
    public (THull? Back, THull? Front) Split(THull splitter);
    TBspNode GetSide(TBspNode node, TPoint point);
    THull Copy();
}

public class Hull1D : IHull<Hull1D, Vector2, float, BspNode1D, long>
{
    public Basis2D local = null!;
    public float min = -1e30f; // float.MaxValue;
    public float max = 1e30f; // float.MaxValue;
    public bool empty = false;
    public Hull1D Flipped => new Hull1D()
    {
        local = local.Flipped,
        min = -max,
        max = -min,
        empty = empty,
    };
    private Hull1D() { }
    public Hull1D(Vector3 plane)
    {
        local = Basis2D.TangentSpace(plane);
    }
    public Hull1D Coplanar() => new Hull1D() { local = local };
    public Hull1D Coplanar(float t0, float t1) => new Hull1D()
    {
        local = local,
        min = MathF.Min(t0, t1),
        max = MathF.Max(t0, t1),
        empty = MathF.Abs(t1 - t0) <= Linealg.Eps,
    };

    public override string ToString()
    {
        return $"{local.Plane} ({min} {max})";
    }
    public float Size() => max - min;
    public Vector2 Pos(float t) => local.Point(max * (t) + min * (1.0f - t));
    public float DistFrom(Basis2D space) => MathF.Max(MathF.Abs(space.Dist(local.Point(min))), MathF.Abs(space.Dist(local.Point(max))));
    public (Hull1D? Back, Hull1D? Front) Split(Hull1D splitter)
    {
        // splitter.tangent.IsCoincident
        var cls = local.ClassifyTo(splitter.local);
        if (cls != Classification.NotParallel)
        {
            if (cls == Classification.Coincident)
            {
                return (this, this);
            }
            return cls == Classification.Back ? (this, null) : (null, this);
        }
        var localPlane = local.Project(splitter.local.Plane);
        var x = localPlane.X * localPlane.Y;
        if (x < min /*+ Linealg.Eps */) return localPlane.X > 0 ? (Coplanar(min, min), this) : (this, Coplanar(min, min));
        if (x > max /*- Linealg.Eps */) return localPlane.X < 0 ? (Coplanar(max, max), this) : (this, Coplanar(max, max));
        var minHull = Coplanar(min, x);
        var maxHull = Coplanar(x, max);
        return localPlane.X > 0 ? (minHull, maxHull) : (maxHull, minHull);
    }
    public (BspNode1D? Back, BspNode1D? Front) SplitNode(BspNode1D splitter, BspNode1D node, ISpaceContentOperation<long> operation, out bool flip)
    {
        var sp = splitter.edge!;
        var np = node.edge!;
        var nn = sp.hull.X * np.hull.X;
        flip = nn < 0;
        var p = nn * np.hull.Y - sp.hull.Y;
        if (p > Linealg.Eps) return (null, node);
        if (p < -Linealg.Eps) return (node, null);
        return (node, node);
    }
    public BspNode1D GetSide(BspNode1D node, float x)
    {
        var c = node;
        var pos = new Vector2(x, -1);
        while (c.edge != null)
        {
            var p = Vector2.Dot(c.edge.hull, pos);
            c = c.GetChild(p >= 0 ? Side.Front : Side.Back)!;
            if (c == null) throw new AssertionException("Tree is not full");
        }
        return c;
    }

    public Hull1D Copy()
    {
        return new Hull1D()
        {
            local = local,
            min = min,
            max = max,
            empty = empty,
        };
    }
}

public class SumContentOperation : IContentOperation<long>
{
    public long Apply(long lhs, long rhs) => lhs + rhs;
}
public class SubContentOperation : IContentOperation<long>
{
    public long Apply(long lhs, long rhs) => lhs - rhs;
}

public class Hull2D : IHull<Hull2D, Vector3, Vector2, BspNode2D, long>
{
    public Basis3D local = null!;
    public List<Hull1D> bounds = new List<Hull1D>();
    public bool empty = false;
    public Hull2D Flipped => new Hull2D()
    {
        local = local.Flipped,
        bounds = bounds.Select(x => x.Flipped).ToList(),
        empty = empty,
    };
    private Hull2D() { }
    public Hull2D(Vector4 plane)
    {
        local = Basis3D.TangentSpace(plane);
    }
    // private static IContentOperation<long> Positive = new SumContentOperation();
    // private static IContentOperation<long> Negative = new SubContentOperation();
    // private static IContentOperation<long> FlipOperation(bool flip) => flip ? Negative : Positive;
    public Vector3 Pos(Vector2 uv) => uv.X * local.Tangent + uv.Y * local.Binormal + local.Origin.Z * local.Normal;

    public (BspNode2D? Back, BspNode2D? Front) SplitNode(BspNode2D splitter, BspNode2D node, ISpaceContentOperation<long> operation, out bool flip)
    {
        var splitterLocal = splitter.edge!.Hull.local;
        var nodeLocal = node.edge!.Hull.local;
        var tangentCls = splitterLocal.ClassifyTo(nodeLocal);
        flip = splitterLocal.IsFlip(nodeLocal);
        var (b, f) = node.edge!.Hull.Split(splitter.edge!.Hull);
        if (b == null || b.empty) return (null, node);
        if (f == null || f.empty) return (node, null);
        if (b == f) // coincide
        {
            node.edge!.Csg(splitter.edge!, flip ? operation.Inverse : operation, true);
            return (node, node);
        }
        var backNode = node.Copy();
        backNode.edge = node.edge.Separate(f, b);
        return (backNode, node);
    }

    public float DistFrom(Basis3D space)
    {
        var proj = Basis2D.TangentSpace(local.Project(space.Plane));
        return bounds.Aggregate(0.0f, (s, i) => MathF.Max(s, i.DistFrom(proj)));
    }
    public BspNode2D GetSide(BspNode2D node, Vector2 point)
    {
        var c = node;
        while (c.edge != null)
        {
            c = c.GetChild(c.edge.Hull.local.ClassifyStrict(point))!;
            if (c == null) throw new AssertionException("Tree is not full");
        }
        return c;
    }

    public Hull2D Copy() => new Hull2D()
    {
        local = local,
        bounds = bounds.Select(x => x.Copy()).ToList(),
    };
    public Hull2D Coplanar(List<Hull1D> bounds, bool empty) => new Hull2D()
    {
        local = local,
        bounds = bounds,
        empty = empty,
    };

    public override string ToString()
    {
        return $"{local.Plane}: {String.Join(", ", bounds.Select(x => x.ToString()))}";
    }

    public (Hull2D? Back, Hull2D? Front) Split(Hull2D splitter)
    {
        var cls = splitter.local.ClassifyTo(local);
        if (cls != Classification.NotParallel)
        {
            if (cls == Classification.Coincident)
            {
                return (this, this);
            }
            return cls == Classification.Back ? (this, null) : (null, this);
        }
        var splitterHull = new Hull1D(local.Project(splitter.local.Plane));
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < bounds.Count; ++i)
        {
            var (hullb, hullf) = splitterHull.Split(bounds[i]);
            if (hullb == null)
            {
                // planes are parallel
                var (b, f) = bounds[i].Split(splitterHull);
                if (b == null) return (Coplanar(new List<Hull1D>() { splitterHull, splitterHull.Flipped }, true), this);
                if (f == null) return (this, Coplanar(new List<Hull1D>() { splitterHull.Flipped, splitterHull }, true));
                // // 
                throw new AssertionException();
            }
            if (hullb == hullf)
            {
                if (bounds[i].local.IsFlip(splitterHull.local))
                {
                    return (Coplanar(new List<Hull1D>() { splitterHull, splitterHull.Flipped }, true), this);
                }
                return (this, Coplanar(new List<Hull1D>() { splitterHull.Flipped, splitterHull }, true));
            }
            splitterHull = hullb;
        }
        var back = Coplanar(new List<Hull1D>(bounds.Count + 1) { splitterHull }, true);
        var front = Coplanar(new List<Hull1D>(bounds.Count + 1) { splitterHull.Flipped }, true);
        for (int i = 0; i < bounds.Count; ++i)
        {
            var (b, f) = bounds[i].Split(splitterHull);
            if (b != null && !b.empty) back.bounds.Add(b);
            if (f != null && !f.empty) front.bounds.Add(f);
        }

        back.empty = back.bounds.Count > 1 ? back.DistFrom(splitter.local) <= Linealg.Eps : splitterHull.empty;
        if (back.empty)
        {
            // back.bounds.RemoveRange(1, back.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.empty)
            {
                return (back, this);
            }
        }

        front.empty = front.bounds.Count > 1 ? front.DistFrom(splitter.local) <= Linealg.Eps : splitterHull.empty;
        if (front.empty)
        {
            // front.bounds.RemoveRange(1, front.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.empty)
            {
                return (this, front);
            }
        }

        return (back, front);
    }
}

public class Hull3D : IHull<Hull3D, Vector4, Vector3, BspNode3D, long>
{
    public List<Hull2D> bounds = new List<Hull2D>();
    public bool empty = false;
    public Hull3D Copy() => new Hull3D()
    {
        bounds = bounds.Select(x => x.Copy()).ToList(),
        empty = empty
    };

    public BspNode3D GetSide(BspNode3D node, Vector3 point)
    {
        var c = node;
        while (c.edge != null)
        {
            c = c.GetChild(c.edge.Hull.local.ClassifyStrict(point))!;
            if (c == null) throw new AssertionException("Tree is not full");
        }
        return c;
    }


    public (BspNode3D? Back, BspNode3D? Front) SplitNode(BspNode3D splitter, BspNode3D node, ISpaceContentOperation<long> operation, out bool flip)
    {
        var splitterLocal = splitter.edge!.Hull.local;
        var nodeLocal = node.edge!.Hull.local;
        var tangentCls = splitterLocal.ClassifyTo(nodeLocal);
        flip = splitterLocal.IsFlip(nodeLocal);
        var (b, f) = node.edge!.Hull.Split(splitter.edge!.Hull);
        if (b == null || b.empty) return (null, node);
        if (f == null || f.empty) return (node, null);
        if (b == f) // coincide
        {
            node.edge!.Csg(splitter.edge!, flip ? operation.Inverse : operation, true);
            return (node, node);
        }
        var backNode = node.Copy();
        backNode.edge = node.edge.Separate(f, b);
        return (backNode, node);
    }

    public (Hull3D? Back, Hull3D? Front) Split(Hull3D splitter) => throw new NotImplementedException();
}