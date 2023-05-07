using System.Numerics;
using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;


public class BspNode<THull, TContent> where THull : class, IHull<THull>
{
    public long numeration = -1;
    public TContent flags = default!;
    public BspNode<THull, TContent>? back;
    public BspNode<THull, TContent>? front;
    public IBspTree<THull, TContent>? edge;
    private BspNode() { }
    public BspNode(IBspTree<THull, TContent> plane)
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
    public void ApplyLeft(IContentOperation<TContent> operation, TContent lhs)
    {
        if (edge == null)
        {
            flags = operation.Apply(lhs, flags);
            return;
        }
        back?.ApplyLeft(operation, lhs);
        front?.ApplyLeft(operation, lhs);
    }
    public BspNode<THull, TContent> Copy() => new BspNode<THull, TContent>()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
    };
    public BspNode<THull, TContent> DeepCopy() => new BspNode<THull, TContent>()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
        back = back?.DeepCopy(),
        front = front?.DeepCopy(),
    };
    public void Detach() => front = back = null;
    public BspNode<THull, TContent>? GetChild(Side side)
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
    public void SetNode(BspNode<THull, TContent> back, BspNode<THull, TContent> front, IBspTree<THull, TContent> edge)
    {
        this.back = back;
        this.front = front;
        this.edge = edge;
        this.flags = default!;
    }
    public BspNode<THull, TContent> SetChild(Side side, BspNode<THull, TContent> node)
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
    public void SetChildren(BspNode<THull, TContent> back, BspNode<THull, TContent> front)
    {
        this.back = back;
        this.front = front;
    }

    public override string ToString() => edge == null ? $"{numeration} [{flags}]" : $"{numeration} {edge}";

    public BspNode<THull, TContent> Search(Vector<float> point)
    {
        var c = this;
        while (c.edge != null)
        {
            c = c.GetChild(c.edge.Hull.GetSide(point));
            if (c == null) throw new AssertionException("Tree is not full");
        }
        return c;
    }
    public (BspNode<THull, TContent>? Back, BspNode<THull, TContent>? Front) Split(BspNode<THull, TContent> splitter, ISpaceContentOperation<TContent> operation, out bool flip)
    {
        // var splitterLocal = splitter.edge!.Hull.local;
        // var nodeLocal = this.edge!.Hull.local;
        flip = splitter.edge!.Hull.IsFlip(edge!.Hull);
        var (b, f) = this.edge!.Hull.Split(splitter.edge!.Hull);
        if (b == null || b.Empty) return (null, this);
        if (f == null || f.Empty) return (this, null);
        if (b == f) // coincide
        {
            if (edge!.Hull.HasSpace) edge!.Csg(splitter.edge!, flip ? operation.Inverse : operation, true);
            return (this, this);
        }
        var backNode = this.Copy();
        backNode.edge = this.edge.Separate(f, b);
        return (backNode, this);
    }
    // public string Info() => edge == null ? $"{numeration}: [{flags}]" : $"{numeration}: {edge}";
}