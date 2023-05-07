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

public interface IHulled<THull>
{
    THull Hull { get; }
}
public interface ICopyable<T>
{
    public T Copy();
}
public interface IBspTree<THull, TContent> : IHulled<THull>, ICopyable<IBspTree<THull, TContent>>
{
    IBspTree<THull, TContent> Separate(THull newHull, THull targetHull);
    IBspTree<THull, TContent> Csg(IBspTree<THull, TContent> other, ISpaceContentOperation<TContent> operation, bool inplace = false);
}
