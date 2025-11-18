using GameCorner.Services;
using GameCorner.ViewModels;
using Hexicon.Core;
using System.Text.Json;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hexicon.Mini;

public enum CheckMark { None, Correct }

public sealed class MiniSave
{
    public string Date { get; set; } = "";
    public List<MiniCrosswordVm.Cell> Grid { get; set; } = new();
    public DateTime SavedAt { get; set; }
}

public sealed class MiniCrosswordVm
{
    public const int Size = 5;
    public const int Cells = Size * Size;

    private readonly Persistence _persist;
    private readonly IDateProvider _dates;
    private readonly PuzzleLoader _loader;

    public DateOnly PuzzleDate => _puzzleDate;
    private DateOnly _puzzleDate = new();

    public bool IsLoaded { get; set; } = false;
    public bool Revealed { get; private set; } // the puzzle was revealed (i.e. the player didn't solve the puzzle)

    public MiniCrosswordVm(PuzzleLoader loader, Persistence persist, IDateProvider dates)
    {
        _loader = loader;
        _persist = persist;
        _dates = dates;
    }

    // --- Public surface for the UI ---
    public IReadOnlyList<Cell> Grid => _grid;
    public IReadOnlyList<Clue> Across => _across;
    public IReadOnlyList<Clue> Down => _down;

