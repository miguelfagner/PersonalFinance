using Microsoft.AspNetCore.Components.Web;
using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PersonalFinance.Client;
using PersonalFinance.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApiBaseUrl = builder.Configuration["apiBaseUrl"];
var apiBaseUrl = string.IsNullOrWhiteSpace(configuredApiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBaseUrl;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<FinanceApi>();

await builder.Build().RunAsync();
