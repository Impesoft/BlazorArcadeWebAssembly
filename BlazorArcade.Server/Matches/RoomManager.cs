using System.Collections.Concurrent;
using System.Security.Cryptography;
using BlazorArcade.Shared.Multiplayer;

namespace BlazorArcade.Server.Matches;

/// <summary>
/// In-memory match rooms. Rooms are short-lived and need no database: a server restart simply
/// ends the games in progress. All state changes for a room happen under that room's lock.
/// </summary>
public sealed class RoomManager
{
    // No 0/O or 1/I/L, so codes survive being read out loud.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 4;
    public static readonly TimeSpan DisconnectGrace = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(2);

    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _roomByConnection = new();
    private readonly Dictionary<string, IGameRules> _rules;
    private readonly TimeProvider _time;

    public RoomManager(IEnumerable<IGameRules> rules, TimeProvider time)
    {
        _rules = rules.ToDictionary(r => r.Game, StringComparer.OrdinalIgnoreCase);
        _time = time;
    }

    public int RoomCount => _rooms.Count;

    public JoinResult Create(string game, string name, string token, string connectionId)
    {
        if (!_rules.TryGetValue(game, out var rules)) return JoinResult.Fail($"Unknown game '{game}'.");

        LeavePreviousRoom(connectionId, null);
        Room room;
        do
        {
            room = new Room(NewCode(), rules, _time.GetUtcNow());
        } while (!_rooms.TryAdd(room.Code, room));

        lock (room.Lock)
        {
            room.Seats[0] = new Seat(name, token, connectionId);
            _roomByConnection[connectionId] = room.Code;
            return new JoinResult(true, null, 0, room.ToState());
        }
    }

    public JoinResult Join(string code, string name, string token, string connectionId)
    {
        if (!_rooms.TryGetValue(code.Trim(), out var room)) return JoinResult.Fail("No room with that code.");
        LeavePreviousRoom(connectionId, room.Code);

        lock (room.Lock)
        {
            int seat = Array.FindIndex(room.Seats, s => s?.Token == token);
            if (seat < 0) seat = Array.FindIndex(room.Seats, s => s == null);
            if (seat < 0) return JoinResult.Fail("That room is full.");

            room.Seats[seat] = new Seat(name, token, connectionId);
            if (room.Status == RoomStatus.WaitingForOpponent && room.Seats.All(s => s != null)) room.Status = RoomStatus.Playing;
            room.Touch(_time.GetUtcNow());
            _roomByConnection[connectionId] = room.Code;
            return new JoinResult(true, null, seat, room.ToState());
        }
    }

    /// <returns>An error message, or null when the move was accepted.</returns>
    public string? Move(string code, string connectionId, string move)
    {
        if (!_rooms.TryGetValue(code, out var room)) return "No room with that code.";

        lock (room.Lock)
        {
            int seat = room.SeatOf(connectionId);
            if (seat < 0) return "You are not in this room.";
            if (room.Status != RoomStatus.Playing) return room.Status == RoomStatus.Finished ? "The game is over." : "Waiting for an opponent.";
            if (room.Session.Turn != seat) return "It is not your turn.";

            string? error = room.Session.TryApply(move);
            if (error != null) return error;

            room.Moves.Add(move);
            if (room.Session.Outcome is { } outcome)
            {
                room.Status = RoomStatus.Finished;
                room.Winner = outcome.Winner;
                room.IsDraw = outcome.IsDraw;
            }
            room.Touch(_time.GetUtcNow());
            return null;
        }
    }

    public RoomState? RequestRematch(string code, string connectionId)
    {
        if (!_rooms.TryGetValue(code, out var room)) return null;

        lock (room.Lock)
        {
            int seat = room.SeatOf(connectionId);
            if (seat < 0 || room.Status != RoomStatus.Finished) return null;
            room.RematchVotes.Add(seat);
            if (room.RematchVotes.Count == 2 && room.Seats.All(s => s != null)) room.StartRematch();
            room.Touch(_time.GetUtcNow());
            return room.ToState();
        }
    }