    public string Title { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Date { get; private set; } = "";

    public sealed record Cell(bool IsBlock, bool IsHighlighted, char? Solution, int? Number)
    {
        public char? Entry { get; set; } = null;
        public bool IsCorrect => IsBlock || Entry == Solution;

        // UI-only decoration (not used in solving logic)
        public CheckMark Mark { get; set; } = CheckMark.None;
    }

    public sealed record Clue(int Number, int Row, int Col, bool IsAcross, string Text, int Length);

    private List<Cell> _grid = new(Cells);
    private readonly List<Clue> _across = new();
    private readonly List<Clue> _down = new();

    public async Task InitAsync()
    {
        IsLoaded = false;

        _grid.Clear();
        _across.Clear();
        _down.Clear();

        var today = _dates.Today;
        _puzzleDate = today;

        var data = await _loader.LoadMiniAsync(today)
                   ?? throw new InvalidOperationException($"No puzzle found for {today:yyyy-MM-dd}");

        HydrateMiniData(data);

        // Try to restore saved state
        var saved = await _persist.LoadAsync<MiniSave>("lunamini", _puzzleDate);
        if (saved?.Grid is { Count: Cells })
        {
            for (int i = 0; i < Cells; i++)
            {
                if (!_grid[i].IsBlock)
                    _grid[i].Entry = saved.Grid[i].Entry;
            }
        }

        IsLoaded = true;
    }

    public void HydrateMiniData(MiniData data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        Title = data.Title ?? "";
        Author = data.Author ?? "";
        Date = data.Date ?? "";

        BuildGridFromRows(data.Rows, data.Highlights);
        AutoNumber();
        BuildClues(data.Clues);
        ValidateClueAnswersAgainstGrid(data.Clues); // only checks if Answer provided
        // Start with blank entries (so it’s playable)
        for (int i = 0; i < _grid.Count; i++)
            if (!_grid[i].IsBlock) _grid[i].Entry = null;
    }

    private void BuildGridFromRows(List<string>? rows, List<string>? highlights)
    {
        if (rows is null || rows.Count != Size || rows.Any(r => r.Length != Size))
            throw new InvalidOperationException("rows must be 5 strings of length 5.");

        bool hasHighlights = highlights != null && highlights.Count == Size;
        
        _grid.Clear();
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                char ch = rows[r][c];
                bool isBlock = ch == '#';
                if (!isBlock && !char.IsLetter(ch))
                    throw new InvalidOperationException($"rows[{r}][{c}] must be A–Z or '#'.");
                char? sol = isBlock ? null : char.ToUpperInvariant(ch);
                bool isHighlighted = hasHighlights && highlights[r][c] == '*';
                _grid.Add(new Cell(isBlock, isHighlighted, sol, null));
            }
        }
    }

    private void AutoNumber()
    {
        int n = 0;
        for (int i = 0; i < Cells; i++)
        {
            if (_grid[i].IsBlock) continue;
            int r = i / Size, c = i % Size;
            bool startsAcross = c == 0 || _grid[i - 1].IsBlock;
            bool startsDown = r == 0 || _grid[i - Size].IsBlock;
            if (startsAcross || startsDown)
            {
                n++;
                _grid[i] = _grid[i] with { Number = n };
            }
        }
    }

    private void BuildClues(MiniClueSet? set)
    {
        if (set is null) throw new InvalidOperationException("Missing clues.");

        _across.Clear(); _down.Clear();

        foreach (var a in set.Across)
        {
            int len = SpanLength(a.Row, a.Col, isAcross: true);
            int num = NumberAt(a.Row, a.Col);
            _across.Add(new Clue(num, a.Row, a.Col, true, a.Clue, len));
        }

        foreach (var d in set.Down)
        {
            int len = SpanLength(d.Row, d.Col, isAcross: false);
            int num = NumberAt(d.Row, d.Col);
            _down.Add(new Clue(num, d.Row, d.Col, false, d.Clue, len));
        }

        _across.Sort((x, y) => x.Number.CompareTo(y.Number));
        _down.Sort((x, y) => x.Number.CompareTo(y.Number));
    }

    private int NumberAt(int row, int col)
    {
        var n = _grid[row * Size + col].Number;
        if (n is null) throw new InvalidOperationException($"({row},{col}) is not a clue start.");
        return n.Value;
    }

    private int SpanLength(int row, int col, bool isAcross)
    {
        if (_grid[row * Size + col].IsBlock)
            throw new InvalidOperationException("Clue start cannot be a block.");

        int len = 0;
        if (isAcross)
        {
            for (int c = col; c < Size && !_grid[row * Size + c].IsBlock; c++) len++;
        }
        else
        {
            for (int r = row; r < Size && !_grid[r * Size + col].IsBlock; r++) len++;
        }
        return len;
    }

    private void ValidateClueAnswersAgainstGrid(MiniClueSet? set)
    {
        if (set is null) return;

        foreach (var a in set.Across.Where(x => !string.IsNullOrWhiteSpace(x.Answer)))
        {
            var norm = Normalize(a.Answer!);
            var fromGrid = ReadSpan(a.Row, a.Col, true);
            if (!string.Equals(norm, fromGrid, StringComparison.Ordinal))
                throw new InvalidOperationException($"Across ({a.Row},{a.Col}) answer mismatch. JSON='{norm}', grid='{fromGrid}'.");
        }

        foreach (var d in set.Down.Where(x => !string.IsNullOrWhiteSpace(x.Answer)))
        {
            var norm = Normalize(d.Answer!);
            var fromGrid = ReadSpan(d.Row, d.Col, false);
            if (!string.Equals(norm, fromGrid, StringComparison.Ordinal))
                throw new InvalidOperationException($"Down ({d.Row},{d.Col}) answer mismatch. JSON='{norm}', grid='{fromGrid}'.");
        }
    }

    private string ReadSpan(int row, int col, bool across)
    {
        var chars = new List<char>();
        if (across)
        {
            for (int c = col; c < Size && !_grid[row * Size + c].IsBlock; c++)
                chars.Add(_grid[row * Size + c].Solution!.Value);
        }
        else
        {
            for (int r = row; r < Size && !_grid[r * Size + col].IsBlock; r++)
                chars.Add(_grid[r * Size + col].Solution!.Value);
        }
        return new string(chars.ToArray());
    }

    public async Task SaveState()
    {
        // Persist progress
        var save = new MiniSave
        {
            Date = _puzzleDate.ToString("yyyy-MM-dd"),
            SavedAt = DateTime.Now,
            Grid = _grid
        };
        
        await _persist.SaveAsync("lunamini", _puzzleDate, save);
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());

    private IEnumerable<int> SpanIndices(int row, int col, bool across)
    {
        if (across)
        {
            for (int c = col; c < Size; c++)
            {
                int idx = row * Size + c;
                if (_grid[idx].IsBlock) yield break;
                yield return idx;
            }
        }
        else
        {
            for (int r = row; r < Size; r++)
            {
                int idx = r * Size + col;
                if (_grid[idx].IsBlock) yield break;
                yield return idx;
            }
        }
    }

    public void ClearMarks()
    {
        for (int i = 0; i < _grid.Count; i++)
            _grid[i].Mark = CheckMark.None;
    }

    public void CheckAllCells()
    {
        // Only highlight correct cells (per your spec)
        for (int i = 0; i < _grid.Count; i++)
            _grid[i].Mark = (!_grid[i].IsBlock && _grid[i].Entry == _grid[i].Solution)
                ? CheckMark.Correct
                : CheckMark.None;
    }

    public void CheckCurrentClue(int row, int col, bool across)
    {
        // Clear existing marks in the clue first (so it doesn’t accumulate)
        foreach (var i in SpanIndices(row, col, across))
            _grid[i].Mark = CheckMark.None;

        foreach (var i in SpanIndices(row, col, across))
        {
            var cell = _grid[i];
            if (!cell.IsBlock && cell.Entry == cell.Solution)
                cell.Mark = CheckMark.Correct;
        }
    }

    // --- Convenience helpers for your UI or tests ---
    public bool IsBlock(int idx) => _grid[idx].IsBlock;
    public char? GetEntry(int idx) => _grid[idx].Entry;
    public void SetEntry(int idx, char? ch)
    {
        if (!_grid[idx].IsBlock)
        {
            _grid[idx].Entry = ch;
            _grid[idx].Mark = CheckMark.None;
        }
    }
    public char? GetSolution(int idx) => _grid[idx].Solution;
    public bool Solved => _grid.All(c => c.IsBlock || c.Entry == c.Solution);

    public string BuildShareText()
    {
        var date = PuzzleDate.ToString("yyyy-MM-dd");
        var title = Title;
        var url = $"https://lunamini.io/mini/{date}?share=1";
        var status = "solved! ⭐";

        return $"Luna Mini {date} — {status}{Environment.NewLine}{title}{Environment.NewLine}{url}";
    }
}