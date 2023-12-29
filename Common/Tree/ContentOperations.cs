using Bsp.Common.Geometry;

namespace Bsp.Common.Tree;

[Flags]
public enum VisibilityFlags : ulong
{
    Open = 0x0,
    BackToFront = 0x1, // vision blocked from back to front
    FrontToBack = 0x2, // vision blocked from front to back
    Blocked = 0x3,
}

public record struct VisibilityContent(long Flags = 0, VisibilityFlags Visibility = 0);

public class SolidContent : IContentProvider<VisibilityContent>
{
    // public static VisibilityContent EdgeContent(long flags)
    private readonly IContentProvider<VisibilityContent>? boundary;
    public VisibilityContent Content { get; }
    public VisibilityContent DefaultContent { get; } = new VisibilityContent();
    public SolidContent(VisibilityContent content, IContentProvider<VisibilityContent>? boundary = null)
    {
        Content = content; this.boundary = boundary;
    }
    public IContentProvider<VisibilityContent> BoundaryProvider => boundary ?? throw new NotImplementedException();
}

public static class ContentOperations
{
    public class FirstOperation<T> : IContentOperation<T>
    {
        public T Apply(T lhs, T rhs) => lhs;
        public T Invert(T content) => content;
    }
    public static class Binary
    {
        public class BinaryOperation { public long Invert(long content) => content; }
        public class AndOperation : BinaryOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs & rhs; }
        public class XorOperation : BinaryOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs ^ rhs; }
        public class OrOperation : BinaryOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs | rhs; }
        public class DifOperation : BinaryOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs & (~rhs); }
        public class InvDifOperation : BinaryOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => (~lhs) & rhs; }
        public static IContentOperation<long> And { get; } = new AndOperation();
        public static IContentOperation<long> Xor { get; } = new XorOperation();
        public static IContentOperation<long> Or { get; } = new OrOperation();
        public static IContentOperation<long> Difference { get; } = new DifOperation();
        public static IContentOperation<long> InvDifference { get; } = new InvDifOperation();
    }
    public static class Arithmetic
    {
        public class ArithmeticOperation { public long Invert(long content) => content; }
        public class SumOperation : ArithmeticOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs + rhs; }
        public class DifOperation : ArithmeticOperation, IContentOperation<long> { public long Apply(long lhs, long rhs) => lhs - rhs; }
        public static IContentOperation<long> Sum { get; } = new SumOperation();
        public static IContentOperation<long> Dif { get; } = new DifOperation();
    }
    public static class Visibility
    {
        public class VisOperation : IContentOperation<VisibilityContent>
        {
            private readonly IContentOperation<long> baseOperation;

            public VisOperation(IContentOperation<long> baseOperation)
            {
                this.baseOperation = baseOperation;
            }
            public VisibilityContent Apply(VisibilityContent lhs, VisibilityContent rhs) => new()
            {
                Flags = baseOperation.Apply(lhs.Flags, rhs.Flags),
                Visibility = lhs.Visibility | rhs.Visibility,
            };
            private static ulong Invert(ulong flags)
            {
                var f = 0x3ul & flags;
                return 0x3ul & ((f << 1) | (f >> 1)) | ((~0x3ul) & flags);
            }
            public VisibilityContent Invert(VisibilityContent content)
            {
                // var vis = (lhs & 0x1ul) | (0x6ul & (lhs | rhs))
                return new VisibilityContent(baseOperation.Invert(content.Flags), (VisibilityFlags)Invert((ulong)content.Visibility));
            }
        }
    }


    public class NoSpaceOperation<T> : ISpaceContentOperation<T>
    {
        public static NoSpaceOperation<T> Instance { get; } = new();
        public ISpaceContentOperation<T> Reverse => this;
        public ISpaceContentOperation<T> EdgeOperation => throw new NotImplementedException();
        public T Apply(T lhs, T rhs) => throw new NotImplementedException();

        public T Invert(T content) => throw new NotImplementedException();
    }

    public class SpaceOperation<T> : ISpaceContentOperation<T>
    {
        private IContentOperation<T> straight = new FirstOperation<T>();
        public ISpaceContentOperation<T> Reverse { get; private set; } = NoSpaceOperation<T>.Instance;
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
                Reverse = fwd,
                EdgeOperation = edge,
            };
            fwd.Reverse = bkd;
            return fwd;
        }
        public static SpaceOperation<T> Create(IContentOperation<T> straight, IContentOperation<T> inverse) =>
            Create(straight, inverse, NoSpaceOperation<T>.Instance);
        public static SpaceOperation<T> Create(IContentOperation<T> straight) =>
            Create(straight, NoSpaceOperation<T>.Instance, NoSpaceOperation<T>.Instance);
        public T Apply(T lhs, T rhs) => straight.Apply(lhs, rhs);

        public T Invert(T content) => straight.Invert(content);
    }

    public static ISpaceContentOperation<long> Space2DEdgeOperation { get; } = SpaceOperation<long>.Create(Arithmetic.Sum, Arithmetic.Dif);
    public static ISpaceContentOperation<long> Space2DIntersect { get; } = SpaceOperation<long>.Create(
        Binary.And,
        Binary.And,
        Space2DEdgeOperation
    );
    public static ISpaceContentOperation<long> Space2DDifference { get; } = SpaceOperation<long>.Create(
        Binary.Difference,
        Binary.InvDifference,
        Space2DEdgeOperation
    );
    public static ISpaceContentOperation<long> Space2DUnion { get; } = SpaceOperation<long>.Create(
        Binary.Or,
        Binary.Or,
        Space2DEdgeOperation
    );
    public static ISpaceContentOperation<VisibilityContent> Space1DVisEdgeOperation { get; } = SpaceOperation<VisibilityContent>.Create(
        new Visibility.VisOperation(Arithmetic.Sum),
        new Visibility.VisOperation(Arithmetic.Sum)
    );
    public static ISpaceContentOperation<VisibilityContent> Space2DVisIntersect { get; } = SpaceOperation<VisibilityContent>.Create(
        new Visibility.VisOperation(Binary.Xor),
        new Visibility.VisOperation(Binary.Xor),
        Space1DVisEdgeOperation
    );
    public static ISpaceContentOperation<VisibilityContent> Space2DVisDifference { get; } = SpaceOperation<VisibilityContent>.Create(
        new Visibility.VisOperation(Binary.Difference),
        new Visibility.VisOperation(Binary.InvDifference),
        Space1DVisEdgeOperation
    );
    public static ISpaceContentOperation<VisibilityContent> Space2DVisUnion { get; } = SpaceOperation<VisibilityContent>.Create(
        new Visibility.VisOperation(Binary.Or),
        new Visibility.VisOperation(Binary.Or),
        Space1DVisEdgeOperation
    );


    public static ISpaceContentOperation<VisibilityContent> Space3DVisBsp { get; } = SpaceOperation<VisibilityContent>.Create(
        new Visibility.VisOperation(Binary.Or),
        new Visibility.VisOperation(Binary.Or),
        Space2DVisUnion
    );
}
public static class AreaBuilders
{
    // public static BspTree3D CreateBspTree(this Mesh mesh)
    class BasicVisPassBuilder : IAreaBuilder<VisibilityContent>
    {
        public VisibilityContent OuterContent { get; }
        public VisibilityContent Aggregate(VisibilityContent portalContent, VisibilityContent portalLeafContent)
        {
            return new(portalContent.Flags | portalLeafContent.Flags, portalContent.Visibility | portalLeafContent.Visibility);
        }
        public bool PassCondition(VisibilityContent portalContent, Side side)
        {
            var v = (int)portalContent.Visibility;
            return (((((int)side & 0x2) >> 1) + 1) & v) == 0;
        }
    }
    public static IAreaBuilder<VisibilityContent> BasicAreaBuilder { get; } = new BasicVisPassBuilder();
}