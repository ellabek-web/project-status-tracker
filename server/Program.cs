using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using ProjectStatusTracker.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Single-process hosting: bind to $PORT when the host sets it
// (Replit / Render / Azure set PORT), otherwise http://localhost:5000.
// This overrides the "Urls" value in appsettings.json.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}
else
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// CORS: allow the separately-hosted Vercel frontend and local Vite dev.
// Same-origin requests (single-process mode) need no CORS at all.
var allowedOrigins = (builder.Configuration["AllowedOrigins"]
        ?? "http://localhost:5173,http://127.0.0.1:5173,https://project-status-tracker-smoky.vercel.app")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("dev", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await SeedData.EnsureSeededAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("dev");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => new { status = "ok" });

// Serve the React client build (client/dist) from this same process.
// Look in the usual places, depending on how the app was started:
//   - running from server/            -> ../client/dist
//   - running from the publish output -> client/dist (copied by the publish target)
//   - running from the repo root      -> ./client/dist
// If no built client is found (e.g. `npm run build` was never run in
// client/), the API still works and / returns a short message instead.
string? clientDist = null;
var contentRoot = app.Environment.ContentRootPath;
var candidates = new[]
{
    Path.GetFullPath(Path.Combine(contentRoot, "..", "client", "dist")),
    Path.GetFullPath(Path.Combine(contentRoot, "client", "dist")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "client", "dist")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "client", "dist")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "client", "dist")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "client", "dist")),
};
foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
{
    if (File.Exists(Path.Combine(candidate, "index.html")))
    {
        clientDist = candidate;
        break;
    }
}

if (clientDist is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(clientDist)
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientDist)
    });
}

// /api/* must never be swallowed by the SPA fallback: unknown API
// paths return 404 JSON instead of index.html.
app.Map("/api/{**_}", () => Results.NotFound(new { error = "API route not found" }));

if (clientDist is not null)
{
    // SPA fallback: any non-API route serves the React app.
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientDist)
    });
}
else
{
    app.MapGet("/", () => Results.Ok(new
    {
        status = "ok",
        message = "API is running. Build the React client first: cd ../client && npm install && npm run build, then restart the server."
    }));
}

app.Run();
