using BlazorArcade.Shared.Multiplayer;
using BlazorArcade.Shared.Scores;
using Microsoft.AspNetCore.SignalR;

namespace BlazorArcade.Server.Matches;

/// <summary>
/// Real-time rooms for two-player turn-based games. Each room is a SignalR group named after
/// its code; every accepted change is broadcast to the group as a full <see cref="RoomState"/>.
/// </summary>
public sealed class MatchHub(RoomManager rooms) : Hub<IMatchClient>
{
    private const int MaxTokenLength = 64;
    private const int MaxMoveLength = 8;

    public async Task<JoinResult> CreateRoom(string game, string playerName, string playerToken)
    {
        if (!ValidToken(playerToken)) return JoinResult.Fail("Missing player token.");
        var result = rooms.Create(game, Name(playerName), playerToken, Context.ConnectionId);
        if (result.Ok) await Groups.AddToGroupAsync(Context.ConnectionId, result.Room!.Code);
        return result;
    }

    public async Task<JoinResult> JoinRoom(string code, string playerName, string playerToken)
    {
        if (!ValidToken(playerToken)) return JoinResult.Fail("Missing player token.");
        if (string.IsNullOrWhiteSpace(code)) return JoinResult.Fail("Enter a room code.");

        var result = rooms.Join(code, Name(playerName), playerToken, Context.ConnectionId);
        if (!result.Ok) return result;

        await Groups.AddToGroupAsync(Context.ConnectionId, result.Room!.Code);
        await Clients.OthersInGroup(result.Room.Code).RoomUpdated(result.Room);
        return result;
    }

    public async Task<string?> MakeMove(string code, string move)
    {
        if (string.IsNullOrEmpty(move) || move.Length > MaxMoveLength) return "Invalid move.";
        string? error = rooms.Move(code, Context.ConnectionId, move);
        if (error == null) await Broadcast(code);
        return error;
    }

    public async Task RequestRematch(string code)
    {
        var state = rooms.RequestRematch(code, Context.ConnectionId);
        if (state != null) await Clients.Group(state.Code).RoomUpdated(state);
    }

    public async Task LeaveRoom(string code)
    {
        var state = rooms.Leave(code, Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, code);
        if (state != null) await Clients.Group(state.Code).RoomUpdated(state);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var state = rooms.Disconnect(Context.ConnectionId);
        if (state != null) await Clients.Group(state.Code).RoomUpdated(state);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task Broadcast(string code)
    {
        if (rooms.Get(code) is { } state) await Clients.Group(state.Code).RoomUpdated(state);
    }

    private static string Name(string playerName) => ScoreGames.NormalizeName(playerName) ?? "Player";

    private static bool ValidToken(string token) => !string.IsNullOrWhiteSpace(token) && token.Length <= MaxTokenLength;
}

/// <summary>Sweeps abandoned rooms once a minute.</summary>
public sealed class RoomJanitor(RoomManager rooms, ILogger<RoomJanitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int removed = rooms.Sweep();
            if (removed > 0) logger.LogInformation("Closed {Count} abandoned rooms; {Open} still open", removed, rooms.RoomCount);
        }
    }
}
