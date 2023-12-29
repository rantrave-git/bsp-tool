using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Bsp.Common.Geometry;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bsp.Common.Tree;

// public interface IHull2<THull>
// {
//     public THull Flipped { get; }
//     public (THull, THull) Split(Vector<float> plane);
//     public THull Copy();
// }

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

public interface ISpaceTransformer<THull>
{
    THull Transform(THull src);
    Vector<float> Transform(Vector<float> src);
}

// normals're directed outside!
public interface IHull<THull> : ISplitable<THull> where THull : class, IHull<THull>
{
    bool HasSpace { get; }
    bool Empty { get; }
    THull Flipped { get; }
    THull Intersect(THull other);
    Side GetSide(Vector<float> point);
    bool IsFlip(THull other);
    THull Copy();
    THull Coplanar();
    THull Union(THull other);
    float LimitDistance(Side side, float distance, THull other);
    Vector<float> PointByDistance(Side side, float distance);
}

public interface IContentProvider<TContent>
{
    TContent Content { get; }
    TContent DefaultContent { get; }
    IContentProvider<TContent> BoundaryProvider { get; }
}

public interface IContentHull<THull, TEdgeHull>
    where THull : class, IHull<THull>, IContentHull<THull, TEdgeHull>
    where TEdgeHull : class, IHull<TEdgeHull>
{
    BspTree<THull, TEdgeHull, TContent> BuildTree<TContent>(IContentProvider<TContent> contentProvider);
    (THull? Back, THull? Front) Split(TEdgeHull splitter);
    IList<TEdgeHull> Boundaries();
    THull Coplanar(List<TEdgeHull> boundaries);
    (THull, ISpaceTransformer<TEdgeHull>) Transform(THull from);
    // TEdgeHull Project(TEdgeHull );
}

public interface IBoxable
{
    AABB Box();
}

public class Hull0D : IHull<Hull0D>
{
    // public static Hull0D Instance = new Hull0D();
    public Vector2 Plane;
    public Hull0D(Vector2 plane) => Plane = plane;
    public bool Empty => false;
    public bool HasSpace => false;

    public Hull0D Copy() => new(Plane);
    public Hull0D Coplanar() => this;

    public Hull0D Flipped => new(-Plane);

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
    public Hull0D Project(Hull0D onto) => new(new Vector2(onto.Plane.X, onto.Plane.Y / Plane.Y * Plane.X));

    public bool IsFlip(Hull0D other) => Plane.X * other.Plane.X < 0.0f;

    public (Hull0D? Back, Hull0D? Front) Split(Hull0D splitter)
    {
        var p = splitter.Plane.X * Plane.X * Plane.Y - splitter.Plane.Y;
        if (p > Linealg.Eps) return (null, this);
        if (p < -Linealg.Eps) return (this, null);
        return (this, this);
    }

    public override string ToString() => $"{Plane}";

    public Hull0D Union(Hull0D other) => this;

    public float LimitDistance(Side side, float distance, Hull0D other)
    {
        var dt = Plane.X * other.Plane.X;
        var dst = (int)side * (other.Plane.Y * dt - Plane.Y);
        if (dst < 0) return distance;
        return MathF.Min(distance, dst);
    }

    public Vector<float> PointByDistance(Side side, float distance) => (Plane.X * (distance * (int)side + Plane.Y)).AsVector();
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

public class Hull1D : IHull<Hull1D>, IContentHull<Hull1D, Hull0D>
{
    public readonly struct H0Transformer : ISpaceTransformer<Hull0D>
    {
        private readonly float sgn;
        private readonly Vector2 ct;
        public H0Transformer(Basis2D from, Basis2D to)
        {
            var tt = Vector2.Dot(from.Tangent, to.Tangent);
            sgn = MathF.Sign(tt);
            ct = new Vector2(from.Origin.Y * Vector2.Dot(from.Normal, to.Tangent), tt);
        }
        public readonly Hull0D Transform(Hull0D src) => new(new(sgn * src.Plane.X, Vector2.Dot(src.Plane, ct) * sgn));
        public readonly Vector2 Transform(Vector2 src) => new(sgn * src.X, Vector2.Dot(src, ct) * sgn);

        public Vector<float> Transform(Vector<float> src) => Vector2.Dot(new(1.0f, src[0]), ct).AsVector();
    }

