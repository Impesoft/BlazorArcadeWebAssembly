using BlazorArcade.Components;
using BlazorArcade.GameLogic;
using BlazorArcade.Shared.Multiplayer;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorArcade.Pages
{
    /// <summary>
    /// Othello against the computer, a friend on the same screen, or online. Online, the server
    /// referees with the same <see cref="OthelloLogic"/> and the board is rebuilt from its move list.
    /// </summary>
    public partial class Othello
    {
        [Inject] private NavigationManager _nav { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        private OthelloLogic _game = new();
        private readonly OthelloAI _ai = new(2);
        private readonly OthelloAI _aiForBlack = new(1);
        private bool _vsAi = true, _hints = true, _aiThinking;
        private int _humanPlayer = 1;
        private int _moveNumber;
        private HashSet<(int, int)> _legal = new(), _flipped = new();

        // Online play. Seat 0 plays black and moves first.
        private bool _online;
        private OnlineLobby? _lobby;
        private RoomState? _room;
        private int _mySeat = -1;

        private string ModeName => _online ? "Online" : _vsAi ? "1 Player (vs AI)" : "2 Players";

        private int AiPlayer => 3 - _humanPlayer;

        /// <summary>Whether a click on the board is this viewer's move.</summary>
        private bool CanClick =>
            !_game.IsGameOver && !_aiThinking &&
            (_online ? _room?.Status == RoomStatus.Playing && _game.CurrentPlayer == _mySeat + 1
                     : !_vsAi || _game.CurrentPlayer == _humanPlayer);

        private bool IsTurn(int player) => !_game.IsGameOver && _game.CurrentPlayer == player;

        private string PlayerName(int player)
        {
            if (_online)
            {
                var seat = _room?.Seats.FirstOrDefault(s => s.Seat == player - 1);
                return seat?.Name ?? "Waiting…";
            }
            if (_vsAi) return player == _humanPlayer ? "You" : $"Computer ({_ai.Level})";
            return player == 1 ? "Black" : "White";
        }

        private string Status
        {
            get
            {
                if (_game.IsGameOver) return "Game over";
                if (_online && _room?.Status != RoomStatus.Playing) return "Create or join a room to play.";
                string passed = _game.PassedPlayer != 0 ? $"{Colour(_game.PassedPlayer)} has no move and passes. " : "";
                string turn = _aiThinking ? "The computer is thinking…"
                    : CanClick && (_vsAi || _online) ? "Your move."
                    : _online ? $"Waiting for {PlayerName(_game.CurrentPlayer)}…"
                    : $"{Colour(_game.CurrentPlayer)} to move.";
                return passed + turn;
            }
        }

        private string Result
        {
            get
            {
                if (_game.Winner == 0) return "It's a draw!";
                if (_online) return _game.Winner == _mySeat + 1 ? "You win!" : $"{PlayerName(_game.Winner)} wins";
                if (_vsAi) return _game.Winner == _humanPlayer ? "You win!" : "The computer wins";
                return $"{Colour(_game.Winner)} wins!";
            }
        }

        private static string Colour(int player) => player == 1 ? "Black" : "White";

        // Discs that just turned get a fresh key so their flip animation plays again.
        private string DiscKey(int row, int col) => _flipped.Contains((row, col)) ? $"{row}{col}-{_moveNumber}" : $"{row}{col}";

        protected override void OnInitialized()
        {
            _online = new Uri(_nav.Uri).Query.Contains("room=");
            Refresh();
        }

        private void Refresh()
        {
            _legal = _game.IsGameOver ? new() : _game.LegalMoves().ToHashSet();
            _flipped = _game.LastFlipped.ToHashSet();
        }

        private async Task NewGame()
        {
            _game = new OthelloLogic();
            _moveNumber = 0;
            _aiThinking = false;
            Refresh();
            await AiMoveAsync();
        }

        private async Task ToggleMode()
        {
            // vs AI → 2 players → online → vs AI.
            if (_online)
            {
                _online = false;
                _vsAi = true;
                _room = null;
                _mySeat = -1;
            }
            else if (_vsAi)
            {
                _vsAi = false;
            }
            else
            {
                _online = true;
            }
            await NewGame();
        }

        private void SetLevel(OthelloLevel level) => _ai.Level = _aiForBlack.Level = level;

        private async Task SetHumanPlayer(int player)
        {
            if (_humanPlayer == player) return;
            _humanPlayer = player;
            await NewGame();
        }

        private void OnRoomChanged((RoomState? Room, int Seat) update)
        {
            _room = update.Room;
            _mySeat = update.Seat;
            ReplayRoom();
        }

        private void ReplayRoom()
        {
            _game = new OthelloLogic();
            foreach (var move in _room?.Moves ?? (IReadOnlyList<string>)Array.Empty<string>()) _game.Play(move);
            _moveNumber = _room?.Moves.Count ?? 0;
            Refresh();
        }

        private async Task PlayAsync(int row, int col)
        {
            if (!CanClick || !Place(row, col)) return;

            if (_online)
            {
                // Shown straight away; if the server disagrees, fall back to its version of the game.
                if (await _lobby!.SendMoveAsync(OthelloLogic.ToMove(row, col)) != null) ReplayRoom();
                return;
            }
            await AiMoveAsync();
        }

        private bool Place(int row, int col)
        {
            if (!_game.Play(row, col)) return false;
            _moveNumber++;
            Refresh();
            _ = PlaySound();
            return true;
        }

        /// <summary>Lets the computer move, as often as it may: the other side can pass.</summary>
        private async Task AiMoveAsync()
        {
            if (!_vsAi || _online) return;
            var ai = AiPlayer == 2 ? _ai : _aiForBlack;
            while (!_game.IsGameOver && _game.CurrentPlayer == AiPlayer)
            {
                _aiThinking = true;
                StateHasChanged();
                await Task.Delay(450);
                var move = ai.GetBestMove(_game);
                _aiThinking = false;
                if (move is not { } m || !Place(m.Row, m.Col)) break;
                StateHasChanged();
            }
            _aiThinking = false;
        }

        private async Task PlaySound()
        {
            try
            {
                await JS.InvokeVoidAsync("playWaka");
            }
            catch (Exception)
            {
                // No sound is no reason to stop the game.
            }
        }
    }
}
