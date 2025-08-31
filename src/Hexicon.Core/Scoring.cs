namespace Hexicon.Core;
public sealed class Scoring
{
    public int MinLen { get; }
    public int PangramBonus { get; }
    public Scoring(int minLen = 4, int pangramBonus = 6) { MinLen = minLen; PangramBonus = pangramBonus; }

    public int Word(string w, HashSet<char> letters)
    {
        //if (w.Length < MinLen) return 0;
        //var basePts = (w.Length == MinLen) ? 1 : w.Length;
        //var pang = w.ToHashSet().SetEquals(letters);
        //return basePts + (pang ? PangramBonus : 0);

        if (w.Length < MinLen) return 0;

        var pang = w.ToHashSet().SetEquals(letters);
        int basePts = pang ? PangramBonus : 0;
        switch (w.Length)
        {
            case 4: return basePts + 2;
            case 5: return basePts + 5;
            case 6: return basePts + 8;
            case 7: return basePts + 12;
            default: return basePts + 16;
        }
    }

    public int Total(IEnumerable<string> words, HashSet<char> letters) => words.Sum(w => Word(w, letters));
}