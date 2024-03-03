using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;

namespace Bsp.BspFormat;

public class EmptyEntity
{
    public Vector3 Origin;
    public Dictionary<string, string> Properties = new();
}

public interface IPlane2D
{
    Vector2 Normal { get; }
    Vector2 Tangent { get; }
    Vector2 Point { get; }
}

public interface IPlane3D
{
    Vector3 Normal { get; }
    Vector3 Tangent { get; }
    Vector3 Binormal { get; }
    // float Displacement { get; }
    Vector3 Point { get; }
    IPlane3D Opposite { get; }
    bool IsOpposite { get; }
}

public static class LinealgExtensions
{
    public const float Eps = 1e-2f;
    public const float EpsAngle = Eps * Eps;
    public const float EpsSq = 1e-4f;
    public const float Big = 1e5f;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EpsEq(this float self, float other)
    {
        return MathF.Abs(self - other) < Eps;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (Vector3, Vector3) TangentSpace(this Vector3 normal)
    {
        Vector3 t = Vector3.Normalize(
            normal.X * normal.X < 0.5f
            ? new Vector3(0.0f, normal.Z, -normal.Y)
            : new Vector3(normal.Z, 0.0f, -normal.X)
        );
        var b = Vector3.Cross(normal, t);
        return (t, b);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 TangentSpace(this Vector2 normal)
    {
        return new Vector2(normal.Y, -normal.X);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Local2D(this Vector3 pos, Vector3 normal, Vector3 tangent, Vector3 binormal, float disp)
    {
        var v = pos - disp * normal;
        return new Vector2(Vector3.Dot(v, tangent), Vector3.Dot(v, binormal));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Local2D(this Vector3 pos, Vector3 normal, Vector3 tangent, Vector3 binormal, Vector3 point)
    {
        var v = pos - point;
        return new Vector2(Vector3.Dot(v, tangent), Vector3.Dot(v, binormal));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Global2D(this Vector2 pos, Vector3 normal, Vector3 tangent, Vector3 binormal, float disp)
    {
        return pos.X * tangent + pos.Y * binormal + normal * disp;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Global2D(this Vector2 pos, Vector3 normal, Vector3 tangent, Vector3 binormal, Vector3 point)
    {
        return pos.X * tangent + pos.Y * binormal + point;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 XYZ(this Vector4 self) => self.AsVector128().AsVector3();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 XY(this Vector4 self) => self.AsVector128().AsVector2();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 XY(this Vector3 self) => self.AsVector128().AsVector2();

    public static Vector4 ToPlane(this Vector4 x) => x / x.XYZ().Length();
    public static Vector4 ToPlane(this Vector3 normal, Vector3 point)
    {
        var n = Vector3.Normalize(normal);
        return new Vector4(n, Vector3.Dot(n, point));
    }
    public static (Vector3 Normal, Vector3 Point) ToPlane(this Span<Vector3> face) => ToPlane((ReadOnlySpan<Vector3>)face);
    public static (Vector3 Normal, Vector3 Point) ToPlane(this ReadOnlySpan<Vector3> face)
    {
        var p = Vector3.Zero;
        var normal = Vector3.Zero;
        for (int i = 0; i < face.Length; ++i)
        {
            normal += Vector3.Cross(face[i], face[(i + 1) % face.Length]) / face.Length;
            p += face[i] / face.Length;
        }
        return (Vector3.Normalize(normal), p);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParallelDistance(float dst)
    {
        if (dst > Eps) return 1;
        if (dst < -Eps) return -1;
        return 0;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Side Classify(float dst)
    {
        if (dst > Eps) return Side.Front;
        if (dst < -Eps) return Side.Back;
        return Side.Coincide;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Coplanar(this Vector4 self, Vector4 other)
    {
        // naive coplanar, probably need some eps tuning
        var (t, b) = self.XYZ().TangentSpace();
        var n = other.XYZ();
        var p = self.XYZ() * self.W;
        return MathF.Abs(Vector3.Dot(t * 10 + b * 10 + p, n) - other.W) < Eps
            && MathF.Abs(Vector3.Dot(t * -10 + b * 10 + p, n) - other.W) < Eps
            && MathF.Abs(Vector3.Dot(t * 10 + b * -10 + p, n) - other.W) < Eps
            && MathF.Abs(Vector3.Dot(t * -10 + b * -10 + p, n) - other.W) < Eps;
        // var nn = Vector3.Dot(self.XYZ(), other.XYZ());
        // return (1.0f - nn) < EpsAngle && (MathF.Abs(self.W * nn - other.W) < Eps || MathF.Abs(self.W - other.W * nn) < Eps);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Coplanar(this IPlane3D self, IPlane3D other)
    {
        var dp = self.Point - other.Point;
        return MathF.Abs(Vector3.Dot(self.Tangent * 10 + self.Binormal * 10 + dp, other.Normal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * -10 + self.Binormal * 10 + dp, other.Normal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * 10 + self.Binormal * -10 + dp, other.Normal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * -10 + self.Binormal * -10 + dp, other.Normal)) < Eps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Coplanar(this IPlane3D self, Vector3 otherNormal, Vector3 otherPoint)
    {
        var dp = self.Point - otherPoint;
        return MathF.Abs(Vector3.Dot(self.Tangent * 10 + self.Binormal * 10 + dp, otherNormal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * -10 + self.Binormal * 10 + dp, otherNormal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * 10 + self.Binormal * -10 + dp, otherNormal)) < Eps
            && MathF.Abs(Vector3.Dot(self.Tangent * -10 + self.Binormal * -10 + dp, otherNormal)) < Eps;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Side Classify(this IPlane3D self, Vector3 point)
    {
        var cdst = Vector3.Dot(self.Normal, point - self.Point);
        return Classify(cdst);
    }
    public static Side Classify(this IPlane3D self, ReadOnlySpan<Vector3> face)
    {
        var result = Side.Coincide;
        for (int i = 0; i < face.Length; ++i)
        {
            var cdst = Vector3.Dot(self.Normal, face[i] - self.Point);
            var side = ParallelDistance(cdst);
            if (side == 0) continue;
            result |= (Side)(1 << ((1 + side) / 2));
        }
        return result;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStrictInside(this IEnumerable<IPlane3D> self, Vector3 point)
    {
        foreach (var i in self)
        {
            if (i.Classify(point) != Side.Back) return false;
        }
        return true;
    }
    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static Vector3 Point(this IPlane3D self) => self.Normal * self.Displacement;
}

public static class LinkedListExtensions
{
    // [[length, last, first], [value, prev, next], [value, prev, next], ...]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinkedListInit(this Span<int> self)
    {
        self[0] = 0;
        self[1] = 0;
        self[2] = 0;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListAddLast(this Span<int> self, int value)
    {
        var last = self[1];
        if (last == 0)
        {
            // empty collection
            self[0] = 1;
            self[1] = 1;
            self[2] = 1;
            self[3] = value;
            self[4] = 0;
            self[5] = 0;
        }
        return self.LinkedListAddAfterUnsafe(last, value);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListAddFirst(this Span<int> self, int value)
    {
        var first = self[2];
        if (first == 0)
        {
            // empty collection
            self[0] = 1;
            self[1] = 1;
            self[2] = 1;
            self[3] = value;
            self[4] = 0;
            self[5] = 0;
        }
        return self.LinkedListAddBeforeUnsafe(first, value);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListAddAfterUnsafe(this Span<int> self, int index, int value)
    {
        var length = self[0];
        var next_index = self[index * 3 + 2];
        if (next_index == 0)
        {
            // last element
            self[length * 3] = value;
            self[length * 3 + 1] = index;
            self[length * 3 + 2] = 0;
            self[index * 3 + 2] = length;
            self[1] = length;
            return self[0] += 1;
        }
        self[length * 3] = value;
        self[length * 3 + 1] = index;
        self[length * 3 + 2] = next_index;
        self[next_index * 3 + 1] = length;
        self[index * 3 + 2] = length;
        return self[0] += 1;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListAddBeforeUnsafe(this Span<int> self, int index, int value)
    {
        var length = self[0];
        var prev_index = self[index * 3 + 1];
        if (prev_index == 0)
        {
            // first element
            self[length * 3] = value;
            self[length * 3 + 1] = 0;
            self[length * 3 + 2] = index;
            self[index * 3 + 1] = length;
            self[2] = length;
            return ++self[0];
        }
        self[length * 3] = value;
        self[length * 3 + 1] = prev_index;
        self[length * 3 + 2] = index;
        self[prev_index * 3 + 2] = length;
        self[index * 3 + 1] = length;
        return ++self[0];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetLength(this Span<int> self) => self[0];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetLast(this Span<int> self) => self[1];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetFirst(this Span<int> self) => self[2];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetValue(this Span<int> self, int index) => self[index * 3];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LinkedListSetValue(this Span<int> self, int index, int value) => self[index * 3] = value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetPrev(this Span<int> self, int index) => self[index * 3 + 1];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListGetNext(this Span<int> self, int index) => self[index * 3 + 2];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LinkedListRemoveUnsafe(this Span<int> self, int index)
    {
        var length = self[0];
        var value = self[index * 3];
        var prev_index = self[index * 3 + 1];
        var next_index = self[index * 3 + 2];
        if (length == 1)
        {
            // only element
            self[0] = 0;
            self[1] = 0;
            self[2] = 0;
            return value;
        }
        // stich hole
        if (prev_index != 0) self[prev_index * 3 + 2] = next_index;
        if (next_index != 0) self[next_index * 3 + 1] = prev_index;
        if (length != index)
        {
            // element in the middle
            // swap last element to current
            var end_prev = self[length * 3 + 1];
            var end_next = self[length * 3 + 2];
            self[index * 3] = self[length * 3];
            self[index * 3 + 1] = end_prev;
            self[index * 3 + 2] = end_next;
            if (end_prev != 0) self[end_prev * 3 + 2] = index;
            if (end_next != 0) self[end_next * 3 + 2] = index;
        }
        return --self[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SortedListInsertUnsafe(this Span<int> self, int length, Span<float> factors, float factor, int value)
    {
        var i = 0;
        for (; i < length; ++i) if (factor < factors[i]) break;
        if (i == length)
        {
            factors[length] = factor;
            self[length] = value;
            return length + 1;
        }
        factors[i..length].CopyTo(factors[(i + 1)..]);
        factors[i] = factor;
        self[i..length].CopyTo(self[(i + 1)..]);
        self[i] = value;
        return length + 1;
    }

    public static int AddIfNotCoplanar(this List<IPlane3D> self, Vector3 normal, Vector3 point)
    {
        for (int i = 0; i < self.Count; ++i)
        {
            if (self[i].Coplanar(normal, point)) return i;
        }
        self.Add(new PlaneBasis(normal, point));
        return self.Count - 1;
    }
}

public class PlaneBasis : IPlane3D
{
    public Vector3 Normal { get; init; }
    public Vector3 Tangent { get; init; }
    public Vector3 Binormal { get; init; }
    // public float Displacement { get; init; }
    public Vector3 Point { get; init; }
    public IPlane3D Opposite { get; init; }
    public bool IsOpposite => false;

    public PlaneBasis(Vector4 plane)
    {
        Normal = plane.XYZ();
        (Tangent, Binormal) = Normal.TangentSpace();
        Point = Normal * plane.W;
        // Displacement = plane.W;
        Opposite = new PlaneOpposite(this);
    }
    public PlaneBasis(Vector3 normal, Vector3 point)
    {
        Normal = normal;
        (Tangent, Binormal) = Normal.TangentSpace();
        Point = point;
        // Displacement = Vector3.Dot(Normal, Point);
        Opposite = new PlaneOpposite(this);
    }
    public PlaneBasis(Vector4 plane, Vector3 origin)
    {
        Normal = plane.XYZ();
        (Tangent, Binormal) = Normal.TangentSpace();

        Point = Vector3.Dot(Tangent, origin) * Tangent + Vector3.Dot(Binormal, origin) * Binormal + plane.W * Normal;
        // Displacement = Vector3.Dot(Normal, Point);
        Opposite = new PlaneOpposite(this);
    }

    public PlaneBasis(ReadOnlySpan<Vector3> face)
    {
        (Normal, Point) = face.ToPlane();
        (Tangent, Binormal) = Normal.TangentSpace();

        // Displacement = Vector3.Dot(Normal, Point);
        Opposite = new PlaneOpposite(this);
    }

    public override string ToString() => $"<{Normal.X} {Normal.Y} {Normal.Z}> {Vector3.Dot(Normal, Point)}";
    public override bool Equals(object? o)
    {
        if (o == null) return false;
        if (o is PlaneOpposite op) return op.Equals(this);
        return base.Equals(o);
    }
    public override int GetHashCode() => Normal.GetHashCode() ^ Tangent.GetHashCode() ^ Binormal.GetHashCode() ^ Point.GetHashCode();
}

public class PlaneOpposite : IPlane3D
{
    private IPlane3D _plane;

    public Vector3 Normal { get; }
    public Vector3 Tangent { get; }
    public Vector3 Binormal { get; }
    public Vector3 Point { get; }
    // public float Displacement => -_plane.Displacement;
    public IPlane3D Opposite => _plane;
    public bool IsOpposite => true;
    public PlaneOpposite(IPlane3D plane)
    {
        _plane = plane;
        Normal = -plane.Normal;
        (Tangent, Binormal) = Normal.TangentSpace();
        Point = plane.Point;
    }
    public override int GetHashCode() => _plane.GetHashCode();
    public override bool Equals(object? o) => _plane.Equals(o);
}

public static class PlaneExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Plane(this IPlane3D self) => new(self.Normal, Vector3.Dot(self.Normal, self.Point));

}

public enum Side : int
{
    Coincide = 0,
    Back = 0x1,
    Front = 0x2,
    Intersect = 0x3,
}
public class Polygon
{
    public IPlane3D Plane { get; init; }
    public List<Vector2> Points { get; } = new();
    public SurfaceFlags Flags { get; set; } = SurfaceFlags.Free;
    public int NumSides => Points.Count;
    public float Diameter { get; init; }
    public Polygon Opposite
    {
        get
        {
            var opposite = Plane.Opposite;
            var transform = new Vector2(
                Vector3.Dot(Plane.Tangent, opposite.Tangent),
                Vector3.Dot(Plane.Binormal, opposite.Binormal)
            );
            var pts = Points.Select(x => transform * x).ToList();
            pts.Reverse();
            return new Polygon(opposite, pts)
            {
                Diameter = Diameter,
            };
        }
    }
    private Polygon(IPlane3D plane, List<Vector2> points)
    {
        Plane = plane;
        Points = points;
    }
    public Polygon(IPlane3D plane, ReadOnlySpan<Vector3> face, SurfaceFlags flags)
    {
        var box = new AABB(face);
        var (normal, _) = face.ToPlane();
        if (Vector3.Dot(plane.Normal, normal) > 0)
        {
            Plane = plane;
        }
        else
        {
            Plane = plane.Opposite;
        }
        Points = new List<Vector2>(face.Length);
        for (int i = 0; i < face.Length; ++i)
        {
            var p = face[i].Local2D(Plane.Normal, Plane.Tangent, Plane.Binormal, Plane.Point);
            Points.Add(p);
        }
        Diameter = 2 * box.MaxDimension;
        Flags = flags;
    }
    public Polygon(ReadOnlySpan<Vector3> face, SurfaceFlags flags)
    {
        var box = new AABB(face);
        Plane = new PlaneBasis(face);
        Points = new List<Vector2>(face.Length);
        for (int i = 0; i < face.Length; ++i)
        {
            var p = face[i].Local2D(Plane.Normal, Plane.Tangent, Plane.Binormal, Plane.Point);
            Points.Add(p);
        }
        Diameter = 2 * box.MaxDimension;
        Flags = flags;
    }
    public float CalcMinDiameter()
    {
        var mindst = 1e10f;
        for (int i = 0; i < Points.Count; ++i)
        {
            var nxt = (i + 1) % Points.Count;
            var nrml = (Points[nxt] - Points[i]).TangentSpace();
            var maxdst = 0.0f;
            for (int j = 0; j < Points.Count; ++j)
            {
                if (i == j || i == nxt) continue;
                var dst = MathF.Abs(Vector2.Dot(nrml, Points[j] - Points[i]));
                if (dst > maxdst) maxdst = dst;
            }
            mindst = MathF.Min(mindst, maxdst);
        }
        return mindst;
    }
    public static Polygon FullPlane(IPlane3D plane, SurfaceFlags flags = SurfaceFlags.Free, float big = LinealgExtensions.Big)
    {
        return new Polygon(plane, new List<Vector2>{
            new (-big, -big),
            new ( big, -big),
            new ( big,  big),
            new (-big,  big),
        })
        {
            Flags = flags,
            Diameter = 4 * big,
        };
    }
    public Side Classify(Vector4 splitter)
    {
        var snorm = splitter.XYZ();
        var nloc = new Vector2(Vector3.Dot(snorm, Plane.Tangent), Vector3.Dot(snorm, Plane.Binormal));
        var norm = nloc.Length();
        var dst = splitter.W - Vector3.Dot(Plane.Point, snorm);
        if (norm < LinealgExtensions.Eps)
        {
            // parallel
            var s = Side.Coincide;
            if (dst >= -LinealgExtensions.Eps) s |= Side.Back;
            if (dst <= LinealgExtensions.Eps) s |= Side.Front;
            return s;
        }
        nloc /= norm;
        var dloc = dst / Vector3.Dot(nloc.X * Plane.Tangent + nloc.Y * Plane.Binormal, snorm);
        var result = Side.Coincide;
        for (int i = 0; i < Points.Count; ++i)
        {
            var cdst = Vector2.Dot(nloc, Points[i]) - dloc;
            var side = LinealgExtensions.ParallelDistance(cdst);
            if (side == 0) continue;
            result |= (Side)(1 << ((1 + side) / 2));
            if (result == Side.Intersect) return result;
        }
        return result;
    }
    public void Loop(Span<Vector3> dest)
    {
        var (t, b, p) = (Plane.Tangent, Plane.Binormal, Plane.Point);
        for (int i = 0; i < Points.Count; ++i)
        {
            var pp = Points[i];
            dest[i] = t * pp.X + b * pp.Y + p;
        }
    }
    public Side Slice(Vector2 nloc, float dloc, List<Polygon>? back, List<Polygon>? front)
    {
        int tangentsLength = 0;
        Span<Vector2> points = stackalloc Vector2[Points.Count * 2 + 1];
        Span<int> sides = stackalloc int[Points.Count * 2 + 1];
        sides.Clear();

        Span<float> pos = stackalloc float[Points.Count * 2 + 1];
        Span<int> tangents = stackalloc int[Points.Count * 2 + 1];
        tangents.Fill(-1);
        var tan = nloc.TangentSpace();
        var tp = nloc * dloc;
        // build intersections and mark sides
        // <-2 below splitter, -1 ajacent edge below
        // 0xF ajacent edges both sides, 0 both on splitter
        // > 2 above splitter,  1 ajacent edge above
        var prep = Points[^1];
        var predst = Vector2.Dot(nloc, prep) - dloc;
        var preside = LinealgExtensions.ParallelDistance(predst);
        points[0] = prep;
        sides[0] = 2 * preside;
        sides[1] = preside;
        var pindex = 1;
        for (int i = 0; i < Points.Count; ++i)
        {
            var cdst = Vector2.Dot(nloc, Points[i]) - dloc;
            var side = LinealgExtensions.ParallelDistance(cdst);
            if (predst < -LinealgExtensions.Eps && cdst > LinealgExtensions.Eps ||
                predst > LinealgExtensions.Eps && cdst < -LinealgExtensions.Eps)
            {
                // has an intersection
                var intersection = Vector2.Lerp(Points[i], prep, MathF.Abs(cdst / (cdst - predst)));
                // fix intersection by projecting onto splitter line
                var tt = Vector2.Dot(intersection - tp, tan);
                tangentsLength = tangents.SortedListInsertUnsafe(tangentsLength, pos, tt, pindex);
                intersection = tt * tan + tp;
                points[pindex] = intersection;
                // sides[pindex - 1] += side;
                sides[pindex] = 0xF;
                pindex++;
                points[pindex] = Points[i];
                sides[pindex] += 2 * side;
                sides[pindex + 1] += side;
                pindex++;
            }
            else
            {
                if (MathF.Abs(cdst) <= LinealgExtensions.Eps)
                {
                    var tt = Vector2.Dot(Points[i] - tp, tan);
                    tangentsLength = tangents.SortedListInsertUnsafe(tangentsLength, pos, tt, pindex);
                }
                points[pindex] = Points[i];
                sides[pindex - 1] += side;
                sides[pindex] += 2 * side;
                sides[pindex + 1] += side;
                pindex++;
            }
            predst = cdst;
            prep = Points[i];
        }
        pindex--;
        sides[0] += sides[pindex];
        for (int i = 0; i < pindex; ++i)
        {
            if (sides[i] == 0 && sides[(i + pindex - 1) % pindex] * sides[(i + 1) % pindex] < 0) sides[i] = 0xF;
        }
        // var initial = 
        for (int i = 0; i < tangentsLength; ++i)
        {
            tangents[i] = tangents[i] % pindex;
        }

        // build zaps
        // (1 - side) * i -> j
        Span<int> zaps = stackalloc int[Points.Count * 4];
        zaps.Fill(-1);
        // side = -1
        for (int i = 0; i < tangentsLength; ++i)
        {
            var fwd = sides[tangents[i]];
            if (fwd == 1 || fwd == 0xF)
            {
                if (tangents[i + 1] == -1) throw new IndexOutOfRangeException("Unclosed polygon");
                zaps[2 * Points.Count + tangents[i + 1]] = tangents[i];
                i++;
                continue;
            }
        }
        // side = 1
        for (int i = tangentsLength; i-- > 1;)
        {
            var fwd = sides[tangents[i]];
            if (fwd == -1 || fwd == 0xF)
            {
                if (tangents[i - 1] == -1) throw new IndexOutOfRangeException("Unclosed polygon");
                zaps[tangents[i - 1]] = tangents[i];
                i--;
                continue;
            }
        }
        // build loops
        Side res = Side.Coincide;
        for (int i = 0; i < pindex; ++i)
        {
            var side = sides[i];
            if (Math.Abs(side) < 2 || side == 0xF) continue;
            side = Math.Sign(side);
            var start = i;
            var j = (start + 1) % pindex;
            var poly = new List<Vector2> { points[i] };
            while (j != start)
            {
                poly.Add(points[j]);
                if (sides[j] == 0)
                {
                    // should never happen
                    j = (j + 1) % pindex;
                    continue;
                }
                if (sides[j] != 0xF) sides[j] = 0;
                var zap = zaps[(1 - side) * Points.Count + j];
                j = zap != -1 ? zap : ((j + 1) % pindex);
            }
            // (((side > 0) == IsCCW) ? front : back)?.Add(new Polygon(Plane, poly, !IsCCW) { Flags = Flags });
            var box = new AABB(points);
            ((side > 0) ? front : back)?.Add(new Polygon(Plane, poly)
            {
                Flags = Flags,
                Diameter = 2 * box.MaxDimension,
            });
            res |= (Side)(1 << ((1 + side) / 2));
        }
        return res;
    }
    public Side Slice(IPlane3D splitter, List<Polygon>? back, List<Polygon>? front)
    {
        var snorm = splitter.Normal;
        var nloc = new Vector2(Vector3.Dot(snorm, Plane.Tangent), Vector3.Dot(snorm, Plane.Binormal));
        var norm = nloc.Length();
        var dst = Vector3.Dot(splitter.Point - Plane.Point, snorm);
        if (norm < LinealgExtensions.Eps)
        {
            // parallel
            var s = Side.Coincide;
            if (dst >= -LinealgExtensions.Eps)
            {
                back?.Add(this);
                s |= Side.Back;
            }
            if (dst <= LinealgExtensions.Eps)
            {
                front?.Add(this);
                s |= Side.Front;
            }
            return s;
        }
        // var dloc = splitter.Displacement / norm;
        nloc /= norm;
        var dloc = dst / Vector3.Dot(nloc.X * Plane.Tangent + nloc.Y * Plane.Binormal, snorm);
        return Slice(nloc, dloc, back, front);
    }

    public SurfaceFlags Point(Vector2 point, SurfaceFlags outside = SurfaceFlags.Free)
    {
        if (Points.Count > 2) return outside;
        var p0 = Points[^1];
        var p1 = Points[0];
        bool inside = false;
        for (var i = 0; i < Points.Count; ++i)
        {
            if (point.Y > MathF.Min(p0.Y, p1.Y) && point.Y <= MathF.Max(p0.Y, p1.Y))
            {
                if (point.X <= MathF.Max(p0.X, p1.X))
                {
                    if (p0.X == p1.X ||
                        (point.X - p0.X) * (p1.Y - p0.Y) <= (point.Y - p0.Y) * (p1.X - p0.X))
                    {
                        inside = !inside;
                    }
                }
            }
            p0 = p1;
            p1 = Points[i];
        }
        return inside ? Flags : outside;
    }

    public override string ToString()
    {
        return $"P: {Plane}\n  " +
            string.Join("\n  ", Points.Select(x => x.ToString())) +
            $"\n   F: {Flags}";
    }

    public int ExtrudePlanes(Vector3 direction, Span<Vector4> sides)
    {
        var sgn = MathF.Sign(Vector3.Dot(direction, Plane.Normal));
        if (sgn == 0)
        {
            // sides[0] = new Vector4(Plane.Normal, Plane.Displacement);
            return 0;
        }
        var p = Plane.Point;
        for (int i = 0; i < Points.Count; ++i)
        {
            var v0 = Points[i].X * Plane.Tangent + Points[i].Y * Plane.Binormal;
            var v1 = Points[(i + 1) % Points.Count].X * Plane.Tangent + Points[(i + 1) % Points.Count].Y * Plane.Binormal;
            var n = sgn * Vector3.Normalize(Vector3.Cross(v1 - v0, direction));
            sides[i] = new Vector4(n, Vector3.Dot(n, v0 + p));
        }
        return Points.Count;
    }

    public Vector3 CalcCenter()
    {
        var m = Vector2.Zero;
        foreach (var i in Points)
        {
            m += i / Points.Count;
        }
        return m.X * Plane.Tangent + m.Y * Plane.Binormal + Plane.Point;
    }

    public AABB CalcBox()
    {
        var p = Plane.Point;
        return new(Points.Select(x => x.X * Plane.Tangent + x.Y * Plane.Binormal + p));
    }
    public MeshContext ToContext()
    {
        Span<VertexData> verts = stackalloc VertexData[NumSides];
        var corners = new CornerData[NumSides];
        var faces = new FaceData[1];
        var n = 0;
        var lastCorner = 0;
        var plane = Plane;
        var ps = plane.Point;
        var start = lastCorner;
        for (int j = 0; j < Points.Count; ++j)
        {
            var p = Points[j];
            var v = p.X * plane.Tangent + p.Y * plane.Binormal + ps;
            var ind = -1;
            for (int k = 0; k < n; ++k)
            {
                if ((verts[k].Pos - v).LengthSquared() < LinealgExtensions.EpsSq)
                {
                    ind = k;
                    break;
                }
            }
            if (ind < 0)
            {
                verts[n] = new VertexData()
                {
                    Pos = v,
                    Flags = 0,
                };
                ind = n;
                n++;
            }
            corners[start + j] = new CornerData()
            {
                Uv0 = Vector2.Zero,
                Uv1 = Vector2.Zero,
                Color = Vector4.One,
                Vertex = ind,
            };
        }
        faces[0] = new FaceData()
        {
            Flags = (long)Flags,
            LoopStart = start,
            LoopTotal = Points.Count,
            Material = 0,
        };
        lastCorner += Points.Count;
        return new MeshContext(verts[..n].ToArray(), corners, faces);
    }
}
public interface IIntervalCollection
{
    void AddInterval(float from, float to);
    bool Pass(float from, float to);
}
// public class IntervalCollection : IIntervalCollection {
//     private List<(float From, float To)> Intervals = new ();

//     public void AddInterval(float from, float to)
//     {
//         Intervals.Add((from, to));
//     }

//     public bool Pass(float from, float to)
//     {

//     }
// }
public class BspNode1D : IIntervalCollection
{
    public const float Eps = 1e-5f;
    public LinkedList<float> Intervals = new();
    public void AddInterval(float from, float to)
    {
        if (to < from + Eps) return; // interval is to short
        bool open = true;
        var i = Intervals.First;
        if (i == null)
        {
            Intervals.AddLast(from);
            Intervals.AddLast(to);
            return;
        }
        if (to <= i.Value + Eps)
        {
            if (to >= i.Value - Eps)
            {
                i.Value = from;
            }
            else
            {
                Intervals.AddFirst(to);
                Intervals.AddFirst(from);
            }
            return;
        }
        if (from > Intervals.Last!.Value - Eps)
        {
            if (from > Intervals.Last.Value + Eps)
            {
                Intervals.AddLast(from);
                Intervals.AddLast(to);
            }
            else
            {
                Intervals.Last.Value = to;
            }
            return;
        }
        while (i.Next != null)
        {
            if (from > i.Value + Eps) { } // .)   [
            else
            {
                LinkedListNode<float> j;
                if (from < i.Value - Eps)
                {
                    // *)  [  (.)
                    if (open)
                    {
                        i.Value = from;
                    }
                    j = i;
                }
                else
                {
                    if (open)
                    {
                        j = i;
                    }
                    else
                    {
                        j = i.Previous!;
                    }
                }
                while (i.Next != null)
                {
                    if (to > i.Value + Eps) { } // .)   ]
                    else
                    {
                        if (to < i.Value - Eps)
                        {
                            // *)  ]  (.)
                            if (open)
                            {
                                Intervals.AddBefore(i, to);
                            }
                        }
                        else
                        {
                            if (open)
                            {
                                Intervals.Remove(i);
                            }
                        }
                        break;
                    }
                    open = !open;
                    i = i.Next;
                    if (i.Previous != j)
                    {
                        Intervals.Remove(i.Previous!);
                    }
                }
                return;
            }
            open = !open;
            i = i.Next;
        }
    }

    public bool Pass(float from, float to)
    {
        bool open = true;
        var i = Intervals.First;
        if (i == null) return true;
        if (from < i.Value - Eps) return true;
        if (to > Intervals.Last!.Value + Eps) return true;
        while (i.Next != null)
        {
            if (from > i.Value + Eps) { } // .)   [
            else
            {
            }

            open = !open;
            i = i.Next;
        }
        throw new NotImplementedException();
    }
}
public class BspNode2D : IPlane2D
{
    public IPlane3D Space;
    public Vector2 Normal { get; private set; }
    public Vector2 Tangent { get; private set; }
    public Vector2 Point { get; private set; }
    public Polygon Volume;
    public void AddEdge(Vector2 p0, Vector2 p1)
    {
        throw new NotImplementedException();

    }

}
[Flags]
public enum ContentFlags : long
{
    Solid = 0x1,
    Lava = 0x8,
    Slime = 0x10,
    Water = 0x20,
    Fog = 0x40,
    PlayerClip = 0x10000,
    Trigger = 0x40000000,
}
[Flags]
public enum SurfaceFlags : long
{
    None = 0x0,
    NoDamage = 0x1,
    Slick = 0x2,
    Sky = 0x4,
    Ladder = 0x8,
    NoImpact = 0x10,
    NoMarks = 0x20,
    Flesh = 0x40,
    NoDraw = 0x80,
    Hint = 0x100,
    Skip = 0x200,
    MetalSteps = 0x1000,
    NoSteps = 0x2000,
    NonSolid = 0x4000,
    Dust = 0x40000,
    NoOB = 0x80000,
    Free = 0x4000000000000000L,
    NonFree = ~Free,
}
public class BspNode3D : IPlane3D
{

    public Vector3 Normal { get; private set; }
    public Vector3 Tangent { get; private set; }
    public Vector3 Binormal { get; private set; }
    public Vector3 Point { get; private set; }
    public bool IsOpposite { get; } = false;
    public IPlane3D Opposite { get; private set; }

    public List<Polygon>? Faces;
    public List<Polytope>? Brushes;

    public BspNode3D? Back { get; private set; }
    public BspNode3D? Front { get; private set; }
    public BspNode3D? Parent { get; private set; }
    public Polytope Volume { get; private set; } // for leafs
    public bool IsLeaf { get; private set; } = true;
    private BspNode3D(BspNode3D? parent, Polytope volume)
    {
        Opposite = null!; // unless it's defined as splitter node
        Volume = volume;
        Parent = parent;
    }

    public static BspNode3D Build(MeshContext context, bool doSubsplit = false)
    {
        Span<Vector3> allverts = stackalloc Vector3[context.MaxCorners];
        var box = new AABB(context.Vertices.Select(x => x.Pos));
        var root = new BspNode3D(null, Polytope.FromBox(0, box.FluentExpand(new Vector3(10.0f, 10.0f, 10.0f)), 0));
        foreach (var f in context.Faces)
        {
            for (int i = 0; i < f.LoopTotal; ++i)
            {
                allverts[i] = context.Vertices[context.Corners[f.LoopStart + i].Vertex].Pos;
            }
            var (n, p) = allverts[..f.LoopTotal].ToPlane();
            root.Split(allverts[..f.LoopTotal], n, p, (SurfaceFlags)f.Flags);
        }
        // root.DebugLeafs();s
        root.FacesToLeafs(doSubsplit);
        root.MarkSolid();
        return root;
    }

    private void FacesToLeafs(bool doSubsplit)
    {
        if (!IsLeaf)
        {
            Back!.FacesToLeafs(doSubsplit);
            Front!.FacesToLeafs(doSubsplit);
            return;
        }
        var maxSides = Volume.Sides.Max(x => x.NumSides);
        Span<Vector4> planes = stackalloc Vector4[maxSides];
        var splitters = new List<IPlane3D>();
        // [TODO] rewrite using 2d bsp tree
        bool isSolid = false;
        var faces = new List<Polygon>();
        for (int i = 0; i < Volume.Sides.Count; ++i)
        {
            if (Volume.Sides[i].Plane is BspNode3D splitter)
            {
                var numplanes = Volume.Sides[i].ExtrudePlanes(Volume.Sides[i].Plane.Normal, planes);
                var planespaces = new List<IPlane3D>(numplanes);
                for (int j = 0; j < numplanes; ++j)
                {
                    planespaces.Add(new PlaneBasis(planes[j], Volume.Sides[i].Plane.Point));
                }
                if (splitter.Faces != null)
                {
                    foreach (var poly in splitter.Faces)
                    {
                        if (poly.Plane.IsOpposite != splitter.IsOpposite) continue; // (splitter.Back == this)) continue;
                        if (poly.Flags.HasFlag(SurfaceFlags.Free)) continue;
                        var polys = new List<Polygon>() { poly };
                        var dst = new List<Polygon>();
                        foreach (var spl in planespaces)
                        {
                            foreach (var p in polys)
                            {
                                p.Slice(spl, dst, null);
                            }
                            (polys, dst) = (dst, polys);
                            dst.Clear();
                        }
                        foreach (var p in polys)
                        {
                            faces.Add(p);
                            if (doSubsplit)
                            {
                                var (t, b, n, pt) = (p.Plane.Tangent, p.Plane.Binormal, p.Plane.Normal, p.Plane.Point);
                                for (int j = 0; j < p.Points.Count; ++j)
                                {
                                    var nx = (j + 1) % p.Points.Count;
                                    var p0 = t * p.Points[j].X + b * p.Points[j].Y + pt;
                                    var p1 = t * p.Points[nx].X + b * p.Points[nx].Y + pt;
                                    if (planespaces.IsStrictInside(p0) || planespaces.IsStrictInside(p1))
                                    {
                                        splitters.AddIfNotCoplanar(Vector3.Normalize(Vector3.Cross(p1 - p0, n)), p0);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        foreach (var s in splitters)
        {
            Split(s.Normal, s.Point);
        }
        // [TODO] probably find better search method
        foreach (var f in faces)
        {
            var p = f.CalcCenter() - 1e-3f * f.Plane.Normal;
            Leaf(p).AddFace(f);
        }
    }

    public BspNode3D Leaf(Vector3 point)
    {
        if (IsLeaf) return this;
        var dst = Vector3.Dot(point - Point, Normal);
        if (dst < 0) return Back!.Leaf(point);
        return Front!.Leaf(point);
    }

    private void AddFace(Polygon poly)
    {
        Faces ??= new();
        Faces.Add(poly);
    }

    private void MarkSolid()
    {
        if (!IsLeaf)
        {
            Back?.MarkSolid();
            Front?.MarkSolid();
            return;
        }
        if (Faces == null) return;
        Brushes = new List<Polytope>();
        var flag = SurfaceFlags.Free;

        foreach (var f in Faces)
        {
            if (f.Flags.HasFlag(SurfaceFlags.Free)) continue;
            if (f.Flags.HasFlag(SurfaceFlags.NonSolid)) continue;
            flag &= SurfaceFlags.NonFree;
            flag |= f.Flags;
        }
        if (flag != SurfaceFlags.Free)
        {
            Volume.Flags = ContentFlags.Solid;
            Volume.SetSurface(flag);
            Brushes.Add(Volume);
        }
    }

    private void DebugLeafs()
    {
        if (IsLeaf)
        {
            GeometryDegugger.SendContext(Volume.ToContext());
        }
        Back?.DebugLeafs();
        Front?.DebugLeafs();
    }

    private void Split(ReadOnlySpan<Vector3> face, Vector3 normal, Vector3 point, SurfaceFlags flags)
    {
        if (IsLeaf)
        {
            Normal = normal;
            (Tangent, Binormal) = normal.TangentSpace();
            Point = point;
            Opposite = new PlaneOpposite(this);
            IsLeaf = false;
            // GeometryDegugger.SendContext(Volume.ToContext());
            var (b, f) = Volume.Slice(this, flags);
            // GeometryDegugger.SendContext(b.ToContext());
            // GeometryDegugger.SendContext(f.ToContext());
            Faces = new() { new Polygon(this, face, flags) };
            Back = new BspNode3D(this, b);
            Front = new BspNode3D(this, f);
            return;
        }
        var side = this.Classify(face);
        if (side == Side.Coincide) Faces!.Add(new Polygon(this, face, flags));
        else if (side == Side.Back) Back!.Split(face, normal, point, flags);
        else if (side == Side.Front) Front!.Split(face, normal, point, flags);
        else
        {
            List<Polygon> back = new(), front = new();
            new Polygon(face, flags).Slice(this, back, front);
            Span<Vector3> dest = stackalloc Vector3[2 * face.Length];
            foreach (var p in back)
            {
                if (p.CalcMinDiameter() < 4 * LinealgExtensions.Eps) continue;
                p.Loop(dest);
                Back!.Split(dest[..p.NumSides], p.Plane.Normal, p.Plane.Point, flags);
            }
            foreach (var p in front)
            {
                if (p.CalcMinDiameter() < 4 * LinealgExtensions.Eps) continue;
                p.Loop(dest);
                Front!.Split(dest[..p.NumSides], p.Plane.Normal, p.Plane.Point, flags);
            }
        }
    }
    private void Split(Vector3 normal, Vector3 point, SurfaceFlags flags = SurfaceFlags.Free)
    {
        if (IsLeaf)
        {
            Normal = normal;
            (Tangent, Binormal) = normal.TangentSpace();
            Point = point;
            Opposite = new PlaneOpposite(this);
            var (b, f) = Volume.Slice(this, flags);
            if (b.Sides.Count == 0 || f.Sides.Count == 0) return;
            IsLeaf = false;
            Faces = new();
            Back = new BspNode3D(this, b);
            Front = new BspNode3D(this, f);
            return;
        }
        if (this.Coplanar(normal, point)) return;
        Back?.Split(normal, point, flags);
        Front?.Split(normal, point, flags);
    }

    public void PushLeafBrushes(List<MeshContext> brushes)
    {
        if (IsLeaf)
        {
            if (Brushes == null) return;
            brushes.AddRange(Brushes.Select(x => x.ToContext()));
            return;
        }
        Back?.PushLeafBrushes(brushes);
        Front?.PushLeafBrushes(brushes);
    }
    public void PushLeafVolumes(List<MeshContext> volumes)
    {
        if (IsLeaf)
        {
            volumes.Add(Volume.ToContext());
            return;
        }
        Back?.PushLeafVolumes(volumes);
        Front?.PushLeafVolumes(volumes);
    }
}

// convex volume
public class Polytope
{
    public List<Polygon> Sides { get; init; }
    public ContentFlags Flags { get; set; }
    public Polytope(List<Polygon> sides, ContentFlags flags)
    {
        Sides = sides;
        Flags = flags;
    }

    public void SetSurface(SurfaceFlags flags)
    {
        foreach (var i in Sides)
        {
            i.Flags = flags;
        }
    }
    public static Polytope FullSpace(ContentFlags flags, float big = 1e5f) => new(new List<Polygon>() {
        Polygon.FullPlane(new PlaneBasis(new Vector4(-1.0f,  0.0f,  0.0f, big)), 0),
        Polygon.FullPlane(new PlaneBasis(new Vector4( 1.0f,  0.0f,  0.0f, big)), 0),
        Polygon.FullPlane(new PlaneBasis(new Vector4( 0.0f, -1.0f,  0.0f, big)), 0),
        Polygon.FullPlane(new PlaneBasis(new Vector4( 0.0f,  1.0f,  0.0f, big)), 0),
        Polygon.FullPlane(new PlaneBasis(new Vector4( 0.0f,  0.0f, -1.0f, big)), 0),
        Polygon.FullPlane(new PlaneBasis(new Vector4( 0.0f,  0.0f,  1.0f, big)), 0),
    }, flags);
    public static Polytope FromBox(ContentFlags flags, AABB box, SurfaceFlags edgeFlags = SurfaceFlags.Free) => new(AABB.Sides().Select(x =>
    {
        Span<Vector3> b = stackalloc Vector3[4];
        box.Side(x, b);
        return new Polygon(b, edgeFlags);
    }).ToList(), flags);

    public static Polytope FromBrush(IEnumerable<(Vector4 Plane, long Flags)> planes, long contents) =>
        FromBrush(planes, contents, Vector3.Zero);
    public static Polytope FromBrush(IEnumerable<(Vector4 Plane, long Flags)> planes, long contents, Vector3 origin)
    {
        var s = FullSpace((ContentFlags)contents);
        foreach (var p in planes)
        {
            s = s.Slice(new PlaneBasis(p.Plane, origin), (SurfaceFlags)p.Flags).Back;
        }
        return s;
    }
    public List<(Vector4 Plane, long Flags)> ToBrush()
    {
        var result = new List<(Vector4 Plane, long Flags)>();
        var box = new AABB();
        var maxSides = 0;
        for (int i = 0; i < Sides.Count; ++i)
        {
            box.FluentAdd(Sides[i].CalcBox());
            maxSides = Math.Max(maxSides, Sides[i].NumSides);
        }
        Span<Vector4> boxPlanes = stackalloc Vector4[] {
            new (-1,  0,  0, -box.Min.X),
            new ( 0, -1,  0, -box.Min.Y),
            new ( 0,  0, -1, -box.Min.Z),
            new ( 1,  0,  0, box.Max.X),
            new ( 0,  1,  0, box.Max.Y),
            new ( 0,  0,  1, box.Max.Z),
        };
        Span<int> boxPicks = stackalloc int[boxPlanes.Length];
        boxPicks.Clear();
        Span<long> boxFlags = stackalloc long[boxPlanes.Length];
        boxFlags.Clear();
        Span<float> bDists = stackalloc float[boxPlanes.Length];
        for (int i = 0; i < Sides.Count; ++i)
        {
            var p = Sides[i].Plane.Plane();
            var b = Sides[i].CalcBox();
            b.Write(bDists);
            // check if plane is boxed
            for (int j = 0; j < boxPlanes.Length; ++j)
            {
                if (p.Coplanar(boxPlanes[j]))
                {
                    boxPicks[j] = 1;
                    boxFlags[j] = (long)Sides[i].Flags;
                }
                else if (boxPlanes[j].W.EpsEq(bDists[j]) && boxPicks[j] != 1)
                {
                    boxFlags[j] |= (long)Sides[i].Flags;
                }
            }
        }
        for (int i = 0; i < boxPlanes.Length; ++i)
        {
            result.Add((boxPlanes[i], boxFlags[i]));
        }
        for (int i = 0; i < Sides.Count; ++i)
        {
            var p = Sides[i].Plane.Plane();
            var b = Sides[i].CalcBox();
            // add to collection
            var noPlane = true;
            for (int j = 0; j < result.Count; ++j)
            {
                if (result[j].Plane.Coplanar(p))
                {
                    noPlane = false;
                    result[j] = (result[j].Plane, result[j].Flags | (long)Sides[i].Flags);
                    break;
                }
            }
            if (noPlane) result.Add((p, (long)Sides[i].Flags));
        }
        Span<Vector4> curPlanes = stackalloc Vector4[maxSides];
        Span<Vector3> axis = stackalloc Vector3[] {
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ,
        };
        int planenum = result.Count;
        for (int i = 0; i < Sides.Count; ++i)
        {
            for (int ax = 0; ax < axis.Length; ++ax)
            {
                var pnum = Sides[i].ExtrudePlanes(axis[ax], curPlanes);
                for (int j = 0; j < pnum; ++j)
                {
                    var p = curPlanes[j];
                    var outside = true;
                    for (int k = 0; k < Sides.Count; ++k)
                    {
                        if (k == i) continue;
                        var cls = Sides[k].Classify(p);
                        if (cls == Side.Back || cls == Side.Coincide)
                        {
                            continue;
                        }
                        outside = false;
                        break;
                    }
                    if (outside)
                    {

                        var noPlane = true;
                        for (int l = 0; l < result.Count; ++l)
                        {
                            if (result[l].Plane.Coplanar(p))
                            {
                                noPlane = false;
                                if (l < planenum) break;
                                result[l] = (result[l].Plane, result[l].Flags | (long)Sides[i].Flags);
                                break;
                            }
                        }
                        if (noPlane) result.Add((p, (long)Sides[i].Flags));
                    }
                }
            }
        }
        return result;
    }
    public (Polytope Back, Polytope Front) Slice(IPlane3D splitter, SurfaceFlags flags = SurfaceFlags.Free)
    {
        List<Polygon> back = new(), front = new();
        var side = Side.Coincide;
        foreach (var i in Sides)
        {
            side |= i.Slice(splitter, back, front);
        }
        if (side != Side.Intersect) return (new Polytope(back, Flags), new Polytope(front, Flags));
        var diameter = back.Sum(x => x.Diameter);
        if (back.Count != 0 && front.Count != 0)
        {
            Polygon poly = Polygon.FullPlane(splitter, flags, diameter);
            List<Polygon> destb = new();
            foreach (var i in back)
            {
                poly.Slice(i.Plane, destb, null);
                if (destb.Count == 0)
                {
                    var context = this.ToContext();
                    var pcon = poly.ToContext();
                    GeometryDegugger.SendContext(context);
                    GeometryDegugger.SendContext(pcon);
                    throw new IndexOutOfRangeException("Polytope slice failed");
                }
                poly = destb[0];
                destb.Clear();
            }
            back.Add(poly);
            front.Add(poly.Opposite);
        }
        return (new Polytope(back, Flags), new Polytope(front, Flags));
    }
    public override string ToString()
    {
        StringBuilder sb = new();
        for (int i = 0; i < Sides.Count; ++i)
        {
            sb.Append($" = {i} = [{Flags}]\n{Sides[i]}\n");
        }
        return sb.ToString();
    }
    public Polygon? Intersect(Polygon poly)
    {
        var b = new List<Polygon>();
        var res = poly;
        foreach (var i in Sides)
        {
            res.Slice(i.Plane, b, null);
            if (b.Count == 0) return null;
            res = b[0];
            b.Clear();
        }
        return res;
    }

    public static Polytope FromContext(ContentFlags flags, MeshContext context)
    {
        var box = new AABB(context.Vertices.Select(x => x.Pos));
        var result = FromBox(flags, box);
        Span<Vector3> points = stackalloc Vector3[context.MaxCorners];
        for (int i = 0; i < context.Faces.Length; ++i)
        {
            var st = context.Faces[i].LoopStart;
            var end = context.Faces[i].LoopTotal;
            for (int j = 0; j < end; ++j)
            {
                points[j] = context.Vertices[context.Corners[context.Faces[i].LoopStart + j].Vertex].Pos;
            }
            result = result.Slice(new PlaneBasis(points[..end]), (SurfaceFlags)context.Faces[i].Flags).Back;
        }
        return result;
    }

    public MeshContext ToContext()
    {
        var n = Sides.Select(x => x.NumSides).Sum();
        Span<VertexData> verts = stackalloc VertexData[n];
        var corners = new CornerData[n];
        var faces = new FaceData[Sides.Count];
        n = 0;
        var lastCorner = 0;
        for (int i = 0; i < Sides.Count; ++i)
        {
            var plane = Sides[i].Plane;
            var ps = plane.Point;
            var start = lastCorner;
            for (int j = 0; j < Sides[i].Points.Count; ++j)
            {
                var p = Sides[i].Points[j];
                var v = p.X * plane.Tangent + p.Y * plane.Binormal + ps;
                var ind = -1;
                for (int k = 0; k < n; ++k)
                {
                    if ((verts[k].Pos - v).LengthSquared() < LinealgExtensions.EpsSq)
                    {
                        ind = k;
                        break;
                    }
                }
                if (ind < 0)
                {
                    verts[n] = new VertexData()
                    {
                        Pos = v,
                        Flags = 0,
                    };
                    ind = n;
                    n++;
                }
                corners[start + j] = new CornerData()
                {
                    Uv0 = Vector2.Zero,
                    Uv1 = Vector2.Zero,
                    Color = Vector4.One,
                    Vertex = ind,
                };
            }
            faces[i] = new FaceData()
            {
                Flags = (long)Sides[i].Flags,
                LoopStart = start,
                LoopTotal = Sides[i].Points.Count,
                Material = 0,
            };
            lastCorner += Sides[i].Points.Count;
        }
        return new MeshContext(verts[..n].ToArray(), corners, faces);
    }
}