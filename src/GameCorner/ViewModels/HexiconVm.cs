using Hexicon.Core;
using GameCorner.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameCorner.ViewModels
{
    public sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public string Date { get; set; } = "";
        public string Pangram { get; set; } = "";
        public char Required { get; set; }
        public List<string> Found { get; set; } = new();
        public int Score { get; set; }
        public int TargetScore { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public sealed class HexiconVm
    {
        private readonly Persistence _persist;
        private readonly IDateProvider _dates;

        private readonly PuzzleLoader _loader;

        // --- Puzzle state (read-only to the UI) ---
        public IReadOnlyList<char> Letters => _letters;
        public char Required { get; private set; }
        public IReadOnlyList<string> Valid => _valid;
        public IReadOnlyCollection<string> Found => _found;
        public IReadOnlyList<string> Clues => _clues;
        public IReadOnlyList<string> FormattedWords => _formattedWords;

        public int Score { get; private set; }
        public int TargetScore { get; private set; }

        // Reveal “Biologist (b)” once intended pangram is found
        public string PangramTitle { get; private set; } = string.Empty;
        public bool TitleRevealed { get; private set; }

        public string? SpecialURL { get; private set; } = "";
        public bool Themed { get; private set; } = false;
        public string? Tagline { get; private set; }
        public bool HasClues { get; private set; } = false;
        public bool Formatted { get; private set; } = false;
        public int TotalWords { get; private set; }
        public DateOnly PuzzleDate => _puzzleDate;
        public string SpecialSlug { get; set; } = string.Empty;

        // Input buffer
        public string CurrentEntry { get; set; } = string.Empty;

        // --- Backing fields ---
        private List<char> _letters = new();
        private List<string> _valid = new();
        private HashSet<string> _validSet = new();
        private List<string> _clues = new();
        private List<string> _formattedWords = new();
        private HashSet<string> _found = new(StringComparer.Ordinal);
        private Scoring _scoring = new(minLen: 4, pangramBonus: 6);
        private HashSet<char> _letterSet = new();
        private DateOnly _puzzleDate = new();

        public HexiconVm(PuzzleLoader loader, Persistence persist, IDateProvider dates)
        {
            _loader = loader;
            _persist = persist;
            _dates = dates;
        }

        // Initialize from curated JSON (required for play)
        public async Task InitAsync()
        {
            var today = _dates.Today;
            _puzzleDate = today;

            var data = string.IsNullOrWhiteSpace(SpecialSlug) ? 
                (await _loader.LoadAsync(today) ?? throw new InvalidOperationException($"No puzzle found for {today:yyyy-MM-dd}")) :
                (await _loader.LoadSpecialAsync(SpecialSlug) ?? throw new InvalidOperationException($"No special puzzle found for '{SpecialSlug}'"));

            // Hydrate
            _letters = (data.Letters ?? new List<char>())
                       .Select(char.ToLowerInvariant)
                       .Distinct()
                       .ToList();

            if (_letters.Count != 7)
                throw new InvalidOperationException("Puzzle must contain exactly 7 unique letters.");

            Required = char.ToLowerInvariant(data.Required);
            _letterSet = _letters.ToHashSet();

            _valid = (data.Words ?? new List<string>()).Select(Norm).Distinct().ToList();
            _validSet = _valid.ToHashSet(StringComparer.Ordinal);

            _clues = (data.Clues ?? new List<string>()).ToList();
            _formattedWords = (data.FormattedWords ?? new List<string>()).Distinct().ToList();

            PangramTitle = string.IsNullOrWhiteSpace(data.Pangram) ? "" : $"{data.Pangram.Trim().ToLowerInvariant()} ({Required})";
            TitleRevealed = false;

            Themed = data.Themed;
            Tagline = data.Tagline;
            TotalWords = (data.Words ?? []).Count;

            SpecialURL = data.SpecialURL;

            HasClues = (data.Clues ?? []).Any();
            Formatted = (data.FormattedWords ?? []).Any();

            CurrentEntry = "";
            _found.Clear();
            Score = 0;

            // Compute target score client-side
            TargetScore = _scoring.Total(_valid, _letterSet);

            // Try to restore saved state
            var saved = string.IsNullOrWhiteSpace(SpecialSlug) ?
                (await _persist.LoadAsync<SaveData>("hexicon", _puzzleDate)) :
                (await _persist.LoadSpecialAsync<SaveData>("hexicon", SpecialSlug));

            if (saved is not null &&
                saved.Pangram.Equals(data.Pangram, StringComparison.OrdinalIgnoreCase) &&
                saved.Required == data.Required)
            {
                _found = saved.Found.ToHashSet(StringComparer.Ordinal);
                Score = saved.Score;
                TitleRevealed = _found.Contains(data.Pangram);
            }

            InitBuckets();
        }

        // --- Input helpers ---
        public void Append(char c)
        {
            c = char.ToLowerInvariant(c);
            if (!_letterSet.Contains(c)) return;          // ignore letters not in the hive
            if (CurrentEntry.Length == 0 && c != Required) { /* optional: allow any start */ }
            CurrentEntry += c;
        }

        public void Backspace()
        {
            if (CurrentEntry.Length > 0)
                CurrentEntry = CurrentEntry[..^1];
        }

        public void ClearEntry() => CurrentEntry = "";

        // Shuffle ring letters (keep required)
        public void Shuffle()
        {
            if (_letters.Count != 7) return;
            var req = Required;
            var ring = _letters.Where(ch => ch != req).OrderBy(_ => Guid.NewGuid()).ToList();
            _letters = new[] { req }.Concat(ring).ToList();
        }

        public bool CanSubmit =>
            CurrentEntry.Length >= _scoring.MinLen &&
            CurrentEntry.Contains(Required) &&
            CurrentEntry.All(_letterSet.Contains);

        public async Task<bool> SubmitAsync()
        {
            if (!CanSubmit) return false;
            var w = Norm(CurrentEntry);
            ClearEntry();

            if (!_validSet.Contains(w) || _found.Contains(w)) return false;

            _found.Add(w);
            AddScore(_scoring.Word(w, _letterSet));

            if (w == PangramTitle.Split(' ')[0]) TitleRevealed = true;

            OnWordAccepted(w);

            // Persist progress
            var save = new SaveData
            {
                Version = 1,
                Date = _puzzleDate.ToString("yyyy-MM-dd"),
                Pangram = PangramTitle.Split(' ')[0],
                Required = Required,
                Found = _found.OrderBy(x => x.Length).ThenBy(x => x).ToList(),
                Score = Score,
                TargetScore = TargetScore,
                SavedAt = DateTime.UtcNow
            };

            if(string.IsNullOrWhiteSpace(SpecialSlug))
                await _persist.SaveAsync("hexicon", _puzzleDate, save);
            else
                await _persist.SaveSpecialAsync("hexicon", SpecialSlug, save);

            return true;
        }

        public async Task ResetTodayAsync()
        {
            // Clear persisted save
            if(string.IsNullOrWhiteSpace(SpecialSlug))
                await _persist.ClearAsync("hexicon", _puzzleDate);
            else
                await _persist.ClearSpecialAsync("hexicon", SpecialSlug);

            // Reset in-memory state
            _found.Clear();
            Score = 0;
            TitleRevealed = false;
            CurrentEntry = "";

            UpdateClearedStarts();
        }

        public bool IsPangram(string w)
        {
            if (string.IsNullOrEmpty(w)) return false;
            var s = w.AsSpan();
            foreach (var ch in _letters)
                if (!s.Contains(ch)) return false;
            return true;
        }

        // Utility for components that want % progress
        public double ScoreRatio => TargetScore <= 0 ? 0 : (double)Score / TargetScore;

        // --- Helpers ---
        private static string Norm(string s) => new string(s.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());

        public event Action<int>? OnScored;

        private void AddScore(int delta)
        {
            Score += delta;
            OnScored?.Invoke(delta);
        }

        // Map: first-letter -> total words that start with that letter
        private Dictionary<char, int> _totalByStart = new();

        // The letters whose buckets are fully cleared
        private readonly HashSet<char> _clearedStarts = new();

        public bool IsStartCleared(char c) => _clearedStarts.Contains(char.ToLowerInvariant(c));

        public void InitBuckets()
        {
            //_totalByStart = _validSet
            //    .GroupBy(w => char.ToLowerInvariant(w[0]))
            //    .ToDictionary(g => g.Key, g => g.Count());

            _totalByStart = _letterSet.ToDictionary(c => c, c => _validSet.Count(vs => vs[0] == c));

            UpdateClearedStarts();
        }

        private void UpdateClearedStarts()
        {
            var foundByStart = Found
                .GroupBy(w => char.ToLowerInvariant(w[0]))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kv in _totalByStart)
            {
                var c = kv.Key;
                var total = kv.Value;
                foundByStart.TryGetValue(c, out var have);
                if (have >= total) _clearedStarts.Add(c);
                else _clearedStarts.Remove(c);
            }
        }

        private void OnWordAccepted(string word)
        {
            _found.Add(word);
            UpdateClearedStarts();
        }

        public string BuildShareTitle()
        {
            return $"Hexicon {PuzzleDate:yyyy-MM-dd}";
        }

        public string BuildShareText(string rank)
        {
            var date = PuzzleDate.ToString("yyyy-MM-dd");
            var url = string.IsNullOrWhiteSpace(SpecialURL) ? $"https://lunamini.io/hexicon/{date}?share=1" : SpecialURL;
            var foundTitlePangram = TitleRevealed;
            var wordsFound = Found.Count;
            var pangramsFound = Found.Where(w => IsPangram(w)).Count();

            return $"Hexicon {date} — {rank} {(foundTitlePangram ? "⭐" : "")}{Environment.NewLine}{wordsFound} word{(wordsFound == 1 ? "" : "s")} found{(pangramsFound > 0 ? $"· {pangramsFound} hexicon{(pangramsFound == 1 ? "" : "s")}" : "")}{Environment.NewLine}{url}";
        }
    }
}
