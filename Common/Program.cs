// See https://aka.ms/new-console-template for more information
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Running;
using Bsp.Common;
using Bsp.Common.Geometry;

// var summary = BenchmarkRunner.Run<BasisBenchmark>();

var p = new Vector4(0.0f, 0.0001f, -1.0f, 1.0f).AsVector128().AsVector<float>();
var eps = -(Vector4.One * 0.01f).AsVector128().AsVector<float>();
Console.WriteLine(Vector.LessThanAny(p, eps));

// var p = new Vector4(1.0f, 1.0f, 1.0f, -3.0f);
// var v0 = new Vector3(3.0f, 0.0f, 0.0f);
// var v1 = new Vector3(0.0f, 3.0f, 0.0f);
// var v2 = new Vector3(0.0f, 0.0f, 3.0f);

// var basis = p.TangentSpace();
// Console.WriteLine(basis);


// var p0 = Vector3.Transform(v0, basis);
// var p1 = Vector3.Transform(v1, basis);
// var p2 = Vector3.Transform(v2, basis);
// Console.WriteLine($"{p0} {p1} {p2}");
// var n0 = Vector3.Normalize(Vector3.Cross(p1 - p0, Vector3.UnitZ));
// var n1 = Vector3.Normalize(Vector3.Cross(p2 - p1, Vector3.UnitZ));
// var n2 = Vector3.Normalize(Vector3.Cross(p0 - p2, Vector3.UnitZ));
// Console.WriteLine($"{n0} {n1} {n2}");
// var pp0 = n0 + Vector3.UnitZ * Vector3.Dot(n0, p0);
// var pp1 = n1 + Vector3.UnitZ * Vector3.Dot(n1, p1);
// var pp2 = n2 + Vector3.UnitZ * Vector3.Dot(n2, p2);
// Console.WriteLine($"{pp0} {pp1} {pp2}");

// Console.WriteLine($"{Vector3.Distance(v0, v1)} == {Vector3.Distance(p0, p1)}");
// Console.WriteLine($"{Vector3.Distance(v1, v2)} == {Vector3.Distance(p1, p2)}");
// Console.WriteLine($"{Vector3.Distance(v0, v2)} == {Vector3.Distance(p0, p2)}");

// var glon = Vector3.Normalize(p.AsVector128().AsVector3());
// Console.WriteLine($"{n0} == {Vector3.TransformNormal(Vector3.Cross(Vector3.Normalize(v1 - v0), glon), basis)}");
// Console.WriteLine($"{n1} == {Vector3.TransformNormal(Vector3.Cross(Vector3.Normalize(v2 - v1), glon), basis)}");
// Console.WriteLine($"{n2} == {Vector3.TransformNormal(Vector3.Cross(Vector3.Normalize(v0 - v2), glon), basis)}");