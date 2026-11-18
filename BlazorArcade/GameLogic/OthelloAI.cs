using System;
using System.Collections.Generic;
using System.Numerics;

namespace BlazorArcade.GameLogic
{
    public enum OthelloLevel { Easy, Normal, Hard }

    /// <summary>
    /// Alpha-beta search over board position (corners good, the squares next to them bad) and
    /// mobility, switching to counting discs once the game is decided. It searches on 64-bit
    /// boards, one bit per square, so a deep search stays quick under WebAssembly.
    /// </summary>
    public class OthelloAI
    {
        private static readonly int[] Weights =
        {
            100, -20, 10,  5,  5, 10, -20, 100,
            -20, -50, -2, -2, -2, -2, -50, -20,
             10,  -2,  1,  1,  1,  1,  -2,  10,
              5,  -2,  1,  0,  0,  1,  -2,   5,
              5,  -2,  1,  0,  0,  1,  -2,   5,
             10,  -2,  1,  1,  1,  1,  -2,  10,
            -20, -50, -2, -2, -2, -2, -50, -20,
            100, -20, 10,  5,  5, 10, -20, 100,
        };

        // Squares from best to worst, so the search tries promising moves first and prunes more.
        private static readonly int[] SearchOrder = BuildSearchOrder();

        private const ulong NotColumnA = 0xFEFEFEFEFEFEFEFEUL, NotColumnH = 0x7F7F7F7F7F7F7F7FUL;

        private readonly int _player;
        private readonly Random _random;

        public OthelloAI(int player = 2, Random? random = null)
        {
            _player = player;
            _random = random ?? new Random();
        }

        public OthelloLevel Level { get; set; } = OthelloLevel.Normal;

        /// <summary>How far ahead to look. Near the end, a hard opponent reads the game out to its last move.</summary>
        private int DepthFor(int emptySquares) => Level switch
        {
            OthelloLevel.Easy => 1,
            OthelloLevel.Normal => 3,
            _ => emptySquares <= 10 ? emptySquares : 6,
        };

        /// <summary>The move to play, or null when the AI has none (or it isn't its turn).</summary>
        public (int Row, int Col)? GetBestMove(OthelloLogic game)
        {
            if (game.IsGameOver || game.CurrentPlayer != _player) return null;
            var (mine, theirs) = ToBitboards(game.Board, _player);
            ulong moves = Moves(mine, theirs);
            if (moves == 0) return null;
            int depth = DepthFor(64 - BitOperations.PopCount(mine | theirs));

            var candidates = new List<int>();
            foreach (int square in SearchOrder) if ((moves & (1UL << square)) != 0) candidates.Add(square);

            // An easy opponent blunders now and then.
            if (Level == OthelloLevel.Easy && _random.NextDouble() < 0.35) return ToRowCol(candidates[_random.Next(candidates.Count)]);

            var best = new List<int>();
            int bestScore = int.MinValue;
            foreach (int square in candidates)
            {
                ulong flips = Flips(mine, theirs, square);
                ulong nextMine = mine | flips | (1UL << square), nextTheirs = theirs & ~flips;
                // Searching just below the best so far still scores ties exactly, so we can pick among them.
                int floor = bestScore == int.MinValue ? -int.MaxValue : bestScore - 1;
                int score = -Search(nextTheirs, nextMine, depth - 1, -int.MaxValue, -floor);
                if (score > bestScore)
                {
                    bestScore = score;
                    best.Clear();
                }
                if (score == bestScore) best.Add(square);
            }
            return ToRowCol(best[_random.Next(best.Count)]);
        }

        /// <summary>Negamax: the score of the position for the side to move (<paramref name="mine"/>).</summary>
        private static int Search(ulong mine, ulong theirs, int depth, int alpha, int beta)
        {
            ulong moves = Moves(mine, theirs);
            if (moves == 0)
            {
                if (Moves(theirs, mine) == 0) return (BitOperations.PopCount(mine) - BitOperations.PopCount(theirs)) * 1000;
                return -Search(theirs, mine, depth, -beta, -alpha); // pass
            }
            if (depth <= 0) return Evaluate(mine, theirs, moves);

            foreach (int square in SearchOrder)
            {
                if ((moves & (1UL << square)) == 0) continue;
                ulong flips = Flips(mine, theirs, square);
                int score = -Search(theirs & ~flips, mine | flips | (1UL << square), depth - 1, -beta, -alpha);
                if (score > alpha) alpha = score;
                if (alpha >= beta) break;
            }
            return alpha;
        }

        private static int Evaluate(ulong mine, ulong theirs, ulong myMoves)
        {
            int position = 0;
            for (ulong b = mine; b != 0; b &= b - 1) position += Weights[BitOperations.TrailingZeroCount(b)];
            for (ulong b = theirs; b != 0; b &= b - 1) position -= Weights[BitOperations.TrailingZeroCount(b)];
            int mobility = BitOperations.PopCount(myMoves) - BitOperations.PopCount(Moves(theirs, mine));
            return position + mobility * 5;
        }

        private static ulong Shift(ulong b, int direction) => direction switch
        {
            0 => (b << 1) & NotColumnA,  // east
            1 => (b >> 1) & NotColumnH,  // west
            2 => b << 8,                 // south
            3 => b >> 8,                 // north
            4 => (b << 9) & NotColumnA,  // south-east
            5 => (b << 7) & NotColumnH,  // south-west
            6 => (b >> 7) & NotColumnA,  // north-east
            _ => (b >> 9) & NotColumnH,  // north-west
        };

        /// <summary>Every empty square that traps a line of <paramref name="theirs"/>.</summary>
        private static ulong Moves(ulong mine, ulong theirs)
        {
            ulong empty = ~(mine | theirs), moves = 0;
            for (int d = 0; d < 8; d++)
            {
                ulong run = Shift(mine, d) & theirs;
                for (int i = 0; i < 5; i++) run |= Shift(run, d) & theirs;
                moves |= Shift(run, d) & empty;
            }
            return moves;
        }

        private static ulong Flips(ulong mine, ulong theirs, int square)
        {
            ulong flips = 0;
            for (int d = 0; d < 8; d++)
            {
                ulong run = 0, x = Shift(1UL << square, d);
                while ((x & theirs) != 0)
                {
                    run |= x;
                    x = Shift(x, d);
                }
                if ((x & mine) != 0) flips |= run;
            }
            return flips;
        }

        private static (ulong Mine, ulong Theirs) ToBitboards(int[,] board, int player)
        {
            ulong mine = 0, theirs = 0;
            for (int r = 0; r < OthelloLogic.Size; r++)
            {
                for (int c = 0; c < OthelloLogic.Size; c++)
                {
                    if (board[r, c] == player) mine |= 1UL << (r * 8 + c);
                    else if (board[r, c] != 0) theirs |= 1UL << (r * 8 + c);
                }
            }
            return (mine, theirs);
        }

        private static (int Row, int Col) ToRowCol(int square) => (square / 8, square % 8);

        private static int[] BuildSearchOrder()
        {
            var order = new int[64];
            for (int i = 0; i < 64; i++) order[i] = i;
            Array.Sort(order, (a, b) => Weights[b].CompareTo(Weights[a]));
            return order;
        }
    }
}
