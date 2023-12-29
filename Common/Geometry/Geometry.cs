using System.Numerics;
using System.Runtime.InteropServices;

namespace Bsp.Common.Geometry;

public class AABB
{
    public Vector3 Min = new Vector3(float.MaxValue);
    public Vector3 Max = new Vector3(-float.MaxValue);

    public AABB(Vector3 point)
    {
        Max = Min = point;
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
}

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
    public long Flags;
    public int LoopStart;
    public int LoopTotal;
    public int Material;
    public int _pad;
}
public class MeshContext
{
    public VertexData[] Vertices;
    public CornerData[] Corners;
    public FaceData[] Faces;
    public long Content;
    public MeshContext(VertexData[] vertices, CornerData[] corners, FaceData[] faces, long content)
    {
        Vertices = vertices;
        Corners = corners;
        Faces = faces;
        Content = content;
    }
}


public class Vertex
{
    public Mesh Mesh { get; }

    public Vector3 Pos { get; init; }
    public Vector4 Uvs { get; init; }
    public Vector4 Color { get; init; }
    public Vertex(Mesh mesh) => Mesh = mesh;
}
public class Face
{
    public Mesh Mesh { get; }
    public int[] Indices { get; init; }
    public long Flags { get; init; }
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

            var v = Vector3.Cross(v0, v1);
            var nrm = v.Length();
            if (nrm < Linealg.Eps) continue;
            sum += v / nrm;
        }
        var len = sum.Length();
        if (len < Linealg.Eps) return Vector3.Zero;
        return Vector3.Normalize(sum);
    }
    public Face(Mesh mesh, int[] indices)
    {
        Mesh = mesh;
        Indices = indices;
        Normal = Face.CalculateNormal(mesh, indices);
    }
    public void UpdateNormal() => Normal = Face.CalculateNormal(Mesh, Indices);
}
public class Mesh
{
    public List<Vertex> Vertices { get; private set; } = default!;
    public List<Face> Faces { get; private set; } = default!;
    public long Content { get; set; } = 0;
    // private SortedSet<int> _removedVertices = new();
    private readonly SortedSet<int> _removedFaces = new();
    private Mesh() { }
    public Mesh(IList<Vector3> points, IList<(int[] Indices, long Flags)> faces, long content)
    {
        Vertices = new();
        Faces = new();
        foreach (var p in points)
        {
            Vertices.Add(new Vertex(this)
            {
                Pos = p
            });
        }
        foreach (var (Indices, Flags) in faces)
        {
            Faces.Add(new Face(this, Indices)
            {
                Flags = Flags,
                Material = 0,
            });
        }
        Content = content;
    }
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
                myFaces[i] = myFaces[^shift];
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
        foreach (var face in m.Faces)
        {
            face.UpdateNormal();
        }
        return m;
    }

    public MeshContext ToContext(float cellSize = 1e-4f)
    {
        Console.WriteLine("BIMBO!!!!!");
        var grid = new Dictionary<(long, long, long), List<int>>();
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
        var remap = new Dictionary<int, int>();
        foreach (var i in grid)
        {
            var v = i.Value.FirstOrDefault();
            foreach (var j in i.Value)
            {
                remap[j] = v;
            }
        }
        var vertices = new List<VertexData>();
        var reremap = new Dictionary<int, int>();
        foreach (var v in remap)
        {
            if (reremap.ContainsKey(v.Value))
                continue;
            reremap[v.Value] = vertices.Count;
            vertices.Add(new VertexData()
            {
                Pos = Vertices[v.Value].Pos,
                Flags = 0
            });
        }
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
                Vertex = reremap[remap[x]]
            }));
            faces[i] = new FaceData()
            {
                Flags = f.Flags,
                LoopStart = lstart,
                LoopTotal = corners.Count - lstart,
                Material = f.Material,
            };
        }
        return new MeshContext(vertices.ToArray(), corners.ToArray(), faces, Content);
    }
}
