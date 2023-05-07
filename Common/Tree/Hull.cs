using System.Numerics;
using System.Runtime.Intrinsics;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public interface IHull2<THull>
{
    public THull Flipped { get; }
    public (THull, THull) Split(Vector<float> plane);
    public THull Copy();
}

/*
public class HullND : IHull2<HullND>
{
    public Vector<float> Plane;
    public List<HullND> Bounds = new List<HullND>();
    public bool Empty = false;

    public int Dimension;
    public HullND(int dim, Vector<float> plane)
    {
        Dimension = dim;
        
    }

    public HullND Flipped => throw new NotImplementedException();

    public HullND Coplanar(List<HullND> bounds)
    {
        throw new NotImplementedException();
    }

    public HullND Copy()
    {
        throw new NotImplementedException();
    }

    public (HullND, HullND) Split(Vector<float> plane)
    {
        var splitterHull = new Hull1D(local.Project(splitter.local.Plane));
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (hullb, hullf) = splitterHull.Split(Bounds[i]);
            if (hullb == null)
            {
                // planes are parallel
                var (b, f) = Bounds[i].Split(splitterHull);
                if (b == null) return (Coplanar(new List<Hull1D>() { splitterHull, splitterHull.Flipped }, true), this);
                if (f == null) return (this, Coplanar(new List<Hull1D>() { splitterHull.Flipped, splitterHull }, true));
                // // 
                throw new AssertionException();
            }
            if (hullb == hullf)
            {
                if (Bounds[i].local.IsFlip(splitterHull.local))
                {
                    return (Coplanar(new List<THullN1D>() { splitterHull, splitterHull.Flipped }, true), this);
                }
                return (this, Coplanar(new List<THullN1D>() { splitterHull.Flipped, splitterHull }, true));
            }
            splitterHull = hullb;
        }
        var back = Coplanar(new List<THullN1D>(Bounds.Count + 1) { splitterHull });
        var front = Coplanar(new List<THullN1D>(Bounds.Count + 1) { splitterHull.Flipped });
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (b, f) = Bounds[i].Split(splitterHull);
            if (b != null && !b.Empty) back.Bounds.Add(b);
            if (f != null && !f.Empty) front.Bounds.Add(f);
        }

        back.Empty = back.Bounds.Count > 1 ? back.DistFrom(splitter.local) <= Linealg.Eps : splitterHull.empty;
        if (back.Empty)
        {
            // back.bounds.RemoveRange(1, back.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.empty)
            {
                return (back, this);
            }
        }

        front.Empty = front.Bounds.Count > 1 ? front.DistFrom(splitter.local) <= Linealg.Eps : splitterHull.empty;
        if (front.Empty)
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
*/

public interface ISplitable<THull>
{
    (THull? Back, THull? Front) Split(THull splitter);
}

public interface IHull<THull> : ISplitable<THull> where THull : IHull<THull>
{
    bool HasSpace { get; }
    bool Empty { get; }
    THull Intersect(THull other);
    Side GetSide(Vector<float> point);
    bool IsFlip(THull other);
    THull Copy();
}

public class Hull0D : IHull<Hull0D>
{
    // public static Hull0D Instance = new Hull0D();
    public Vector2 Plane;
    public Hull0D(Vector2 plane) => Plane = plane;
    public bool Empty => false;
    public bool HasSpace => false;

    public Hull0D Copy() => new Hull0D(Plane);

    public Side GetSide(Vector<float> point)
    {
        var pos = new Vector2(point[0], -1);
        var p = Vector2.Dot(Plane, pos);
        return p >= 0 ? Side.Front : Side.Back;
    }

    public Hull0D Intersect(Hull0D other)
    {
        return this;
        // var p = other.Plane.X * Plane.X * Plane.Y - other.Plane.Y;
        // if (MathF.Abs(p) < Linealg.Eps) return Copy();
        // return
    }

    public bool IsFlip(Hull0D other) => Plane.X * other.Plane.X < 0.0f;

    public (Hull0D? Back, Hull0D? Front) Split(Hull0D splitter)
    {
        var p = splitter.Plane.X * Plane.X * Plane.Y - splitter.Plane.Y;
        if (p > Linealg.Eps) return (null, this);
        if (p < -Linealg.Eps) return (this, null);
        return (this, this);
    }

