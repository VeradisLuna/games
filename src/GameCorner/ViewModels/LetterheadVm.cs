using GameCorner.Services; // Persistence, IDateProvider, PuzzleLoader live here in your app
using GameCorner.ViewModels;
using Hexicon.Core;
using Hexicon.Mini;
using System.Collections.ObjectModel;
using static System.Net.WebRequestMethods;

namespace Hexicon.Letterhead;


public enum TileState { Empty, Pending, Absent, Present, Correct }
public enum GameState { Playing, Won, Lost }


public sealed class LetterheadSave
{
    public string Date { get; set; } = "";
    public List<string> Guesses { get; set; } = new(); // normalized A–Z
    public DateTime SavedAt { get; set; }
    public string Letterhead { get; set; } = "";
}


public sealed class LetterheadVm
{
    public const int WordLen = 5;
    public const int MaxRows = 6;


    private readonly Persistence _persist;
    private readonly IDateProvider _dates;
    private readonly PuzzleLoader _loader;


    private DateOnly _puzzleDate;
    public string SpecialSlug { get; set; } = string.Empty;

    public bool IsLoaded { get; private set; }
    public GameState State { get; private set; } = GameState.Playing;
    public string Author { get; private set; } = "";
    public string Date { get; private set; } = "";
    public int CurrentRow { get; private set; } = 0;
    public int CurrentCol { get; private set; } = 0;

    public void SetActive(int row, int col)
    {
        if (State != GameState.Playing) return;
        if (row != CurrentRow) return;           // only current row is editable
        if (col < 0 || col >= WordLen) return;
        CurrentCol = col;
    }

    public sealed record Cell(char? Ch, TileState State);
    private readonly Cell[][] _grid =
    Enumerable.Range(0, MaxRows)
    .Select(_ => Enumerable.Range(0, WordLen).Select(_ => new Cell(null, TileState.Empty)).ToArray())
    .ToArray();
    public IReadOnlyList<IReadOnlyList<Cell>> Grid => _grid;


    public readonly Dictionary<char, TileState> KeyStates =
    Enumerable.Range('A', 26).ToDictionary(c => (char)c, _ => TileState.Empty);


    private string _answer = ""; // UPPERCASE
    private HashSet<string> _valid = new(StringComparer.Ordinal);


    public string AnswerMasked => _answer; // exposed only when lost (UI guards it)


    public LetterheadVm(PuzzleLoader loader, Persistence persist, IDateProvider dates)
    { _loader = loader; _persist = persist; _dates = dates; }


    public async Task InitAsync()
    {
        IsLoaded = false;

        ResetState();

        State = GameState.Playing; CurrentRow = 0; CurrentCol = 0;

        var today = _dates.Today;
        _puzzleDate = today;

        var data = string.IsNullOrWhiteSpace(SpecialSlug) ?
            (await _loader.LoadLetterheadAsync(_puzzleDate) ?? throw new InvalidOperationException($"No Letterhead found for {_puzzleDate:yyyy-MM-dd}")) :
            (await _loader.LoadSpecialLetterheadAsync(SpecialSlug) ?? throw new InvalidOperationException($"No special Letterhead found for '{SpecialSlug}'"));

        Author = data.Author ?? string.Empty;
        Date = data.Date ?? _puzzleDate.ToString("yyyy-MM-dd");
        _answer = Normalize(data.Answer!);

        _valid = await LoadAllowedLetterheadGuessesAsync();

        var saved = string.IsNullOrWhiteSpace(SpecialSlug) ?
            (await _persist.LoadAsync<LetterheadSave>("letterhead", _puzzleDate)) :
            (await _persist.LoadSpecialAsync<LetterheadSave>("letterhead", SpecialSlug));
        if (saved is { Guesses.Count: > 0 })
        {
            foreach (var g in saved.Guesses) ApplyGuess(g, restore: true);
        }

        IsLoaded = true;
    }

    private HashSet<string>? _letterheadAllowed;
    public async Task<HashSet<string>> LoadAllowedLetterheadGuessesAsync()
    {
        if (_letterheadAllowed is not null) return _letterheadAllowed;

        var text = await _loader.LoadAllowedLetterheadGuessesAsync();
        if (text == null)
            throw new InvalidOperationException("Could not load Letterhead allowed guesses list.");

        _letterheadAllowed = new HashSet<string>(text!.Select(Normalize), StringComparer.Ordinal);
        return _letterheadAllowed;
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());

