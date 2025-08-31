using GameCorner;
using Hexicon.Core;
using GameCorner.ViewModels;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Hexicon
builder.Services.AddSingleton<IWordRepo, EmbeddedWordRepo>();
builder.Services.AddSingleton<PuzzleGenerator>();
builder.Services.AddScoped<PuzzleLoader>();
builder.Services.AddScoped<GameCorner.Services.Persistence>();

builder.Services.AddScoped<HexiconVm>();

await builder.Build().RunAsync();