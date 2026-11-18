using System.Threading.RateLimiting;
using BlazorArcade.Server.Matches;
using BlazorArcade.Server.Scores;
using BlazorArcade.Shared.Multiplayer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IGameRules, ChessRules>();
builder.Services.AddSingleton<IGameRules, Connect4Rules>();
builder.Services.AddSingleton<IGameRules, OthelloRules>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<RoomJanitor>();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ScoresDb>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Scores") ?? "Data Source=data/scores.db"));

// The arcade is a static site on another origin, so it needs CORS for both the API and the hub.
string[] origins = builder.Configuration.GetSection("Arcade:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ScoreEndpoints.SubmitRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScoresDb>();
    var dataSource = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
    if (Path.GetDirectoryName(Path.GetFullPath(dataSource)) is { } dir) Directory.CreateDirectory(dir);
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok("ok"));
app.MapScoreEndpoints();
app.MapHub<MatchHub>(MatchHubRoutes.Path);

app.Run();

public partial class Program;
