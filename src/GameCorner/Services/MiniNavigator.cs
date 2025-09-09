namespace GameCorner.Services;

public readonly record struct NavResult(int To, bool Wrapped);

public sealed class MiniNavigator
{
    private readonly int _size;
    private readonly int _cells;
    private readonly Func<int, bool> _isBlock;

    private readonly int[] _acrossOrder; // 0..N-1 row-major
    private readonly int[] _downOrder;   // column-major
    private readonly int[] _posAcross;   // index -> position in acrossOrder
    private readonly int[] _posDown;     // index -> position in downOrder

    public MiniNavigator(int size, Func<int, bool> isBlock)
    {
        _size = size;
        _cells = size * size;
        _isBlock = isBlock;

        _acrossOrder = Enumerable.Range(0, _cells).ToArray();

        _downOrder = BuildDownOrder(size);
        _posAcross = BuildPositions(_acrossOrder, _cells);
        _posDown = BuildPositions(_downOrder, _cells);
    }

    public NavResult NextRightOpen(int from) => NextInOrder(from, _acrossOrder, _posAcross, forward: true);
    public NavResult NextLeftOpen(int from) => NextInOrder(from, _acrossOrder, _posAcross, forward: false);

    public NavResult NextDownOpen(int from) => NextInOrder(from, _downOrder, _posDown, forward: true);
    public NavResult NextUpOpen(int from) => NextInOrder(from, _downOrder, _posDown, forward: false);

    public NavResult NextAfterInput(bool downMode, int from)
        => downMode ? NextDownOpen(from) : NextRightOpen(from);

    public NavResult PrevOnBackspace(bool downMode, int from)
        => downMode ? NextUpOpen(from) : NextLeftOpen(from);

    // --- internals ---

    private NavResult NextInOrder(int from, int[] order, int[] posMap, bool forward)
    {
        int pos = posMap[from];
        int n = order.Length;
        for (int step = 1; step <= n; step++)
        {
            int nextPos = Mod(pos + (forward ? step : -step), n);
            int to = order[nextPos];
            if (!_isBlock(to))
            {
                bool wrapped = forward ? nextPos < pos : nextPos > pos;
                return new NavResult(to, wrapped);
            }
        }
        return new NavResult(from, false);
    }

    private static int[] BuildDownOrder(int size)
    {
        var list = new List<int>(size * size);
        for (int c = 0; c < size; c++)
            for (int r = 0; r < size; r++)
                list.Add(r * size + c);
        return list.ToArray();
    }

    private static int[] BuildPositions(int[] order, int cells)
    {
        var pos = new int[cells];
        for (int i = 0; i < order.Length; i++) pos[order[i]] = i;
        return pos;
    }

    private static int Mod(int x, int m) => (x % m + m) % m;
}