    public Basis2D Local = default;
    public float min = -1e30f; // -float.MaxValue;
    public float max = 1e30f; // float.MaxValue;
    public bool Empty { get; private set; } = false;
    public bool HasSpace => true;
    public IList<Hull0D> Boundaries()
    {
        return new List<Hull0D>() {
            new Hull0D(new Vector2(-1, -min)),
            new Hull0D(new Vector2(1, max)),
        };
    }
    public Vector2 Center() => Pos(0.5f);
    public float LimitDistance(Side side, float distance, Hull1D other)
    {
        var c = Center();
        var dt = Vector2.Dot(Local.Normal * (int)side, other.Local.Normal);
        if (MathF.Abs(dt) < Linealg.Eps) return distance;
        var dst = (other.Local.Origin.Y - Vector2.Dot(c, other.Local.Normal)) / dt;
        if (dst < 0) return distance;
        return MathF.Min(distance, dst);
    }
    public Vector<float> PointByDistance(Side side, float distance)
    {
        var c = Center();
        return (Local.Normal * distance * (int)side + c).AsVector();
    }

    public Hull1D Coplanar(List<Hull0D> boundaries)
    {
        min = -1e30f;
        max = 1e30f;
        foreach (var b in boundaries)
        {
            if (b.Plane.X < 0)
            {
                min = MathF.Max(min, -b.Plane.Y);
            }
            else
            {
                max = MathF.Min(max, b.Plane.Y);
            }
        }
        return Coplanar(min, max);
    }
    public Hull1D Flipped => new()
    {
        Local = Local.Flipped,
        min = -max,
        max = -min,
        Empty = Empty,
    };
    private Hull1D() { }
    public Hull1D(Vector3 plane)
    {
        Local = Basis2D.TangentSpace(plane);
    }
    public Hull1D(Vector3 plane, float t0, float t1)
    {
        Local = Basis2D.TangentSpace(plane);
        min = MathF.Min(t0, t1);
        max = MathF.Max(t0, t1);
        EnsureEmpty();
    }
    public Hull1D(Vector2 v0, Vector2 v1)
    {
        var p = LinealgExtensions.MakePlane(v0, v1);
        Local = Basis2D.TangentSpace(p);
        var t0 = Local.GetTangent(v0);
        var t1 = Local.GetTangent(v1);
        min = MathF.Min(t0, t1);
        max = MathF.Max(t0, t1);
        EnsureEmpty();
    }
    public (Hull1D, ISpaceTransformer<Hull0D>) Transform(Hull1D from)
    {
        var t = new H0Transformer(from.Local, Local);
        var dst = new Hull1D()
        {
            Local = Local
        };
        dst.AddBound(t.Transform(new Vector2(-1.0f, -from.min)));
        dst.AddBound(t.Transform(new Vector2(1.0f, from.max)));
        dst.EnsureEmpty();
        return (dst, t);
    }
    public Hull1D CreateEmpty() => Coplanar(min, min);
    public Hull1D Coplanar() => new() { Local = Local };
    public void EnsureEmpty() => Empty = max - min <= Linealg.Eps;
    public Hull1D Coplanar(float t0, float t1) => new()
    {
        Local = Local,
        min = MathF.Min(t0, t1),
        max = MathF.Max(t0, t1),
        Empty = MathF.Max(t0, t1) - MathF.Min(t0, t1) <= Linealg.Eps,
    };

