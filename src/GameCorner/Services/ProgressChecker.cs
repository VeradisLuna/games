using GameCorner.ViewModels;
using Hexicon.Letterhead;
using Hexicon.Mini;
using Microsoft.JSInterop;
using System.Text.Json;

namespace GameCorner.Services;

public sealed class ProgressChecker
{
    private readonly IJSRuntime _js;
    public ProgressChecker(IJSRuntime js) => _js = js;

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task<(int progress, bool showTitle)> GetProgressAsync(string gamePrefix, DateOnly date)
    {
        return await GetProgressAsync(gamePrefix, date.ToString("yyyy-MM-dd"));
    }

    public async Task<(int progress, bool showTitle)> GetProgressAsync(string gamePrefix, string slug)
    {
        var json = await _js.InvokeAsync<string?>("hexiconStore.get", $"{gamePrefix}:{slug}");
        try
        {
            switch (gamePrefix)
            {
                case "cryptini":
                    {
                        if (string.IsNullOrWhiteSpace(json)) return (0, true);
                        var state = JsonSerializer.Deserialize<CryptiniSave>(json, JsonOpts);
                        if (state.Solved) return (3, true);
                        if (state.Revealed) return (2, true);
                        if (state.HintsRevealed > 0) return (1, true);
                        return (0, true);
                    }
                    break;
                case "letterhead":
                    {
                        if (string.IsNullOrWhiteSpace(json)) return (0, false);
                        var state = JsonSerializer.Deserialize<LetterheadSave>(json, JsonOpts);
                        if (state.Guesses.Contains(state.Letterhead)) return (3, true);
                        if (state.Guesses.Count >= 6) return (2, true);
                        if (state.Guesses.Count > 0) return (1, false);
                        return (0, false);
                    }
                    break;
                case "hexicon":
                    {
                        if (string.IsNullOrWhiteSpace(json)) return (0, false);
                        var state = JsonSerializer.Deserialize<SaveData>(json, JsonOpts);
                        bool showTitle = state.Found.Contains(state.Pangram);
                        if (state.Score >= state.TargetScore * 0.66) return (3, showTitle);
                        if (state.Found.Count > 0) return (1, showTitle);
                        return (0, false);
                    }
                    break;
                case "lunamini":
                    {
                        if (string.IsNullOrWhiteSpace(json)) return (0, true);
                        var state = JsonSerializer.Deserialize<MiniSave>(json, JsonOpts);
                        if (state.Solved) return (3, true);
                        if (state.Grid.Any(c => c.Entry is not null)) return (1, true);
                        return (0, true);
                    }
                    break;
            }
        }
        catch
        {
        }

        return (-1, false);
    }
}