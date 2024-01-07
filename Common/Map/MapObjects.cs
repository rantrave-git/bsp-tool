using System.Numerics;
using Bsp.Common.Geometry;
using Bsp.Common.Tree;

namespace Bsp.Common.MapObjects;

public record class LevelLeaf(int Area, List<int> Faces, List<int> Brushes, List<int> Models);
public class LevelGeometry
{
    public Mesh Geometry { get; set; }
    public List<Brush> Collision { get; set; }
    public List<Mesh> Models { get; set; }
    public TreeNode<LevelLeaf> Tree { get; set; }
}