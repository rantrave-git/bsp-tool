using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

public interface IContentOperation<TContent>
{
    TContent Apply(TContent lhs, TContent rhs);
    TContent Invert(TContent content);
}
public interface ISpaceContentOperation<TContent> : IContentOperation<TContent>
{
    ISpaceContentOperation<TContent> Reverse { get; }
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
    IEnumerable<TContent> Leafs(THull hull);
    IBspTree<THull, TContent> Separate(THull newHull, THull targetHull);
    IBspTree<THull, TContent> Csg(IBspTree<THull, TContent> other, ISpaceContentOperation<TContent> operation, bool inplace = false);
    IBspTree<THull, TContent> CloneProjected(THull hull);

}