    public static TileState[] ScoreGuess(string guess, string answer)
    {
        var res = new TileState[WordLen];
        var remaining = new Dictionary<char, int>();

        // First pass: mark exact matches; count the rest of the answer
        for (int i = 0; i < WordLen; i++)
        {
            if (guess[i] == answer[i]) res[i] = TileState.Correct;
            else
            {
                res[i] = TileState.Absent;
                remaining[answer[i]] = remaining.TryGetValue(answer[i], out var n) ? n + 1 : 1;
            }
        }

        // Second pass: mark presents using the remaining pool
        for (int i = 0; i < WordLen; i++)
        {
            if (res[i] == TileState.Correct) continue;
            var g = guess[i];
            if (remaining.TryGetValue(g, out var n) && n > 0)
            {
                res[i] = TileState.Present;
                remaining[g] = n - 1;
            }
        }
        return res;
    }

    private void ApplyGuess(string guess, bool restore)
    {
        var states = ScoreGuess(guess, _answer);

        for (int i = 0; i < WordLen; i++)
        {
            _grid[CurrentRow][i] = _grid[CurrentRow][i] with { State = states[i], Ch = guess[i] };

            // update keyboard, never downgrading: Correct > Present > Absent > Empty
            var k = guess[i];
            var existing = KeyStates[k];
            if ((int)states[i] > (int)existing)
                KeyStates[k] = states[i];
        }

        // game state
        if (guess == _answer) State = GameState.Won;
        else if (CurrentRow == MaxRows - 1) State = GameState.Lost;
        else { CurrentRow++; CurrentCol = 0; }

        if (!restore) _ = SaveState();
    }

    public async Task SaveState()
    {
        // Persist progress
        var save = new LetterheadSave
        {
            Date = _puzzleDate.ToString("yyyy-MM-dd"),
            SavedAt = DateTime.Now,
            Guesses = _grid.Take(CurrentRow + (State != GameState.Playing ? 1 : 0))
                                   .Select(row => new string(row.Select(c => c.Ch ?? ' ').ToArray()).Trim())
                                   .Where(g => g.Length == WordLen)
                                   .ToList(),
            Letterhead = _answer
        };

        if (string.IsNullOrWhiteSpace(SpecialSlug))
            await _persist.SaveAsync("letterhead", _puzzleDate, save);
        else
            await _persist.SaveSpecialAsync("letterhead", SpecialSlug, save);
    }

    // Allows Enter button to know if the row is full
    public bool CanSubmit =>
        State == GameState.Playing &&
        _grid[CurrentRow].All(c => c.Ch is char);

    // Called when Enter is pressed
    public string? Submit()
    {
        if (!CanSubmit) return "Not enough letters.";

        var guess = new string(_grid[CurrentRow].Select(c => c.Ch!.Value).ToArray());
        if (!_valid.Contains(guess)) return "Not in word list.";

        ApplyGuess(guess, restore: false);
        return null; // success
    }

    // Called for regular letter keys
    public void TypeChar(char ch)
    {
        if (State != GameState.Playing || CurrentCol >= WordLen) return;
        ch = char.ToUpperInvariant(ch);
        if (ch < 'A' || ch > 'Z') return;

        _grid[CurrentRow][CurrentCol] =
            _grid[CurrentRow][CurrentCol] with { Ch = ch, State = TileState.Pending };

        if (CurrentCol < WordLen - 1)
            CurrentCol++;
    }

    // Called for backspace
    public void Backspace()
    {
       if (State != GameState.Playing) return;

        if (_grid[CurrentRow][CurrentCol].Ch is not null)
        {
            _grid[CurrentRow][CurrentCol] = new Cell(null, TileState.Empty);
            return;
        }

        if(CurrentCol > 0)
        {
            CurrentCol--;
            _grid[CurrentRow][CurrentCol] = new Cell(null, TileState.Empty);
            return;
        }
    }

    private void ResetState()
    {
        State = GameState.Playing;
        CurrentRow = 0;
        CurrentCol = 0;

        // clear grid
        for (int r = 0; r < MaxRows; r++)
            for (int c = 0; c < WordLen; c++)
                _grid[r][c] = new Cell(null, TileState.Empty);

        // clear keyboard
        foreach (var k in KeyStates.Keys.ToList())
            KeyStates[k] = TileState.Empty;

        IsLoaded = false;
    }

    public string BuildShareTitle()
    {
        return $"Letterhead {_puzzleDate:yyyy-MM-dd}";
    }

    public string BuildShareText()
    {
        var date = _puzzleDate.ToString("yyyy-MM-dd");
        var url = $"https://lunamini.io/letterhead/{date}?share=1";
        var status = "";
        if (State == GameState.Won)
        {
            status = $"solved in {CurrentRow + 1}/{MaxRows} tries! ⭐";
        }
        else if (State == GameState.Lost)
        {
            status = "defeated!";
        }

        foreach (var row in _grid.Take(CurrentRow + 1))
        {
            status += Environment.NewLine;
            foreach (var cell in row)
            {
                status += cell.State switch
                {
                    TileState.Correct => "🟩",
                    TileState.Present => "🟦",
                    _ => "⬜"
                };
            }
        }

        return $"Letterhead {date} — {status}{Environment.NewLine}{url}";
    }
}