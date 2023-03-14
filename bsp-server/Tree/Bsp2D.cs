using System.Numerics;
using Bsp.Geometry;

namespace Bsp.Tree;


struct Interval
{
    public float Min;
    public float Max;
}

interface IBspNode2D
{
    BspTree2D Tree { get; }
    void AddEdge(float min, float max);
    // BspTree2D Merge(BspNode2D other);
    IBspNode2D Clone();
}
public class BspNode2D : IBspNode2D
{
    public BspTree2D Tree { get; init; }
    // null for leaf
    private IntervalSystem? _boundary = null;
    // null for node
    private HashSet<int>? _faces = null;
    // ax + by + d = 0
    private Vector3 _plane;
    private BspNode2D? _front;
    private BspNode2D? _back;

    public void AddEdge(float min, float max) => _boundary?.Add(min, max);

}
public class BspTree2D
{
    private Mesh _context;
    public Vector4 TangentPlane { get; }
    public Vector2 GetPlane(int face)
    {
        var f = _context.Faces[face];
        f.Indices
    }
}