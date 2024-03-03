using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Bsp.BspFormat;


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
    public Texture(string? name, int flags, int contents)
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
    public int Unknown = 5;

    public Effect()
    {
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            sp.Clear();
        }
    }
    public Effect(string? name, int brush, int unknown = 5)
    {
        name ??= "noshader";
        fixed (byte* d = Name)
        {
            Span<byte> sp = new(d, 64);
            var shift = Encoding.ASCII.GetBytes(name.AsSpan(), sp);
            sp[shift..].Clear();
        }
        Brush = brush;
        Unknown = unknown;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = sizeof(float) * 4)]
public struct Plane
{
    public Vector3 Normal;
    public float Dist;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Extents2
{
    public int X;
    public int Y;
}
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Extents3
{
    public int X;
    public int Y;
    public int Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Node
{
    public int Plane;
    public int Back;
    public int Front;
    public Extents3 Mins;
    public Extents3 Maxs;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ArrayRange
{
    public int Offset;
    public int Length;
    public readonly int End() => Offset + Length;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Leaf
{
    public int Cluster;
    public int Area;
    public Extents3 Mins;
    public Extents3 Maxs;
    public ArrayRange Faces;
    public ArrayRange Brushes;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Model
{
    public Extents3 Mins;
    public Extents3 Maxs;
    public ArrayRange Faces;
    public ArrayRange Brushes;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Brush
{
    public ArrayRange Sides;
    public int Texture;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BrushSide
{
    public int Plane;
    public int Texture;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ColorRGB
{
    public byte R;
    public byte G;
    public byte B;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ColorRGBA
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex
{
    public Vector3 Position;
    public Vector2 UV;
    public Vector2 LM;
    public Vector3 Normal;
    public ColorRGBA Color;
}

public enum FaceType : int
{
    Polygon = 1,
    Patch = 2,
    Mesh = 3,
    Billboard = 4,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Face
{
    public int Texture;
    public int Effect;
    public FaceType Type;
    public ArrayRange Vertices;
    public ArrayRange Indices;
    readonly int _LmIndex;
    readonly long _LmStart;
    readonly long _LmSize;
    readonly Vector3 _LmOrigin;
    readonly Vector3 _lmVecS;
    readonly Vector3 _lmVecT;
    public Vector3 Normal;
    public Extents2 Size;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128 * 128 * 3)]
public unsafe struct Lightmap
{
    public fixed byte Map[128 * 128 * 3];
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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LightVolume
{
    public ColorRGB Ambient;
    public ColorRGB Directional;
    public byte DirPhi;
    public byte DirTheta;
}

public interface ILump
{
    int CalcSize();
    int Read(ReadOnlySpan<byte> data);
    void Write(Span<byte> data);
}

public class StructLump<T> : ILump where T : struct
{
    private T[] _data = Array.Empty<T>();
    public int CalcSize() => _data.Length * Marshal.SizeOf<T>();

    public StructLump() { }
    public StructLump(IEnumerable<T> elements)
    {
        _data = elements.ToArray();
    }
    public int Read(ReadOnlySpan<byte> data)
    {
        var size = Marshal.SizeOf<T>();
        _data = new T[data.Length / size];
        var length = _data.Length * size;
        MemoryMarshal.Cast<byte, T>(data)[0..length].CopyTo(_data.AsSpan());
        return length;
    }

    public void Write(Span<byte> data) => MemoryMarshal.Cast<T, byte>(_data).CopyTo(data);

    public bool IsValid(int index) => index >= 0 && index < _data.Length;
}

public class ReferenceLump<T> : ILump where T : struct
{
    private int[] _refs = Array.Empty<int>();
    private readonly StructLump<T> _lump;

    public ReferenceLump(StructLump<T> lump)
    {
        _lump = lump;
    }
    public int CalcSize() => _refs.Length * sizeof(int);

    public int Read(ReadOnlySpan<byte> data)
    {
        _refs = new int[data.Length / sizeof(int)];
        var length = _refs.Length * sizeof(int);
        MemoryMarshal.Cast<byte, int>(data)[0..length].CopyTo(_refs.AsSpan());
        return length;
    }

    public void Write(Span<byte> data) => MemoryMarshal.Cast<int, byte>(_refs).CopyTo(data);
    public bool IsValid(int index) => index >= 0 && index < _refs.Length && _lump.IsValid(_refs[index]);
}

public class Entity
{
    public Dictionary<string, string> Values = new();
    public Entity() { }
    public Entity(Dictionary<string, string> values)
    {
        Values = values.ToDictionary(x => x.Key, x => x.Value);
    }
    public int CalcSize()
    {
        int res = 4;
        foreach (var i in Values)
        {
            res += 8 + i.Key.Length + i.Value.Length;
        }
        return res;
    }
    public void Print(StringBuilder stringBuilder)
    {
        stringBuilder.Append("{\n");
        foreach (var i in Values)
        {
            stringBuilder.Append("  \"");
            stringBuilder.Append(i.Key);
            stringBuilder.Append("\" \"");
            stringBuilder.Append(i.Value);
            stringBuilder.Append("\"\n");
        }
        stringBuilder.Append("}\n");
    }

    public int Read(ReadOnlySpan<byte> bytes)
    {
        int state = 0;
        int offset = 0;
        string key = null!;
        if (bytes.Length == 0) return 0;
        for (int i = 0; i < bytes.Length; ++i)
        {
            if (state == 0 && bytes[i] == '{')
            {
                // reading object
                state = 1;
            }
            if (state == 1 && bytes[i] == '"')
            {
                // reading key
                state = 2;
                offset = i + 1;
            }
            if (state == 2 && bytes[i] == '"')
            {
                // done reading key
                state = 3;
                key = Encoding.ASCII.GetString(bytes[offset..(i - 1)]);
            }
            if (state == 3 && bytes[i] == '"')
            {
                // reading value
                state = 4;
                offset = i + 1;
            }
            if (state == 4 && bytes[i] == '"')
            {
                // done reading key
                state = 1;
                Values[key!] = Encoding.ASCII.GetString(bytes[offset..(i - 1)]);
            }
            if (state == 1 && bytes[i] == '}')
            {
                state = 0;
                offset = i;
                break;
            }
        }
        if (state != 0) return -1; //throw new FormatException("Unable to read object");
        return offset;
    }
}

public class EntityLump : ILump
{
    private readonly List<Entity> _entities = new();

    public int CalcSize() => _entities.Sum(x => x.CalcSize());

    public int Read(ReadOnlySpan<byte> data)
    {
        var s = data;
        int pos = 0;
        while (true)
        {
            var e = new Entity();
            var offset = e.Read(s);
            if (offset <= 0) break;
            _entities.Add(e);
            pos += offset;
            s = data[pos..];
        }
        return pos;
    }

    public void Write(Span<byte> data)
    {
        var sb = new StringBuilder();
        foreach (var i in _entities) i.Print(sb);
        Encoding.ASCII.GetBytes(sb.ToString(), data);
    }
}

public class VisLump : ILump
{
    private int _count = 0;
    private int _size = 0;
    private byte[] _vecs = Array.Empty<byte>();
    public VisLump() { }
    public VisLump(int count, int size, byte[] vecs)
    {
        _count = count;
        _size = size;
        _vecs = vecs;
    }
    public int CalcSize() => _count * _size + 2 * sizeof(int);

    public int Read(ReadOnlySpan<byte> data)
    {
        _count = BitConverter.ToInt32(data[..sizeof(int)]);
        _size = BitConverter.ToInt32(data[sizeof(int)..(sizeof(int))]);
        _vecs = new byte[_count * _size];
        data[(2 * sizeof(int))..(2 * sizeof(int) + _vecs.Length)].CopyTo(_vecs);
        return CalcSize();
    }

    public void Write(Span<byte> data)
    {
        BitConverter.TryWriteBytes(data[..sizeof(int)], _count);
        BitConverter.TryWriteBytes(data[sizeof(int)..(2 * sizeof(int))], _size);
        _vecs.CopyTo(data[(2 * sizeof(int))..]);
    }
}
