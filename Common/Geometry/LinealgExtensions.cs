using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Bsp.Common.Geometry;

[Flags]
public enum Classification
{
    Back = 0x1,
    Front = 0x2,
    NotParallel = 0x3,
    Coincident = 0x0
}
public enum Side
{
    Back = -1,
    Front = 1,
    Incident = 0,
}

// public class BasisND
// {
//     public static Vector<float> V0;
//     public static Vector<float> V1;
//     public static Vector<float> V2;
//     public static Vector<float> V3;
//     public static Vector<float> _V3;
//     static BasisND()
//     {
//         Span<float> v = stackalloc float[Vector<float>.Count];
//         v.Slice(0, 4).Fill(1.0f);
//         V0 = new Vector<float>(v);
//         v.Slice(0, 4).Fill(0.0f);
//         v.Slice(4, 8).Fill(1.0f);
//         V1 = new Vector<float>(v);
//         v.Slice(4, 8).Fill(0.0f);
//         v.Slice(8, 12).Fill(1.0f);
//         V2 = new Vector<float>(v);
//         v.Slice(8, 12).Fill(0.0f);
//         v.Slice(12, 16).Fill(1.0f);
//         V3 = new Vector<float>(v);
//         v.Slice(0, 16).Fill(0.0f);
//         v.Slice(0, 12).Fill(1.0f);
//         _V3 = new Vector<float>(v);
//     }
//     public Vector<float> Basis;
//     public int Dimension;
//     public Vector4 TransformNormal(Vector4 normal)
//     {
//         var v = Vector.Multiply(Basis, normal.AsVector128().AsVector<float>());
//         return new Vector4(Vector.Dot(v, V0), Vector.Dot(v, V1), Vector.Dot(v, V2), 0.0f);
//     }
//     public Vector4 Transform(Vector4 position)
//     {
//         var v = Vector.Multiply(Basis, position.AsVector128().AsVector<float>());
//         return new Vector4(Vector.Dot(v, V0), Vector.Dot(v, V1), Vector.Dot(v, V2), 0.0f) - Vector.
//     }
// }

public struct Basis3D
{
    public Vector3 Tangent = Vector3.UnitX; //{ get; init; }
    public Vector3 Binormal = Vector3.UnitY; //{ get; init; }
    public Vector3 Normal = Vector3.UnitZ; //{ get; init; }
    public Vector3 Origin = Vector3.Zero; //{ get; init; }

    public Basis3D() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 TransformNormal(Vector3 normal) =>
        new(Vector3.Dot(normal, Tangent), Vector3.Dot(normal, Binormal), Vector3.Dot(normal, Normal));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 TransformNormal(Vector4 normal) => TransformNormal(normal.XYZ());
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Transform(Vector3 pos) => TransformNormal(pos) - Origin;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Transform(Vector4 pos) => Transform(pos.XYZ());

    public Vector4 Plane => new(Normal, Origin.Z);
    public Basis3D Flipped => new()
    {
        Tangent = -Tangent,
        Binormal = -Binormal,
        Normal = -Normal,
        Origin = -Origin,
    };

