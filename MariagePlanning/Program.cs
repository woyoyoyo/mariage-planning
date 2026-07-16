using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MariagePlanning;
using MariagePlanning.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<GistService>();
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<WeddingStore>();

await builder.Build().RunAsync();
