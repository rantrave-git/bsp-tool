using System.Numerics;
using Bsp.Geometry;

namespace Bsp.Tree;


public class BspNode2D
{
    private BspTree2D _tree;
    private List<int> _faces;
    private Vector2 _plane;
    private BspNode _front;
    private BspNode _back;

    public void AddFace(int face)
    {

    }
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