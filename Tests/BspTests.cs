using NUnit.Framework;
using Bsp.Common.Geometry;
using Bsp.Common.Tree;
using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;

namespace Bsp.Tests;

class CsgContentOperation : IContentOperation<long>
{
    private readonly CsgOperation _operationType;
    public CsgContentOperation(CsgOperation operationType) => _operationType = operationType;
    public long Apply(long lhs, long rhs) => _operationType.Apply(lhs, rhs);

    public long Invert(long content)
    {
        return ~content;
    }
}
static class CsgOperationExtension
{
    private static readonly Dictionary<CsgOperation, ISpaceContentOperation<long>> _ops = new();
    public static ISpaceContentOperation<long> Use(this CsgOperation op)
    {
        if (_ops.TryGetValue(op, out var cop)) return cop;
        return _ops[op] = ContentOperations.SpaceOperation<long>.Create(
            new CsgContentOperation(op),
            new CsgContentOperation(op));
    }
}
public class BspTests
{
    class Content1D : IContentProvider<long>
    {
        public long Content { get; }
        public long DefaultContent { get; } = 0;
        public Content1D(long content)
        {
            Content = content;
        }
        public IContentProvider<long> BoundaryProvider => throw new NullReferenceException();
    }

    class Content2D : IContentProvider<long>
    {
        public long Content { get; }
        public long DefaultContent { get; }
        public Content2D(long content)
        {
            Content = content;
        }
        public IContentProvider<long> BoundaryProvider { get; } = new Content1D(1);
    }
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
        var segm1 = p.Coplanar(-1.0f, 1.0f).BuildTree(new Content1D(2));
        var segm2 = p.Coplanar(-2.0f, 0.0f).BuildTree(new Content1D(4));
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
        var segm1 = p.Coplanar(0.0f, 4.0f).BuildTree(new Content1D(1));
        var segm2 = p.Coplanar(6.0f, 9.0f).BuildTree(new Content1D(2));
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
        IBspTree<Hull1D, long> res = space.BuildTree(new Content1D(0));
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
            var tree = space.Coplanar(bounds[Math.Min(t0, t1)], bounds[Math.Max(t0, t1)]).BuildTree(new Content1D(f));
            res = res.Csg(tree, op);
            // File.WriteAllText($"tree_{i}.log", res.Print() + $"\n{op}, ({t0}, {t1})");
            // File.WriteAllText($"tree_{i}_mrg.log", res.Print() + $"\n{op}, ({t0}, {t1})");
        }

        // TestContext.WriteLine(String.Join(" ", flags));
        // File.WriteAllText($"tree.log", res.Print());

        Assert.AreEqual(0, res.Search(bounds.First() - 1.0f).flags);
        Assert.AreEqual(0, res.Search(bounds.Last() + 1.0f).flags);
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

        var t0 = h0.BuildTree(new Content2D(3));
        var t1 = h1.BuildTree(new Content2D(5));

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
        var sdiff = t0.Csg(t1, ContentOperations.Space2DDifference);
        Assert.AreEqual(2, sdiff.Search(p0.XY()).flags);
        Assert.AreEqual(0, sdiff.Search(p1.XY()).flags);
        Assert.AreEqual(3, sdiff.Search(p2.XY()).flags);
    }
    class VisPassBuilder : IAreaBuilder<VisibilityContent>
    {
        public VisibilityContent OuterContent { get; }
        public VisibilityContent Aggregate(VisibilityContent portalContent, VisibilityContent portalLeafContent)
        {
            return new(portalContent.Flags | portalLeafContent.Flags, portalContent.Visibility | portalLeafContent.Visibility);
            // var lf = portalLeafContent.Visibility & VisibilityFlags.Blocked;
            // var pc = portalContent.Visibility & VisibilityFlags.Blocked;
            // if ((portalContent.Visibility ^ portalLeafContent.Visibility).HasFlag(VisibilityFlags.Flipped))
            // {
            //     var tr = (ulong)lf;
            //     lf = VisibilityFlags.Blocked & (VisibilityFlags)((tr << 1) | (tr >> 1));
            // }
            // return new(
            //     portalContent.Flags | portalLeafContent.Flags,
            //     (portalContent.Visibility & VisibilityFlags.Flipped) | pc | lf);
        }
        public bool PassCondition(VisibilityContent portalContent, Side side)
        {
            var v = (int)portalContent.Visibility;
            return (((((int)side & 0x2) >> 1) + 1) & v) == 0;
        }
    }
    [Test]
    public void TestVisPass()
    {
        var vp = new VisPassBuilder();
        Assert.True(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.Open), Side.Front));
        Assert.True(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.Open), Side.Back));
        Assert.True(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.FrontToBack), Side.Front));
        Assert.True(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.BackToFront), Side.Back));
        Assert.False(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.BackToFront), Side.Front));
        Assert.False(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.FrontToBack), Side.Back));
        Assert.False(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.Blocked), Side.Front));
        Assert.False(vp.PassCondition(new VisibilityContent(0, VisibilityFlags.Blocked), Side.Back));
    }
    [Test]
    public void TestTree1DVis()
    {
        var h0 = new Hull1D(Vector2.UnitY.Plane(0.0f), -3, 1);
        var h1 = new Hull1D(-Vector2.UnitY.Plane(0.0f), -3, 1);
        Console.WriteLine(h0);

        var t0 = h0.BuildTree(new SolidContent(new VisibilityContent(0, VisibilityFlags.FrontToBack)));
        var t1 = h1.BuildTree(new SolidContent(new VisibilityContent(0, VisibilityFlags.FrontToBack)));

        // t0.Hull.AddBound(new Vector2(-1, -1));

        t0.Csg(t1, ContentOperations.Space2DVisIntersect.EdgeOperation.Reverse, true);
        var p0 = Vector2.Zero;
        var p1 = new Vector2(-2, 0);
        var p2 = new Vector2(2, 0);

        Assert.IsTrue(t0.Search(0.0f).flags.Visibility.HasFlag(VisibilityFlags.Blocked), $"{t0.Search(0.0f).flags.Visibility}");
        Assert.IsTrue(t0.Search(-2.0f).flags.Visibility.HasFlag(VisibilityFlags.FrontToBack), $"{t0.Search(-2.0f).flags.Visibility}");
        Assert.IsTrue(t0.Search(2.0f).flags.Visibility.HasFlag(VisibilityFlags.BackToFront), $"{t0.Search(2.0f).flags.Visibility}");
        Assert.AreEqual(VisibilityFlags.Open, t0.Search(-3.0f).flags.Visibility);
        Assert.AreEqual(VisibilityFlags.Open, t0.Search(3.0f).flags.Visibility);
    }
    public void TestTree1DCopy()
    {
    }

    [Test]
    public void TestTree2DVis()
    {
        var r0 = Matrix4x4.CreateTranslation(new Vector3(-1.0f, -1.0f, 0.0f)).Rect(2.0f, 2.0f);
        var r1 = Matrix4x4.CreateTranslation(new Vector3(1.0f, 1.0f, 0.0f)).Rect(2.0f, 2.0f);

        var n = Linealg.Plane(r0[0], r0[1], r0[2]);
        var h0 = n.CreateHull(r0);
        var h1 = n.CreateHull(r1);

        var edgeContent = new SolidContent(new VisibilityContent(0, VisibilityFlags.BackToFront));

        var t0 = h0.BuildTree(new SolidContent(new VisibilityContent(3, VisibilityFlags.BackToFront), edgeContent));
        var t1 = h1.BuildTree(new SolidContent(new VisibilityContent(5, VisibilityFlags.FrontToBack), edgeContent));

        t0.Numerate();
        t1.Numerate();

        var p0 = t0.Hull.Local.Transform(new Vector3(0.0f, 0.0f, 0.0f));
        var p1 = t0.Hull.Local.Transform(new Vector3(2.0f, 2.0f, 0.0f));
        var p2 = t0.Hull.Local.Transform(new Vector3(-2.0f, -2.0f, 0.0f));

        var inter = (BspTree<Hull2D, Hull1D, VisibilityContent>)t0.Csg(t1, ContentOperations.Space2DVisIntersect);
        Assert.AreEqual(VisibilityFlags.BackToFront | VisibilityFlags.FrontToBack, inter.Search(p0.XY()).flags.Visibility);
        Assert.AreEqual(VisibilityFlags.FrontToBack, inter.Search(p1.XY()).flags.Visibility);
        Assert.AreEqual(VisibilityFlags.BackToFront, inter.Search(p2.XY()).flags.Visibility);

        var passBuilder = new VisPassBuilder();
        var graph = PortalGraph<Hull2D, Hull1D, VisibilityContent>.Build(inter, passBuilder);
        Assert.IsNotEmpty(graph.Portals);
        var lst = new List<List<int>>() {
            new () { 2,3,6 },
            new () { 3, 4, 5 },
            new () { 0, 6, 9 ,7 },
            new () { 0, 1, 4, 6 },
            new () { 1,3,5,8,10 },
            new () { 1, 4, 11 },
            new () { 0, 2, 3, 8, 9 },
            new () { 2, 9, 10, 11 },
            new () { 4, 6, 9, 10 },
            new () { 2, 6, 7, 8 },
            new () { 4, 7, 8, 11 },
            new () { 5, 7, 10 },
        };
        var lspass = lst.Select(x => x.ToDictionary(y => y, y => true)).ToList();
        lspass[0][6] = false;
        lspass[1][4] = false;
        lspass[2][6] = false;
        lspass[2][9] = false;
        lspass[3][4] = false;
        lspass[3][6] = false;
        lspass[4][8] = false;
        lspass[5][4] = false;
        lspass[6][8] = false;
        lspass[7][9] = false;
        lspass[7][10] = false;
        lspass[9][8] = false;
        lspass[10][8] = false;
        lspass[11][10] = false;
        Assert.AreEqual(lst.Count, graph.Portals.Count);
        for (int i = 0; i < lspass.Count; ++i)
        {
            foreach (var j in lspass[i])
            {
                Assert.True(graph.Portals[i].ContainsKey(j.Key));
                Assert.AreEqual(j.Value, graph.Portals[i][j.Key].Pass, $"Pass [{i}->{j}] {graph.Portals[i][j.Key].Pass} is not {j.Value}");
            }
            foreach (var j in graph.Portals[i])
            {
                Assert.True(lspass[i].ContainsKey(j.Key));
            }
        }
        Assert.AreEqual(new VisibilityContent(), graph.Portals[5].TryGetValue(11, out var v0) ? v0.Flags : null);
        Assert.AreEqual(new VisibilityContent(), graph.Portals[0].TryGetValue(2, out var v1) ? v1.Flags : null);
        Assert.AreEqual(new VisibilityContent(), graph.Portals[2].TryGetValue(0, out var v2) ? v2.Flags : null);
    }

    [Test]
    public void TestTree2DVis2()
    {
        var r0 = Matrix4x4.CreateTranslation(new Vector3(-2.0f, -1.0f, 0.0f)).Rect(2.0f, 2.0f);
        var r1 = Matrix4x4.CreateTranslation(new Vector3(2.0f, 1.0f, 0.0f)).Rect(2.0f, 2.0f);

        var n = Linealg.Plane(r0[0], r0[1], r0[2]);
        var h0 = n.CreateHull(r0);
        var h1 = n.CreateHull(r1);

        var edgeContent = new SolidContent(new VisibilityContent(0, VisibilityFlags.FrontToBack));

        var t0 = h0.BuildTree(new SolidContent(new VisibilityContent(3, VisibilityFlags.BackToFront), edgeContent));
        var t1 = h1.BuildTree(new SolidContent(new VisibilityContent(5, VisibilityFlags.FrontToBack), edgeContent));

        t0.Numerate();
        t1.Numerate();

        var p1 = t0.Hull.Local.Transform(new Vector3(2.0f, 2.0f, 0.0f));
        var p2 = t0.Hull.Local.Transform(new Vector3(-2.0f, -2.0f, 0.0f));

        var inter = (BspTree<Hull2D, Hull1D, VisibilityContent>)t0.Csg(t1, ContentOperations.Space2DVisUnion);
        Assert.AreEqual(VisibilityFlags.FrontToBack, inter.Search(p1.XY()).flags.Visibility);
        Assert.AreEqual(VisibilityFlags.BackToFront, inter.Search(p2.XY()).flags.Visibility);

        var passBuilder = new VisPassBuilder();
        var graph = PortalGraph<Hull2D, Hull1D, VisibilityContent>.Build(inter, passBuilder);
        Assert.IsNotEmpty(graph.Portals);
        var lst = new List<List<int>>() {
            new () { 2, 3, 4 },
            new () { 3, 5, 6 },
            new () { 0, 4, 7 },
            new () { 0, 1, 4, 5 },
            new () { 0, 2, 3, 7, 8 },
            new () { 1, 3, 6, 8 },
            new () { 1, 5, 9 },
            new () { 2, 4, 8, 9 },
            new () { 4, 5, 7, 9 },
            new () { 6, 7, 8 },
        };
        var lspass = lst.Select(x => x.ToDictionary(y => y, y => true)).ToList();
        lspass[4][0] = false;
        lspass[5][1] = false;
        lspass[4][2] = false;
        lspass[4][3] = false;
        lspass[5][3] = false;
        lspass[8][4] = false;
        lspass[5][6] = false;
        lspass[4][7] = false;
        lspass[8][7] = false;
        lspass[4][8] = false;
        lspass[8][9] = false;
        Assert.AreEqual(lst.Count, graph.Portals.Count);
        for (int i = 0; i < lspass.Count; ++i)
        {
            foreach (var j in lspass[i])
            {
                Assert.True(graph.Portals[i].ContainsKey(j.Key));
                Assert.AreEqual(j.Value, graph.Portals[i][j.Key].Pass, $"Pass [{i}->{j}] {graph.Portals[i][j.Key].Pass} is not {j.Value}");
            }
            foreach (var j in graph.Portals[i])
            {
                Assert.True(lspass[i].ContainsKey(j.Key));
            }
        }
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[5].TryGetValue(11, out var v0) ? v0.Flags : null);
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[0].TryGetValue(2, out var v1) ? v1.Flags : null);
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[2].TryGetValue(0, out var v2) ? v2.Flags : null);
    }

    [Test]
    public void TestTree2DVis3()
    {
        var r0 = Matrix4x4.CreateTranslation(new Vector3(-2.0f, 0.0f, 0.0f)).Rect(2.0f, 2.0f);
        var r1 = Matrix4x4.CreateTranslation(new Vector3(2.0f, 0.0f, 0.0f)).Rect(2.0f, 2.0f);

        var n = Linealg.Plane(r0[0], r0[1], r0[2]);
        var h0 = n.CreateHull(r0);
        var h1 = n.CreateHull(r1);

        var edgeContent = new SolidContent(new VisibilityContent(0, VisibilityFlags.FrontToBack));

        var t0 = h0.BuildTree(new SolidContent(new VisibilityContent(3, VisibilityFlags.BackToFront), edgeContent));
        var t1 = h1.BuildTree(new SolidContent(new VisibilityContent(5, VisibilityFlags.FrontToBack), edgeContent));

        t0.Numerate();
        t1.Numerate();

        var p1 = t0.Hull.Local.Transform(new Vector3(2.0f, 0.0f, 0.0f));
        var p2 = t0.Hull.Local.Transform(new Vector3(-2.0f, 0.0f, 0.0f));

        var inter = (BspTree<Hull2D, Hull1D, VisibilityContent>)t0.Csg(t1, ContentOperations.Space2DVisUnion);
        Assert.AreEqual(VisibilityFlags.FrontToBack, inter.Search(p1.XY()).flags.Visibility);
        Assert.AreEqual(VisibilityFlags.BackToFront, inter.Search(p2.XY()).flags.Visibility);

        var passBuilder = new VisPassBuilder();
        var graph = PortalGraph<Hull2D, Hull1D, VisibilityContent>.Build(inter, passBuilder);
        Assert.IsNotEmpty(graph.Portals);
        var lst = new List<List<int>>() {
            new () { 1, 3, 4, 5 },
            new () { 0, 2, 3 },
            new () { 1, 3, 4, 5 },
            new () { 0, 1, 2, 4 },
            new () { 0, 2, 3, 5 },
            new () { 0, 2, 4 }
        };
        var lspass = lst.Select(x => x.ToDictionary(y => y, y => true)).ToList();
        lspass[3][0] = false;
        lspass[3][1] = false;
        lspass[3][2] = false;
        lspass[3][4] = false;
        lspass[4][0] = false;
        lspass[4][2] = false;
        lspass[4][3] = false;
        lspass[4][5] = false;
        Assert.AreEqual(lst.Count, graph.Portals.Count);
        for (int i = 0; i < lspass.Count; ++i)
        {
            foreach (var j in lspass[i])
            {
                Assert.True(graph.Portals[i].ContainsKey(j.Key));
                Assert.AreEqual(j.Value, graph.Portals[i][j.Key].Pass, $"Pass [{i}->{j}] {graph.Portals[i][j.Key].Pass} is not {j.Value}");
            }
            foreach (var j in graph.Portals[i])
            {
                Assert.True(lspass[i].ContainsKey(j.Key));
            }
        }
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[5].TryGetValue(11, out var v0) ? v0.Flags : null);
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[0].TryGetValue(2, out var v1) ? v1.Flags : null);
        // Assert.AreEqual(new VisibilityContent(), graph.Portals[2].TryGetValue(0, out var v2) ? v2.Flags : null);
    }
}