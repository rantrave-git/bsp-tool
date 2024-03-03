using System.Numerics;
using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;


public class BspNode<TEdgeHull, TContent> where TEdgeHull : class, IHull<TEdgeHull>
{
    public int numeration = -1;
    public TContent flags = default!;
    public BspNode<TEdgeHull, TContent>? back;
    public BspNode<TEdgeHull, TContent>? front;
    public IBspTree<TEdgeHull, TContent>? edge;
    private BspNode() { }
    public BspNode(IBspTree<TEdgeHull, TContent> plane)
    {
        this.edge = plane;
    }
    public BspNode(TContent flags)
    {
        this.flags = flags;
    }
    public void ApplyRight(IContentOperation<TContent> operation, TContent rhs)
    {
        if (edge == null)
        {
            flags = operation.Apply(flags, rhs);
            return;
        }
        back?.ApplyRight(operation, rhs);
        front?.ApplyRight(operation, rhs);
    }
    public void Apply(IContentOperation<TContent> operation, TContent lhs)
    {
        if (edge == null)
        {
            flags = operation.Apply(lhs, flags);
            return;
        }
        back?.Apply(operation, lhs);
        front?.Apply(operation, lhs);
    }
    public void Invert(IContentOperation<TContent> operation)
    {
        if (edge == null)
        {
            flags = operation.Invert(flags);
            return;
        }
        back?.Invert(operation);
        front?.Invert(operation);
    }
    public BspNode<TEdgeHull, TContent> Copy() => new()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
    };
    public BspNode<TEdgeHull, TContent> DeepCopy() => new()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
        back = back?.DeepCopy(),
        front = front?.DeepCopy(),
    };
    public BspNode<TEdgeHull, TContent> DeepCopy(ISpaceTransformer<TEdgeHull> transformer) => new()
    {
        edge = edge == null ? default : edge.CloneProjected(transformer.Transform(edge.Hull)),
        flags = flags,
        numeration = numeration,
        back = back?.DeepCopy(transformer),
        front = front?.DeepCopy(transformer),
    };
    public void Detach() => front = back = null;
    public BspNode<TEdgeHull, TContent>? GetChild(Side side)
    {
        if (side == Side.Incident)
        {
            return this;
        }
        if (side == Side.Back) return back;
        return front;
    }
    public void SetLeaf(TContent flags)
    {
        this.flags = flags;
        back = front = null;
        edge = default;
    }
    public void SetNode(BspNode<TEdgeHull, TContent> back, BspNode<TEdgeHull, TContent> front, IBspTree<TEdgeHull, TContent> edge)
    {
        this.back = back;
        this.front = front;
        this.edge = edge;
        this.flags = default!;
    }
    public void Optimize()
    {
        if (edge == null) return;
        back!.Optimize();
        front!.Optimize();

        if (back.edge == null && front.edge == null)
        {
            if (Object.Equals(this.back.flags, this.front.flags))
            {
                edge = null;
                flags = back.flags;
                back = front = null;
            }
        }
    }
    public BspNode<TEdgeHull, TContent> SetChild(Side side, BspNode<TEdgeHull, TContent> node)
    {
        if (side == Side.Incident)
        {
            edge = node.edge;
            flags = node.flags;
            front = node.front;
            back = node.back;
            numeration = node.numeration;
            return this;
        }
        if (side == Side.Back) return back = node;
        return front = node;
    }
    public void SetChildren(BspNode<TEdgeHull, TContent> back, BspNode<TEdgeHull, TContent> front)
    {
        this.back = back;
        this.front = front;
    }

    public override string ToString() => edge == null ? $"{numeration} [{flags}]" : $"{numeration} {edge}";

    public BspNode<TEdgeHull, TContent> Search(Vector<float> point)
    {
        var c = this;
        while (c.edge != null)
        {
            c = c.GetChild(c.edge.Hull.GetSide(point));
            if (c == null) throw new AssertionException("Tree is not full");
        }
        return c;
    }
    public (BspNode<TEdgeHull, TContent>? Back, BspNode<TEdgeHull, TContent>? Front) Split(BspNode<TEdgeHull, TContent> splitter, ISpaceContentOperation<TContent> operation, out bool flip)
    {
        // var splitterLocal = splitter.edge!.Hull.local;
        // var nodeLocal = this.edge!.Hull.local;
        flip = splitter.edge!.Hull.IsFlip(edge!.Hull);
        var (b, f) = edge!.Hull.Split(splitter.edge!.Hull);
        if (b == null || b.Empty) return (null, this);
        if (f == null || f.Empty) return (this, null);
        if (b == f) // coincide
        {
            if (edge!.Hull.HasSpace)
            {
                var t = edge!.Csg(splitter.edge!, operation, false);
                splitter.edge!.Csg(edge!, operation.Reverse, true);
                edge = t;
            }
            return (this, this);
        }
        var backNode = Copy();
        backNode.edge = edge.Separate(f, b);
        return (backNode, this);
    }
    // public string Info() => edge == null ? $"{numeration}: [{flags}]" : $"{numeration}: {edge}";
}