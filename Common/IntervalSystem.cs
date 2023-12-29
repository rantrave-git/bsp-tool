namespace Bsp.Common.Geometry;

class IntervalSystem
{
    static float Eps = 1e-5f;
    class Node
    {
        public float Min;
        public float Max;
        public Node? Left;
        public Node? Right;
        public int Classify(float p)
        {
            if (p <= Min - IntervalSystem.Eps) return -1;
            if (p >= Max + IntervalSystem.Eps) return 1;
            return 0;
        }
        public Node Insert(float min, float max)
        {
            var minc = Classify(min);
            if (minc == 1)
            {
                if (Right != null)
                {
                    Right = Right.Insert(min, max);
                    return this;
                }
                Right = new Node()
                {
                    Min = min,
                    Max = max,
                    Left = this,
                };
                return this;
            }
            var maxc = Classify(max);
            if (maxc == -1)
            {
                var n = new Node()
                {
                    Min = min,
                    Max = max,
                    Left = Left,
                    Right = this
                };
                Left = n;
                return n;
            }
            // minc == 0
            if (maxc == 0)
            {
                Min = Math.Min(Min, min);
                return this;
            }
            if (Right != null)
            {
                return Right.Insert(Math.Min(Min, min), max);
            }
            Min = Math.Min(Min, min);
            Max = Math.Max(Max, max);
            return this;
        }
    }
    Node? _left;
    public float Min { get; private set; } = float.MaxValue;
    public float Max { get; private set; } = -float.MaxValue;
    public void Add(float min, float max)
    {
        if (_left != null)
        {
            _left = _left.Insert(min, max);
        }
        else
        {
            _left = new Node() { Min = min, Max = max };
        }
        Min = MathF.Min(min, Min);
        Max = MathF.Max(min, Max);
    }
    private IEnumerator<Node> Enumerate()
    {
        var n = _left;
        while (n != null)
        {
            yield return n;
            n = n.Right;
        }
    }
    private IEnumerator<Node> EnumerateReversed()
    {
        var n = _left;
        if (n == null) yield break;
        while (n.Right != null)
        {
            n = n.Right;
        }
        while (n != null)
        {
            yield return n;
            n = n.Left;
        }
    }
    public IntervalSystem StripTail(float border)
    {
        var n = Enumerate();
        var tail = new IntervalSystem();
        while (n.MoveNext())
        {
            var cls = n.Current.Classify(border);
            if (cls == 0)
            {
                // cut right on interval
                tail._left = new Node()
                {
                    Min = border,
                    Max = n.Current.Max,
                    Right = n.Current.Right
                };
                n.Current.Max = border;
                n.Current.Right = null;
                Max = border;
                tail.Min = border;
                tail.Max = Max;
                Max = border;
                break;
            }
            else if (cls == 1)
            {
                if (n.Current.Left != null)
                {
                    // cut inbetween intervals
                    n.Current.Left.Right = null;
                    tail.Max = Max;
                    Max = n.Current.Left.Max;
                    n.Current.Left = null;
                    tail._left = n.Current;
                    tail.Min = tail._left.Min;
                    break;
                }
                // cut before first interval
                tail._left = _left;
                tail.Min = Min;
                tail.Max = Max;
                Min = float.MaxValue;
                Max = -float.MaxValue;
                _left = null;
                break;
            }
        }
        return tail;
    }

    private IntervalSystem Clone()
    {
        var res = new IntervalSystem();
        if (_left == null) return res;
        res._left = new Node()
        {
            Min = _left.Min,
            Max = _left.Max,
        };
        var p = res._left;
        var n = Enumerate();
        n.MoveNext();
        while (n.MoveNext())
        {
            p.Right = new Node()
            {
                Min = n.Current.Min,
                Max = n.Current.Max,
            };
            p.Right.Left = p;
        }
        return res;
    }
    public IntervalSystem Union(IntervalSystem other)
    {
        var res = Clone();
        var n = other.EnumerateReversed();
        while (n.MoveNext())
        {
            res.Add(n.Current.Min, n.Current.Max);
        }
        return res;
    }
}
