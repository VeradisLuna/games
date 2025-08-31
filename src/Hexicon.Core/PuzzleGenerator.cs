using System.Diagnostics;
using System.Security.Cryptography;

namespace Hexicon.Core;

public sealed class PuzzleGenerator
{
    private readonly HashSet<string> _dict;

    public PuzzleGenerator(IWordRepo repo) => _dict = repo.AllWords().ToHashSet();

    public Puzzle GenerateForDate(DateOnly date, int minLen = 4, string? salt = null)
    {
        var seedStr = salt is null ? date.ToString("yyyy-MM-dd") : $"{date:yyyy-MM-dd}:{salt}";
        var rng = new Random(StableSeed(seedStr));

        // Strategy: try pangram-based sets first (a word with exactly 7 unique letters)
        var pangrams = _dict.Where(w => w.Length >= minLen && w.ToHashSet().Count == 7).ToList();
        foreach (var pang in pangrams.OrderBy(_ => rng.Next()).Take(200))
        {
            var letters = pang.ToHashSet().ToArray();
            foreach (var required in letters.OrderBy(_ => rng.Next()))
            {
                var valid = ValidWords(letters, required, minLen).ToList();
                if (valid.Count is < 10 or > 250) continue; // tune range for feel
                var target = Score(valid, letters);
                // Nice UI: put required at index 0, others shuffled after
                var ring = letters.Where(c => c != required).OrderBy(_ => rng.Next()).ToArray();
                var arranged = (new[] { required }).Concat(ring).ToArray();
                return new Puzzle(required, arranged, minLen, seedStr, target);
            }
        }

        // Fallback: random 7-letter sets
        for (int i = 0; i < 5000; i++)
        {
            var letters = RandomLetterSet(rng);
            foreach (var required in letters.OrderBy(_ => rng.Next()))
            {
                var valid = ValidWords(letters, required, minLen).ToList();
                if (valid.Count is < 10 or > 250) continue;
                var target = Score(valid, letters);
                var ring = letters.Where(c => c != required).OrderBy(_ => rng.Next()).ToArray();
                var arranged = (new[] { required }).Concat(ring).ToArray();
                return new Puzzle(required, arranged, minLen, seedStr, target);
            }
        }

        throw new InvalidOperationException("Could not find a playable set.");
    }

    public IEnumerable<string> ValidWords(char[] letters, char required, int minLen)
    {
        var set = letters.ToHashSet();
        foreach (var w in _dict)
        {
            if (w.Length < minLen) continue;
            if (!w.Contains(required)) continue;
            if (w.All(c => set.Contains(c))) yield return w;
        }
    }

    public static int Score(IEnumerable<string> words, char[] letters)
    {
        int total = 0;
        var set = letters.ToHashSet();
        foreach (var w in words)
        {
            int pts = w.Length == 4 ? 1 : w.Length;
            if (w.ToHashSet().SetEquals(set)) pts += 7; // pangram bonus
            total += pts;
        }
        return (int)Math.Round(total * 0.3, MidpointRounding.AwayFromZero); // target ~30%
    }

    private static char[] RandomLetterSet(Random rng)
    {
        var s = new HashSet<char>();
        while (s.Count < 7) s.Add((char)('a' + rng.Next(0, 26)));
        return s.ToArray();
    }

    private static int StableSeed(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToInt32(hash, 0);
    }
}