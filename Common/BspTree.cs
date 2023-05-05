using System.Numerics;

namespace Bsp.Common.Geometry;

public interface IGeometryContext
{
    Vector4 GetPlane(int id);
    int AddPlane(Vector4 plane);
    bool TryFindPlane(Vector4 plane, out int id);

    int AddFace(FaceData face);
}
public interface IBspNode
{
    public int Plane { get; }
}

