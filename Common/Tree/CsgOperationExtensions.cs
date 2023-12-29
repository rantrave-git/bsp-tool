namespace Bsp.Common.Tree;

public static class CsgOperationExtensions
{
    public static long Apply(this CsgOperation self, long lhs, long rhs)
    {
        switch (self)
        {
            case CsgOperation.Union:
                return lhs | rhs;
            case CsgOperation.Intersect:
                return lhs & rhs;
            case CsgOperation.SymmetricDifference:
                return lhs ^ rhs;
            default:
                throw new NotImplementedException();
        }
    }
}
