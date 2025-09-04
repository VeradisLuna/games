using GameCorner;
using GameCorner.Services;
using GameCorner.ViewModels;
using Hexicon.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IDateProvider>(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    Func<DateOnly> clock = () => DateOnly.FromDateTime(DateTime.Now);
#if DEBUG
    return new UrlDateProvider(nav, clock, allowFutureDates: true);
#else
    return new GameCorner.Services.UrlDateProvider(nav, clock, allowFutureDates: false);
#endif
});

// Hexicon
builder.Services.AddSingleton<IWordRepo, EmbeddedWordRepo>();
builder.Services.AddSingleton<PuzzleGenerator>();
builder.Services.AddScoped<PuzzleLoader>();
builder.Services.AddScoped<GameCorner.Services.Persistence>();
builder.Services.AddScoped<HexiconVm>();

// Cryptini
builder.Services.AddScoped<CryptiniVm>();

await builder.Build().RunAsync();