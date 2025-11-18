using GameCorner.Services;
using Hexicon.Core;
using System.Text.Json.Serialization;

namespace GameCorner.ViewModels;

public sealed class CryptiniSave
{
    public string Date { get; set; } = "";
    public bool Solved { get; set; }
    public bool Revealed { get; set; }
    public string LastGuess { get; set; } = "";
    public DateTime SavedAt { get; set; }
    public int HintsRevealed { get; set; }
}

public sealed class CryptiniVm
{
    private readonly Persistence _persist;
    private readonly IDateProvider _dates;
    private readonly PuzzleLoader _loader;

    public string Clue { get; private set; } = "";
    public string Answer { get; private set; } = "";
    public string? Author { get; private set; }
    public string Enumeration { get; private set; } = "";
    public string CurrentEntry { get; set; } = "";
    public bool Solved { get; private set; }
    public bool Revealed { get; private set; } // the puzzle was revealed (i.e. the player didn't solve the puzzle)
    public string? Explanation { get; private set; }
    public DateOnly PuzzleDate { get; private set; }

    private string _answerNorm = "";
    private HashSet<string> _alts = new(StringComparer.Ordinal);

    private HashSet<string> _hints = new();
    public int HintsRevealed { get; private set; }
    public bool HasHints => _hints.Count > 0;
    public bool HasMoreHints => HintsRevealed < _hints.Count;
    public IReadOnlyList<string> VisibleHints => _hints.Take(HintsRevealed).ToList().AsReadOnly();

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

        CurrentEntry = "";
        Solved = false;
        Revealed = false;

        var data = await _loader.LoadCryptiniAsync(today)
                   ?? throw new InvalidOperationException($"No cryptic for {today:yyyy-MM-dd}");

        Clue = data.Clue ?? "";
        Enumeration = data.Enumeration ?? "";
        Explanation = data.Explanation;
        Answer = data.Answer;
        Author = data.Author;

        _answerNorm = Norm(data.Answer ?? "");

        _hints.Clear();
        if(data.Hints is { Count: > 0 })
        {
            foreach(var h in data.Hints)
                _hints.Add(h);
        }

        var saved = await _persist.LoadAsync<CryptiniSave>("cryptini", PuzzleDate);
        if (saved is not null)
        {
            Solved = saved.Solved;
            if (Solved)
                CurrentEntry = data.Answer!;
            Revealed = saved.Revealed;

            HintsRevealed = Math.Clamp(saved.HintsRevealed, 0, _hints.Count);
        }
        else
        {
            HintsRevealed = 0;
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

        await _persist.SaveAsync("cryptini", PuzzleDate, new CryptiniSave
        {
            Date = PuzzleDate.ToString("yyyy-MM-dd"),
            Solved = Solved,
            Revealed = Revealed,
            //LastGuess = CurrentEntry,
            SavedAt = DateTime.UtcNow,
            HintsRevealed = HintsRevealed
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
        await _persist.ClearAsync("cryptini", _dates.Today);
        Solved = false;
        Revealed = false;
        CurrentEntry = "";
        HintsRevealed = 0;
    }

    public async Task RevealHintAsync()
    {
        if(!HasMoreHints)
            return;

        HintsRevealed++;

        await _persist.SaveAsync("cryptini", PuzzleDate, new CryptiniSave
        {
            Date = PuzzleDate.ToString("yyyy-MM-dd"),
            Solved = Solved,
            Revealed = Revealed,
            //LastGuess = CurrentEntry,
            SavedAt = DateTime.UtcNow,
            HintsRevealed = HintsRevealed
        });
    }

    private static string Norm(string s) =>
        new string(s.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());

    public string BuildShareTitle()
    {
        return $"Cryptini {PuzzleDate:yyyy-MM-dd}";
    }

    public string BuildShareText()
    {
        var date = PuzzleDate.ToString("yyyy-MM-dd");
        var url = $"https://lunamini.io/cryptini/{date}?share=1";
        var status = "";
        if (Revealed)
        {
            switch(HintsRevealed)
            {
                case 0:
                    status = "gave up without using hints!";
                    break;
                case 1:
                    status = "gave up after 1 hint!";
                    break;
                default:
                    status = $"gave up after {HintsRevealed} hints!";
                    break;
            }
        }
        else
        {
            switch (HintsRevealed)
            {
                case 0:
                    status = "solved without using hints! ⭐";
                    break;
                case 1:
                    status = "solved after 1 hint!";
                    break;
                default:
                    status = $"solved after {HintsRevealed} hints!";
                    break;
            }
        }

            return $"Cryptini {date} — {status}{Environment.NewLine}{url}";
    }
}