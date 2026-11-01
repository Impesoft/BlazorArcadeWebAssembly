using BlazorArcade.GameLogic;
using BlazorArcade.Shared.Multiplayer;
using ChessDotNet;

namespace BlazorArcade.Server.Matches;

public sealed record GameOutcome(int? Winner, bool IsDraw);

/// <summary>
/// The authoritative rules of a two-player turn-based game. The hub never trusts a client's board;
/// every move goes through a session, and only accepted moves are broadcast.
/// To host another game on the match hub, implement this and register it in Program.cs.
/// </summary>
public interface IGameRules
{
    string Game { get; }
    IGameSession NewSession();
}

public interface IGameSession
{
    /// <summary>Seat (0 or 1) that moves next.</summary>
    int Turn { get; }

    /// <summary>Applies the move for the seat on turn; returns an error message when it is not legal.</summary>
    string? TryApply(string move);

    /// <summary>Set once the game has ended.</summary>
    GameOutcome? Outcome { get; }
}

public sealed class Connect4Rules : IGameRules
{
    public string Game => MatchGames.Connect4;
    public IGameSession NewSession() => new Session();

    private sealed class Session : IGameSession
    {
        private readonly Connect4Logic _board = new();

        public int Turn => _board.CurrentPlayer - 1;
        public GameOutcome? Outcome { get; private set; }

        public string? TryApply(string move)
        {
            if (!int.TryParse(move, out int column) || column < 0 || column >= Connect4Logic.Columns)
                return "Not a column.";
            if (!_board.DropPiece(column))
                return "That column is full.";
            if (_board.IsGameOver)
                Outcome = new GameOutcome(_board.IsDraw ? null : _board.Winner - 1, _board.IsDraw);
            return null;
        }
    }
}

/// <summary>Chess moves are coordinate pairs with an optional promotion piece: "e2e4", "e7e8q".</summary>
public sealed class ChessRules : IGameRules
{
    public string Game => MatchGames.Chess;
    public IGameSession NewSession() => new Session();

    private sealed class Session : IGameSession
    {
        private readonly ChessGame _game = new();

        public int Turn => _game.WhoseTurn == Player.White ? 0 : 1;
        public GameOutcome? Outcome { get; private set; }

        public string? TryApply(string move)
        {
            if (move.Length is not (4 or 5)) return "Moves look like e2e4 or e7e8q.";
            Move parsed;
            try
            {
                char? promotion = move.Length == 5 ? char.ToUpperInvariant(move[4]) : null;
                parsed = new Move(move[..2].ToLowerInvariant(), move[2..4].ToLowerInvariant(), _game.WhoseTurn, promotion);
            }
            catch (Exception)
            {
                return "Not a square on the board.";
            }
            if (!_game.IsValidMove(parsed)) return "Illegal move.";
            _game.MakeMove(parsed, true);

            var mover = parsed.Player;
            var opponent = mover == Player.White ? Player.Black : Player.White;
            if (_game.IsCheckmated(opponent))
                Outcome = new GameOutcome(mover == Player.White ? 0 : 1, false);
            else if (_game.IsStalemated(opponent) || _game.IsInsufficientMaterial() || _game.IsDraw())
                Outcome = new GameOutcome(null, true);
            return null;
        }
    }
}
