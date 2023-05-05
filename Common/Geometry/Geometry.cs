using System.Numerics;
using System.Runtime.InteropServices;

namespace Bsp.Common.Geometry;

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
[StructLayout(LayoutKind.Sequential)]
public struct FaceData
{
    public int LoopStart;
    public int LoopTotal;
    public int Flags;
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
}


public class Vertex
{
    private Mesh _mesh;

    public Vector3 Pos { get; init; }
    public Vector4 Uvs { get; init; }
    public Vector4 Color { get; init; }
    public Vertex(Mesh mesh) => _mesh = mesh;
}
public class Face
{
    private Mesh _mesh;
    public int[] Indices { get; init; }
    public int Flags { get; init; }
    public int Material { get; init; }
    public Vector3 Normal { get; private set; }
    public static Vector3 CalculateNormal(Mesh mesh, int[] indices)
    {
        var s = indices.Length;
        var sum = Vector3.Zero;
        for (int i = 0; i < indices.Length; ++i)
        {
            var v0 = mesh.Vertices[indices[(i + 1) % s]].Pos - mesh.Vertices[indices[i]].Pos;
            var v1 = mesh.Vertices[indices[(i + 2) % s]].Pos - mesh.Vertices[indices[i]].Pos;

            sum += Vector3.Cross(v0, v1);
        }

        return Vector3.Normalize(sum);
    }
    public Face(Mesh mesh, int[] indices)
    {
        _mesh = mesh;
        Indices = indices;
        Normal = Face.CalculateNormal(mesh, indices);
    }
    public void UpdateNormal() => Normal = Face.CalculateNormal(_mesh, Indices);
}
public class Mesh
{
    public List<Vertex> Vertices { get; private set; }
    public List<Face> Faces { get; private set; }
    private SortedSet<int> _removedVertices = new SortedSet<int>();
    private SortedSet<int> _removedFaces = new SortedSet<int>();
    private Mesh() { }
    public int AddFace(Face face)
    {
        Faces.Add(face);
        return Faces.Count - 1;
    }
    public bool RemoveFace(int face) => _removedFaces.Add(face);
    public void ApplyFaceRemove(List<int> myFaces)
    {
        var remap = new Dictionary<int, int>();
        var shift = 0;
        for (int i = Faces.Count - 1; i >= 0; --i)
        {
            if (_removedFaces.Contains(i))
            {
                shift++;
                continue;
            }
            Faces[i - shift] = Faces[i];
            remap[i] = i - shift;
        }
        for (int i = 0; i < myFaces.Count; ++i)
        {
            myFaces[i] = remap.GetValueOrDefault(myFaces[i], -1);

        }
        shift = 1;
        for (int i = myFaces.Count - 1; i >= 0; ++i)
        {
            if (myFaces[i] == -1)
            {
                myFaces[i] = myFaces[myFaces.Count - shift];
                shift++;
            }
        }
    }
    public static Mesh FromContext(MeshContext context)
    {
        var m = new Mesh();
        m.Vertices = context.Corners.Select(x => new Vertex(m)
        {
            Pos = context.Vertices[x.Vertex].Pos,
            Color = x.Color,
            Uvs = new Vector4(x.Uv0, x.Uv1.X, x.Uv1.Y),
        }).ToList();
        m.Faces = context.Faces.Select(x => new Face(m, Enumerable.Range(x.LoopStart, x.LoopTotal).ToArray())
        {
            Flags = x.Flags,
            Material = x.Material
        }).ToList();
        return m;
    }
    public MeshContext ToContext(float cellSize = 1e-4f)
    {
        Dictionary<(long, long, long), List<int>> grid = new Dictionary<(long, long, long), List<int>>();
        for (int i = 0; i < Vertices.Count; ++i)
        {
            var noise = new Vector3(Random.Shared.NextSingle(), Random.Shared.NextSingle(), Random.Shared.NextSingle());
            var v = Vertices[i].Pos / cellSize + noise * cellSize * .5f;
            var vv = ((int)v.X, (int)v.Y, (int)v.Z);
            if (grid.TryGetValue(vv, out var lst))
            {
                lst.Add(i);
            }
            else
            {
                grid[vv] = new List<int>() { i };
            }
        }
        Dictionary<int, int> remap = new Dictionary<int, int>();
        foreach (var i in grid)
        {
            var v = i.Value.FirstOrDefault();
            foreach (var j in i.Value)
            {
                remap[j] = v;
            }
        }
        var vertices = remap.Select(x => new VertexData() { Pos = Vertices[x.Value].Pos, Flags = 0 }).ToArray();
        var faces = new FaceData[Faces.Count];
        var corners = new List<CornerData>();
        for (int i = 0; i < Faces.Count; ++i)
        {
            var f = Faces[i];
            var lstart = corners.Count;
            corners.AddRange(f.Indices.Select(x => new CornerData()
            {
                Color = Vertices[x].Color,
                Uv0 = new Vector2(Vertices[x].Uvs.X, Vertices[x].Uvs.Y),
                Uv1 = new Vector2(Vertices[x].Uvs.Z, Vertices[x].Uvs.W),
                Vertex = remap[x]
            }));
            faces[i] = new FaceData()
            {
                Flags = f.Flags,
                LoopStart = lstart,
                LoopTotal = corners.Count - lstart,
                Material = f.Material,
            };
        }
        return new MeshContext(vertices, corners.ToArray(), faces);
    }
}
