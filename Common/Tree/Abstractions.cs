using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public interface IContentOperation<TContent>
{
    TContent Apply(TContent lhs, TContent rhs);
}
public interface ISpaceContentOperation<TContent> : IContentOperation<TContent>
{
    ISpaceContentOperation<TContent> Inverse { get; }
    ISpaceContentOperation<TContent> EdgeOperation { get; }
}
public interface IBspTree<T, TContent> where T : IBspTree<T, TContent>
{
    T Csg(T other, ISpaceContentOperation<TContent> operation, bool inplace = false);
}

public interface IBspNode<TNode, TContent>
    where TNode : IBspNode<TNode, TContent>
{
    // void Apply(IContentOperation<TContent> operation, TContent rhs);
    TNode Copy();
    TNode DeepCopy();
    void Detach();
    TNode? GetChild(Side side);
    TNode SetChild(Side side, TNode node);
    void SetChildren(TNode back, TNode front);
}