    /// <summary>Leaving mid-game forfeits it. Returns the updated room, or null if the room closed.</summary>
    public RoomState? Leave(string code, string connectionId)
    {
        _roomByConnection.TryRemove(connectionId, out _);
        if (!_rooms.TryGetValue(code, out var room)) return null;

        lock (room.Lock)
        {
            int seat = room.SeatOf(connectionId);
            if (seat < 0) return room.ToState();
            room.Seats[seat] = null;
            room.RematchVotes.Clear();

            if (room.Status == RoomStatus.Playing)
            {
                room.Status = RoomStatus.Finished;
                room.Winner = 1 - seat;
            }
            if (room.Seats.All(s => s == null))
            {
                _rooms.TryRemove(room.Code, out _);
                return null;
            }
            room.Touch(_time.GetUtcNow());
            return room.ToState();
        }
    }

    /// <summary>Marks the connection's seat as away; it can be reclaimed with the same player token.</summary>
    public RoomState? Disconnect(string connectionId)
    {
        if (!_roomByConnection.TryRemove(connectionId, out var code) || !_rooms.TryGetValue(code, out var room)) return null;

        lock (room.Lock)
        {
            int seat = room.SeatOf(connectionId);
            if (seat < 0) return null;
            room.Seats[seat] = room.Seats[seat]! with { ConnectionId = null, DisconnectedAt = _time.GetUtcNow() };
            return room.ToState();
        }
    }

    /// <summary>Drops rooms nobody has touched for a while, or whose players all left long ago.</summary>
    public int Sweep()
    {
        var now = _time.GetUtcNow();
        int removed = 0;
        foreach (var room in _rooms.Values)
        {
            bool stale;
            lock (room.Lock)
            {
                bool everyoneGone = room.Seats.All(s => s == null || (s.ConnectionId == null && now - s.DisconnectedAt > DisconnectGrace));
                stale = everyoneGone || now - room.LastActivity > IdleLimit;
            }
            if (stale && _rooms.TryRemove(room.Code, out _)) removed++;
        }
        return removed;
    }

    public RoomState? Get(string code) =>
        _rooms.TryGetValue(code, out var room) ? Locked(room, r => r.ToState()) : null;

    private static T Locked<T>(Room room, Func<Room, T> read)
    {
        lock (room.Lock) return read(room);
    }

    /// <summary>One room per connection: joining another room leaves the old one. Called outside any room lock.</summary>
    private void LeavePreviousRoom(string connectionId, string? nextCode)
    {
        if (_roomByConnection.TryGetValue(connectionId, out var previous) && previous != nextCode)
            Leave(previous, connectionId);
    }

    private static string NewCode() =>
        string.Create(CodeLength, 0, (span, _) =>
        {
            for (int i = 0; i < span.Length; i++) span[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        });

    private sealed record Seat(string Name, string Token, string? ConnectionId, DateTimeOffset? DisconnectedAt = null);

    private sealed class Room
    {
        public readonly object Lock = new();
        public string Code { get; }
        public IGameRules Rules { get; }
        public IGameSession Session { get; private set; }
        public Seat?[] Seats { get; private set; } = new Seat?[2];
        public List<string> Moves { get; } = new();
        public HashSet<int> RematchVotes { get; } = new();
        public RoomStatus Status { get; set; } = RoomStatus.WaitingForOpponent;
        public int? Winner { get; set; }
        public bool IsDraw { get; set; }
        public int GameNumber { get; private set; } = 1;
        public DateTimeOffset LastActivity { get; private set; }

        public Room(string code, IGameRules rules, DateTimeOffset now)
        {
            Code = code;
            Rules = rules;
            Session = rules.NewSession();
            LastActivity = now;
        }

        public void Touch(DateTimeOffset now) => LastActivity = now;

        public int SeatOf(string connectionId) => Array.FindIndex(Seats, s => s?.ConnectionId == connectionId);

        /// <summary>New game, players swap seats so the other one moves first.</summary>
        public void StartRematch()
        {
            Seats = new[] { Seats[1], Seats[0] };
            Session = Rules.NewSession();
            Moves.Clear();
            RematchVotes.Clear();
            Winner = null;
            IsDraw = false;
            Status = RoomStatus.Playing;
            GameNumber++;
        }

        public RoomState ToState() => new(
            Code,
            Rules.Game,
            Status,
            Seats.Select((s, i) => s == null ? null : new SeatState(i, s.Name, s.ConnectionId != null)).Where(s => s != null).ToList()!,
            Moves.ToList(),
            Session.Turn,
            Winner,
            IsDraw,
            RematchVotes.ToList(),
            GameNumber);
    }
}
