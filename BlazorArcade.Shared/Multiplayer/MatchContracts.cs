namespace BlazorArcade.Shared.Multiplayer;

/// <summary>Turn-based games the match hub can host. Adding one means adding server-side rules for it.</summary>
public static class MatchGames
{
    public const string Chess = "chess";
    public const string Connect4 = "connect4";
    public const string Othello = "othello";
}

public static class MatchHubRoutes
{
    public const string Path = "/hubs/match";
}

/// <summary>Hub method names, so client calls can't drift from the server.</summary>
public static class MatchHubMethods
{
    /// <summary>(string game, string playerName, string playerToken) → <see cref="JoinResult"/></summary>
    public const string CreateRoom = "CreateRoom";

    /// <summary>(string code, string playerName, string playerToken) → <see cref="JoinResult"/>. Rejoining with the same token reclaims your seat.</summary>
    public const string JoinRoom = "JoinRoom";

    /// <summary>(string code, string move) → error message, or null when the move was accepted.</summary>
    public const string MakeMove = "MakeMove";

    /// <summary>(string code) → nothing. When both players ask, a new game starts with colours swapped.</summary>
    public const string RequestRematch = "RequestRematch";

    /// <summary>(string code) → nothing.</summary>
    public const string LeaveRoom = "LeaveRoom";

    /// <summary>Server → client: the room changed.</summary>
    public const string RoomUpdated = "RoomUpdated";
}

public enum RoomStatus { WaitingForOpponent, Playing, Finished }

public sealed record SeatState(int Seat, string Name, bool Connected);

/// <summary>
/// Everything a client needs to draw a match. Moves are game-specific strings
/// (chess: "e2e4", "e7e8q"; connect 4: the column "0".."6"); clients rebuild the board by replaying them.
/// Seat 0 moves first (White / red).
/// </summary>
public sealed record RoomState(
    string Code,
    string Game,
    RoomStatus Status,
    IReadOnlyList<SeatState> Seats,
    IReadOnlyList<string> Moves,
    int Turn,
    int? Winner,
    bool IsDraw,
    IReadOnlyList<int> RematchVotes,
    int GameNumber);

public sealed record JoinResult(bool Ok, string? Error, int Seat, RoomState? Room)
{
    public static JoinResult Fail(string error) => new(false, error, -1, null);
}

/// <summary>Server → client callbacks.</summary>
public interface IMatchClient
{
    Task RoomUpdated(RoomState room);
}