    public override string ToString()
    {
        return $"{Local.Plane} ({min} {max})";
    }
    public float Size() => max - min;
    public Vector2 Pos(float t) => Local.Point(max * (t) + min * (1.0f - t));
    public float DistFrom(Basis2D space) => MathF.Max(MathF.Abs(space.Dist(Local.Point(min))), MathF.Abs(space.Dist(Local.Point(max))));
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
    public void MakeEmpty()
    {
        max = 0;
        min = 0;
        EnsureEmpty();
    }
    public void AddBound(Hull1D splitter) => AddBound(Local.Project(splitter.Local.Plane));
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
    public (Hull1D Back, Hull1D Front) Split(Hull0D splitter) => Split(splitter.Plane);
    public (Hull1D? Back, Hull1D? Front) Split(Hull1D splitter)
    {
        // splitter.tangent.IsCoincident
        var cls = Local.ClassifyTo(splitter.Local);
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
    public bool IsFlip(Hull1D other) => Local.IsFlip(other.Local);
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
    public Side GetSide(Vector<float> pnt) => Local.ClassifyStrict(pnt.AsVector128().AsVector2());

    public Hull1D Copy()
    {
        return new Hull1D()
        {
            Local = Local,
            min = min,
            max = max,
            Empty = Empty,
        };
    }

    public BspTree<Hull1D, Hull0D, TContent> BuildTree<TContent>(IContentProvider<TContent> contentProvider)
    {
        if (Empty) return new BspTree<Hull1D, Hull0D, TContent>(new BspNode<Hull0D, TContent>(contentProvider.DefaultContent), this);
        return new BspTree<Hull1D, Hull0D, TContent>(BspOperationsHelper<Hull0D, TContent>.MakeNode(
                new BspTree0D<TContent>(new Vector2(-1.0f, -min)),
                BspOperationsHelper<Hull0D, TContent>.MakeNode(
                    new BspTree0D<TContent>(new Vector2(1.0f, max)),
                    new BspNode<Hull0D, TContent>(contentProvider.Content),
                    new BspNode<Hull0D, TContent>(contentProvider.DefaultContent)
                ),
                new BspNode<Hull0D, TContent>(contentProvider.DefaultContent)
            ), this);
    }
    public Side Classify(Vector2 point)
    {
        var t = Local.GetTangent(point);
        if (t < min - Linealg.Eps || t > max + Linealg.Eps) return Side.Front;
        if (t > min + Linealg.Eps && t < max - Linealg.Eps) return Side.Back;
        return Side.Incident;
    }

    public IEnumerable<Vector2> GeneratePoints()
    {
        yield return Local.Point(min);
        yield return Local.Point(max);
    }

    public Hull1D Union(Hull1D other) => Coplanar(MathF.Min(min, other.min), MathF.Max(max, other.max));
}

public class Hull2D : IHull<Hull2D>, IBoxable, IContentHull<Hull2D, Hull1D>
{
    readonly struct H1Transformer : ISpaceTransformer<Hull1D>
    {
        private static readonly Vector<float> _v0 = new(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        private static readonly Vector<float> _v1 = new(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 1.0f });
        private readonly Vector<float> transfer;
        public H1Transformer(Basis3D from, Basis3D to)
        {
            Span<float> v0 = stackalloc float[16];
            v0.Clear();
            from.Tangent.CopyTo(v0[0..4]);
            from.Binormal.CopyTo(v0[4..8]);
            var m0 = MemoryMarshal.Read<Matrix4x4>(MemoryMarshal.AsBytes(v0));
            to.Tangent.CopyTo(v0[0..4]);
            to.Binormal.CopyTo(v0[4..8]);
            to.Normal.CopyTo(v0[8..12]);
            var m1 = Matrix4x4.Multiply(m0, Matrix4x4.Transpose(MemoryMarshal.Read<Matrix4x4>(MemoryMarshal.AsBytes(v0))));
            MemoryMarshal.Write(MemoryMarshal.AsBytes(v0), ref m1);
            v0[7] = from.Origin.Z;
            transfer = new Vector<float>(v0[0..8]);
        }
        public Hull1D Transform(Hull1D src)
        {
            Span<float> s0 = stackalloc float[16];
            s0.Clear();
            src.Local.Normal.CopyTo(s0[0..2]);
            src.Local.Normal.CopyTo(s0[4..6]);
            var z = new Vector<float>(s0) * transfer;
            var n = new Vector2(Vector.Dot(z, _v0), Vector.Dot(z, _v1));
            var p = src.Local.Origin.Y * src.Local.Normal;
            p.CopyTo(s0[0..2]);
            p.CopyTo(s0[4..6]);
            s0[2] = s0[6] = transfer[7];
            z = new Vector<float>(s0) * transfer;
            p = new Vector2(Vector.Dot(z, _v0), Vector.Dot(z, _v1));
            var dst = new Hull1D(new Vector3(n, Vector2.Dot(n, p)));
            var t = new Hull1D.H0Transformer(src.Local, dst.Local);
            dst.AddBound(t.Transform(new Vector2(-1.0f, -src.min)));
            dst.AddBound(t.Transform(new Vector2(1.0f, src.max)));
            dst.EnsureEmpty();
            return dst;
        }
        public Vector<float> Transform(Vector<float> src)
        {
            Span<float> s0 = stackalloc float[Vector<float>.Count];
            src.CopyTo(s0);
            s0[2] = transfer[7];
            s0[3] = 0.0f;
            s0[0..3].CopyTo(s0[4..7]);
            var z = new Vector<float>(s0) * transfer;
            return new Vector2(Vector.Dot(z, _v0), Vector.Dot(z, _v1)).AsVector();
        }
    }
    public Basis3D Local = default;
    public List<Hull1D> Bounds = new();
    public bool Empty { get; private set; } = false;
    public bool HasSpace => true;
    public IList<Hull1D> Boundaries() => Bounds;
    public float LimitDistance(Side side, float distance, Hull2D other)
    {
        var c = Center();
        var dt = Vector3.Dot(Local.Normal * (int)side, other.Local.Normal);
        if (MathF.Abs(dt) < Linealg.Eps) return distance;
        var dst = (other.Local.Origin.Y - Vector3.Dot(c, other.Local.Normal)) / dt;
        if (dst < 0) return distance;
        return MathF.Min(distance, dst);
    }
    public Vector<float> PointByDistance(Side side, float distance)
    {
        var c = Center();
        return (Local.Normal * distance * (int)side + c).AsVector();
    }
    private Vector3 Center()
    {
        var result = Vector3.Zero;
        foreach (var bnd in Bounds)
        {
            result += Pos(bnd.Center());
        }
        return result / Bounds.Count;
    }
    public Hull2D Coplanar(List<Hull1D> boundaries)
    {
        var res = new Hull2D()
        {
            Local = Local,
            Bounds = boundaries
        };
        res.EnsureEmpty();
        return res;
    }
    private void EnsureEmpty()
    {
        Empty = Bounds.All(x => DistFrom(x.Local) < Linealg.Eps);
    }
    public Hull2D Flipped => new()
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

