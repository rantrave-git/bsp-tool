namespace Bsp.Common.Tree;

public static class ContentOperations
{
    public class FirstOperation<T> : IContentOperation<T> { public T Apply(T lhs, T rhs) => lhs; }
    public static class Binary
    {
        public class AndOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs & rhs; }
        public class XorOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs ^ rhs; }
        public class OrOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs | rhs; }
        public class DifOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs & (~rhs); }
        public static IContentOperation<long> And { get; } = new AndOperation();
        public static IContentOperation<long> Xor { get; } = new XorOperation();
        public static IContentOperation<long> Or { get; } = new OrOperation();
        public static IContentOperation<long> Difference { get; } = new OrOperation();
    }
    public static class Arithmetic
    {
        public class SumOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs + rhs; }
        public class DifOperation : IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs - rhs; }
        public static IContentOperation<long> Sum { get; } = new SumOperation();
        public static IContentOperation<long> Dif { get; } = new DifOperation();
    }



    public class NoSpaceOperation<T> : ISpaceContentOperation<T>
    {
        public static NoSpaceOperation<T> Instance = new NoSpaceOperation<T>();
        public ISpaceContentOperation<T> Inverse => this;
        public ISpaceContentOperation<T> EdgeOperation => throw new NotImplementedException();
        public T Apply(T lhs, T rhs) { throw new NotImplementedException(); }
    }

    public class SpaceOperation<T> : ISpaceContentOperation<T>
    {
        private IContentOperation<T> straight = new FirstOperation<T>();
        public ISpaceContentOperation<T> Inverse { get; private set; } = NoSpaceOperation<T>.Instance;
        public ISpaceContentOperation<T> EdgeOperation { get; private set; } = NoSpaceOperation<T>.Instance;

        public static SpaceOperation<T> Create(IContentOperation<T> straight, IContentOperation<T> inverse, ISpaceContentOperation<T> edge)
        {
            var fwd = new SpaceOperation<T>()
            {
                straight = straight,
                EdgeOperation = edge,
            };
            var bkd = new SpaceOperation<T>()
            {
                straight = inverse,
                Inverse = fwd,
                EdgeOperation = edge,
            };
            fwd.Inverse = bkd;
            return fwd;
        }
        public static SpaceOperation<T> Create(IContentOperation<T> straight, IContentOperation<T> inverse) =>
            Create(straight, inverse, NoSpaceOperation<T>.Instance);
        public static SpaceOperation<T> Create(IContentOperation<T> straight) =>
            Create(straight, NoSpaceOperation<T>.Instance, NoSpaceOperation<T>.Instance);
        public T Apply(T lhs, T rhs) => straight.Apply(lhs, rhs);
    }

    public static ISpaceContentOperation<long> Space2DIntersect = SpaceOperation<long>.Create(
        Binary.And,
        Binary.Xor,
        SpaceOperation<long>.Create(Arithmetic.Sum, Arithmetic.Dif)
    );
    public static ISpaceContentOperation<long> Space2DUnion = SpaceOperation<long>.Create(
        Binary.Or,
        Binary.Difference,
        SpaceOperation<long>.Create(Arithmetic.Sum, Arithmetic.Dif)
    );
}