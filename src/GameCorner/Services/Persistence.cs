using System.Text.Json;
using Microsoft.JSInterop;

namespace GameCorner.Services;

public sealed class Persistence
{
    private readonly IJSRuntime _js;
    public Persistence(IJSRuntime js) => _js = js;

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    static string KeyFor(DateOnly date) => $"hexicon:{date:yyyy-MM-dd}";

    public async Task SaveAsync(DateOnly date, object state)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        await _js.InvokeVoidAsync("hexiconStore.set", KeyFor(date), json);
    }

    public async Task<T?> LoadAsync<T>(DateOnly date)
    {
        var json = await _js.InvokeAsync<string?>("hexiconStore.get", KeyFor(date));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    }

    public Task ClearAsync(DateOnly date) =>
        _js.InvokeVoidAsync("hexiconStore.remove", KeyFor(date)).AsTask();
}