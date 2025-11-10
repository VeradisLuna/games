using GameCorner;
using GameCorner.Services;
using GameCorner.ViewModels;
using Hexicon.Core;
using Hexicon.Mini;
using Hexicon.Letterhead;
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

// probably don't need these two any more - due a clean-up at some point!
builder.Services.AddSingleton<IWordRepo, EmbeddedWordRepo>();
builder.Services.AddSingleton<PuzzleGenerator>();

builder.Services.AddScoped<PuzzleLoader>();
builder.Services.AddScoped<Persistence>();
builder.Services.AddScoped<HexiconVm>();
builder.Services.AddScoped<CryptiniVm>();
builder.Services.AddScoped<MiniCrosswordVm>();
builder.Services.AddScoped<LetterheadVm>();

await builder.Build().RunAsync();