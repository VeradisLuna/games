using System.Net.Http.Json;

namespace Hexicon.Core;
public sealed class PuzzleLoader
{
    private readonly HttpClient _http;
    public PuzzleLoader(HttpClient http) => _http = http;

    public async Task<PuzzleData?> LoadAsync(DateOnly date)
    {
        try { return await _http.GetFromJsonAsync<PuzzleData>($"puzzles/hexicon/{date:yyyy-MM-dd}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<PuzzleData?> LoadSpecialAsync(string slug)
    {
        try { return await _http.GetFromJsonAsync<PuzzleData>($"puzzles/hexicon/special/{slug}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<CryptiniData?> LoadCryptiniAsync(DateOnly date)
    {
        try { return await _http.GetFromJsonAsync<CryptiniData>($"puzzles/cryptini/{date:yyyy-MM-dd}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<CryptiniData?> LoadSpecialCryptiniAsync(string slug)
    {
        try { return await _http.GetFromJsonAsync<CryptiniData>($"puzzles/cryptini/special/{slug}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<MiniData?> LoadMiniAsync(DateOnly date)
    {
        try { return await _http.GetFromJsonAsync<MiniData>($"puzzles/mini/{date:yyyy-MM-dd}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<MiniData?> LoadSpecialMiniAsync(string slug)
    {
        try { return await _http.GetFromJsonAsync<MiniData>($"puzzles/mini/special/{slug}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<LetterheadData?> LoadLetterheadAsync(DateOnly date)
    {
        try { return await _http.GetFromJsonAsync<LetterheadData>($"puzzles/letterhead/{date:yyyy-MM-dd}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<LetterheadData?> LoadSpecialLetterheadAsync(string slug)
    {
        try { return await _http.GetFromJsonAsync<LetterheadData>($"puzzles/letterhead/special/{slug}.json?v={DateTime.UtcNow.Ticks}"); }
        catch { return null; }
    }

    public async Task<string[]?> LoadAllowedLetterheadGuessesAsync()
    {
        try { return await _http.GetStringAsync($"puzzles/letterhead/allowed.txt?v={DateTime.UtcNow.Ticks}").ContinueWith(a => a.Result.Split(Environment.NewLine)); }
        catch { return null; }
    }
}