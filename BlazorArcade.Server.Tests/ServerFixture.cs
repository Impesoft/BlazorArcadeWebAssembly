using System.Threading.Channels;
using BlazorArcade.Shared.Multiplayer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorArcade.Server.Tests;

/// <summary>The real server in memory, with its own throwaway SQLite file.</summary>
public sealed class ArcadeServer : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"arcade-scores-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Scores", $"Data Source={_dbPath};Pooling=False");
        builder.UseEnvironment("Development");
    }

    public async Task<TestPlayer> ConnectAsync(string name)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, MatchHubRoutes.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            })
            .Build();
        var player = new TestPlayer(connection, name);
        await connection.StartAsync();
        return player;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}

/// <summary>A connected hub client that records every room update it receives.</summary>
public sealed class TestPlayer : IAsyncDisposable
{
    private readonly Channel<RoomState> _updates = Channel.CreateUnbounded<RoomState>();

    public HubConnection Connection { get; }
    public string Name { get; }
    public string Token { get; } = Guid.NewGuid().ToString("N");

    public TestPlayer(HubConnection connection, string name)
    {
        Connection = connection;
        Name = name;
        connection.On<RoomState>(MatchHubMethods.RoomUpdated, state => _updates.Writer.TryWrite(state));
    }

    public Task<JoinResult> CreateAsync(string game) =>
        Connection.InvokeAsync<JoinResult>(MatchHubMethods.CreateRoom, game, Name, Token);

    public Task<JoinResult> JoinAsync(string code) =>
        Connection.InvokeAsync<JoinResult>(MatchHubMethods.JoinRoom, code, Name, Token);

    public Task<string?> MoveAsync(string code, string move) =>
        Connection.InvokeAsync<string?>(MatchHubMethods.MakeMove, code, move);

    /// <summary>Waits for the next broadcast that satisfies the condition.</summary>
    public async Task<RoomState> WaitForAsync(Func<RoomState, bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var state = await _updates.Reader.ReadAsync(timeout.Token);
            if (condition(state)) return state;
        }
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