    public float DistFrom(Basis2D localSpace) => Bounds.Aggregate(0.0f, (s, i) => MathF.Max(s, i.DistFrom(localSpace)));
    public float DistFrom(Basis3D space) => DistFrom(Basis2D.TangentSpace(Local.Project(space.Plane)));
    public Side GetSide(Vector<float> pnt) => Local.ClassifyStrict(pnt.AsVector128().AsVector3());

    public Hull2D Intersect(Hull2D other)
    {
        var res = this.Copy();
        foreach (var b in other.Bounds)
        {
            res.AddBound(b.Local.Plane);
        }
        return res;
    }

    public Hull2D CreateEmpty() => Coplanar(new List<Hull1D>() { new Hull1D(Local.AnyLocalPlane), new Hull1D(Local.AnyLocalPlane).Flipped }, true);
    public Hull2D Copy() => new()
    {
        Local = Local,
        Bounds = Bounds.Select(x => x.Copy()).ToList(),
    };
    public Hull2D Coplanar() => new()
    {
        Local = Local,
    };
    public Hull2D Coplanar(List<Hull1D> bounds, bool empty) => new()
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
            var cls = splitterHull.Local.ClassifyTo(Bounds[i].Local);
            if (cls == Classification.Coincident)
            {
                if (Bounds[i].Local.IsFlip(splitterHull.Local))
                {
                    Bounds = new List<Hull1D>() { Bounds[i], Bounds[i].Flipped };
                    Empty = true;
                    return;
                }
                return;
            }
            if (cls == Classification.NotParallel)
            {
                splitterHull.AddBound(splitterHull.Local.Project(Bounds[i].Local.Plane));
                if (splitterHull.Empty) break;
            }
            else if (cls == Classification.Front)
            {
                splitterHull.MakeEmpty();
                break;
            }
        }
        int last = Bounds.Count - 1;
        for (int i = last; i >= 0; --i)
        {
            var cls = Bounds[i].Local.ClassifyTo(splitterHull.Local);
            if (cls == Classification.NotParallel)
                Bounds[i].AddBound(Bounds[i].Local.Project(localPlane));

            if (cls == Classification.Front || Bounds[i].Empty)
            {
                Bounds[i] = Bounds[last];
                last--;
            }
        }
        // if (last + 1 >= Bounds.Count) Bounds.RemoveRange(last + 1, Bounds.Count - last - 1);
        last += 1;
        if (last < Bounds.Count) Bounds.RemoveRange(last, Bounds.Count - last);
        if (Empty) return;
        Empty = Bounds.Count > 0 ? DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
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
    public (Hull2D Back, Hull2D Front) Split(Hull1D splitter) => Split(splitter.Local.Plane);
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
                if (Bounds[i].Local.IsFlip(splitterHull.Local))
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

