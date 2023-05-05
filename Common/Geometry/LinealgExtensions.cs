using System.Numerics;
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

public class Basis3D
{
    public Vector3 Tangent; //{ get; init; }
    public Vector3 Binormal; //{ get; init; }
    public Vector3 Normal; //{ get; init; }
    public Vector3 Origin; //{ get; init; }

    public Vector3 TransformNormal(Vector3 normal) =>
        new Vector3(Vector3.Dot(normal, Tangent), Vector3.Dot(normal, Binormal), Vector3.Dot(normal, Normal));
    public Vector3 TransformNormal(Vector4 normal) => TransformNormal(normal.XYZ());
    public Vector3 Transform(Vector3 pos) => TransformNormal(pos) - Origin;
    public Vector3 Transform(Vector4 pos) => Transform(pos.XYZ());

    public Vector4 Plane => new Vector4(Normal, Origin.Z);
    public Basis3D Flipped => new Basis3D()
    {
        Tangent = -Tangent,
        Binormal = -Binormal,
        Normal = -Normal,
        Origin = -Origin,
    };

    public bool IsFlip(Basis3D other) => Vector3.Dot(Normal, other.Normal) < 0.0f;

    public Vector3 Plane2D(Vector3 v0, Vector3 v1, out float t0, out float t1)
    {
        // plane: (n, x) - w = 0
        var p0 = Transform(v0);
        var p1 = Transform(v1);
        var a = Vector3.Normalize(p1 - p0);
        var n = new Vector3(a.Y, -a.X, 0); // [a, <0,0,1>]
        var w = Vector3.Dot(n, p0);
        var a0 = w * n;
        t0 = Vector3.Dot(p0 - a0, a);
        t1 = Vector3.Dot(p1 - a0, a);
        return n + Vector3.UnitZ * w;
    }
    public Vector3 Plane2D2(Vector3 v0, Vector3 v1, out float t0, out float t1)
    {
        // plane: (n, x) - w = 0
        var dn = Vector3.Cross(v1 - v0, Normal);
        var dnw = Vector3.Dot(dn, v0);
        var p = Project(new Vector4(dn, dnw));
        t0 = GetPos(p, v0);
        t1 = GetPos(p, v1);
        return p;
    }
    public float GetPos(Vector3 plane, Vector3 pos) =>
        Vector2.Dot(new Vector2(Vector3.Dot(pos, Tangent),  // - plane.Z * plane.X,
                                Vector3.Dot(pos, Binormal)),// - plane.Z * plane.Y),
                    new Vector2(-plane.Y, plane.X));

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

    public Vector3 Project(Vector4 plane)
    {
        var pn = TransformNormal(plane);
        var nrm = pn.XY().Length();
        pn = pn / nrm;
        var pw = plane.W / nrm - Vector3.Dot(Origin, pn); // or plane.W / nrm - Origin.Z * pn.Z
        return new Vector3(pn.X, pn.Y, pw);
    }
    public Vector3 Point(float u, float v) => u * Tangent + v * Binormal;
    public Vector3 Point(Vector2 uv) => Point(uv.X, uv.Y);
    public float Dist(Vector3 x) => Vector3.Dot(Normal, x) - Origin.Z;
    public Side Classify(Vector3 point)
    {
        var x = Dist(point);
        if (x > Linealg.Eps) return Side.Front;
        if (x < -Linealg.Eps) return Side.Back;
        return Side.Incident;
    }
    public Side ClassifyStrict(Vector3 point)
    {
        var x = Dist(point);
        if (x < 0.0f) return Side.Back;
        return Side.Front;
    }

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
    public bool IsCoincident(Vector4 plane) => (ClassifyTo(plane) & Classification.NotParallel) == 0;
}

public class Basis2D
{
    public Vector2 Tangent;
    public Vector2 Normal;
    public Vector2 Origin;
    public Vector2 TransformNormal(Vector2 normal) =>
        new Vector2(Vector2.Dot(normal, Tangent), Vector2.Dot(normal, Normal));
    public Vector2 TransformNormal(Vector3 normal) => TransformNormal(normal.XY());
    public Vector2 Transform(Vector2 pos) => TransformNormal(pos) - Origin;
    public Vector2 Transform(Vector3 pos) => Transform(pos.XY());

    public Vector3 Plane => new Vector3(Normal, Origin.Y);
    public Basis2D Flipped => new Basis2D()
    {
        Tangent = -Tangent,
        Normal = -Normal,
        Origin = -Origin,
    };

