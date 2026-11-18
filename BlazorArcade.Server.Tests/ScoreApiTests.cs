using System.Net;
using System.Net.Http.Json;
using BlazorArcade.Server.Matches;
using BlazorArcade.Shared.Scores;

namespace BlazorArcade.Server.Tests;

public class ScoreApiTests
{
    [Fact]
    public async Task SubmittedScores_ComeBackRankedHighestFirst()
    {
        await using var server = new ArcadeServer();
        var http = server.CreateClient();

        await Submit(http, "knightmare", "  Ward  ", 1200);
        var second = await Submit(http, "knightmare", "Ada", 5000);
        await Submit(http, "knightmare", "Bob", 300);
        await Submit(http, "cryptcrawler", "Ada", 9000); // other games don't mix in

        Assert.Equal(1, second.Rank);
        var top = await http.GetFromJsonAsync<List<ScoreEntry>>("/api/scores/knightmare");
        Assert.Equal(new[] { ("Ada", 5000, 1), ("Ward", 1200, 2), ("Bob", 300, 3) },
            top!.Select(s => (s.Player, s.Score, s.Rank)));
    }

    [Fact]
    public async Task ImplausibleOrAnonymousScores_AreRejected()
    {
        await using var server = new ArcadeServer();
        var http = server.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await Post(http, "cryptcrawler", "Ada", 10_000_000)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(http, "cryptcrawler", "Ada", -5)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(http, "cryptcrawler", "   ", 100)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(http, "minesweeper-royale", "Ada", 100)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/scores/minesweeper-royale")).StatusCode);
    }

    [Fact]
    public async Task Submissions_AreRateLimitedPerClient()
    {
        await using var server = new ArcadeServer();
        var http = server.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 12; i++) statuses.Add((await Post(http, "tetris", "Spammer", 100 + i)).StatusCode);

        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Cors_AllowsTheArcadeOrigin()
    {
        await using var server = new ArcadeServer();
        var http = server.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/scores/tetris");
        request.Headers.Add("Origin", "http://localhost:5143");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await http.SendAsync(request);
        Assert.Equal("http://localhost:5143", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory]
    [InlineData("  Ward   Impe ", "Ward Impe")]
    [InlineData("A\u0007B", "AB")]
    [InlineData("ThisNameIsWayTooLongForTheBoard", "ThisNameIsWayToo")]
    [InlineData("   ", null)]
    public void Names_AreTidied(string input, string? expected) =>
        Assert.Equal(expected, ScoreGames.NormalizeName(input));

    private static Task<HttpResponseMessage> Post(HttpClient http, string game, string player, int score) =>
        http.PostAsJsonAsync($"/api/scores/{game}", new SubmitScoreRequest(player, score));

    private static async Task<SubmitScoreResponse> Submit(HttpClient http, string game, string player, int score)
    {
        var response = await Post(http, game, player, score);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmitScoreResponse>())!;
    }
}

public class RoomManagerTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Sweep_ClosesRoomsOnceEveryoneHasBeenGoneLongEnough()
    {
        var clock = new ManualClock();
        var rooms = new RoomManager(new IGameRules[] { new Connect4Rules() }, clock);
        string code = rooms.Create("connect4", "A", "token-a", "conn-a").Room!.Code;
        rooms.Join(code, "B", "token-b", "conn-b");

        rooms.Disconnect("conn-a");
        clock.Now += TimeSpan.FromMinutes(30);
        Assert.Equal(0, rooms.Sweep()); // B is still connected

        rooms.Disconnect("conn-b");
        clock.Now += RoomManager.DisconnectGrace - TimeSpan.FromMinutes(1);
        Assert.Equal(0, rooms.Sweep()); // B may still come back
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.Equal(1, rooms.Sweep());
        Assert.Null(rooms.Get(code));
    }
}