        back.Empty = back.Bounds.Count > 1 ? back.DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
        if (back.Empty)
        {
            // back.bounds.RemoveRange(1, back.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.Empty)
            {
                return (back, this);
            }
        }

        front.Empty = front.Bounds.Count > 1 ? front.DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
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
        var cls = Local.ClassifyTo(splitter.Local);
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

    public AABB Box()
    {
        var box = new AABB(Bounds.Select(x => Pos(x.Pos(0.0f))));
        foreach (var b in Bounds)
        {
            box.FluentAdd(Pos(b.Pos(1.0f)));
        }
        return box;
    }

    public BspTree<Hull2D, Hull1D, TContent> BuildTree<TContent>(IContentProvider<TContent> contentProvider)
    {
        if (Empty) return new BspTree<Hull2D, Hull1D, TContent>(BspOperationsHelper<Hull1D, TContent>.MakeLeaf(contentProvider.DefaultContent), this);
        var hulls = Bounds.Select(x => x.Coplanar()).ToArray();
        for (int i = 0; i < hulls.Length; ++i)
        {
            var hi = hulls[i];
            if (hi == null || hi.Empty) continue;
            for (int j = 0; j < hulls.Length; ++j)
            {
                if (i == j) continue;
                var hj = hulls[j];
                if (hj == null || hj.Empty) continue;
                (hj, _) = hj!.Split(hi);
                hulls[j] = hj;
            }
        }
        var root = BspOperationsHelper<Hull1D, TContent>.MakeLeaf(contentProvider.Content);
        for (int i = hulls.Length; i-- > 0;)
        {
            if (hulls[i] == null || hulls[i].Empty) continue;
            root = BspOperationsHelper<Hull1D, TContent>.MakeNode(
                hulls[i].BuildTree(contentProvider.BoundaryProvider),
                root,
                BspOperationsHelper<Hull1D, TContent>.MakeLeaf(contentProvider.DefaultContent));
        }
        return new BspTree<Hull2D, Hull1D, TContent>(root, this);
    }

