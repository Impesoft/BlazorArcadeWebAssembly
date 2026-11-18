using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using BlazorArcade;
using BlazorArcade.Services;
using Toolbelt.Blazor.Extensions.DependencyInjection;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddPWAUpdater();

// Online play and leaderboards (BlazorArcade.Server); hidden when ArcadeBackend:BaseUrl is empty.
builder.Services.AddSingleton(new ArcadeBackendOptions(builder.Configuration["ArcadeBackend:BaseUrl"]));
builder.Services.AddSingleton<ScoreboardClient>();
builder.Services.AddScoped<PlayerProfile>();
await builder.Build().RunAsync();
