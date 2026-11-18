using System;
using System.Collections.Generic;

namespace BlazorArcade.GameLogic
{
    /// <summary>
    /// Othello on an 8x8 board. Black (player 1) moves first. A player with no legal move passes
    /// automatically; the game ends when neither player can move. Moves are written as squares,
    /// column letter then row number: "d3".
    /// </summary>
    public class OthelloLogic
    {
        public const int Size = 8;

        private static readonly (int Row, int Col)[] Directions =
        {
            (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1),
        };

        // 0 = empty, 1 = black, 2 = white. Row 0 is the top (row "1").
        public int[,] Board { get; private set; } = new int[Size, Size];
        public int CurrentPlayer { get; private set; } = 1;
        public bool IsGameOver { get; private set; }

        /// <summary>1 or 2 once the game is over, or 0 for a draw.</summary>
        public int Winner { get; private set; }

        /// <summary>The player who had to pass on the last turn, or 0.</summary>
        public int PassedPlayer { get; private set; }

        public (int Row, int Col)? LastMove { get; private set; }

        /// <summary>The discs the last move turned over.</summary>
        public IReadOnlyList<(int Row, int Col)> LastFlipped { get; private set; } = Array.Empty<(int, int)>();

        public OthelloLogic()
        {
            InitializeGame();
        }

        public void InitializeGame()
        {
            Board = new int[Size, Size];
            Board[3, 3] = Board[4, 4] = 2;
            Board[3, 4] = Board[4, 3] = 1;
            CurrentPlayer = 1;
            IsGameOver = false;
            Winner = 0;
            PassedPlayer = 0;
            LastMove = null;
            LastFlipped = Array.Empty<(int, int)>();
        }

        public OthelloLogic Clone()
        {
            var copy = (OthelloLogic)MemberwiseClone();
            copy.Board = (int[,])Board.Clone();
            return copy;
        }

        public int Count(int player)
        {
            int count = 0;
            foreach (int cell in Board) if (cell == player) count++;
            return count;
        }

        public bool IsLegal(int row, int col) => !IsGameOver && Flips(Board, row, col, CurrentPlayer).Count > 0;

        public List<(int Row, int Col)> LegalMoves() => LegalMoves(Board, CurrentPlayer);

        public static List<(int Row, int Col)> LegalMoves(int[,] board, int player)
        {
            var moves = new List<(int, int)>();
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (Flips(board, r, c, player).Count > 0) moves.Add((r, c));
            return moves;
        }

        /// <summary>Places a disc for the player on turn; false when the square isn't a legal move.</summary>
        public bool Play(int row, int col)
        {
            if (IsGameOver) return false;
            var flips = Flips(Board, row, col, CurrentPlayer);
            if (flips.Count == 0) return false;

            Board[row, col] = CurrentPlayer;
            foreach (var (r, c) in flips) Board[r, c] = CurrentPlayer;
            LastMove = (row, col);
            LastFlipped = flips;

            int opponent = 3 - CurrentPlayer;
            PassedPlayer = 0;
            if (LegalMoves(Board, opponent).Count > 0)
            {
                CurrentPlayer = opponent;
            }
            else if (LegalMoves(Board, CurrentPlayer).Count > 0)
            {
                PassedPlayer = opponent;
            }
            else
            {
                IsGameOver = true;
                int black = Count(1), white = Count(2);
                Winner = black > white ? 1 : white > black ? 2 : 0;
            }
            return true;
        }

        public bool Play(string move) => TryParse(move, out int row, out int col) && Play(row, col);

        public static string ToMove(int row, int col) => $"{(char)('a' + col)}{row + 1}";

        public static bool TryParse(string? move, out int row, out int col)
        {
            row = col = -1;
            if (move is not { Length: 2 }) return false;
            col = char.ToLowerInvariant(move[0]) - 'a';
            row = move[1] - '1';
            return row is >= 0 and < Size && col is >= 0 and < Size;
        }

        /// <summary>The opposing discs a disc at (row, col) would turn over; empty when the move is illegal.</summary>
        public static List<(int Row, int Col)> Flips(int[,] board, int row, int col, int player)
        {
            var flips = new List<(int, int)>();
            if (row < 0 || row >= Size || col < 0 || col >= Size || board[row, col] != 0) return flips;

            int opponent = 3 - player;
            foreach (var (dr, dc) in Directions)
            {
                int r = row + dr, c = col + dc, run = 0;
                while (r >= 0 && r < Size && c >= 0 && c < Size && board[r, c] == opponent)
                {
                    r += dr;
                    c += dc;
                    run++;
                }
                if (run == 0 || r < 0 || r >= Size || c < 0 || c >= Size || board[r, c] != player) continue;
                for (int i = 1; i <= run; i++) flips.Add((row + dr * i, col + dc * i));
            }
            return flips;
        }
    }
}