    public Side Classify(Vector3 point)
    {
        var loc = Local.Transform(point).XY();
        var p = loc.AsVector();
        bool allback = true;
        foreach (var i in Bounds)
        {
            var s = i.GetSide(p);
            if (s == Side.Front) return Side.Front;
            if (s != Side.Back) allback = false;
        }
        if (allback) return Side.Back;
        return Side.Incident;
    }
    public void Points(List<Vector3> dest)
    {
        Span<Vector2> points = stackalloc Vector2[Bounds.Count * 2];
        var lastIndex = 0;
        Vector2 middle = Vector2.Zero;
        foreach (var bnd in Bounds)
        {
            foreach (var p in bnd.GeneratePoints())
            {
                bool contained = false;
                for (int i = 0; i < lastIndex; ++i)
                {
                    if (Vector2.DistanceSquared(points[i], p) < Linealg.EpsSquared)
                    {
                        contained = true;
                    }
                }
                if (!contained)
                {
                    points[lastIndex] = p;
                    middle += p;
                    lastIndex++;
                }
            }
        }
        middle /= lastIndex;
        points[..lastIndex].Sort((x, y) => MathF.Atan2(x.Y - middle.Y, x.X - middle.X).CompareTo(MathF.Atan2(y.Y - middle.Y, y.X - middle.X)));

        for (int i = 0; i < lastIndex; ++i)
        {
            dest.Add(Local.Point(points[i]));
        }
    }
    private static void FindHull(Span<Vector2> points, Span<int> selection, LinkedListNode<int> c)
    {
        if (selection.Length == 0) return;
        Span<int> pos = stackalloc int[selection.Length];
        int posi = 0;
        var p0 = points[c.Value];
        var p1 = points[c.Next!.Value];
        var p = LinealgExtensions.MakePlane(p0, p1);
        int di = -1;
        float d = 0;
        for (int i = 0; i < selection.Length; ++i)
        {
            var dst = p.Dist(points[selection[i]]);
            if (dst > Linealg.Eps)
            {
                if (dst > d)
                {
                    di = posi;
                    d = dst;
                }
                pos[posi++] = selection[i];
            }
        }
        if (di < 0) return; // no positive point
        int px = pos[di];
        pos[di] = pos[--posi]; // remove px
        var cpx = c.List!.AddAfter(c, px);
        FindHull(points, pos, c);
        FindHull(points, pos, cpx);
    }
    public static LinkedList<int> QuickHull(Span<Vector2> points)
    {
        Span<int> hullPoints = stackalloc int[points.Length];
        if (points.Length < 3)
        {
            return new LinkedList<int>(Enumerable.Range(0, points.Length));
        }
        for (int i = 0; i < points.Length; ++i)
        {
            hullPoints[i] = i;
        }
        // find leftmost and rightmost
        int li = 0;
        float lx = points[0].X;
        int ri = li;
        float rx = lx;
        for (int i = 1; i < points.Length; ++i)
        {
            if (points[i].X < lx)
            {
                lx = points[i].X;
                li = i;
            }
            else if (points[i].X > rx)
            {
                rx = points[i].X;
                ri = i;
            }
        }
        if (li == ri)
        {
            li = 0;
            lx = points[0].Y;
            ri = li;
            rx = lx;
            for (int i = 1; i < points.Length; ++i)
            {
                if (points[i].X < lx)
                {
                    lx = points[i].Y;
                    li = i;
                }
                else if (points[i].Y > rx)
                {
                    rx = points[i].Y;
                    ri = i;
                }
            }
            var res = new LinkedList<int>();
            res.AddFirst(li);
            if (li != ri) res.AddLast(ri);
            return res;
        }
        // do not include selected points
        var mxl = Math.Max(li, ri);
        var mnl = Math.Min(li, ri);
        hullPoints[mxl] = hullPoints[^1];
        hullPoints[mnl] = hullPoints[^2];
        var l0 = new LinkedList<int>();
        l0.AddLast(ri);
        FindHull(points, hullPoints[..^2], l0.AddFirst(li));

        var lend = l0.Last!;
        l0.AddLast(li);
        FindHull(points, hullPoints[..^2], lend);
        l0.RemoveLast();
        return l0!;
    }
    public static Hull2D ConvexHull(Vector4 plane, Span<Vector3> points)
    {
        var result = new Hull2D(plane);
        Span<Vector2> locs = stackalloc Vector2[points.Length];
        for (int j = 0; j < points.Length; ++j)
        {
            locs[j] = result.Local.TransformNormal(points[j]).XY();
        }
        var contour = QuickHull(locs);
        var cnt = contour.Count;
        if (cnt < 2 || contour.First == null) return result.CreateEmpty();
        var pre = contour.Last!;
        var cur = contour.First!;
        var nxt = cur.Next!;
        if (cnt == 2)
        {
            if (Vector2.DistanceSquared(locs[contour.First!.Value], locs[contour.Last!.Value]) < Linealg.EpsSquared)
                return result.CreateEmpty();
        }
        else
        {
            for (int i = 0; i < cnt; ++i)
            {
                var v0 = locs[pre.Value];
                var v1 = locs[nxt.Value];
                var v = locs[cur.Value];
                // not corrected to vector length
                if (MathF.Abs(Vector2.Dot((v1 - v0).Ort(), v - v0)) < Linealg.Eps)
                {
                    contour.Remove(cur);
                }
                else
                {
                    pre = cur;
                }
                cur = nxt;
                nxt = nxt.Next ?? contour.First;
            }
        }
        cnt = contour.Count;
        if (cnt < 2 || contour.First == null) return result.CreateEmpty();
        cur = contour.First;
        while (cur.Next != null)
        {
            result.Bounds.Add(new Hull1D(locs[cur.Value], locs[cur.Next.Value]));
            cur = cur.Next;
        }
        result.Bounds.Add(new Hull1D(locs[cur.Value], locs[contour.First.Value]));
        result.EnsureEmpty();
        return result;
    }

    public static Hull2D ConvexHull(Vector4 plane, ICollection<Vector3> points)
    {
        Span<Vector3> ps = stackalloc Vector3[points.Count];
        var i = 0;
        foreach (var p in points) ps[i++] = p;
        return ConvexHull(plane, ps);
    }

    public void WritePoints(Span<Vector3> points)
    {
        var i = 0;
        foreach (var b in Bounds)
        {
            foreach (var p in b.GeneratePoints())
            {
                points[i++] = Pos(p);
            }
        }
    }
    public IEnumerable<Vector3> GeneratePoints()
    {
        foreach (var b in Bounds)
            foreach (var p in b.GeneratePoints())
                yield return Pos(p);
    }

