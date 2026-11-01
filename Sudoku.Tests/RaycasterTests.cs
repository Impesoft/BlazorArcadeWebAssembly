using BlazorArcade.GameLogic;

namespace Sudoku.Tests;

public class RaycasterTests
{
    private const double Dt = 1 / 60.0;

    private static RaycasterGame NewGame()
    {
        var game = new RaycasterGame();
        game.StartNewGame();
        return game;
    }

    private static void Run(RaycasterGame game, double seconds, RaycasterInput input = RaycasterInput.None)
    {
        for (double t = 0; t < seconds; t += Dt) game.Update(Dt, input, 0);
    }

    private static void Tap(RaycasterGame game, RaycasterInput input)
    {
        game.Update(Dt, input, 0);
        game.Update(Dt, RaycasterInput.None, 0);
    }

    [Fact]
    public void AllLevels_AreEnclosedPlayableAndDoorsHaveJambs()
    {
        foreach (var level in RaycasterLevels.All)
        {
            var rows = level.Rows;
            int w = rows[0].Length, h = rows.Length;
            Assert.All(rows, r => Assert.Equal(w, r.Length));

            for (int x = 0; x < w; x++)
            {
                Assert.True(RaycasterGame.IsWallChar(rows[0][x]), $"{level.Name}: top edge open at {x}");
                Assert.True(RaycasterGame.IsWallChar(rows[h - 1][x]), $"{level.Name}: bottom edge open at {x}");
            }
            for (int y = 0; y < h; y++)
            {
                Assert.True(RaycasterGame.IsWallChar(rows[y][0]), $"{level.Name}: left edge open at {y}");
                Assert.True(RaycasterGame.IsWallChar(rows[y][w - 1]), $"{level.Name}: right edge open at {y}");
            }

            bool Wall(int x, int y) => RaycasterGame.IsWallChar(rows[y][x]);
            (int X, int Y)? start = null;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (rows[y][x] == 'P') start = (x, y);
                    if (rows[y][x] != 'D') continue;
                    bool alongY = Wall(x, y - 1) && Wall(x, y + 1) && !Wall(x - 1, y) && !Wall(x + 1, y);
                    bool alongX = Wall(x - 1, y) && Wall(x + 1, y) && !Wall(x, y - 1) && !Wall(x, y + 1);
                    Assert.True(alongX || alongY, $"{level.Name}: door at {x},{y} needs jambs on exactly one axis");
                }
            }
            Assert.NotNull(start);

            // The exit switch must be reachable from the start.
            var seen = new bool[w, h];
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start.Value);
            seen[start.Value.X, start.Value.Y] = true;
            bool exitReachable = false;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (rows[ny][nx] == 'E') exitReachable = true;
                    if (Wall(nx, ny) || seen[nx, ny]) continue;
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            Assert.True(exitReachable, $"{level.Name}: exit is not reachable");
        }
    }

    [Fact]
    public void Use_OpensDoor_AndPlayerCanWalkThrough()
    {
        var game = NewGame();
        game.PosX = 6.5;
        game.PosY = 3.5;
        game.Angle = 0; // facing the door at (7,3)

        Run(game, 0.5, RaycasterInput.Forward);
        Assert.True(game.PosX < 7, "a closed door must block the player");

        Tap(game, RaycasterInput.Use);
        Run(game, 1);
        Assert.True(game.DoorAt(7, 3)!.IsPassable);

        Run(game, 0.6, RaycasterInput.Forward);
        Assert.True(game.PosX > 8, $"player should be through the door, is at {game.PosX:F2}");
    }

    [Fact]
    public void OpenDoor_ClosesAgainAfterThePlayerLeaves()
    {
        var game = NewGame();
        // A ghoul standing in the doorway would (correctly) hold the door open.
        foreach (var ghoul in game.Actors.Where(a => a.Kind == ActorKind.Ghoul)) ghoul.State = GhoulState.Dead;
        game.PosX = 5.5;
        game.PosY = 3.5;
        game.Angle = 0;
        Run(game, 0.2, RaycasterInput.Forward);
        Tap(game, RaycasterInput.Use);
        Run(game, 6);
        Assert.Equal(0, game.DoorAt(7, 3)!.Open);
    }

    [Fact]
    public void Shooting_KillsAGhoulInSight()
    {
        var game = NewGame();
        var ghoul = game.Actors.First(a => a.Kind == ActorKind.Ghoul && (int)a.X == 10 && (int)a.Y == 2);
        game.PosX = 13.5;
        game.PosY = 2.5;
        game.Angle = Math.PI; // facing west, straight at it

        for (int i = 0; i < 20 && ghoul.State != GhoulState.Dead; i++)
        {
            Tap(game, RaycasterInput.Fire);
            Run(game, 0.4);
            game.PosX = 13.5; // stand still even if the ghoul pushes
            game.Angle = Math.Atan2(ghoul.Y - game.PosY, ghoul.X - game.PosX);
        }

        Assert.Equal(GhoulState.Dead, ghoul.State);
        Assert.Equal(1, game.Kills);
        Assert.True(game.Score >= 100);
        Assert.True(game.Ammo < 16);
    }

    [Fact]
    public void AwakeGhoul_FindsAWayThroughADoorToThePlayer()
    {
        var game = NewGame();
        var ghoul = game.Actors.First(a => a.Kind == ActorKind.Ghoul && (int)a.X == 10 && (int)a.Y == 2);
        game.PosX = 3.5;
        game.PosY = 3.5;
        Assert.False(game.HasLineOfSight(ghoul.X, ghoul.Y, game.PosX, game.PosY));

        ghoul.State = GhoulState.Chase;
        double closest = double.MaxValue;
        for (double t = 0; t < 10 && game.State == RaycasterState.Playing; t += Dt)
        {
            game.Update(Dt, RaycasterInput.None, 0);
            closest = Math.Min(closest, Math.Sqrt(Math.Pow(ghoul.X - game.PosX, 2) + Math.Pow(ghoul.Y - game.PosY, 2)));
        }

        Assert.True(closest < 1.2, $"ghoul only got within {closest:F2}");
        Assert.True(game.Health < 100, "ghoul should have attacked");
    }

    [Fact]
    public void UsingTheExitSwitch_CompletesTheLevel_AndAdvances()
    {
        var game = NewGame();
        game.PosX = 22.5;
        game.PosY = 14.5;
        game.Angle = 0;

        Tap(game, RaycasterInput.Use);
        Assert.Equal(RaycasterState.LevelComplete, game.State);
        Assert.True(game.LevelBonus > 0);

        game.NextLevel();
        Assert.Equal(RaycasterState.Playing, game.State);
        Assert.Equal(1, game.LevelIndex);
    }

    [Fact]
    public void Renderer_DrawsAFullFrameWithoutGaps()
    {
        var game = NewGame();
        var renderer = new RaycasterRenderer();
        for (double a = 0; a < Math.PI * 2; a += 0.7)
        {
            game.Angle = a;
            renderer.Render(game);
            for (int i = 3; i < renderer.Buffer.Length; i += 4)
                Assert.Equal(255, renderer.Buffer[i]); // every pixel opaque, so nothing was left unpainted
        }
    }
}
