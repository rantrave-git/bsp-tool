using System.Numerics;
using System.Runtime.InteropServices;

namespace Bsp.BspFormat;

[StructLayout(LayoutKind.Sequential)]
public struct VertexData
{
    public Vector3 Pos;
    public int Flags;
}
[StructLayout(LayoutKind.Sequential)]
public struct CornerData
{
    public Vector2 Uv0;
    public Vector2 Uv1;
    public Vector4 Color;
    public int Vertex;
}
[StructLayout(LayoutKind.Sequential, Size = 20)]
public struct FaceData
{
    public long Flags;
    public int LoopStart;
    public int LoopTotal;
    public int Material;
}
public class MeshContext
{
    public VertexData[] Vertices;
    public CornerData[] Corners;
    public FaceData[] Faces;
    public MeshContext(VertexData[] vertices, CornerData[] corners, FaceData[] faces)
    {
        Vertices = vertices;
        Corners = corners;
        Faces = faces;
    }
    public int MaxCorners => Faces.Max(x => x.LoopTotal);
}