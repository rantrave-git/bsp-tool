using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using Bsp.BspFormat;

// Console.WriteLine(Marshal.SizeOf<Bsp.BspFormat.ColorRGBA>());


// var s = new StructLump<Texture>(new[] {
//     new Texture("test0", 0, 0),
//     new Texture("test1", 0, 0),
//     new Texture("test2", 0, 0),
// });

// var sp = new byte[s.CalcSize()];
// // Console.WriteLine(s.TryWrite(sp));

// // Console.WriteLine(string.Join(' ', sp.Select(x => $"{x:x2}")));

// Span<int> ss = stackalloc int[10];
// ss.Clear();
// for (int i = 0; i < 6; ++i)
// {
//     ss[i] = i + 1;
// }

// ss[3..6].CopyTo(ss[4..7]);

// Console.WriteLine(string.Join(", ", ss.ToImmutableArray().Select(x => $"{x}")));

var pp = Polytope.FromBrush(new (Vector4, long)[] {
    (new (-1.0f,  0.0f,  0.0f, 12.345678f), 1),
    (new ( 0.0f, -1.0f,  0.0f, 12.345678f), 2),
    (new ( 0.0f,  1.0f,  0.0f, 12.345678f), 4),
    (new ( 0.0f,  0.0f, -1.0f, 12.345678f), 8),
    (new ( 0.0f,  0.0f,  1.0f, 12.345678f), 16),
    // (new ( 1.0f,  0.0f,  0.0f, 12.345678f), 2),
    (new Vector4(1.0f,  1.0f,  1.0f, 12.345678f).ToPlane(), 32),
}, 1);

Console.WriteLine(pp.ToString());

var b = pp.ToBrush();
foreach (var i in b)
{
    Console.WriteLine($"{i.Plane}  {i.Flags}");
}
var mc = pp.ToContext();

// GeometryDegugger.SendContext(mc);

var tree = BspNode3D.Build(mc);
Console.WriteLine("DONE");