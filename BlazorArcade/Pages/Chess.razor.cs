using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChessDotNet;
using BlazorArcade.GameLogic;
using BlazorArcade.Components;
using BlazorArcade.Shared.Multiplayer;

namespace BlazorArcade.Pages
{
    public partial class Chess
    {
        public ChessGame Game { get; set; } = new ChessGame();
        public Position? SelectedPosition { get; set; }
        public List<Move> ValidMoves { get; set; } = new List<Move>();
        
        public bool PlayAgainstComputer { get; set; } = true;
        private ChessAI _ai = new ChessAI(Player.Black, 3); // AI plays Black, Depth 3
        private bool _isAiThinking = false;

        // Online play: the server referees, we replay its move list. Seat 0 is White.
        private bool _online;
        private OnlineLobby? _lobby;
        private RoomState? _room;
        private int _mySeat = -1;
        private Player MyColor => _mySeat == 1 ? Player.Black : Player.White;
        private bool Flipped => _online && _mySeat == 1;

        protected override void OnInitialized()
        {
            _online = new Uri(_nav.Uri).Query.Contains("room=");
            if (_online) PlayAgainstComputer = false;
            NewGame();
        }

        private void NewGame()
        {
            Game = new ChessGame();
            SelectedPosition = null;
            ValidMoves?.Clear();
            _isAiThinking = false;
        }

        private void SetMode(bool online, bool vsComputer)
        {
            _online = online;
            PlayAgainstComputer = !online && vsComputer;
            _room = null;
            _mySeat = -1;
            NewGame();
        }

        private void OnRoomChanged((RoomState? Room, int Seat) update)
        {
            _room = update.Room;
            _mySeat = update.Seat;
            ReplayRoom();
        }

        private void ReplayRoom()
        {
            Game = new ChessGame();
            foreach (var move in _room?.Moves ?? (IReadOnlyList<string>)Array.Empty<string>())
            {
                char? promotion = move.Length == 5 ? char.ToUpperInvariant(move[4]) : null;
                Game.MakeMove(new Move(move[..2], move[2..4], Game.WhoseTurn, promotion), true);
            }
            SelectedPosition = null;
            ValidMoves.Clear();
        }

        private static string Encode(Move move) =>
            (move.OriginalPosition.ToString() + move.NewPosition + move.Promotion).ToLowerInvariant();

        private Position GetPosition(int row, int col)
        {
            // ChessDotNet uses Rank 1-8 (bottom to top), File A-H (left to right)
            // UI row 0 is Rank 8. UI col 0 is File A. Playing Black online turns the board around.
            return Flipped ? new Position((ChessDotNet.File)(7 - col), row + 1) : new Position((ChessDotNet.File)col, 8 - row);
        }

        private string GetPieceImage(Piece piece)
        {
            if (piece == null) return "";
            
            string color = piece.Owner == Player.White ? "w" : "b";
            string type = "";

            if (piece is ChessDotNet.Pieces.Pawn) type = "p";
            else if (piece is ChessDotNet.Pieces.Knight) type = "n";
            else if (piece is ChessDotNet.Pieces.Bishop) type = "b";
            else if (piece is ChessDotNet.Pieces.Rook) type = "r";
            else if (piece is ChessDotNet.Pieces.Queen) type = "q";
            else if (piece is ChessDotNet.Pieces.King) type = "k";

            return $"./images/chess/{color}{type}.svg";
        }

        private async Task OnSquareClick(Position pos)
        {
            if (_isAiThinking) return;
            if (_online && (_room?.Status != RoomStatus.Playing || Game.WhoseTurn != MyColor)) return;

            if (Game.IsCheckmated(Player.White) || Game.IsCheckmated(Player.Black) || Game.IsStalemated(Player.White) || Game.IsStalemated(Player.Black))
                return; // Game over

            // If we have a selected piece and we clicked a valid move destination
            var move = ValidMoves.FirstOrDefault(m => m.NewPosition.Equals(pos));
            if (move != null)
            {
                // A pawn reaching the last rank needs a promotion piece: always take a queen.
                if (move.Promotion == null && Game.GetPieceAt(move.OriginalPosition) is ChessDotNet.Pieces.Pawn && move.NewPosition.Rank is 1 or 8)
                    move = new Move(move.OriginalPosition, move.NewPosition, move.Player, 'Q');

                Game.MakeMove(move, true);
                SelectedPosition = null;
                ValidMoves?.Clear();

                if (_online)
                {
                    // Shown straight away; if the server disagrees, fall back to its version of the game.
                    if (await _lobby!.SendMoveAsync(Encode(move)) != null) ReplayRoom();
                    return;
                }

                if (PlayAgainstComputer && Game.WhoseTurn == Player.Black && !Game.IsCheckmated(Player.Black) && !Game.IsStalemated(Player.Black))
                {
                    _isAiThinking = true;
                    StateHasChanged();
                    
                    // Allow UI to update before AI calculation blocks thread
                    await Task.Delay(50);
                    
                    var aiMove = _ai.GetBestMove(Game);
                    if (aiMove != null)
                    {
                        Game.MakeMove(aiMove, true);
                    }
                    
                    _isAiThinking = false;
                }
            }
            else
            {
                // Select piece
                var piece = Game.GetPieceAt(pos);
                if (piece != null && piece.Owner == Game.WhoseTurn)
                {
                    SelectedPosition = pos;
                    ValidMoves = Game.GetValidMoves(pos).ToList();
                }
                else
                {
                    SelectedPosition = null;
                    ValidMoves?.Clear();
                }
            }
        }
    }
}

