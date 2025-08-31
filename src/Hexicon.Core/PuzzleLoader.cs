using System.Net.Http.Json;

namespace Hexicon.Core;
public sealed class PuzzleLoader
{
    private readonly HttpClient _http;
    public PuzzleLoader(HttpClient http) => _http = http;

    public async Task<PuzzleData?> LoadAsync(DateOnly date)
    {
        try { return await _http.GetFromJsonAsync<PuzzleData>($"puzzles/hexicon/{date:yyyy-MM-dd}.json"); }
        catch { return null; } // not found / bad json -> null
    }
}