    // public (BspNode1D? Back, BspNode1D? Front) SplitNode(BspNode1D splitter, BspNode1D node, ISpaceContentOperation<long> operation, out bool flip)
    // {
    //     var sp = splitter.edge!;
    //     var np = node.edge!;
    //     var nn = sp.Hull.Plane.X * np.Hull.Plane.X;
    //     flip = nn < 0;
    //     var p = nn * np.Hull.Plane.Y - sp.Hull.Plane.Y;
    //     if (p > Linealg.Eps) return (null, node);
    //     if (p < -Linealg.Eps) return (node, null);
    //     return (node, node);
    // }
}

public class Hull1D : IHull<Hull1D>
{
    public Basis2D local = null!;
    public float min = -1e30f; // float.MaxValue;
    public float max = 1e30f; // float.MaxValue;
    public bool Empty { get; private set; } = false;
    public bool HasSpace { get; } = true;
    public Hull1D Flipped => new Hull1D()
    {
        local = local.Flipped,
        min = -max,
        max = -min,
        Empty = Empty,
    };
    private Hull1D() { }
    public Hull1D(Vector3 plane)
    {
        local = Basis2D.TangentSpace(plane);
    }
    public Hull1D CreateEmpty() => Coplanar(min, min);
    public Hull1D Coplanar() => new Hull1D() { local = local };
    private void EnsureEmpty() => Empty = (max - min <= Linealg.Eps);
    public Hull1D Coplanar(float t0, float t1) => new Hull1D()
    {
        local = local,
        min = MathF.Min(t0, t1),
        max = MathF.Max(t0, t1),
        Empty = MathF.Abs(t1 - t0) <= Linealg.Eps,
    };

