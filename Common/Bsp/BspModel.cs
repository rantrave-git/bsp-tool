using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using Bsp.Common.Geometry;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bsp.Common.Bsp;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex
{
    public Vector3 Co;
    public Vector2 Uv;
    public Vector2 LightmapUv;
    public int Color;
}
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64 + sizeof(int) * 2)]
public unsafe struct Texture
{
    public fixed byte Name[64];
    public int Flags = 0;
    public int Contents = 0;

    public Texture()
    {
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            sp.Clear();
        }
    }
    public Texture(string name, int flags, int contents)
    {
        name ??= "noshader";
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            var shift = Encoding.ASCII.GetBytes(name.AsSpan(), sp);
            sp[shift..].Clear();
        }
        Flags = flags;
        Contents = contents;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64 + sizeof(int) * 2)]
public unsafe struct Effect
{
    public fixed byte Name[64];
    public int Brush = 0;
    public int Unused = -1;

    public Effect()
    {
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            sp.Clear();
        }
    }
    public Effect(string name, int brush, int unused = -1)
    {
        name ??= "noshader";
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            var shift = Encoding.ASCII.GetBytes(name.AsSpan(), sp);
            sp[shift..].Clear();
        }
        Brush = brush;
        Unused = unused;
    }
}
public interface ITexture
{
    string Name { get; }
    int Flags { get; }
    int Contents { get; }
}
public interface IEffect
{
    string Name { get; }
    IBrush Brush { get; }
    int Contents { get; }
}
public interface IBrush
{
    List<(Vector4 Plane, ITexture Texture)> Sides { get; }
    ITexture Texture { get; }
}
public enum MeshType : int
{
    Face = 0x1,
    Patch = 0x2,
    Mesh = 0x3,
    Billboard = 0x4,
}
public interface IMesh
{
    ITexture Texture { get; }
    IEffect? Effect { get; }
    MeshType Type { get; }
    List<Vertex> Vertices { get; }
    List<int> Indices { get; }
}


public interface IBaseBspNode
{
    Vector3i Mins { get; }
    Vector3i Maxs { get; }
}

public interface IBspNode : IBaseBspNode
{
    Vector4 Plane { get; }
    IBspNode[] Children { get; }
}
public interface IBspLeaf : IBaseBspNode
{
    List<IBrush> Brushes { get; }
    List<IMesh> Meshes { get; }
}

public class BspModel
{
    private record Brush(int Id, List<(Vector4 Plane, ITexture Texture)> Sides, ITexture Texture) : IBrush { }
    private record Mesh(
        int Id,
        ITexture Texture,
        IEffect? Effect,
        MeshType Type,
        List<Vertex> Vertices,
        List<int> Indices
        ) : IMesh
    { }
    private record BspNode(int Id, Vector4 Plane, IBspNode[] Children, Vector3i Mins, Vector3i Maxs) : IBspNode { }
    private record BspLeaf(int Id, List<IBrush> Brushes, List<IMesh> Meshes, Vector3i Mins, Vector3i Maxs) : IBspLeaf { }
    private sealed record Effect(int Id, string Name, IBrush Brush, int Contents) : IEffect
    {
        public override int GetHashCode() => Name.GetHashCode() ^ Brush.GetHashCode() ^ Contents.GetHashCode();
        public bool Equals(Effect? val) => val != null && Name == val.Name && Brush == val.Brush && Contents == val.Contents;
    }

    private Dictionary<Effect, Effect> Effects { get; } = new();
    private BspNode? BspTree { get; }
    private List<Brush> Brushes { get; } = new();
    private List<Mesh> Meshes { get; } = new();

    // public int Serialize(int index, IContext<Vector4> planes, IContext<ITexture> textures)

}
public class Entity
{
    public Dictionary<string, string> Fields { get; }
}

public interface IContext<T>
{
    int GetOrAdd(T value);
}

