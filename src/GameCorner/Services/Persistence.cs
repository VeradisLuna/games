using Microsoft.JSInterop;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameCorner.Services;

public sealed class Persistence
{
    private readonly IJSRuntime _js;
    public Persistence(IJSRuntime js) => _js = js;

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    static string KeyFor(string gamePrefix, DateOnly date) => $"{gamePrefix}:{date:yyyy-MM-dd}";

    public async Task SaveAsync(string gamePrefix, DateOnly date, object state)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        await _js.InvokeVoidAsync("hexiconStore.set", KeyFor(gamePrefix, date), json);
    }

    public async Task SaveSpecialAsync(string gamePrefix, string slug, object state)
    {
        var json = JsonSerializer.Serialize(state, JsonOpts);
        await _js.InvokeVoidAsync("hexiconStore.set", $"{gamePrefix}:{slug}", json);
    }

    public async Task<T?> LoadAsync<T>(string gamePrefix, DateOnly date)
    {
        var json = await _js.InvokeAsync<string?>("hexiconStore.get", KeyFor(gamePrefix, date));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task<T?> LoadSpecialAsync<T>(string gamePrefix, string slug)
    {
        var json = await _js.InvokeAsync<string?>("hexiconStore.get", $"{gamePrefix}:{slug}");
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json);
    }

    public Task ClearAsync(string gamePrefix, DateOnly date) =>
        _js.InvokeVoidAsync("hexiconStore.remove", KeyFor(gamePrefix, date)).AsTask();

    public Task ClearSpecialAsync(string gamePrefix, string slug) =>
    _js.InvokeVoidAsync("hexiconStore.remove", $"{gamePrefix}:{slug}").AsTask();

    public async Task UnlockCollectionAsync(string collectionPrefix)
    {
        await _js.InvokeVoidAsync("hexiconStore.set", $"collection:{collectionPrefix}", "1");
    }

    public async Task<bool> CollectionUnlocked (string collectionPrefix)
    {
        var json = await _js.InvokeAsync<string?>("hexiconStore.get", $"collection:{collectionPrefix}");
        return string.IsNullOrWhiteSpace(json) ? false : true;
    }
}