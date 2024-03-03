using System.Runtime.InteropServices;
using System.Text;

namespace Bsp.BspFormat;


public class File
{
    class Header
    {
        public int Version;
        public Dictionary<LumpType, ArrayRange> Direntries = new();
        public const int Padding = 64;
        public static byte[] VersionInfo = { 0x42, 0x53, 0x50, 0x20, 0x62, 0x79, 0x20, 0x72, 0x61, 0x6e, 0x74, 0x72, 0x61, 0x76, 0x65, 0x00 };

        private static int NumEntries(int version) => version switch
        {
            0x2e => 17,
            0x2f => 18,
            _ => 0,
        };
        private static readonly Dictionary<int, IEnumerable<LumpType>> VersionLumps = new()
        {
            [0x2e] = new List<LumpType>() {
                LumpType.Entities,
                LumpType.Textures,
                LumpType.Planes,
                LumpType.Nodes,
                LumpType.Leafs,
                LumpType.LeafFaces,
                LumpType.LeafBrushes,
                LumpType.Models,
                LumpType.Brushes,
                LumpType.BrushSides,
                LumpType.Vertices,
                LumpType.MeshVertices,
                LumpType.Effects,
                LumpType.Faces,
                LumpType.Lightmaps,
                LumpType.Lightvols,
                LumpType.Visdata,
            },
            [0x2f] = new List<LumpType>() {
                LumpType.Entities,
                LumpType.Textures,
                LumpType.Planes,
                LumpType.Nodes,
                LumpType.Leafs,
                LumpType.LeafFaces,
                LumpType.LeafBrushes,
                LumpType.Models,
                LumpType.Brushes,
                LumpType.BrushSides,
                LumpType.Vertices,
                LumpType.MeshVertices,
                LumpType.Effects,
                LumpType.Faces,
                LumpType.Lightmaps,
                LumpType.Lightvols,
                LumpType.Visdata,
                LumpType.Advertisements,
            },
        };
        public static IEnumerable<LumpType> Lumps(int version) => VersionLumps.GetValueOrDefault(version, Enumerable.Empty<LumpType>());
        public IEnumerable<LumpType> LumpTypes => Lumps(Version);
        public static int CalcSize(int version) => 4 + sizeof(int) + Marshal.SizeOf<ArrayRange>() * NumEntries(version) + Padding + VersionInfo.Length;
        public int Size => CalcSize(Version);
        public int ReadInfo(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4 + sizeof(int)) return -1;
            if (data[0] != 'I' ||
                data[1] != 'B' ||
                data[2] != 'S' ||
                data[3] != 'P')
            {
                throw new FormatException("Invalid BSP header");
            }
            Version = BitConverter.ToInt32(data[4..(4 + sizeof(int))]);
            return 4 + sizeof(int);
        }
        public int ReadDirentries(ReadOnlySpan<byte> data)
        {
            var nent = NumEntries(Version);
            var end = nent * Marshal.SizeOf<ArrayRange>();
            if (data.Length < end) return -1;
            var de = new ArrayRange[nent];
            MemoryMarshal.Cast<byte, ArrayRange>(data[..end]).CopyTo(de);

            Direntries = de.Zip(LumpTypes).ToDictionary(x => x.Second, x => x.First);
            return end;
        }

        public void Write(Span<byte> dest)
        {
            var nent = NumEntries(Version);
            Encoding.ASCII.GetBytes("IBSP", dest[0..4]);
            BitConverter.TryWriteBytes(dest[4..(4 + sizeof(int))], Version);
            var de = LumpTypes.Select(x => Direntries[x]).ToArray();
            var db = MemoryMarshal.Cast<ArrayRange, byte>(de.AsSpan(0, nent));

            dest.CopyTo(dest[(4 + sizeof(int))..][..(db.Length * Marshal.SizeOf<ArrayRange>())]);
            dest[(4 + sizeof(int) + (db.Length * Marshal.SizeOf<ArrayRange>()))..][..Padding].Clear();
            VersionInfo.CopyTo(dest[(4 + sizeof(int) + db.Length * Marshal.SizeOf<ArrayRange>() + Padding)..][..VersionInfo.Length]);
        }

