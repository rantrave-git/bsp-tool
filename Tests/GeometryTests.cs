using NUnit.Framework;
using Bsp.Common.Geometry;
using System;
using System.Numerics;

namespace Bsp.Tests;

public class GeometryTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestTransformInversion()
    {
        var scale = 20.0f;
        var cell = 0.01f;
        var cellsq = 2 * cell * cell;
        for (int i = 0; i < 1000000; ++i)
        {
            bool conclusive = false;
            int retries = 10;
            while (!conclusive && retries-- > 0)
            {
                var v0 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v1 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v2 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v3 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);

                var n = Vector3.Cross(v1 - v0, v2 - v0);
                if (n.LengthSquared() < 1e-6) // test define plane
                {
                    // TestContext.WriteLine("Inconclusive retry");
                    continue;
                }

                n = Vector3.Normalize(n);
                var v3d = v3 - v0;
                if (MathF.Abs(Vector3.Dot(n, v3d)) < cell)
                {
                    continue;
                }
                var nn = new Vector4(n, Vector3.Dot(n, v0));
                var ts = Basis3D.TangentSpace(nn);
                var p0 = ts.Transform(v0);
                var p1 = ts.Transform(v1);
                var p2 = ts.Transform(v2);

                var locn = Vector3.Cross(p1 - p0, p2 - p0);

                Assert.Greater(locn.Z, cell);

                var p3dloc = ts.Transform(v3) - p0;
                var p3d = ts.TransformNormal(v3d);
                Assert.AreEqual(Math.Sign(p3dloc.Z), Math.Sign(p3d.Z));
                conclusive = true;
            }
            if (!conclusive)
            {
                Assert.Fail();
            }
        }
    }
    [Test]
    public void CoincidanceTest()
    {
        var ts = Basis3D.TangentSpace(new Vector3(0.2f, 0.5f, 0.2f).Plane(123.0f));
        var plane0 = new Vector3(0.2f, 0.5f, 0.2f).Plane(125.0f);
        var plane1 = new Vector3(0.2f, 0.5f, 0.2f).Plane(122.0f);
        var plane2 = new Vector3(0.2f, 0.5f, 0.3f).Plane(122.0f);
        var plane3 = new Vector3(0.2f, 0.5f, 0.2f).Plane(123.001f);
        var plane4 = -new Vector3(0.2f, 0.5f, 0.2f).Plane(123.001f);
        var plane5 = -new Vector3(0.2f, 0.50001f, 0.20001f).Plane(123.0001f);
        var plane6 = -new Vector3(0.2f, 0.5f, 0.2f).Plane(124.0f);

        Assert.AreEqual(Classification.Back, ts.ClassifyTo(plane0));
        Assert.AreEqual(Classification.Front, ts.ClassifyTo(plane1));
        Assert.AreEqual(Classification.NotParallel, ts.ClassifyTo(plane2));
        Assert.AreEqual(Classification.Coincident, ts.ClassifyTo(plane3));
        Assert.AreEqual(Classification.Coincident, ts.ClassifyTo(plane4));
        Assert.AreEqual(Classification.Coincident, ts.ClassifyTo(plane5));
        Assert.AreEqual(Classification.Front, ts.ClassifyTo(plane6));
    }
    [Test]
    public void TestPlaneLocal()
    {
        var scale = 20.0f;
        var cell = 0.02f;
        var cellsq = 2 * cell * cell;
        for (int i = 0; i < 1000000; ++i)
        {
            bool conclusive = false;
            int retries = 10;
            while (!conclusive && retries-- > 0)
            {
                var v0 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v1 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v2 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);
                var v3 = (scale * TestContext.CurrentContext.Random.NextVector3()); //.Round(cell);

                if (Vector3.DistanceSquared(v0, v1) < cellsq ||
                    Vector3.DistanceSquared(v0, v2) < cellsq ||
                    Vector3.DistanceSquared(v0, v3) < cellsq ||
                    Vector3.DistanceSquared(v1, v2) < cellsq ||
                    Vector3.DistanceSquared(v1, v3) < cellsq ||
                    Vector3.DistanceSquared(v2, v3) < cellsq)
                {
                    continue;
                }

                var n = Vector3.Cross(v1 - v0, v2 - v0);
                if (n.LengthSquared() < 1e-6) // test define plane
                {
                    continue;
                }
                n = Vector3.Normalize(n);
                var nn = new Vector4(n, Vector3.Dot(n, v0));

                var p = (v2 - v1) * TestContext.CurrentContext.Random.NextSingle();

                var n1 = Vector3.Cross(v1 - v3, v2 - v3);
                if (n1.LengthSquared() < 1e-6) // test define plane
                {
                    continue;
                }
                n1 = Vector3.Normalize(n1);
                if (Vector3.Dot(v3 - v0, n) < 0)
                {
                    n1 = -n1;
                }

                // if (Vector3.Cross(n1, n).LengthSquared() < 1e-6) // test parallel
                // {
                //     continue;
                // }

                var nn1 = new Vector4(n1, Vector3.Dot(n1, v3));
                var ts = Basis3D.TangentSpace(nn);

                if (ts.IsCoincident(nn1))
                {
                    continue;
                }

                // ts.SetOrigin((v0 + v1 + v2 + v3) * 0.25f);
                var p3 = ts.Transform(v3);
                if (MathF.Abs(p3.Z) < cell)
                {
                    continue;
                }

                var pplane = ts.Project(nn1);
                var pp = ts.Plane2D(v1, v2, out var t1, out var t2);


                var locn1 = ts.Transform(n1);

                var ppt1 = ts.GetPos(pplane, v1);
                var ppt2 = ts.GetPos(pplane, v2);
                var cross = Vector3.Cross(new Vector3(pplane.X, pplane.Y, 0.0f), new Vector3(pp.X, pp.Y, 0.0f));

                if (MathF.Abs(cross.Z) > 1e-3 ||
                    MathF.Abs((pp.Z - pplane.Z)) / MathF.Max(MathF.Abs(p.Z), 1.0f) > cell ||
                    MathF.Abs(ppt1 - t1) > cell ||
                    MathF.Abs(ppt2 - t2) > cell)
                {
                    TestContext.WriteLine(Vector3.Dot(Vector3.Cross(v1 - v0, ts.Normal), n1));
                    TestContext.WriteLine();
                    TestContext.WriteLine(v0);
                    TestContext.WriteLine(v1);
                    TestContext.WriteLine(v2);
                    TestContext.WriteLine(v3);
                    TestContext.WriteLine();
                    TestContext.WriteLine(ts.Transform(v0));
                    TestContext.WriteLine(ts.Transform(v1));
                    TestContext.WriteLine(ts.Transform(v2));
                    TestContext.WriteLine(ts.Transform(v3));
                    TestContext.WriteLine();
                    TestContext.WriteLine(pplane);
                    TestContext.WriteLine(pp);
                    TestContext.WriteLine(cross.Z);
                    TestContext.WriteLine(MathF.Abs(pp.Z - pplane.Z));
                    TestContext.WriteLine(MathF.Abs(ppt1 - t1));
                    TestContext.WriteLine(MathF.Abs(ppt2 - t2));
                    TestContext.WriteLine($"{t1} {t2}");
                    TestContext.WriteLine($"{ppt1} {ppt2}");
                }

                Assert.Less(MathF.Abs(cross.Z), 1e-3, "Normals are not parallel");
                Assert.Less(MathF.Abs((pp.Z - pplane.Z)) / MathF.Max(MathF.Abs(p.Z), 1.0f), cell, "Shift is not equal");
                Assert.Less(MathF.Abs(ppt1 - t1), 2 * cell, "Parameters are different");
                Assert.Less(MathF.Abs(ppt2 - t2), 2 * cell, "Parameters are different");
                conclusive = true;
            }
            if (!conclusive)
            {
                Assert.Fail();
            }
        }
        // Assert.Pass();

    }
    [Test]
    public void TestProject()
    {
        var plane = new Vector4(1, 1, 1, 3);
        var basis = Basis3D.TangentSpace(new Vector4(0, 0, 1, 1));
        var s = basis.Project(plane);
        TestContext.WriteLine(basis.Tangent);
        TestContext.WriteLine(basis.Binormal);
        TestContext.WriteLine(basis.Normal);
        TestContext.WriteLine(basis.Origin);
        TestContext.WriteLine(basis.TransformNormal(plane));

        TestContext.WriteLine(s);
    }
}