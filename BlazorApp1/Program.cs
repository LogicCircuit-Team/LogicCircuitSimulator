using BlazorApp1;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ =>
    new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });

var configuredApiAddress = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(configuredApiAddress))
    configuredApiAddress = builder.HostEnvironment.BaseAddress;

if (!Uri.TryCreate(configuredApiAddress.TrimEnd('/') + "/", UriKind.Absolute, out var apiAddress) ||
    (apiAddress.Scheme != Uri.UriSchemeHttp && apiAddress.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException("ApiBaseUrl must be an absolute HTTP or HTTPS address.");
}

builder.Services.AddScoped(_ =>
    new CircuitApiClient(new HttpClient(new BrowserCredentialsHandler())
    {
        BaseAddress = apiAddress
    }));

builder.Services.AddScoped(_ =>
    new HdlImportClient(new HttpClient(new BrowserCredentialsHandler())
    {
        BaseAddress = apiAddress,
        Timeout = TimeSpan.FromSeconds(45)
    }));

await builder.Build().RunAsync();