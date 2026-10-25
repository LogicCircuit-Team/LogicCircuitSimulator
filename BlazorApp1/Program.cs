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

builder.Services.AddScoped<CircuitApiClient>(sp =>
{
    var http = new HttpClient
    {
        BaseAddress = new Uri("https://localhost:7178/")
    };

    return new CircuitApiClient(http);
});

await builder.Build().RunAsync();