    public Vector3 AnyLocalPlane = new(1.0f, 0.0f, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFlip(Basis3D other) => Vector3.Dot(Normal, other.Normal) < 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Plane2D(Vector3 v0, Vector3 v1, out float t0, out float t1)
    {
        // plane: (n, x) - w = 0
        var dn = Vector3.Cross(v1 - v0, Normal);
        var dnw = Vector3.Dot(dn, v0);
        var p = Project(new Vector4(dn, dnw));
        t0 = GetPos(p, v0);
        t1 = GetPos(p, v1);
        return p;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float GetPos(Vector3 plane, Vector3 pos) =>
        Vector2.Dot(new Vector2(Vector3.Dot(pos, Tangent),  // - plane.Z * plane.X,
                                Vector3.Dot(pos, Binormal)),// - plane.Z * plane.Y),
                    new Vector2(-plane.Y, plane.X));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Basis3D TangentSpace(Vector4 plane)
    {
        Vector3 t = Vector3.Normalize(
            plane.X * plane.X < 0.5f
            ? new Vector3(0.0f, plane.Z, -plane.Y)
            : new Vector3(plane.Z, 0.0f, -plane.X)
        );
        var n = plane.XYZ();
        // var p = n.Length();
        // n /= p;
        var b = Vector3.Cross(n, t);
        return new Basis3D()
        {
            Tangent = t,
            Binormal = b,
            Normal = n,
            Origin = plane.W * Vector3.UnitZ // / p
        };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vector3 Project(Vector4 plane)
    {
        var pn = TransformNormal(plane);
        var nrm = pn.XY().Length();
        pn = pn / nrm;
        var pw = plane.W / nrm - Vector3.Dot(Origin, pn); // or plane.W / nrm - Origin.Z * pn.Z
        return new Vector3(pn.X, pn.Y, pw);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Point(float u, float v) => u * Tangent + v * Binormal + Origin.Z * Normal;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 Point(Vector2 uv) => Point(uv.X, uv.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dist(Vector3 x) => Vector3.Dot(Normal, x) - Origin.Z;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Side Classify(Vector3 point)
    {
        var x = Dist(point);
        if (x > Linealg.Eps) return Side.Front;
        if (x < -Linealg.Eps) return Side.Back;
        return Side.Incident;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Side ClassifyStrict(Vector3 point)
    {
        var x = Dist(point);
        if (x < 0.0f) return Side.Back;
        return Side.Front;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Classification ClassifyTo(Basis3D splitter)
    {
        // 1 to 10000 precision comparison
        var nn = Vector3.Dot(splitter.Normal, Normal);
        if (1.0f - MathF.Abs(nn) > Linealg.Eps) return Classification.NotParallel;
        var step = 100.0f;
        var w = new Vector4(splitter.Origin.Z, splitter.Origin.Z, splitter.Origin.Z, splitter.Origin.Z);
        var o = new Vector4(Origin.Z, Origin.Z, Origin.Z, Origin.Z) * nn;
        var nt = Vector3.Dot(splitter.Normal, Tangent);
        var nb = Vector3.Dot(splitter.Normal, Binormal);
        var x = (step * new Vector4(nt, nb, nt, nb) + o - w).AsVector128().AsVector<float>();
        return Linealg.Classify(x, 4);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Classification ClassifyTo(Vector4 splitterPlane)
    {
        // 1 to 10000 precision comparison
        var norm = splitterPlane.XYZ();
        var nn = Vector3.Dot(norm, Normal);
        if (1.0f - MathF.Abs(nn) > Linealg.Eps) return Classification.NotParallel;
        var step = 100.0f;

        var w = new Vector4(splitterPlane.W, splitterPlane.W, splitterPlane.W, splitterPlane.W);
        var o = new Vector4(Origin.Z, Origin.Z, Origin.Z, Origin.Z) * nn;
        var nt = Vector3.Dot(norm, Tangent);
        var nb = Vector3.Dot(norm, Binormal);
        var x = (step * new Vector4(nt, nb, -nt, -nb) + o - w).AsVector128().AsVector<float>();
        return Linealg.Classify(x, 4);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCoincident(Vector4 plane) => (ClassifyTo(plane) & Classification.NotParallel) == 0;
}

public struct Basis2D
{
    public Vector2 Tangent = Vector2.UnitY;
    public Vector2 Normal = Vector2.UnitX;
    public Vector2 Origin = Vector2.Zero;
    public Basis2D() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 TransformNormal(Vector2 normal) =>
        new(Vector2.Dot(normal, Tangent), Vector2.Dot(normal, Normal));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 TransformNormal(Vector3 normal) => TransformNormal(normal.XY());
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Transform(Vector2 pos) => TransformNormal(pos) - Origin;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Transform(Vector3 pos) => Transform(pos.XY());

    public Vector2 AnyLocalPlane = new(1.0f, 0.0f);
    public Vector3 Plane => new(Normal, Origin.Y);
    public Basis2D Flipped => new()
    {
        Tangent = -Tangent,
        Normal = -Normal,
        Origin = -Origin,
    };

    public float GetTangent(Vector2 global) => Vector2.Dot(Tangent, global);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Basis2D TangentSpace(Vector3 plane)
    {
        var n = plane.XY();
        var p = n.Length();
        n /= p;
        return new Basis2D()
        {
            Tangent = n.Ort(), //new Vector2(-n.Y, n.X),
            Normal = n,
            Origin = plane.Z / p * Vector2.UnitY
        };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFlip(Basis2D other) => Vector2.Dot(Normal, other.Normal) < 0.0f;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Point(float t) => Normal * Origin.Y + Tangent * t;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dist(Vector2 x) => Vector2.Dot(Normal, x) - Origin.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 Project(Vector3 plane)
    {
        var den = Vector2.Dot(Tangent, plane.XY());
        var t = Vector2.Dot(new Vector2(plane.Z, Origin.Y), new Vector2(1.0f, -Vector2.Dot(Normal, plane.XY()))) / den;
        return den > 0 ? new Vector2(1.0f, t) : new Vector2(-1.0f, -t);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Side Classify(Vector2 point)
    {
        var x = Dist(point);
        if (x > Linealg.Eps) return Side.Front;
        if (x < -Linealg.Eps) return Side.Back;
        return Side.Incident;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Side ClassifyStrict(Vector2 point)
    {
        var x = Dist(point);
        if (x < 0.0f) return Side.Back;
        return Side.Front;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Classification ClassifyTo(Basis2D splitter)
    {
        // 1 to 10000 precision comparison
        var nn = Vector2.Dot(splitter.Normal, Normal);
        if (1.0f - MathF.Abs(nn) > Linealg.Eps) return Classification.NotParallel;
        var step = 100.0f;
        var w = new Vector2(splitter.Origin.Y, splitter.Origin.Y);
        var o = new Vector2(Origin.Y, Origin.Y) * nn;
        var nt = Vector2.Dot(splitter.Normal, Tangent);
        var x = (step * new Vector2(nt, -nt) + o - w).AsVector128().AsVector();
        return Linealg.Classify(x, 2);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Classification ClassifyTo(Vector3 splitterPlane)
    {
        // 1 to 10000 precision comparison
        var norm = splitterPlane.XY();
        var nn = Vector2.Dot(norm, Normal);
        if (1.0f - MathF.Abs(nn) > Linealg.Eps) return Classification.NotParallel;
        var step = 100.0f;
        var w = new Vector2(splitterPlane.Z, splitterPlane.Z);
        var o = new Vector2(Origin.Y, Origin.Y) * nn;
        var nt = Vector2.Dot(norm, Tangent);
        var x = (step * new Vector2(nt, -nt) + o - w).AsVector128().AsVector();
        return Linealg.Classify(x, 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCoincident(Vector3 plane) => (ClassifyTo(plane) & Classification.NotParallel) == 0;
}

public static class LinealgExtensions
{
    public static Vector3 XYZ(this Vector4 self) => self.AsVector128().AsVector3();
    public static Vector2 XY(this Vector4 self) => self.AsVector128().AsVector2();
    public static Vector2 XY(this Vector3 self) => self.AsVector128().AsVector2();
    public static Vector3 NextVector3(this Random self) =>
        new(self.NextSingle(),
             self.NextSingle(),
             self.NextSingle());
    public static Vector4 NextVector4(this Random self) =>
        new(self.NextSingle(),
             self.NextSingle(),
             self.NextSingle(),
             self.NextSingle());
    public static Vector3 NextVector3Direction(this Random self)
    {
        var phi = self.NextSingle() * 2.0f * MathF.PI;
        var tht = MathF.Acos(self.NextSingle() * 1.99999f - 1.0f);
        var (sp, cp) = MathF.SinCos(phi);
        var (st, ct) = MathF.SinCos(tht);
        return new Vector3(cp * st, sp * st, ct);
    }

    public static Vector3 Round(this Vector3 self, float cell_size = 1e-2f)
    {
        return new(
            (long)(self.X * 1.0 / cell_size) * cell_size,
            (long)(self.Y * 1.0 / cell_size) * cell_size,
            (long)(self.Z * 1.0 / cell_size) * cell_size
        );
    }
    public static Vector4 Round(this Vector4 self, float cell_size = 1e-2f)
    {
        return new Vector4(
            (long)(self.X * 1.0 / cell_size) * cell_size,
            (long)(self.Y * 1.0 / cell_size) * cell_size,
            (long)(self.Z * 1.0 / cell_size) * cell_size,
            (long)(self.W * 1.0 / cell_size) * cell_size
        );
    }
    public static Vector4 Plane(this Vector3 normal, float d) => new Vector4(normal, d) / normal.Length();
    public static Vector4 Plane(this Vector3 normal, Vector3 point) => normal.Plane(Vector3.Dot(normal, point));
    public static Vector3 Plane(this Vector2 normal, float d) => new Vector3(normal, d) / normal.Length();
    public static Vector3 Plane(this Vector2 normal, Vector2 point) => normal.Plane(Vector2.Dot(normal, point));

    public static float Dist(this Vector3 plane, Vector2 point) => Vector2.Dot(plane.XY(), point) - plane.Z;
    public static float Dist(this Vector4 plane, Vector3 point) => Vector3.Dot(plane.XYZ(), point) - plane.W;

    public static Vector3 MakePlane(Vector2 v0, Vector2 v1)
    {
        var t = Vector2.Normalize(v1 - v0);
        return new Vector2(t.Y, -t.X).Plane(v0);
    }
    public static Vector2 Ort(this Vector2 v) => new(-v.Y, v.X);
    public static Vector4 MakePlane(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var n = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
        return n.Plane(v0);
    }
    public static Vector<float> AsVector(this float self)
    {
        Span<float> v = stackalloc float[Vector<float>.Count];
        v[0] = self;
        return new Vector<float>(v);
    }
    public static Vector<float> AsVector(this Vector2 self) => self.AsVector128().AsVector<float>();
    public static Vector<float> AsVector(this Vector3 self) => self.AsVector128().AsVector<float>();
    public static Vector<float> AsVector(this Vector4 self) => self.AsVector128().AsVector<float>();
}
public static class Linealg
{
    public static readonly float Eps = 1e-2f;
    public static readonly float EpsSquared = Eps * Eps;
    public static readonly Vector<float> EpsVec = (Vector4.One * Eps).AsVector128().AsVector<float>();
    public static readonly float DecentDistance = 32f;
    public static readonly Vector3 DecentVolume = new(DecentDistance, DecentDistance, DecentDistance);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Classification Classify(Vector<float> disposition, int dim)
    {
        Span<float> s = stackalloc float[Vector<float>.Count];
        s[..dim].Fill(disposition[0]);
        s[dim..].Clear();
        bool isParallel = Vector.LessThanOrEqualAll(Vector.Abs(new Vector<float>(s) - disposition), EpsVec);
        if (!isParallel) return Classification.NotParallel;
        bool hasBack = Vector.LessThanAny(disposition, -EpsVec);
        bool hasFront = Vector.GreaterThanAny(disposition, EpsVec);
        Classification res = 0;
        if (hasBack) res |= Classification.Back;
        if (hasFront) res |= Classification.Front;
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Plane(Vector3 v0, Vector3 v1, Vector3 v2) => Vector3.Cross(v1 - v0, v2 - v1).Plane(v0);
}