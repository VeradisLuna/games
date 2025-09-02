using GameCorner.Services;
using Hexicon.Core;
using System.Text.Json.Serialization;

namespace GameCorner.ViewModels;

public sealed class CryptiniSave
{
    public string Date { get; set; } = "";
    public bool Solved { get; set; }
    public string LastGuess { get; set; } = "";
    public DateTime SavedAt { get; set; }
}

public sealed class CryptiniVm
{
    private readonly Persistence _persist;
    private readonly IDateProvider _dates;
    private readonly PuzzleLoader _loader;

    public string Clue { get; private set; } = "";
    public string Answer { get; private set; } = "";
    public string Enumeration { get; private set; } = "";
    public string CurrentEntry { get; set; } = "";
    public bool Solved { get; private set; }
    public bool Revealed { get; private set; } // the puzzle was revealed (i.e. the player didn't solve the puzzle)
    public string? Explanation { get; private set; }
    public DateOnly PuzzleDate { get; private set; }

    private string _answerNorm = "";
    private HashSet<string> _alts = new(StringComparer.Ordinal);

    public CryptiniVm(PuzzleLoader loader, Persistence persist, IDateProvider dates)
    {
        _loader = loader;
        _persist = persist;
        _dates = dates;
    }

    public async Task InitAsync()
    {
        var today = _dates.Today;
        PuzzleDate = today;

        var data = await _loader.LoadCryptiniAsync(today)
                   ?? throw new InvalidOperationException($"No cryptic for {today:yyyy-MM-dd}");

        Clue = data.Clue ?? "";
        Enumeration = data.Enumeration ?? "";
        Explanation = data.Explanation;
        Answer = data.Answer;

        _answerNorm = Norm(data.Answer ?? "");

        var saved = await _persist.LoadAsync<CryptiniSave>(PuzzleDate);
        if (saved is not null)
        {
            Solved = saved.Solved;
            if (Solved)
                CurrentEntry = data.Answer!;
        }
    }

    public void Append(char c) => CurrentEntry += c;
    public void Backspace()
    {
        if (CurrentEntry.Length > 0) CurrentEntry = CurrentEntry[..^1];
    }
    public void ClearEntry() => CurrentEntry = "";

    public async Task<bool> SubmitAsync()
    {
        var norm = Norm(CurrentEntry);
        var ok = norm == _answerNorm || _alts.Contains(norm);
        if (ok) Solved = true;

        await _persist.SaveAsync(PuzzleDate, new CryptiniSave
        {
            Date = PuzzleDate.ToString("yyyy-MM-dd"),
            Solved = Solved,
            //LastGuess = CurrentEntry,
            SavedAt = DateTime.UtcNow
        });

        CurrentEntry = ""; // clear down on submit
        return ok;
    }

    public Task RevealAsync()
    {
        Revealed = true;
        Solved = true;
        CurrentEntry = "";
        return Task.CompletedTask;
    }

    public async Task ResetAsync()
    {
        await _persist.ClearAsync(_dates.Today);
        Solved = false;
        CurrentEntry = "";
    }

    private static string Norm(string s) =>
        new string(s.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());
}