using BlazorArcade.GameLogic;

namespace Sudoku.Tests;

public class KnightmareTests
{
    private const double Dt = 1 / 60.0;

    private static KnightmareGame NewGame()
    {
        var game = new KnightmareGame();
        game.StartNewGame();
        return game;
    }

    [Fact]
    public void AllStages_Are16Wide_AndHaveAWalkableRouteToTheTop()
    {
        foreach (var stage in KnightmareStages.All)
        {
            var rows = stage.Rows;
            Assert.All(rows, r => Assert.Equal(16, r.Length));

            // Flood fill upward from the open cells of the bottom row; the top area must be reachable.
            int h = rows.Length;
            var seen = new bool[16, h];
            var queue = new Queue<(int X, int Y)>();
            for (int x = 0; x < 16; x++)
            {
                if (KnightmareGame.IsSolid(rows[h - 1][x])) continue;
                seen[x, h - 1] = true;
                queue.Enqueue((x, h - 1));
            }
            int highest = h - 1;
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                highest = Math.Min(highest, y);
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (nx < 0 || nx >= 16 || ny < 0 || ny >= h || seen[nx, ny] || KnightmareGame.IsSolid(rows[ny][nx])) continue;
                    seen[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
            Assert.True(highest <= 1, $"{stage.Name}: can only reach row {highest}");
        }
    }

    [Fact]
    public void Sprites_AreTheExpectedSizes()
    {
        Assert.All(KnightmareArt.Player, s => Assert.Equal((16, 16), (s.Width, s.Height)));
        Assert.All(KnightmareArt.Crystals, s => Assert.Equal((14, 14), (s.Width, s.Height)));
        Assert.Equal((32, 32), (KnightmareArt.Boss.Width, KnightmareArt.Boss.Height));
        foreach (var tiles in KnightmareArt.BuildTiles(0).Values)
            Assert.All(tiles, t => Assert.Equal((16, 16), (t.Width, t.Height)));
    }

    [Fact]
    public void Screen_ScrollsUp_AndCarriesTheKnight()
    {
        var game = NewGame();
        double camera = game.CameraY, screenY = game.PlayerY - game.CameraY;
        for (int i = 0; i < 60; i++) game.Update(Dt, KnightmareInput.None);

        Assert.True(game.CameraY < camera - 20);
        Assert.Equal(screenY, game.PlayerY - game.CameraY, 0); // stayed in place on screen
    }

    [Fact]
    public void ShootingAQuestionBlock_DropsACrystal_ThatCyclesWhenShot()
    {
        var game = NewGame();
        // Find the nearest "?" ahead and stand under it.
        int row = Enumerable.Range(0, game.MapRows).Reverse().First(r => Enumerable.Range(0, 16).Any(c => game.TileAt(c, r) == '?'));
        int col = Enumerable.Range(0, 16).First(c => game.TileAt(c, row) == '?');
        while (game.CameraY > row * 16 - 100) game.Update(Dt, KnightmareInput.None);
        foreach (var e in game.Enemies) e.Dead = true;
        game.PlayerX = col * 16;
        game.PlayerY = Math.Max(game.PlayerY, row * 16 + 40);

        for (int i = 0; i < 60 && game.Pickups.Count == 0; i++) game.Update(Dt, i % 2 == 0 ? KnightmareInput.Fire : KnightmareInput.None);
        Assert.Single(game.Pickups);
        Assert.Equal(',', game.TileAt(col, row));
        Assert.Equal(KnightmareItem.DoubleArrow, game.Pickups[0].Type);

        for (int i = 0; i < 40; i++) game.Update(Dt, i % 2 == 0 ? KnightmareInput.Fire : KnightmareInput.None);
        Assert.True(game.Pickups.Count == 0 || game.Pickups[0].Type != KnightmareItem.DoubleArrow, "shooting the crystal should change its type");
    }

    [Fact]
    public void CollectingCrystals_UpgradesTheKnight()
    {
        var game = NewGame();
        foreach (var type in new[] { KnightmareItem.Fire, KnightmareItem.Shield, KnightmareItem.Speed })
        {
            game.Pickups.Add(new KnightmarePickup { X = game.PlayerX, Y = game.PlayerY, Type = type });
            game.Update(Dt, KnightmareInput.None);
        }
        Assert.Equal(KnightmareWeapon.Fire, game.Weapon);
        Assert.Equal(3, game.Shield);
        Assert.Equal(1, game.SpeedLevel);
    }

    [Fact]
    public void BeingPinnedByTerrain_CrushesTheKnight()
    {
        var game = NewGame();
        // Walk into the first river and wait on the bank, away from the bridge.
        int river = Enumerable.Range(0, game.MapRows).Reverse().First(r => game.TileAt(0, r) == '~');
        while (game.CameraY > river * 16 - 40) game.Update(Dt, KnightmareInput.None);
        int lives = game.Lives;
        game.PlayerX = 0;
        game.PlayerY = river * 16 + 16;

        for (int i = 0; i < 60 * 10 && game.Lives == lives; i++)
        {
            foreach (var e in game.Enemies) e.Dead = true;
            game.Bullets.Clear();
            game.Update(Dt, KnightmareInput.None);
        }
        Assert.Equal(lives - 1, game.Lives);
    }

    [Fact]
    public void ReachingTheTop_SummonsTheBoss_AndBeatingItClearsTheStage()
    {
        var game = NewGame();
        for (int i = 0; i < 60 * 300 && game.Boss == null; i++)
        {
            foreach (var e in game.Enemies) e.Dead = true;
            game.Bullets.Clear();
            game.Update(Dt, KnightmareInput.None);
            game.PlayerX = 120; // keep clear of walls so the scroll never crushes the test knight
            game.PlayerY = game.CameraY + 100;
        }
        Assert.NotNull(game.Boss);
        Assert.Equal(0, game.CameraY);

        game.Boss!.Health = 1;
        game.Boss.Y = game.CameraY + 22;
        game.PlayerX = game.Boss.X + 8;
        for (int i = 0; i < 60 * 6 && game.State == KnightmareState.Playing; i++)
        {
            game.Bullets.Clear();
            if (game.Boss != null) game.PlayerX = game.Boss.X + 8;
            game.Update(Dt, i % 2 == 0 ? KnightmareInput.Fire : KnightmareInput.None);
        }
        Assert.Equal(KnightmareState.StageClear, game.State);

        game.NextStage();
        Assert.Equal(1, game.StageIndex);
        Assert.Equal(KnightmareState.Playing, game.State);
    }

    [Fact]
    public void Renderer_FillsEveryPixel()
    {
        var game = NewGame();
        var renderer = new KnightmareRenderer();
        for (int i = 0; i < 120; i++) game.Update(Dt, KnightmareInput.Fire);
        renderer.Render(game);
        for (int i = 3; i < renderer.Buffer.Length; i += 4) Assert.Equal(255, renderer.Buffer[i]);
    }
}