    public Hull2D Union(Hull2D other)
    {
        Span<Vector3> points = stackalloc Vector3[Bounds.Count * 2 + other.Bounds.Count * 2];
        WritePoints(points[..(Bounds.Count * 2)]);
        other.WritePoints(points[(Bounds.Count * 2)..]);
        return ConvexHull(Local.Plane, points);
    }

    public (Hull2D, ISpaceTransformer<Hull1D>) Transform(Hull2D from)
    {
        var t = new H1Transformer(from.Local, Local);
        var h = new Hull2D()
        {
            Local = Local,
            Bounds = Bounds.Select(x => t.Transform(x)).ToList(),
            Empty = Empty
        };
        return (h, t);
    }
}

public class Hull3D : IHull<Hull3D>, IBoxable, IContentHull<Hull3D, Hull2D>
{
    class H2Transformer : ISpaceTransformer<Hull2D>
    {
        public Hull2D Transform(Hull2D src) => src;
        public Vector<float> Transform(Vector<float> src) => src;

    }
    public bool HasSpace => true;
    public List<Hull2D> Bounds = new();
    public bool Empty { get; private set; } = false;

    public Hull3D Flipped => throw new AssertionException("Unable to flip Hull3D");

    public Hull3D Copy() => new()
    {
        Bounds = Bounds.Select(x => x.Copy()).ToList(),
        Empty = Empty,
    };

    public Side GetSide(Vector<float> point) => throw new AssertionException("Unable to GetSide Hull3D");
    public bool IsFlip(Hull3D other) => throw new AssertionException("Unable to check flip of Hull3D");
    public AABB Box() => new(Bounds.Select(x => x.Box()));

    public Hull3D Coplanar(List<Hull2D> boundaries)
    {
        var res = new Hull3D()
        {
            Bounds = boundaries
        };
        res.EnsureEmpty();
        return res;
    }
    private void EnsureEmpty()
    {
        Empty = Bounds.All(x => DistFrom(x.Local) > Linealg.Eps);
    }

    public Hull3D Intersect(Hull3D other)
    {
        var res = this.Copy();
        foreach (var b in other.Bounds)
        {
            res.AddBound(b.Local.Plane);
        }
        return res;
    }

    // public Hull3D CreateEmpty() => Coplanar(new List<Hull1D>() { new Hull1D(Local.AnyLocalPlane), new Hull1D(Local.AnyLocalPlane).Flipped }, true);
    public Hull3D Coplanar() => new() { };
    public static Hull3D Coplanar(List<Hull2D> bounds, bool empty) => new()
    {
        Bounds = bounds,
        Empty = empty,
    };
    public float DistFrom(Basis3D localSpace) => Bounds.Aggregate(0.0f, (s, i) => MathF.Max(s, i.DistFrom(localSpace)));

    public override string ToString() => $"3D: {String.Join(", ", Bounds.Select(x => x.ToString()))}";
    public void AddBound(Vector4 localPlane)
    {
        var splitterHull = new Hull2D(localPlane);
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < Bounds.Count; ++i)
        {
            splitterHull.AddBound(splitterHull.Local.Project(Bounds[i].Local.Plane));
            if (splitterHull.Empty) break;
        }
        int last = Bounds.Count - 1;
        for (int i = last; i >= 0; --i)
        {
            Bounds[i].AddBound(Bounds[i].Local.Project(localPlane));
            if (Bounds[i].Empty)
            {
                Bounds[i] = Bounds[last];
                last--;
            }
        }
        if (last + 1 >= Bounds.Count) Bounds.RemoveRange(last + 1, Bounds.Count - last - 1);
        Empty = Bounds.Count > 0 ? DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
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
    public (Hull3D Back, Hull3D Front) Split(Hull2D splitter) => Split(splitter.Local.Plane);
    public (Hull3D Back, Hull3D Front) Split(Vector4 localPlane)
    {
        var splitterHull = new Hull2D(localPlane);
        // var tangent = splitterHull.tangent;
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (hullb, hullf) = splitterHull.Split(Bounds[i]);
            if (hullb == null)
            {
                // planes are parallel
                var (b, f) = Bounds[i].Split(splitterHull);
                if (b == null) return (Coplanar(new List<Hull2D>() { Bounds[i], Bounds[i].Flipped }, true), this);
                if (f == null) return (this, Coplanar(new List<Hull2D>() { Bounds[i].Flipped, Bounds[i] }, true));
                // // 
                throw new AssertionException();
            }
            if (hullb == hullf)
            {
                if (Bounds[i].Local.IsFlip(splitterHull.Local))
                {
                    return (Coplanar(new List<Hull2D>() { Bounds[i], Bounds[i].Flipped }, true), this);
                }
                return (this, Coplanar(new List<Hull2D>() { Bounds[i].Flipped, Bounds[i] }, true));
            }
            splitterHull = hullb;
        }
        var back = Coplanar(new List<Hull2D>(Bounds.Count + 1) { splitterHull }, true);
        var front = Coplanar(new List<Hull2D>(Bounds.Count + 1) { splitterHull.Flipped }, true);
        for (int i = 0; i < Bounds.Count; ++i)
        {
            var (b, f) = Bounds[i].Split(splitterHull);
            if (b != null && !b.Empty) back.Bounds.Add(b);
            if (f != null && !f.Empty) front.Bounds.Add(f);
        }

