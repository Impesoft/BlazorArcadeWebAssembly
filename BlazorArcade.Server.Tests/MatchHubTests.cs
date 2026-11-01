using BlazorArcade.Shared.Multiplayer;
using Microsoft.AspNetCore.SignalR.Client;

namespace BlazorArcade.Server.Tests;

public class MatchHubTests : IClassFixture<ArcadeServer>
{
    private readonly ArcadeServer _server;

    public MatchHubTests(ArcadeServer server) => _server = server;

    [Fact]
    public async Task Connect4_FullGame_IsRefereedByTheServer_AndRematchSwapsSeats()
    {
        await using var ada = await _server.ConnectAsync("Ada");
        await using var bob = await _server.ConnectAsync("Bob");

        var created = await ada.CreateAsync(MatchGames.Connect4);
        Assert.True(created.Ok);
        Assert.Equal(0, created.Seat);
        Assert.Equal(RoomStatus.WaitingForOpponent, created.Room!.Status);
        string code = created.Room.Code;
        Assert.Matches("^[A-Z2-9]{4}$", code);

        var joined = await bob.JoinAsync(code.ToLowerInvariant()); // codes are case-insensitive
        Assert.True(joined.Ok);
        Assert.Equal(1, joined.Seat);
        Assert.Equal(RoomStatus.Playing, joined.Room!.Status);
        await ada.WaitForAsync(s => s.Status == RoomStatus.Playing && s.Seats.Count == 2);

        Assert.Equal("It is not your turn.", await bob.MoveAsync(code, "3"));
        Assert.Equal("Not a column.", await ada.MoveAsync(code, "9"));

        // Ada stacks column 0, Bob column 1: Ada connects four vertically.
        string[] moves = { "0", "1", "0", "1", "0", "1", "0" };
        for (int i = 0; i < moves.Length; i++)
        {
            var mover = i % 2 == 0 ? ada : bob;
            Assert.Null(await mover.MoveAsync(code, moves[i]));
        }

        var finished = await bob.WaitForAsync(s => s.Status == RoomStatus.Finished);
        Assert.Equal(0, finished.Winner);
        Assert.Equal(moves, finished.Moves);
        Assert.Equal("The game is over.", await bob.MoveAsync(code, "2"));

        await ada.Connection.InvokeAsync(MatchHubMethods.RequestRematch, code);
        await bob.Connection.InvokeAsync(MatchHubMethods.RequestRematch, code);
        var rematch = await ada.WaitForAsync(s => s.GameNumber == 2);
        Assert.Equal(RoomStatus.Playing, rematch.Status);
        Assert.Empty(rematch.Moves);
        Assert.Equal("Bob", rematch.Seats.Single(s => s.Seat == 0).Name); // Bob moves first now
        Assert.Null(await bob.MoveAsync(code, "3"));
    }

    [Fact]
    public async Task Chess_RejectsIllegalMoves_AndDetectsCheckmate()
    {
        await using var white = await _server.ConnectAsync("White");
        await using var black = await _server.ConnectAsync("Black");
        string code = (await white.CreateAsync(MatchGames.Chess)).Room!.Code;
        await black.JoinAsync(code);

        Assert.Equal("Illegal move.", await white.MoveAsync(code, "e2e5"));
        Assert.Equal("Moves look like e2e4 or e7e8q.", await white.MoveAsync(code, "e4"));

        // Fool's mate.
        Assert.Null(await white.MoveAsync(code, "f2f3"));
        Assert.Null(await black.MoveAsync(code, "e7e5"));
        Assert.Null(await white.MoveAsync(code, "g2g4"));
        Assert.Null(await black.MoveAsync(code, "d8h4"));

        var state = await white.WaitForAsync(s => s.Status == RoomStatus.Finished);
        Assert.Equal(1, state.Winner);
        Assert.False(state.IsDraw);
    }

    [Fact]
    public async Task ADroppedPlayer_CanReclaimTheirSeat_WithTheSameToken()
    {
        await using var host = await _server.ConnectAsync("Host");
        var guest = await _server.ConnectAsync("Guest");
        string code = (await host.CreateAsync(MatchGames.Connect4)).Room!.Code;
        await guest.JoinAsync(code);
        Assert.Null(await host.MoveAsync(code, "3"));

        string token = guest.Token;
        await guest.DisposeAsync(); // connection drops
        var away = await host.WaitForAsync(s => s.Seats.Any(seat => seat.Seat == 1 && !seat.Connected));
        Assert.Equal(RoomStatus.Playing, away.Status);

        await using var stranger = await _server.ConnectAsync("Stranger");
        Assert.Equal("That room is full.", (await stranger.JoinAsync(code)).Error);

        await using var back = await _server.ConnectAsync("Guest");
        var rejoined = await back.Connection.InvokeAsync<JoinResult>(MatchHubMethods.JoinRoom, code, "Guest", token);
        Assert.True(rejoined.Ok);
        Assert.Equal(1, rejoined.Seat);
        Assert.Equal(new[] { "3" }, rejoined.Room!.Moves); // picks up where the game was
        Assert.Null(await back.MoveAsync(code, "4"));
    }

    [Fact]
    public async Task LeavingMidGame_Forfeits()
    {
        await using var a = await _server.ConnectAsync("A");
        await using var b = await _server.ConnectAsync("B");
        string code = (await a.CreateAsync(MatchGames.Chess)).Room!.Code;
        await b.JoinAsync(code);

        await b.Connection.InvokeAsync(MatchHubMethods.LeaveRoom, code);
        var state = await a.WaitForAsync(s => s.Status == RoomStatus.Finished);
        Assert.Equal(0, state.Winner);
    }

    [Fact]
    public async Task UnknownRoomsAndGames_AreReportedNotThrown()
    {
        await using var p = await _server.ConnectAsync("P");
        Assert.Equal("No room with that code.", (await p.JoinAsync("ZZZZ")).Error);
        Assert.False((await p.CreateAsync("tic-tac-toe")).Ok);
    }
}
