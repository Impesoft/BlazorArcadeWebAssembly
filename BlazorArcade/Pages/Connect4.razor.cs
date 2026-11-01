using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorArcade.Components;
using BlazorArcade.GameLogic;
using BlazorArcade.Shared.Multiplayer;
using System.Threading.Tasks;

namespace BlazorArcade.Pages
{
    public partial class Connect4
    {
        [Inject]
        public IJSRuntime JSRuntime { get; set; }

        public bool IsGameStarted { get; set; } = false;
        public Connect4Logic GameInstance { get; set; } = new Connect4Logic();
        private Connect4AI _ai = new Connect4AI(2);
        private ElementReference GameContainer;
        public bool IsAiMode { get; set; } = true;
        private bool _isAiThinking = false;

        // Online play: the server referees with the same Connect4Logic; we replay its move list.
        // Seat 0 is player 1 (moves first), seat 1 is player 2.
        private bool _online;
        private OnlineLobby? _lobby;
        private RoomState? _room;
        private int _mySeat = -1;

        protected override void OnInitialized()
        {
            _online = new System.Uri(_nav.Uri).Query.Contains("room=");
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await GameContainer.FocusAsync();
            }
        }

        public void RestartGame()
        {
            GameInstance.InitializeGame();
            _isAiThinking = false;
            GameContainer.FocusAsync();
        }

        // Cycles: vs AI → 2 players → online → vs AI.
        public void ToggleMode()
        {
            if (_online)
            {
                _online = false;
                IsAiMode = true;
                _room = null;
                _mySeat = -1;
            }
            else if (IsAiMode)
            {
                IsAiMode = false;
            }
            else
            {
                _online = true;
            }
            RestartGame();
        }

        private void OnRoomChanged((RoomState? Room, int Seat) update)
        {
            _room = update.Room;
            _mySeat = update.Seat;
            ReplayRoom();
        }

        private void ReplayRoom()
        {
            GameInstance.InitializeGame();
            foreach (var move in _room?.Moves ?? (System.Collections.Generic.IReadOnlyList<string>)System.Array.Empty<string>())
            {
                GameInstance.DropPiece(int.Parse(move));
            }
        }

        public async Task DropPiece(int col)
        {
            if (_isAiThinking || GameInstance.IsGameOver) return;
            if (_online && (_room?.Status != RoomStatus.Playing || GameInstance.CurrentPlayer != _mySeat + 1)) return;

            if (GameInstance.DropPiece(col))
            {
                _ = JSRuntime.InvokeVoidAsync("playWaka"); // Using the same sound for piece drop as an arcade feel

                if (_online)
                {
                    // Shown straight away; if the server disagrees, fall back to its version of the game.
                    if (await _lobby!.SendMoveAsync(col.ToString()) != null) ReplayRoom();
                    return;
                }

                if (IsAiMode && !GameInstance.IsGameOver && GameInstance.CurrentPlayer == 2)
                {
                    _isAiThinking = true;
                    StateHasChanged();

                    await Task.Delay(500); // Artificial delay to make AI feel more natural

                    int bestMove = _ai.GetBestMove(GameInstance.Board);
                    if (bestMove != -1 && GameInstance.DropPiece(bestMove))
                    {
                        _ = JSRuntime.InvokeVoidAsync("playWaka");
                    }

                    _isAiThinking = false;
                    StateHasChanged();
                }
            }
        }
    }
}