    public override string ToString()
    {
        return $"{local.Plane} ({min} {max})";
    }
    public float Size() => max - min;
    public Vector2 Pos(float t) => local.Point(max * (t) + min * (1.0f - t));
    public float DistFrom(Basis2D space) => MathF.Max(MathF.Abs(space.Dist(local.Point(min))), MathF.Abs(space.Dist(local.Point(max))));
    public void AddBound(Vector2 localPlane)
    {
        var x = localPlane.X * localPlane.Y;
        if (localPlane.X > 0)
        {
            max = MathF.Min(max, MathF.Max(min, x));
        }
        else
        {
            min = MathF.Max(min, MathF.Min(max, x));
        }
        EnsureEmpty();
    }
    public void AddBound(Hull1D splitter) => AddBound(local.Project(splitter.local.Plane));
    public Hull1D Intersect(Hull1D other)
    {
        var mn = MathF.Max(min, other.min);
        var mx = MathF.Min(max, other.max);
        if (mn > mx) return Coplanar(0.5f * (mn + mx), 0.5f * (mn + mx));
        return Coplanar(mn, mx);
    }
    public (Hull1D Back, Hull1D Front) Split(Vector2 localPlane)
    {
        var x = localPlane.X * localPlane.Y;
        if (x < min /*+ Linealg.Eps */) return localPlane.X > 0 ? (Coplanar(min, min), this) : (this, Coplanar(min, min));
        if (x > max /*- Linealg.Eps */) return localPlane.X < 0 ? (Coplanar(max, max), this) : (this, Coplanar(max, max));
        var minHull = Coplanar(min, x);
        var maxHull = Coplanar(x, max);
        return localPlane.X > 0 ? (minHull, maxHull) : (maxHull, minHull);
    }
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
        return Split(local.Project(splitter.local.Plane));
    }
    public bool IsFlip(Hull1D other) => local.IsFlip(other.local);
    // public (BspNode2D? Back, BspNode2D? Front) SplitNode(BspNode2D splitter, BspNode2D node, ISpaceContentOperation<long> operation, out bool flip)
    // {
    //     var splitterLocal = splitter.edge!.Hull.local;
    //     var nodeLocal = node.edge!.Hull.local;
    //     var tangentCls = splitterLocal.ClassifyTo(nodeLocal);
    //     flip = splitterLocal.IsFlip(nodeLocal);
    //     var (b, f) = node.edge!.Hull.Split(splitter.edge!.Hull);
    //     if (b == null || b.Empty) return (null, node);
    //     if (f == null || f.Empty) return (node, null);
    //     if (b == f) // coincide
    //     {
    //         node.edge!.Csg(splitter.edge!, flip ? operation.Inverse : operation, true);
    //         return (node, node);
    //     }
    //     var backNode = node.Copy();
    //     backNode.edge = node.edge.Separate(f, b);
    //     return (backNode, node);
    // }
    public Side GetSide(Vector<float> pnt) => local.ClassifyStrict(pnt.AsVector128().AsVector2());

    public Hull1D Copy()
    {
        return new Hull1D()
        {
            local = local,
            min = min,
            max = max,
            Empty = Empty,
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

public class Hull2D : IHull<Hull2D>
{
    public Basis3D Local = null!;
    public List<Hull1D> Bounds = new List<Hull1D>();
    public bool Empty { get; private set; } = false;
    public bool HasSpace { get; } = true;
    public Hull2D Flipped => new Hull2D()
    {
        Local = Local.Flipped,
        Bounds = Bounds.Select(x => x.Flipped).ToList(),
        Empty = Empty,
    };
    private Hull2D() { }
    public Hull2D(Vector4 plane)
    {
        Local = Basis3D.TangentSpace(plane);
    }
    public Vector3 Pos(Vector2 uv) => uv.X * Local.Tangent + uv.Y * Local.Binormal + Local.Origin.Z * Local.Normal;

    public bool IsFlip(Hull2D other) => Local.IsFlip(other.Local);
    // public (BspNode3D? Back, BspNode3D? Front) SplitNode(BspNode3D splitter, BspNode3D node, ISpaceContentOperation<long> operation, out bool flip)
    // {
    //     var splitterLocal = splitter.edge!.Hull.Local;
    //     var nodeLocal = node.edge!.Hull.Local;
    //     var tangentCls = splitterLocal.ClassifyTo(nodeLocal);
    //     flip = splitterLocal.IsFlip(nodeLocal);
    //     var (b, f) = node.edge!.Hull.Split(splitter.edge!.Hull);
    //     if (b == null || b.Empty) return (null, node);
    //     if (f == null || f.Empty) return (node, null);
    //     if (b == f) // coincide
    //     {
    //         node.edge!.Csg(splitter.edge!, flip ? operation.Inverse : operation, true);
    //         return (node, node);
    //     }
    //     var backNode = node.Copy();
    //     backNode.edge = node.edge.Separate(f, b);
    //     return (backNode, node);
    // }

    public float DistFrom(Basis2D localSpace) => Bounds.Aggregate(0.0f, (s, i) => MathF.Max(s, i.DistFrom(localSpace)));
    public float DistFrom(Basis3D space) => DistFrom(Basis2D.TangentSpace(Local.Project(space.Plane)));
    public Side GetSide(Vector<float> pnt) => Local.ClassifyStrict(pnt.AsVector128().AsVector3());

    public Hull2D Intersect(Hull2D other)
    {
        var res = this.Copy();
        foreach (var b in other.Bounds)
        {
            res.AddBound(b.local.Plane);
        }
        return res;
    }

    public Hull2D CreateEmpty() => Coplanar(new List<Hull1D>() { new Hull1D(Local.AnyLocalPlane), new Hull1D(Local.AnyLocalPlane).Flipped }, true);
    public Hull2D Copy() => new Hull2D()
    {
        Local = Local,
        Bounds = Bounds.Select(x => x.Copy()).ToList(),
    };
    public Hull2D Coplanar(List<Hull1D> bounds, bool empty) => new Hull2D()
    {
        Local = Local,
        Bounds = bounds,
        Empty = empty,
    };

    public override string ToString() => $"{Local.Plane}: {String.Join(", ", Bounds.Select(x => x.ToString()))}";
    public void AddBound(Vector3 localPlane)
    {
        var splitterHull = new Hull1D(localPlane);
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < Bounds.Count; ++i)
        {
            splitterHull.AddBound(splitterHull.local.Project(Bounds[i].local.Plane));
            if (splitterHull.Empty) break;
        }
        int last = Bounds.Count - 1;
        for (int i = last; i >= 0; --i)
        {
            Bounds[i].AddBound(Bounds[i].local.Project(localPlane));
            if (Bounds[i].Empty)
            {
                Bounds[i] = Bounds[last];
                last--;
            }
        }
        if (last + 1 >= Bounds.Count) Bounds.RemoveRange(last + 1, Bounds.Count - last - 1);
        Empty = Bounds.Count > 0 ? DistFrom(splitterHull.local) <= Linealg.Eps : splitterHull.Empty;
        if (Empty)
        {
            Bounds.Clear(); // add flipped splitter instead?
            Bounds.Add(splitterHull);
            Bounds.Add(splitterHull.Flipped);
        }
        else
        {
            if (!splitterHull.Empty)
            {
                Bounds.Add(splitterHull);
            }
        }
    }
    public void AddBound(Hull2D splitter) => AddBound(Local.Project(splitter.Local.Plane));
    public (Hull2D Back, Hull2D Front) Split(Vector3 localPlane)
    {
        var splitterHull = new Hull1D(localPlane);
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (hullb, hullf) = splitterHull.Split(Bounds[i]);
            if (hullb == null)
            {
                // planes are parallel
                var (b, f) = Bounds[i].Split(splitterHull);
                if (b == null) return (Coplanar(new List<Hull1D>() { Bounds[i], Bounds[i].Flipped }, true), this);
                if (f == null) return (this, Coplanar(new List<Hull1D>() { Bounds[i].Flipped, Bounds[i] }, true));
                // // 
                throw new AssertionException();
            }
            if (hullb == hullf)
            {
                if (Bounds[i].local.IsFlip(splitterHull.local))
                {
                    return (Coplanar(new List<Hull1D>() { Bounds[i], Bounds[i].Flipped }, true), this);
                }
                return (this, Coplanar(new List<Hull1D>() { Bounds[i].Flipped, Bounds[i] }, true));
            }
            splitterHull = hullb;
        }
        var back = Coplanar(new List<Hull1D>(Bounds.Count + 1) { splitterHull }, true);
        var front = Coplanar(new List<Hull1D>(Bounds.Count + 1) { splitterHull.Flipped }, true);
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (b, f) = Bounds[i].Split(splitterHull);
            if (b != null && !b.Empty) back.Bounds.Add(b);
            if (f != null && !f.Empty) front.Bounds.Add(f);
        }

        back.Empty = back.Bounds.Count > 1 ? back.DistFrom(splitterHull.local) <= Linealg.Eps : splitterHull.Empty;
        if (back.Empty)
        {
            // back.bounds.RemoveRange(1, back.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.Empty)
            {
                return (back, this);
            }
        }

        front.Empty = front.Bounds.Count > 1 ? front.DistFrom(splitterHull.local) <= Linealg.Eps : splitterHull.Empty;
        if (front.Empty)
        {
            // front.bounds.RemoveRange(1, front.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.Empty)
            {
                return (this, front);
            }
        }

        return (back, front);
    }
    public (Hull2D? Back, Hull2D? Front) Split(Hull2D splitter)
    {
        var cls = splitter.Local.ClassifyTo(Local);
        if (cls != Classification.NotParallel)
        {
            if (cls == Classification.Coincident)
            {
                return (this, this);
            }
            return cls == Classification.Back ? (this, null) : (null, this);
        }
        return Split(Local.Project(splitter.Local.Plane));
    }
}

public class Hull3D : IHull<Hull3D>
{
    public bool HasSpace => true;
    public List<Hull2D> bounds = new List<Hull2D>();
    public bool Empty { get; private set; } = false;

    public Hull3D Copy() => new Hull3D()
    {
        bounds = bounds.Select(x => x.Copy()).ToList(),
        Empty = Empty,
    };

    public Side GetSide(Vector<float> point) => throw new NotImplementedException();
    public bool IsFlip(Hull3D other) => throw new NotImplementedException();
    public (Hull3D? Back, Hull3D? Front) Split(Hull3D splitter) => throw new NotImplementedException();

    public Hull3D Intersect(Hull3D other)
    {
        throw new NotImplementedException();
    }
}