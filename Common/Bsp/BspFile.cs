using System.Numerics;
using System.Runtime.InteropServices;
using Bsp.Common.Geometry;

namespace Bsp.Common.BspTest;


public enum LumpType
{
    Entities,
    Textures,
    Planes,
    Nodes,
    Leafs,
    LeafFaces,
    LeafBrushes,
    Models,
    Brushes,
    BrushSides,
    Vertices,
    MeshVertices,
    Effects,
    Faces,
    Lightmaps,
    Lightvols,
    Visdata,
    Advertisements,
}

public enum LumpMode
{
    Unique,
    Sequential,
    Single,
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class LumpMemberAttribute : Attribute
{
    public LumpType Lump { get; }

    public LumpMemberAttribute(LumpType lump)
    {
        Lump = lump;
    }
}

// [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
// public sealed class LumpDependencyAttribute : Attribute
// {
//     public LumpType Lump { get; }

//     public LumpDependencyAttribute(LumpType lump)
//     {
//         Lump = lump;
//     }
// }

public interface ILump
{
    IEnumerable<ILump> Dependants { get; }
    int Serialize(Stream dst);
    int Deserialize(Stream src);
}

public interface ILump<T>
{
    IObjectReference<T> AddElement(T value);
}

public class LumpUnique<T> : ILump<T> where T : notnull
{
    class ObjectReference : IObjectReference<T>
    {
        public T Value { get; init; }
        public int Index { get; set; }
        public ObjectReference(T value) { Value = value; }
    }
    Dictionary<T, ObjectReference> Values { get; } = new();

    public IObjectReference<T> AddElement(T value)
    {
        if (Values.TryGetValue(value, out var r)) return r;
        r = new ObjectReference(value);
        Values[value] = r;
        return r;
    }
}
public class LumpSingle<T> : ILump<T>
{
    class ObjectReference : IObjectReference<T>
    {
        public T Value { get; }
        public int Index => 0;
        public ObjectReference(T value) { Value = value; }
    }
    ObjectReference? Value { get; set; } = null;

    public IObjectReference<T> AddElement(T value)
    {
        if (Value != null) return Value;
        Value = new ObjectReference(value);
        return Value;
    }
}
public class LumpSequential<T> : ILump<T>
{
    class ObjectReference : IObjectReference<T>
    {
        public T Value { get; }
        public int Index => 0;
        public ObjectReference(T value) { Value = value; }
    }
    public List<T> Values { get; } = new();

    public IObjectReference<T> AddElement(T value)
    {
        throw new NotImplementedException();
    }
}
public class BspContext
{
    public List<(LumpType Lump, LumpMode Mode)> LumpMetadata;
}
public interface ISerializable
{
    int Size { get; }
    Span<byte> Write(Span<byte> dest);
}

public interface IObjectReference<T>
{
    T Value { get; }
    int Index { get; }
}
public static class BinaryWriteExtensions
{
    public static Span<byte> Write<T>(this Span<byte> dest, T value) where T : struct
    {
        MemoryMarshal.TryWrite(dest, ref value);
        return dest[Marshal.SizeOf<T>()..];
    }
    public static Span<byte> Write<T>(this Span<byte> dest, IObjectReference<T> value) => dest.Write(value.Index);
    public static Span<byte> Write<T>(this Span<byte> dest, T[] values) where T : struct
    {
        MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(dest);
        return dest[(Marshal.SizeOf<T>() * values.Length)..];
    }
}
[LumpMember(LumpType.LeafBrushes)]
public record LeafBrush(IObjectReference<Brush> Brush);
[LumpMember(LumpType.LeafFaces)]
public record LeafFace(IObjectReference<Face> Face);
[LumpMember(LumpType.MeshVertices)]
public record MeshVertex(IObjectReference<Vertex> Vertex);

[LumpMember(LumpType.Planes)]
public record Plane(Vector4 P);

[LumpMember(LumpType.BrushSides)]
public class BrushSide
{
    public IObjectReference<Plane> Plane { get; }
    public IObjectReference<Texture> Texture { get; }
}
[LumpMember(LumpType.Brushes)]
public class Brush
{
    public List<BrushSide> Sides { get; }
    public IObjectReference<Texture> Texture { get; }
}
[LumpMember(LumpType.Vertices)]
public class Vertex : ISerializable
{
    public Vector3 Co { get; }
    public Vector2 Uv { get; }
    public Vector2 LightmapUv { get; }
    public int Color { get; }

    public int Size => 7 * sizeof(float) + sizeof(int);

    public Span<byte> Write(Span<byte> dest) => dest.Write(Co).Write(Uv).Write(LightmapUv).Write(Color);
}
[LumpMember(LumpType.Faces)]
public class Face
{
    public IObjectReference<Texture> Texture { get; }
    public IObjectReference<Effect> Effect { get; }
    public int Type { get; }
    public List<Vertex> Vertices { get; }
    public List<MeshVertex> Indices { get; }
    public Vector3 Normal { get; }
    public int PatchX { get; }
    public int PatchY { get; }
}
[LumpMember(LumpType.Models)]
public class Model
{
    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public List<Face> Faces { get; }
    public List<Brush> Brushes { get; }

}
public interface IEntityField
{
    public string Value { get; }
}
public class EntityField : IEntityField
{
    public string Value { get; }
}
public class ModelReference : IEntityField
{
    public string Value => $"*{Model.Index}";
    public IObjectReference<Model> Model { get; }
}
[LumpMember(LumpType.Entities)]
public class Entity
{
    public Dictionary<string, IEntityField> Fields { get; }
}
[LumpMember(LumpType.Textures)]
public record Texture(string Name, int Flags, int Contents) { }
[LumpMember(LumpType.Effects)]
public record Effect(string Name, IObjectReference<Brush> Brush, int Contents) { }

[LumpMember(LumpType.Lightmaps)]
public class Lightmap
{
    public byte[] Map = new byte[128 * 128 * 3];
}
[LumpMember(LumpType.Lightvols)]
public class Lightvol
{

}
[LumpMember(LumpType.Visdata)]
public class Visdata
{
    public int VecsN { get; }
    public int VecsSize { get; }
    public byte[] Vecs { get; }
}
public interface INode
{
}
[LumpMember(LumpType.Nodes)]
public class Node : INode
{
    public INode? Front { get; }
    public INode? Back { get; }
    public Vector3 Plane { get; }
    public int[] Min { get; } = new int[3];
    public int[] Max { get; } = new int[3];
}
[LumpMember(LumpType.Leafs)]
public class Leaf : INode
{
    public int Cluster { get; }
    public int Area { get; }
    public int[] Min { get; } = new int[3];
    public int[] Max { get; } = new int[3];
    public List<LeafFace> LeafFaces { get; } = new();
    public List<LeafBrush> LeafBrushes { get; } = new();
}


public class BspFile
{
    public List<Brush> Brushes { get; } = new List<Brush>();
    public List<Model> Models { get; } = new List<Model>();
    public List<Texture> Textures { get; } = new List<Texture>();
    public List<Effect> Effects { get; } = new List<Effect>();
    public List<Entity> Entities { get; } = new List<Entity>();

    public void ReadStream(ReadOnlySpan<byte> bytes)
    {

    }
}