public interface ILump<T>
{
    int Size { get; }
    int Write(Span<byte> dst);
}
public abstract class ContextCollection<T, TKey> : IContext<T> where TKey : notnull
{
    readonly List<T> _values = new();
    readonly Dictionary<TKey, int> _indices = new();
    public T this[int index]
    {
        get => _values[index];
    }

    public int GetOrAdd(T value)
    {
        var key = GetKey(value);
        if (_indices.TryGetValue(key, out var i)) return i;
        i = _indices[key] = _values.Count;
        _values.Add(value);
        return i;
    }

    protected abstract TKey GetKey(T value);
}
public class PlaneCollection : IContext<Vector4>
{
    readonly List<Vector4> _planes = new();
    readonly Dictionary<Vector128<ulong>, int> _planeKeys = new();
    public Vector4 this[int index]
    {
        get
        {
            return _planes[index];
        }
    }
    public int GetOrAdd(Vector4 plane)
    {
        var pos = plane.AsVector128().AsUInt64();
        if (_planeKeys.TryGetValue(pos, out var i)) return i;
        var neg = (-plane).AsVector128().AsUInt64();
        if (_planeKeys.TryGetValue(neg, out i)) return i;

        var res = _planeKeys[pos] = _planes.Count;
        _planeKeys[neg] = _planes.Count + 1;
        _planes.Add(plane);
        _planes.Add(-plane);
        return res;
    }
}

public class Lightmap
{
    public byte[] Texture { get; }
}
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LightVolume
{
    public ulong Lightdata;
    public LightVolume(Vector3 ambient, Vector3 directional, Vector2 direction)
    {
        var v = Vector3.Clamp(ambient, Vector3.Zero, Vector3.One) * 256f - Vector3.One * 0.01f;
        Lightdata = ((ulong)(v.X) << (7 * 8)) | ((ulong)(v.Y) << (6 * 8)) | ((ulong)(v.Z) << (5 * 8));
        v = Vector3.Clamp(directional, Vector3.Zero, Vector3.One) * 256f - Vector3.One * 0.01f;
        Lightdata |= ((ulong)(v.X) << (4 * 8)) | ((ulong)(v.Y) << (3 * 8)) | ((ulong)(v.Z) << (2 * 8));
        var vv = Vector2.Clamp(direction, Vector2.Zero, Vector2.One) * 256f - Vector2.One * 0.01f;
        Lightdata |= ((ulong)(vv.X) << 8) | (ulong)(vv.Y);
    }
}

public class TextureCollection : ContextCollection<ITexture, (string, int, int)>
{
    protected override (string, int, int) GetKey(ITexture value) => (value.Name, value.Flags, value.Contents);
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 256)]
public unsafe struct Advertisement
{
    public fixed byte Data[256];

    public Advertisement() { }
    public Advertisement(string data)
    {
        fixed (byte* d = Data)
        {
            Span<byte> sp = new(d, 256);
            var shift = Encoding.ASCII.GetBytes(data.AsSpan(), sp);
            sp[shift..].Clear();
        }
    }
}

public interface IBspContext
{
    IContext<Entity> Entities { get; }
    IContext<ITexture> Textures { get; }
    IContext<Vector4> Planes { get; }
    IContext<IBspNode> Nodes { get; }
    IContext<IBspLeaf> Leafs { get; }
    IContext<int> LeafFaces { get; }
    IContext<int> LeafBrushes { get; }
    IContext<BspModel> Models { get; }
    IContext<IBrush> Brushes { get; }
    IContext<int> BrushSides { get; }
    IContext<Vertex> Vertices { get; }
    IContext<int> MeshVerts { get; }
    IContext<IEffect> Effects { get; }
    IContext<Lightmap> Lightmaps { get; }
    IContext<LightVolume> LightVolumes { get; }
    IContext<Advertisement> Ads { get; }
}

public class BspContext
{
    public PlaneCollection Planes { get; } = new();
    public TextureCollection Textures { get; } = new();
    public List<BspModel> Models { get; } = new();
    public List<Entity> Entities { get; } = new();

}