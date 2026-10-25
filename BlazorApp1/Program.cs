using BlazorApp1;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });

var configuredApiAddress = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7178/";
if (!Uri.TryCreate(configuredApiAddress.TrimEnd('/') + "/", UriKind.Absolute, out var apiAddress) ||
    (apiAddress.Scheme != Uri.UriSchemeHttp && apiAddress.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException("ApiBaseUrl must be an absolute HTTP or HTTPS address.");
}

builder.Services.AddScoped<CircuitApiClient>(_ =>
    new CircuitApiClient(new HttpClient { BaseAddress = apiAddress }));

builder.Services.AddScoped<HdlImportClient>(_ =>
    new HdlImportClient(new HttpClient
    {
        BaseAddress = apiAddress,
        Timeout = TimeSpan.FromSeconds(45)
    }));

await builder.Build().RunAsync();
