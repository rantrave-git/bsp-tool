using NUnit.Framework;
using Bsp.Common.Geometry;
using Bsp.Common.Tree;
using System;
using System.Numerics;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace Bsp.Tests;

class CsgContentOperation : IContentOperation<long>
{
    private CsgOperation _operationType;
    public CsgContentOperation(CsgOperation operationType) => _operationType = operationType;
    public long Apply(long lhs, long rhs) => _operationType.Apply(lhs, rhs);
}
static class CsgOperationExtension
{
    private static Dictionary<CsgOperation, ISpaceContentOperation<long>> _ops = new Dictionary<CsgOperation, ISpaceContentOperation<long>>();
    public static ISpaceContentOperation<long> Use(this CsgOperation op)
    {
        if (_ops.TryGetValue(op, out var cop)) return cop;
        return _ops[op] = ContentOperations.SpaceOperation<long>.Create(new CsgContentOperation(op));
    }
}
public class BspTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestTree1D()
    {
        // 000000000<-1>2222222<1>0000000
        // 00<-2>444444444<0>000000000000
        // ->
        // 00<-2>444<-1>66<0>22<1>0000000
        var p = new Hull1D(new Vector3(1.0f, 1.0f, 0.0f));
        var segm1 = p.Coplanar(-1.0f, 1.0f).CreateBspTree(2);
        var segm2 = p.Coplanar(-2.0f, 0.0f).CreateBspTree(4);
        var c = segm1.Csg(segm2, CsgOperation.Union.Use());
        Assert.AreEqual(c.Search(-3.0f).flags, 0);
        Assert.AreEqual(c.Search(-1.5f).flags, 4);
        Assert.AreEqual(c.Search(-0.5f).flags, 6);
        Assert.AreEqual(c.Search(0.5f).flags, 2);
        Assert.AreEqual(c.Search(1.5f).flags, 0);
    }
    [Test]
    public void TestNotIntersectTree1D()
    {
        var p = new Hull1D(new Vector3(4.0f, 2.0f, 0.0f));
        var segm1 = p.Coplanar(0.0f, 4.0f).CreateBspTree(1);
        var segm2 = p.Coplanar(6.0f, 9.0f).CreateBspTree(2);
        var c = segm1.Csg(segm2, CsgOperation.Union.Use());
        Assert.AreEqual(c.Search(-1).flags, 0);
        Assert.AreEqual(c.Search(2).flags, 1);
        Assert.AreEqual(c.Search(5).flags, 0);
        Assert.AreEqual(c.Search(7).flags, 2);
        Assert.AreEqual(c.Search(10).flags, 0);
    }
    [Test]
    [Repeat(1000)]
    public void TestRandomTree1D()
    {
        var N = 100;
        var bounds = Enumerable.Range(0, N).Select(x => x + 0.05f + TestContext.CurrentContext.Random.NextSingle() * 0.9f).ToArray();
        // var borders = new[] {
        //     (0, 4),
        //     (6, 9),
        //     (3, 6),
        //     (3, 6),
        //     (6, 7)
        // };
        var flags = new long[N - 1];
        var space = new Hull1D(TestContext.CurrentContext.Random.NextVector3());
        IBspTree<Hull1D, long> res = space.CreateBspTree1D(0);
        for (int i = 0; i < 200; ++i)
        {
            var f = i + 1;
            var t0 = TestContext.CurrentContext.Random.Next(bounds.Length);
            var t1 = TestContext.CurrentContext.Random.Next(bounds.Length);
            if (t1 == t0) t1 = (t1 + 1) % bounds.Length;
            // var (t0, t1) = borders[i];
            var op = ((CsgOperation)TestContext.CurrentContext.Random.Next(3)).Use();
            for (int j = 0; j < Math.Min(t0, t1); ++j)
            {
                flags[j] = op.Apply(flags[j], 0);
            }
            for (int j = Math.Min(t0, t1); j < Math.Max(t0, t1); ++j)
            {
                flags[j] = op.Apply(flags[j], f);
            }
            for (int j = Math.Max(t0, t1); j < flags.Length; ++j)
            {
                flags[j] = op.Apply(flags[j], 0);
            }
            var tree = space.Coplanar(bounds[Math.Min(t0, t1)], bounds[Math.Max(t0, t1)]).CreateBspTree(f);
            res = res.Csg(tree, op);
            // File.WriteAllText($"tree_{i}.log", res.Print() + $"\n{op}, ({t0}, {t1})");
            // File.WriteAllText($"tree_{i}_mrg.log", res.Print() + $"\n{op}, ({t0}, {t1})");
        }

        // TestContext.WriteLine(String.Join(" ", flags));
        // File.WriteAllText($"tree.log", res.Print());

        Assert.AreEqual(res.Search(bounds.First()).flags, 0);
        Assert.AreEqual(res.Search(bounds.Last()).flags, 0);
        for (int i = 0; i < N - 1; ++i)
        {
            Assert.AreEqual(flags[i], res.Search((bounds[i + 1] + bounds[i]) * 0.5f).flags);
        }
    }

    [Test]
    public void TestTree2D()
    {
        var r0 = Matrix4x4.CreateTranslation(new Vector3(-1.0f, -1.0f, 0.0f)).Rect(2.0f, 2.0f);
        var r1 = Matrix4x4.CreateTranslation(new Vector3(1.0f, 1.0f, 0.0f)).Rect(2.0f, 2.0f);

        var n = Linealg.Plane(r0[0], r0[1], r0[2]);
        var h0 = n.CreateHull(r0);
        var h1 = n.CreateHull(r1);

        var t0 = h0.CreateBspTree(3);
        var t1 = h1.CreateBspTree(5);

        t0.Numerate();
        t1.Numerate();

        var p0 = t0.Hull.Local.Transform(new Vector3(0.0f, 0.0f, 0.0f));
        var p1 = t0.Hull.Local.Transform(new Vector3(2.0f, 2.0f, 0.0f));
        var p2 = t0.Hull.Local.Transform(new Vector3(-2.0f, -2.0f, 0.0f));

        var inter = t0.Csg(t1, ContentOperations.Space2DIntersect);
        Assert.AreEqual(1, inter.Search(p0.XY()).flags);
        Assert.AreEqual(0, inter.Search(p1.XY()).flags);
        Assert.AreEqual(0, inter.Search(p2.XY()).flags);
        var union = t0.Csg(t1, ContentOperations.Space2DUnion);
        Assert.AreEqual(7, union.Search(p0.XY()).flags);
        Assert.AreEqual(5, union.Search(p1.XY()).flags);
        Assert.AreEqual(3, union.Search(p2.XY()).flags);
        var sdiff = t0.Csg(t1, ContentOperations.Space2DIntersect.Inverse);
        Assert.AreEqual(6, sdiff.Search(p0.XY()).flags);
        Assert.AreEqual(5, sdiff.Search(p1.XY()).flags);
        Assert.AreEqual(3, sdiff.Search(p2.XY()).flags);
    }
}