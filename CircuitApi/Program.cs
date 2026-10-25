using CircuitApi.Data;
using CircuitApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorApp", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7007",
                "http://localhost:5232")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddScoped<IHdlSynthesisService, HdlSynthesisService>();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("CircuitDatabase")
    ?? throw new InvalidOperationException(
        "CircuitDatabase connection string is missing.");

builder.Services.AddDbContext<CircuitDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.UseCors("BlazorApp");

app.UseAuthorization();
app.MapControllers();

app.Run();