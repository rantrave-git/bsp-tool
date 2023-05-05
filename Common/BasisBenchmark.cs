using System.Numerics;
using BenchmarkDotNet.Attributes;
using Bsp.Common.Geometry;

namespace Bsp.Common;

[MemoryDiagnoser]
public class BasisBenchmark
{
    int NumberOfItems = 5000000;
    // [Benchmark]
    // public Vector3 TransformUsingBasis()
    // {
    //     var sb = Basis.TangentSpace(new Vector4(0.1234511f, 0.64323f, 0.75632f, 0.23345f)); // pretty random choice
    //     var res = Vector3.Zero;
    //     var rand = new Random(123);
    //     for (int i = 0; i < NumberOfItems; i++)
    //     {
    //         var v = Random.Shared.NextVector3();
    //         res += sb.Transform(v);
    //     }
    //     return res;
    // }
    // [Benchmark]
    // public Vector3 TransformUsingMatrix()
    // {
    //     var sb = (new Vector4(0.1234511f, 0.64323f, 0.75632f, 0.23345f)).TangentSpace(); // pretty random choice
    //     var res = Vector3.Zero;
    //     var rand = new Random(123);
    //     for (int i = 0; i < NumberOfItems; i++)
    //     {
    //         var v = Random.Shared.NextVector3();
    //         res += Vector3.Transform(v, sb);
    //     }
    //     return res;
    // }
    // [Benchmark]
    // public float BasisGetPos()
    // {
    //     var rand = new Random(123);
    //     var sb = Basis.TangentSpace(rand.NextVector4()); // pretty random choice
    //     var res = 0.0f;
    //     var plane = rand.NextVector3();
    //     var point = rand.NextVector3();
    //     for (int i = 0; i < NumberOfItems; i++)
    //     {
    //         res += sb.GetPos(plane, point);
    //     }
    //     return res;
    // }
    // [Benchmark]
    // public float BasisGetPosOpt()
    // {
    //     var rand = new Random(123);
    //     var sb = Basis.TangentSpace(rand.NextVector4()); // pretty random choice
    //     var res = 0.0f;
    //     var plane = rand.NextVector3();
    //     var point = rand.NextVector3();
    //     for (int i = 0; i < NumberOfItems; i++)
    //     {
    //         res += sb.GetPosOpt(plane, point);
    //     }
    //     return res;
    // }
    [Benchmark]
    public Vector3 BasisProject()
    {
        var rand = new Random(123);
        var sb = Basis3D.TangentSpace(rand.NextVector4()); // pretty random choice
        var res = Vector3.Zero;
        var planes = Enumerable.Range(0, 200).Select(x => rand.NextVector4()).ToArray();
        for (int i = 0; i < NumberOfItems; i++)
        {
            res += sb.Project(planes[i % planes.Length]);
        }
        return res;
    }
}