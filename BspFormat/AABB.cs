using System.Numerics;
using System.Runtime.InteropServices;

namespace Bsp.BspFormat;

public enum BoxSide
{
    XMin,
    XMax,
    YMin,
    YMax,
    ZMin,
    ZMax,
}
public class AABB
{
    public Vector3 Min = new(float.MaxValue);
    public Vector3 Max = new(-float.MaxValue);

    public Vector3 Center => Min * .5f + Max * .5f;
    public Vector3 Dimensions => Max - Min;
    public float MaxDimension
    {
        get
        {
            var d = Dimensions;
            return MathF.Max(d.X, MathF.Max(d.Y, d.Z));
        }
    }
    // public float Diameter => MathF.Abs()
    public AABB() { }
    public AABB(Vector3 point)
    {
        Max = Min = point;
    }
    public AABB(ReadOnlySpan<Vector3> points)
    {
        Min = Max = points[0];
        for (int i = 1; i < points.Length; ++i) FluentAdd(points[i]);
    }
    public AABB(ReadOnlySpan<Vector2> points)
    {
        Min = Max = new(points[0], 0.0f);
        for (int i = 1; i < points.Length; ++i) FluentAdd(new Vector3(points[i], 0.0f));
    }
    public AABB(IEnumerable<Vector3> points)
    {
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(-float.MaxValue);
        foreach (var i in points) FluentAdd(i);
    }
    public AABB(IEnumerable<AABB> boxes)
    {
        Min = new Vector3(float.MaxValue);
        Max = new Vector3(-float.MaxValue);
        foreach (var i in boxes) FluentAdd(i);
    }
    public AABB FluentAdd(Vector3 point)
    {
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
        return this;
    }
    public AABB FluentAdd(AABB box)
    {
        Min = Vector3.Min(Min, box.Min);
        Max = Vector3.Max(Max, box.Max);
        return this;
    }
    public AABB FluentExpand(Vector3 extents)
    {
        Min -= extents;
        Max += extents;
        return this;
    }
    public AABB FluentIntersect(AABB box)
    {
        Min = Vector3.Max(Min, box.Min);
        Max = Vector3.Min(Max, box.Max);
        return this;
    }
    public void Write(Span<float> dest)
    {
        var c = MemoryMarshal.Cast<float, Vector3>(dest);
        c[0] = -Min;
        c[1] = Max;
    }
    public static IEnumerable<BoxSide> Sides()
    {
        yield return BoxSide.XMin;
        yield return BoxSide.XMax;
        yield return BoxSide.YMin;
        yield return BoxSide.YMax;
        yield return BoxSide.ZMin;
        yield return BoxSide.ZMax;
    }
    public void Side(BoxSide side, Span<Vector3> dst)
    {
        switch (side)
        {
            case BoxSide.XMin:
                dst[0] = new Vector3(Min.X, Min.Y, Min.Z);
                dst[1] = new Vector3(Min.X, Min.Y, Max.Z);
                dst[2] = new Vector3(Min.X, Max.Y, Max.Z);
                dst[3] = new Vector3(Min.X, Max.Y, Min.Z);
                break;
            case BoxSide.XMax:
                dst[0] = new Vector3(Max.X, Min.Y, Min.Z);
                dst[1] = new Vector3(Max.X, Max.Y, Min.Z);
                dst[2] = new Vector3(Max.X, Max.Y, Max.Z);
                dst[3] = new Vector3(Max.X, Min.Y, Max.Z);
                break;
            case BoxSide.YMin:
                dst[0] = new Vector3(Min.X, Max.Y, Min.Z);
                dst[1] = new Vector3(Min.X, Max.Y, Max.Z);
                dst[2] = new Vector3(Max.X, Max.Y, Max.Z);
                dst[3] = new Vector3(Max.X, Max.Y, Min.Z);
                break;
            case BoxSide.YMax:
                dst[0] = new Vector3(Min.X, Min.Y, Min.Z);
                dst[1] = new Vector3(Max.X, Min.Y, Min.Z);
                dst[2] = new Vector3(Max.X, Min.Y, Max.Z);
                dst[3] = new Vector3(Min.X, Min.Y, Max.Z);
                break;
            case BoxSide.ZMin:
                dst[0] = new Vector3(Min.X, Min.Y, Min.Z);
                dst[1] = new Vector3(Min.X, Max.Y, Min.Z);
                dst[2] = new Vector3(Max.X, Max.Y, Min.Z);
                dst[3] = new Vector3(Max.X, Min.Y, Min.Z);
                break;
            case BoxSide.ZMax:
                dst[0] = new Vector3(Min.X, Min.Y, Max.Z);
                dst[1] = new Vector3(Max.X, Min.Y, Max.Z);
                dst[2] = new Vector3(Max.X, Max.Y, Max.Z);
                dst[3] = new Vector3(Min.X, Max.Y, Max.Z);
                break;
        }
    }
}
