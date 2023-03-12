using System.Numerics;

namespace Bsp.Geometry;

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


public interface IBspTree
{
    IBspTree? Front { get; }
    IBspTree? Back { get; }
    IBspTree? Boundary { get; }
    // split tree into two parts by node
    (IBspTree? Front, IBspTree? Back) Split(IBspNode node);
    IBspTree Merge(IBspTree tree);
    IBspTree Copy();
}

class BspLeaf : IBspTree
{
    public IBspTree? Front => null;

    public IBspTree? Back => null;

    public IBspTree? Boundary => null;
    public int Flags { get; private set; }

    public IBspTree Copy()
    {
        return new BspLeaf() { Flags = Flags };
    }

    public IBspTree Merge(IBspTree tree)
    {
        throw new NotImplementedException();
    }

    public (IBspTree? Front, IBspTree? Back) Split(IBspNode node)
    {
        throw new NotImplementedException();
    }
}

class Bsp1D : IBspNode, IBspTree
{
    private IGeometryContext _context;
    public int Dimension => 1;

    public int Plane { get; init; }

    public IBspTree? Boundary { get; init; }
    public IBspTree? Front { get; private set; }
    public IBspTree? Back { get; private set; }
    public Bsp1D(IGeometryContext context)
    {
        _context = context;
    }

    public IBspTree Copy()
    {
        return new Bsp1D(_context)
        {
            Plane = Plane,
            Boundary = Boundary?.Copy(),
            Front = Front?.Copy(),
            Back = Back?.Copy()
        };
    }

    public IBspTree Merge(IBspTree tree)
    {
        throw new NotImplementedException();
    }

    public (IBspTree? Front, IBspTree? Back) Split(IBspNode node)
    {
        throw new NotImplementedException();
    }
}

class BspNode
{
    public int Dimension { get; init; }
    public Vector4 Plane { get; init; }
    public BspNode? Front { get; set; }
    public BspNode? Back { get; set; }

}