using CircuitApi.Data;
using CircuitApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddControllers();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "https://localhost:7007", "http://localhost:5232" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddScoped<IHdlSynthesisService, HdlSynthesisService>();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "LogicCircuit.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(180);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
var protection = builder.Services.AddDataProtection().SetApplicationName("LogicCircuitSimulator");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    Directory.CreateDirectory(keyPath);
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

var connectionString = builder.Configuration.GetConnectionString("CircuitDatabase")
    ?? throw new InvalidOperationException(
        "CircuitDatabase connection string is missing.");

string databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<CircuitDbContext>(options =>
{
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
    else
        throw new InvalidOperationException("Database:Provider must be SqlServer or Sqlite.");
});

var app = builder.Build();
bool migrateOnly = builder.Configuration.GetValue<bool>("migrate-only");
if (migrateOnly || builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<CircuitDbContext>().Database;
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        await database.EnsureCreatedAsync();
    else
        await database.MigrateAsync();
    Console.WriteLine("Database migrations completed.");
    if (migrateOnly)
        return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("BlazorApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/health/database", async (CircuitDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        await db.Circuits.AsNoTracking().Select(circuit => circuit.CircuitId).Take(1).ToListAsync(cancellationToken);
        await db.SharedCircuits.AsNoTracking().Select(circuit => circuit.SharedCircuitId).Take(1).ToListAsync(cancellationToken);
        return Results.Ok(new { status = "ok" });
    }
    catch (Exception) when (!cancellationToken.IsCancellationRequested)
    {
        return Results.Problem("The database is unavailable or its migrations have not been applied.", statusCode: 503);
    }
});
app.MapStaticAssets();
app.MapFallbackToFile("index.html");

app.Run();
