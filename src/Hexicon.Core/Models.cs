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
    }
}