        back.Empty = back.Bounds.Count > 1 ? back.DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
        if (back.Empty)
        {
            // back.bounds.RemoveRange(1, back.bounds.Count - 1); // add flipped splitter instead?
            if (splitterHull.Empty)
            {
                return (back, this);
            }
        }

        front.Empty = front.Bounds.Count > 1 ? front.DistFrom(splitterHull.Local) <= Linealg.Eps : splitterHull.Empty;
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
    public (Hull3D? Back, Hull3D? Front) Split(Hull3D splitter) => throw new AssertionException("Unable to split with Hull3D");

    public BspTree<Hull3D, Hull2D, TContent> BuildTree<TContent>(IContentProvider<TContent> contentProvider)
    {
        if (Empty) return new BspTree<Hull3D, Hull2D, TContent>(BspOperationsHelper<Hull2D, TContent>.MakeLeaf(contentProvider.DefaultContent), this);
        var hulls = Bounds.Select(x => x.Copy()).ToArray();
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
        var root = BspOperationsHelper<Hull2D, TContent>.MakeLeaf(contentProvider.Content);
        for (int i = hulls.Length; i-- > 0;)
        {
            if (hulls[i] == null || hulls[i].Empty) continue;
            root = BspOperationsHelper<Hull2D, TContent>.MakeNode(
                hulls[i].BuildTree(contentProvider.BoundaryProvider),
                root,
                BspOperationsHelper<Hull2D, TContent>.MakeLeaf(contentProvider.DefaultContent));
        }
        return new BspTree<Hull3D, Hull2D, TContent>(root, this);
    }
    public Side Classify(Vector3 point)
    {
        var p = point.AsVector();
        bool allback = true;
        foreach (var i in Bounds)
        {
            var s = i.GetSide(p);
            if (s == Side.Front) return Side.Front;
            if (s != Side.Back) allback = false;
        }
        if (allback) return Side.Back;
        return Side.Incident;
    }

    public Hull3D Union(Hull3D other)
    {
        var result = Coplanar();
        foreach (var b in Bounds)
        {
            bool allin = true;
            foreach (var ob in other.Bounds)
            {
                foreach (var p in ob.GeneratePoints())
                {
                    if (b.GetSide(p.AsVector()) == Side.Front)
                    {
                        allin = false;
                        break;
                    }
                }
            }
            if (allin) result.AddBound(b.Local.Plane);
        }
        foreach (var b in other.Bounds)
        {
            bool allin = true;
            foreach (var ob in Bounds)
            {
                foreach (var p in ob.GeneratePoints())
                {
                    if (b.GetSide(p.AsVector()) == Side.Front)
                    {
                        allin = false;
                        break;
                    }
                }
            }
            if (allin) result.AddBound(b.Local.Plane);
        }
        return result;
    }

    public IList<Hull2D> Boundaries() => Bounds;
    public (Hull3D, ISpaceTransformer<Hull2D>) Transform(Hull3D from) => (this, new H2Transformer());
    public float LimitDistance(Side side, float distance, Hull3D other) => throw new NotImplementedException();
    public Vector<float> PointByDistance(Side side, float distance) => throw new NotImplementedException();
}