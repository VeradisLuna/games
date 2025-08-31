using System.Text;

namespace Hexicon.Core;

public interface IWordRepo
{
    IEnumerable<string> AllWords();
}

public sealed class EmbeddedWordRepo : IWordRepo
{
    // Replace this with your curated list later.
    private static readonly string[] SeedWords = new[]
    {
        "hexicon","hex","icon","cone","once","chic","chin","coin","ionic","echo","conic",
        "hone","honein","exonic","ionic","none","niche","choice","chox" // just for demo
    };

    private readonly Lazy<HashSet<string>> _words = new(() =>
        SeedWords
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length >= 2 && w.All(c => c is >= 'a' and <= 'z'))
            .ToHashSet()
    );

    public IEnumerable<string> AllWords() => _words.Value;
}