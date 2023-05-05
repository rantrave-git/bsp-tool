using NUnit.Framework;
using Bsp.Common.Geometry;
using System;
using System.Numerics;
using Bsp.Common.Tree;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace Bsp.Tests;

public class HullTests
{
    [SetUp]
    public void Setup()
    {
    }
    [System.Serializable]
    public class Vector3S
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public Vector3S(Vector3 src)
        {
            X = src.X;
            Y = src.Y;
            Z = src.Z;
        }
        [JsonIgnore]
        public Vector3 Vector => new Vector3(X, Y, Z);
    }
    [Test]
    public void RunFailedTest()
    {
        var path = "./failed-f7d8a9c1-264e-440d-a936-cd1446bf91fd.test";
        var def = new { num = 0, p = new Vector3S[0] };
        var test = JsonConvert.DeserializeAnonymousType(File.ReadAllText(path), def);
        var num = test.num;
        var p = test.p.Select(x => x.Vector).ToArray();
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[1]);
        var h = new Hull2D(n.Plane(p[0]));
        for (int i = 0; i < p.Length; ++i)
        {
            var plane = Vector3.Cross(p[(i + 1) % p.Length] - p[i], n).Plane(p[i]);
            h = h!.Split(new Hull2D(plane)).Back;
            Assert.IsNotNull(h, $"Plane {i}: {plane}");
            // TestContext.WriteLine($"{h.empty} {String.Join(",", h.bounds.Select(x => $"({x.min} {x.max})"))}");
            Assert.IsFalse(h!.empty, "Splitter is empty");
        }
        // TestContext.WriteLine("--------------------");
        for (int i = 0; i < p.Length; ++i)
        {
            var b = h!.bounds[p.Length - i - 1];
            var ppp = h.Pos(b.Pos(0.0f));
            Assert.LessOrEqual(Vector3.Distance(p[i], ppp), Linealg.Eps);
            ppp = h.Pos(b.Pos(1.0f));
            Assert.LessOrEqual(Vector3.Distance(p[(i + 1) % p.Length], ppp), Linealg.Eps);
        }
    }
    [Test]
    [Repeat(10000)]
    public void TestHull()
    {
        var c = TestContext.CurrentContext.Random.NextVector3() * 100.0f;
        // var c = Vector3.Zero;
        var num = TestContext.CurrentContext.Random.Next(3, 30);
        var angles = Enumerable.Range(0, num + 1).Select(x => TestContext.CurrentContext.Random.NextSingle() + x * 1.05f).OrderBy(x => x).ToArray();
        var s = angles.Last();
        // angles = angles.Select(x => x / s).ToArray();
        var randrot = Matrix4x4.CreateFromAxisAngle(TestContext.CurrentContext.Random.NextVector3Direction(), TestContext.CurrentContext.Random.NextSingle() * 2.0f * MathF.PI);
        // var randrot = Matrix4x4.Identity;
        // Matrix4x4.CreateRotationY(ay) * Matrix4x4.CreateRotationZ(az)
        var p = angles.Select(x => Vector3.Transform(Vector3.UnitX, Matrix4x4.CreateRotationZ(x * 2.0f * MathF.PI / s))).Select(x => Vector3.Transform(x, randrot) + c).SkipLast(1).ToArray();
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[1]);
        var h = new Hull2D(n.Plane(p[0]));
        try
        {
            for (int i = 0; i < p.Length; ++i)
            {
                var plane = Vector3.Cross(p[(i + 1) % p.Length] - p[i], n).Plane(p[i]);
                h = h!.Split(new Hull2D(plane)).Back;
                Assert.IsNotNull(h, $"Plane {i}: {plane}");
                Assert.IsFalse(h!.empty, "Splitter is empty");
            }
            // TestContext.WriteLine("--------------------");
            for (int i = 0; i < p.Length; ++i)
            {
                var b = h!.bounds[p.Length - i - 1];
                var ppp = h.Pos(b.Pos(0.0f));
                Assert.LessOrEqual(Vector3.Distance(p[i], ppp), Linealg.Eps);
                ppp = h.Pos(b.Pos(1.0f));
                Assert.LessOrEqual(Vector3.Distance(p[(i + 1) % p.Length], ppp), Linealg.Eps);
            }
        }
        catch
        {
            var sss = new
            {
                num = num,
                p = p.Select(x => new Vector3S(x))
            };
            var path = Path.GetFullPath($"./failed-{Guid.NewGuid()}.test");
            File.WriteAllText(path, JsonConvert.SerializeObject(sss));
            TestContext.WriteLine($"SOME TESTS FAILED: {path}");
            // TestContext.WriteLine($"{h.empty} {String.Join(",", h.bounds.Select(x => $"({x.min} {x.max})"))}");
            throw;
        }
    }
    [Test]
    public void TestCrossHull()
    {
        var p = new[] {
            new Vector3(1.0f, 1.0f, 0.0f),
            new Vector3(-1.0f, 1.0f, 0.0f),
            new Vector3(-1.0f, -1.0f, 0.0f),
            new Vector3(1.0f, -1.0f, 0.0f)
        };
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[1]);
        var h = new Hull2D(n.Plane(p[0]));
        for (int i = 0; i < p.Length; ++i)
        {
            var plane = Vector3.Cross(p[(i + 1) % p.Length] - p[i], n).Plane(p[i]);
            h = h!.Split(new Hull2D(plane)).Back;
            Assert.IsNotNull(h, $"Plane {i}: {plane}");
            Assert.False(h!.empty, $"Plane {i}: {plane}");
        }
        var overSplitters = new[] {
            (new Vector3(1.0f, 0.0f, 0.0f)).Plane(1.0f),
            (new Vector3(1.0f, 1.0f, 0.0f)).Plane(2.0f),
            (new Vector3(-2.0f, 1.0f, 0.0f)).Plane(3.0f),
            (new Vector3(1.0f, -1.0f, 0.0f)).Plane(2.0f),
        };
        for (int i = 0; i < overSplitters.Length; ++i)
        {
            var plane = overSplitters[i];
            var (b, f) = h!.Split(new Hull2D(plane));
            Assert.IsNotNull(b, $"Plane {i}: {-plane}");
            Assert.IsNotNull(f, $"Plane {i}: {plane}");
            Assert.False(b!.empty, $"Plane {i}: {-plane}");
            Assert.True(f!.empty, $"Plane {i}: {plane}");
            // Assert.IsNull(f, $"Plane {i}: {plane}");
            (f, b) = h.Split(new Hull2D(-plane));
            Assert.IsNotNull(b, $"Plane {i}: {-plane}");
            Assert.IsNotNull(f, $"Plane {i}: {plane}");
            Assert.False(b!.empty, $"Plane {i}: {-plane}");
            Assert.True(f!.empty, $"Plane {i}: {plane}");
            // Assert.IsNull(f, $"Plane {i}: {-plane}");
        }

        var (back, front) = h!.Split(new Hull2D(Vector4.UnitX));
        Assert.IsNotNull(back, $"Cross split");
        Assert.IsNotNull(front, $"Cross split");
        Assert.False(back!.empty, $"Cross split");
        Assert.False(front!.empty, $"Cross split");
        var backs = new[] {
            new Vector2(-1, 0),
            new Vector2(-1, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
        };
        for (var i = 0; i < back!.bounds.Count; ++i)
        {
            var b = back.bounds[i];
            Assert.LessOrEqual(Vector2.Distance(b.Pos(0.0f), backs[i]), Linealg.Eps);
            Assert.LessOrEqual(Vector2.Distance(b.Pos(1.0f), backs[(i + backs.Length - 1) % backs.Length]), Linealg.Eps);
        }
        var fronts = new[] {
            (new Vector2(1, 0), new Vector2(-1, 0)),
            (new Vector2(-1, -1), new Vector2(1, -1)),
            (new Vector2(-1, 0), new Vector2(-1, -1)),
            (new Vector2(1, -1), new Vector2(1, 0)),
        };
        for (var i = 0; i < front!.bounds.Count; ++i)
        {
            var b = front.bounds[i];
            TestContext.WriteLine($"{b.Pos(0.0f)} {b.Pos(1.0f)}");
            Assert.LessOrEqual(Vector2.Distance(b.Pos(0.0f), fronts[i].Item1), Linealg.Eps);
            Assert.LessOrEqual(Vector2.Distance(b.Pos(1.0f), fronts[i].Item2), Linealg.Eps);
        }
    }
    [Test]
    public void TestNarrowHull()
    {
        var p = new[] {
            new Vector3(1e-1f, 0.0f, 0.0f),
            new Vector3(0, 1e-3f, 0.0f),
            new Vector3(-1e-1f, 0.0f, 0.0f),
        };
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[1]);
        var h = new Hull2D(n.Plane(p[0]));
        for (int i = 0; i < p.Length; ++i)
        {
            var plane = Vector3.Cross(p[(i + 1) % p.Length] - p[i], n).Plane(p[i]);
            h = h!.Split(new Hull2D(plane)).Back;
        }
        Assert.IsNotNull(h);
        Assert.True(h!.empty);
    }
}