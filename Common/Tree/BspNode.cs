using System.Text;
using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;


public class BspNode<TEdgeTree, TContent> : IBspNode<BspNode<TEdgeTree, TContent>, TContent>
    where TEdgeTree : ICopyable<TEdgeTree>
{
    public long numeration = -1;
    public TContent flags = default!;
    public BspNode<TEdgeTree, TContent>? back;
    public BspNode<TEdgeTree, TContent>? front;
    public TEdgeTree? edge;
    private BspNode() { }
    public BspNode(TEdgeTree plane)
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
    public BspNode<TEdgeTree, TContent> Copy() => new BspNode<TEdgeTree, TContent>()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
    };
    public BspNode<TEdgeTree, TContent> DeepCopy() => new BspNode<TEdgeTree, TContent>()
    {
        edge = edge == null ? default : edge.Copy(),
        flags = flags,
        numeration = numeration,
        back = back?.DeepCopy(),
        front = front?.DeepCopy(),
    };
    public void Detach() => front = back = null;
    public BspNode<TEdgeTree, TContent>? GetChild(Side side)
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
    public void SetNode(BspNode<TEdgeTree, TContent> back, BspNode<TEdgeTree, TContent> front, TEdgeTree edge)
    {
        this.back = back;
        this.front = front;
        this.edge = edge;
        this.flags = default!;
    }
    public BspNode<TEdgeTree, TContent> SetChild(Side side, BspNode<TEdgeTree, TContent> node)
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
    public void SetChildren(BspNode<TEdgeTree, TContent> back, BspNode<TEdgeTree, TContent> front)
    {
        this.back = back;
        this.front = front;
    }

    public override string ToString() => edge == null ? $"{numeration} [{flags}]" : $"{numeration} {edge}";
    // public string Info() => edge == null ? $"{numeration}: [{flags}]" : $"{numeration}: {edge}";
}