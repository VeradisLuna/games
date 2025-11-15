using System.Text.Json.Serialization;

namespace Hexicon.Core
{
    public sealed record Puzzle(
        char Required,
        char[] Letters,       // 7 unique letters incl. Required; index 0 = required (nice for UI)
        int MinWordLength,
        string Seed,
        int TargetScore
    );

    public sealed class PuzzleData
    {
        public string Date { get; set; } = "";
        public string Pangram { get; set; } = "";
        public List<char> Letters { get; set; } = new List<char>();
        public char Required { get; set; }
        public List<string> Words { get; set; } = new List<string>();
        public bool Themed { get; set; } = false;
        public string? Tagline { get; set; }
        public List<string> Clues { get; set; } = new List<string>();
        public List<string> FormattedWords { get; set; } = new List<string>();
    }

    public sealed class CryptiniData
    {
        public string Date { get; set; } = "";
        public string Clue { get; set; }
        public string Enumeration { get; set; }
        public string Answer { get; set; }
        public string Explanation { get; set; }
        public List<string>? Hints { get; set; }
        public string? Author { get; set; }
    }

    public sealed class MiniData
    {
        public string Date { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public List<string> Rows { get; set; } = new();
        public List<string> Highlights { get; set; } = new();
        public MiniClueSet? Clues { get; set; }
    }

    public sealed class MiniClueSet
    {
        public List<MiniClueData> Across { get; set; } = new();
        public List<MiniClueData> Down { get; set; } = new();
    }

    public sealed class MiniClueData
    {
        public int Row { get; set; }
        public int Col { get; set; }
        public string Clue { get; set; } = "";
        public string? Answer { get; set; }  // optional, for validation/debug
    }

    public sealed class LetterheadData
    {
        public string Date { get; set; } = "";
        public string Author { get; set; } = "";
        public string Answer { get; set; } = "";
    }
}
