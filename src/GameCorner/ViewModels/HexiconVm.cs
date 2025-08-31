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
        public DateTime SavedAt { get; set; }
    }

    public sealed class HexiconVm
    {
        private readonly Persistence _persist;
        private DateOnly _today;

        private readonly PuzzleLoader _loader;

        // --- Puzzle state (read-only to the UI) ---
        public IReadOnlyList<char> Letters => _letters;
        public char Required { get; private set; }
        public IReadOnlyList<string> Valid => _valid;
        public IReadOnlyCollection<string> Found => _found;

        public int Score { get; private set; }
        public int TargetScore { get; private set; }

        // Reveal “Biologist (b)” once intended pangram is found
        public string PangramTitle { get; private set; } = string.Empty;
        public bool TitleRevealed { get; private set; }

        // Input buffer
        public string CurrentEntry { get; set; } = string.Empty;

        // --- Backing fields ---
        private List<char> _letters = new();
        private List<string> _valid = new();
        private HashSet<string> _validSet = new();
        private HashSet<string> _found = new(StringComparer.Ordinal);
        private Scoring _scoring = new(minLen: 4, pangramBonus: 6);
        private HashSet<char> _letterSet = new();

        public HexiconVm(PuzzleLoader loader, Persistence persist)
        {
            _loader = loader;
            _persist = persist;
        }

        // Initialize from curated JSON (required for play)
        public async Task InitAsync(DateOnly? date = null)
        {
            var today = date ?? DateOnly.FromDateTime(DateTime.Now);

            var data = await _loader.LoadAsync(today)
                       ?? throw new InvalidOperationException($"No puzzle found for {today:yyyy-MM-dd}");

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

            // Optional meta overrides
            //if (data.Meta is not null)
            //    _scoring = new Scoring(data.Meta.MinLen, data.Meta.PangramBonus);

            PangramTitle = string.IsNullOrWhiteSpace(data.Pangram)
                ? ""
                : $"{data.Pangram.Trim().ToLowerInvariant()} ({Required})";

            TitleRevealed = false;
            CurrentEntry = "";
            _found.Clear();
            Score = 0;

            // Compute target score client-side
            TargetScore = _scoring.Total(_valid, _letterSet);

            // Try to restore saved state
            var saved = await _persist.LoadAsync<SaveData>(_today);
            if (saved is not null &&
                saved.Pangram.Equals(data.Pangram, StringComparison.OrdinalIgnoreCase) &&
                saved.Required == data.Required)
            {
                _found = saved.Found.ToHashSet(StringComparer.Ordinal);
                Score = saved.Score;
                TitleRevealed = _found.Contains(data.Pangram);
            }
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
            Score += _scoring.Word(w, _letterSet);

            if (w == PangramTitle.Split(' ')[0]) TitleRevealed = true;

            // Persist progress
            var save = new SaveData
            {
                Version = 1,
                Date = _today.ToString("yyyy-MM-dd"),
                Pangram = PangramTitle.Split(' ')[0],
                Required = Required,
                Found = _found.OrderBy(x => x.Length).ThenBy(x => x).ToList(),
                Score = Score,
                SavedAt = DateTime.UtcNow
            };
            await _persist.SaveAsync(_today, save);

            return true;
        }

        public async Task ResetTodayAsync()
        {
            // Clear persisted save
            await _persist.ClearAsync(_today);

            // Reset in-memory state
            _found.Clear();
            Score = 0;
            TitleRevealed = false;
            CurrentEntry = "";
        }

        // Utility for components that want % progress
        public double ScoreRatio => TargetScore <= 0 ? 0 : (double)Score / TargetScore;

        // --- Helpers ---
        private static string Norm(string s) => new string(s.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());
    }
}
