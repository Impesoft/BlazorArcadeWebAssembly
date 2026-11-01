using BlazorArcade.Shared.Scores;
using Microsoft.EntityFrameworkCore;

namespace BlazorArcade.Server.Scores;

public sealed class ScoreRecord
{
    public long Id { get; set; }
    public required string Game { get; set; }
    public required string Player { get; set; }
    public int Score { get; set; }
    public DateTimeOffset AchievedAt { get; set; }
}

public sealed class ScoresDb(DbContextOptions<ScoresDb> options) : DbContext(options)
{
    public DbSet<ScoreRecord> Scores => Set<ScoreRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var score = model.Entity<ScoreRecord>();
        score.Property(s => s.Game).HasMaxLength(32);
        score.Property(s => s.Player).HasMaxLength(ScoreGames.MaxNameLength);
        // SQLite can't order by DateTimeOffset natively; store it as ticks.
        score.Property(s => s.AchievedAt).HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
        score.HasIndex(s => new { s.Game, s.Score });
    }
}

public static class ScoreEndpoints
{
    public const int DefaultTake = 10;
    public const int MaxTake = 50;

    /// <summary>Scores kept per game; anything below the cut is pruned on insert.</summary>
    public const int KeepPerGame = 100;

    public const string SubmitRateLimit = "score-submit";

    public static void MapScoreEndpoints(this IEndpointRouteBuilder app)
    {
        var scores = app.MapGroup("/api/scores");

        scores.MapGet("/{game}", async (string game, int? take, ScoresDb db) =>
        {
            if (!ScoreGames.MaxScore.ContainsKey(game)) return Results.NotFound();
            return Results.Ok(await Top(db, game, Math.Clamp(take ?? DefaultTake, 1, MaxTake)));
        });

        scores.MapPost("/{game}", async (string game, SubmitScoreRequest request, ScoresDb db, TimeProvider time) =>
        {
            if (!ScoreGames.MaxScore.TryGetValue(game, out int max)) return Results.NotFound();
            string? player = ScoreGames.NormalizeName(request.Player);
            if (player == null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["player"] = ["Enter a name."] });
            if (request.Score <= 0 || request.Score > max)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["score"] = ["That score is not possible."] });

            var record = new ScoreRecord { Game = game, Player = player, Score = request.Score, AchievedAt = time.GetUtcNow() };
            db.Scores.Add(record);
            await db.SaveChangesAsync();

            // Rank among stored scores; an equal score set earlier keeps the higher place.
            int better = await db.Scores.CountAsync(s => s.Game == game &&
                (s.Score > record.Score || (s.Score == record.Score && s.Id < record.Id)));
            int rank = better + 1;

            await db.Scores
                .Where(s => s.Game == game)
                .OrderByDescending(s => s.Score).ThenBy(s => s.Id)
                .Skip(KeepPerGame)
                .ExecuteDeleteAsync();

            return Results.Ok(new SubmitScoreResponse(rank <= KeepPerGame ? rank : 0, await Top(db, game, DefaultTake)));
        }).RequireRateLimiting(SubmitRateLimit);
    }

    private static async Task<List<ScoreEntry>> Top(ScoresDb db, string game, int take)
    {
        var rows = await db.Scores
            .Where(s => s.Game == game)
            .OrderByDescending(s => s.Score).ThenBy(s => s.Id)
            .Take(take)
            .ToListAsync();
        return rows.Select((s, i) => new ScoreEntry(i + 1, s.Player, s.Score, s.AchievedAt)).ToList();
    }
}