    public static Basis2D TangentSpace(Vector3 plane)
    {
        var n = plane.XY();
        var p = n.Length();
        n /= p;
        return new Basis2D()
        {
            Tangent = new Vector2(-n.Y, n.X),
            Normal = n,
            Origin = plane.Z / p * Vector2.UnitY
        };
    }
    public bool IsFlip(Basis2D other) => Vector2.Dot(Normal, other.Normal) < 0.0f;
    public Vector2 Point(float t) => Normal * Origin.Y + Tangent * t;
    public float Dist(Vector2 x) => Vector2.Dot(Normal, x) - Origin.Y;

    public Vector2 Project(Vector3 plane)
    {
        var den = Vector2.Dot(Tangent, plane.XY());
        var t = Vector2.Dot(new Vector2(plane.Z, Origin.Y), new Vector2(1.0f, -Vector2.Dot(Normal, plane.XY()))) / den;
        return den > 0 ? new Vector2(1.0f, t) : new Vector2(-1.0f, -t);
    }
    public Side Classify(Vector2 point)
    {
        var x = Dist(point);
        if (x > Linealg.Eps) return Side.Front;
        if (x < -Linealg.Eps) return Side.Back;
        return Side.Incident;
    }
    public Side ClassifyStrict(Vector2 point)
    {
        var x = Dist(point);
        if (x < 0.0f) return Side.Back;
        return Side.Front;
    }
    public Classification ClassifyTo(Basis2D splitter)
    {
        // 1 to 10000 precision comparison
        var nn = Vector2.Dot(splitter.Normal, Normal);
        if (1.0f - MathF.Abs(nn) > Linealg.Eps) return Classification.NotParallel;
        var step = 100.0f;
        var w = new Vector2(splitter.Origin.Y, splitter.Origin.Y);
        var o = new Vector2(Origin.Y, Origin.Y) * nn;
        var nt = Vector2.Dot(splitter.Normal, Tangent);
        var x = (step * new Vector2(nt, -nt) + o - w).AsVector128().AsVector<float>();
        return Linealg.Classify(x, 2);
    }
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
        var x = (step * new Vector2(nt, -nt) + o - w).AsVector128().AsVector<float>();
        return Linealg.Classify(x, 2);
    }

    public bool IsCoincident(Vector3 plane) => (ClassifyTo(plane) & Classification.NotParallel) == 0;
}

public static class LinealgExtensions
{
    public static Vector3 XYZ(this Vector4 self) => self.AsVector128().AsVector3();
    public static Vector2 XY(this Vector4 self) => self.AsVector128().AsVector2();
    public static Vector2 XY(this Vector3 self) => self.AsVector128().AsVector2();
    public static Vector3 NextVector3(this Random self) =>
        new Vector3(self.NextSingle(),
                    self.NextSingle(),
                    self.NextSingle());
    public static Vector4 NextVector4(this Random self) =>
        new Vector4(self.NextSingle(),
                    self.NextSingle(),
                    self.NextSingle(),
                    self.NextSingle());
    public static Vector3 NextVector3Direction(this Random self)
    {
        var phi = self.NextSingle() * 2.0f * MathF.PI;
        var tht = MathF.Acos(self.NextSingle() * 1.99999f - 1.0f);
        var (sp, cp) = MathF.SinCos(phi);
        var (st, ct) = MathF.SinCos(phi);
        return new Vector3(cp * st, sp * st, ct);
    }

    public static Vector3 Round(this Vector3 self, float cell_size = 1e-2f)
    {
        return new Vector3(
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
}
public static class Linealg
{
    public static float Eps = 1e-2f;
    public static Vector<float> EpsVec = (Vector4.One * Eps).AsVector128().AsVector<float>();
    public static Classification Classify(Vector<float> disposition, int dim)
    {
        Span<float> s = stackalloc float[Vector<float>.Count];
        s.Slice(0, dim).Fill(disposition[0]);
        s.Slice(dim).Fill(0.0f);
        bool isParallel = Vector.LessThanOrEqualAll(Vector.Abs(new Vector<float>(s) - disposition), Linealg.EpsVec);
        if (!isParallel) return Classification.NotParallel;
        bool hasBack = Vector.LessThanAny(disposition, -Linealg.EpsVec);
        bool hasFront = Vector.GreaterThanAny(disposition, Linealg.EpsVec);
        Classification res = 0;
        if (hasBack) res |= Classification.Back;
        if (hasFront) res |= Classification.Front;
        return res;
    }

    public static Vector4 Plane(Vector3 v0, Vector3 v1, Vector3 v2) => Vector3.Cross(v1 - v0, v2 - v1).Plane(v0);
}