        public bool Verify(int size)
        {
            foreach (var i in Direntries)
            {
                if (i.Value.Offset < 0 || i.Value.End() > size) return false;
                foreach (var j in Direntries)
                {
                    if (i.Key == j.Key) continue;
                    if (i.Value.Offset <= j.Value.Offset)
                    {
                        if (i.Value.End() > j.Value.Offset) return false;
                    }
                    else if (j.Value.End() > i.Value.Offset) return false;
                }
            }
            return true;
        }
    }
    private Dictionary<LumpType, ILump> Lumps;
    public EntityLump Entities = new();
    public StructLump<Texture> Textures = new();
    public StructLump<Plane> Planes = new();
    public StructLump<Node> Nodes = new();
    public StructLump<Leaf> Leafs = new();
    public ReferenceLump<Face> LeafFaces;
    public ReferenceLump<Brush> LeafBrushes;
    public StructLump<Model> Models = new();
    public StructLump<Brush> Brushes = new();
    public StructLump<BrushSide> BrushSides = new();
    public StructLump<Vertex> Vertices = new();
    public ReferenceLump<Vertex> MeshVertices;
    public StructLump<Effect> Effects = new();
    public StructLump<Face> Faces = new();
    public StructLump<Lightmap> Lightmaps = new();
    public StructLump<LightVolume> Lightvols = new();
    public VisLump Visdata = new();
    public StructLump<Advertisement> Advertisements = new();

    public File()
    {
        LeafFaces = new(Faces);
        LeafBrushes = new(Brushes);
        MeshVertices = new(Vertices);

        Lumps = new()
        {
            [LumpType.Entities] = Entities,
            [LumpType.Textures] = Textures,
            [LumpType.Planes] = Planes,
            [LumpType.Nodes] = Nodes,
            [LumpType.Leafs] = Leafs,
            [LumpType.LeafFaces] = LeafFaces,
            [LumpType.LeafBrushes] = LeafBrushes,
            [LumpType.Models] = Models,
            [LumpType.Brushes] = Brushes,
            [LumpType.BrushSides] = BrushSides,
            [LumpType.Vertices] = Vertices,
            [LumpType.MeshVertices] = MeshVertices,
            [LumpType.Effects] = Effects,
            [LumpType.Faces] = Faces,
            [LumpType.Lightmaps] = Lightmaps,
            [LumpType.Lightvols] = Lightvols,
            [LumpType.Visdata] = Visdata,
            [LumpType.Advertisements] = Advertisements,
        };
    }

    public int Read(ReadOnlySpan<byte> data)
    {
        var h = new Header();
        var sz = h.ReadInfo(data);
        if (sz < 0) return -1;
        var res = sz;
        sz = h.ReadDirentries(data);
        if (sz < 0) return -1;
        res += sz;
        if (!h.Verify(data.Length)) return -1;

        foreach (var i in h.LumpTypes)
        {
            res += Lumps[i].Read(data[h.Direntries[i].Offset..h.Direntries[i].End()]);
        }
        return res;
    }

    public int CalcSize(int version, int alignment = 1)
    {
        var s = Header.CalcSize(version);
        foreach (var i in Header.Lumps(version))
        {
            s = s / alignment * alignment;
            s += Lumps[i].CalcSize();
        }
        return s;
    }
    public void Write(int version, Span<byte> dest, int alignment = 1)
    {
        var h = new Header
        {
            Version = version
        };
        var pos = h.Size;
        foreach (var i in h.LumpTypes)
        {
            pos = pos / alignment * alignment;
            var sz = Lumps[i].CalcSize();
            Lumps[i].Write(dest[pos..(pos + sz)]);
            h.Direntries[i] = new ArrayRange()
            {
                Offset = pos,
                Length = sz
            };
            pos += sz;
        }
        h.Write(dest[0..h.Size]);
    }
}