using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BlazorArcade.GameLogic
{
    /// <summary>Draws <see cref="KnightmareGame"/> into a 256x192 RGBA framebuffer, the MSX's native resolution.</summary>
    public sealed class KnightmareRenderer
    {
        public const int Width = KnightmareGame.ScreenWidth;
        public const int Height = KnightmareGame.ScreenHeight;
        private const int T = KnightmareArt.Tile;

        public readonly byte[] Buffer = new byte[Width * Height * 4];
        private Dictionary<char, PixelSprite[]> _tiles = KnightmareArt.BuildTiles(0);
        private int _tileStage;
        private readonly Dictionary<PixelSprite, PixelSprite> _flashed = new();

        public void Render(KnightmareGame g)
        {
            Span<uint> px = MemoryMarshal.Cast<byte, uint>(Buffer.AsSpan());
            if (_tileStage != g.StageIndex)
            {
                _tiles = KnightmareArt.BuildTiles(g.StageIndex);
                _tileStage = g.StageIndex;
            }

            DrawTerrain(px, g);
            int cam = (int)Math.Floor(g.CameraY);

            foreach (var p in g.Pickups)
            {
                bool blink = p.Cooldown > 0 && (int)(p.Cooldown * 30) % 2 == 0;
                if (!blink) Blit(px, KnightmareArt.Crystals[(int)p.Type], (int)p.X, (int)p.Y - cam);
            }

            foreach (var e in g.Enemies)
            {
                var sprite = EnemySprite(e, g.Time);
                Blit(px, e.Flash > 0 ? Flashed(sprite) : sprite, (int)e.X, (int)e.Y - cam);
            }

            if (!g.PlayerDying && (g.Invulnerable <= 0 || (int)(g.Invulnerable * 12) % 2 == 0) && g.State != KnightmareState.Title)
            {
                int x = (int)g.PlayerX, y = (int)g.PlayerY - cam;
                Blit(px, KnightmareArt.Player[(int)g.WalkPhase % 2], x, y);
                if (g.Shield > 0) Blit(px, KnightmareArt.Shield, x + 1, y - 5);
            }

            foreach (var s in g.Shots)
            {
                var sprite = s.Weapon switch
                {
                    KnightmareWeapon.Fire => KnightmareArt.Flame,
                    KnightmareWeapon.Boomerang => KnightmareArt.Boomerang[(int)(s.Age * 16) % 4],
                    _ => KnightmareArt.Arrow,
                };
                Blit(px, sprite, (int)s.X, (int)s.Y - cam);
            }

            foreach (var b in g.Bullets) Blit(px, KnightmareArt.EnemyBullet, (int)b.X, (int)b.Y - cam);

            foreach (var e in g.Explosions)
            {
                if (e.Age < 0) continue;
                Blit(px, KnightmareArt.Explosion[Math.Min(3, (int)(e.Age * 10))], (int)e.X, (int)e.Y - cam);
            }

            if (g.Boss is { } boss) DrawBossBar(px, boss.Health / (double)g.BossMaxHealth);
        }

        private void DrawTerrain(Span<uint> px, KnightmareGame g)
        {
            int cam = (int)Math.Floor(g.CameraY);
            int firstRow = cam / T, offset = cam % T;
            int waterFrame = (int)(g.Time * 3) % 2;

            for (int r = 0; r <= Height / T; r++)
            {
                int row = firstRow + r;
                for (int col = 0; col < 16; col++)
                {
                    char c = g.TileAt(col, row);
                    var frames = _tiles.TryGetValue(c, out var f) ? f : _tiles['.'];
                    var tile = frames.Length > 1 ? frames[c == '.' ? (col * 7 + row * 3) % 2 : waterFrame] : frames[0];
                    BlitOpaque(px, tile, col * T, r * T - offset);
                }
            }
        }

        private static PixelSprite EnemySprite(KnightmareEnemy e, double time)
        {
            int frame = (int)(e.Age * 6) % 2;
            return e.Kind switch
            {
                KnightmareEnemyKind.Bat => KnightmareArt.Bat[frame],
                KnightmareEnemyKind.Skeleton => KnightmareArt.Skeleton[(int)(e.Age * 4) % 2],
                KnightmareEnemyKind.Knight => KnightmareArt.Knight[(int)(e.Age * 3) % 2],
                KnightmareEnemyKind.Wisp => KnightmareArt.Wisp[frame],
                _ => KnightmareArt.Boss,
            };
        }

        private PixelSprite Flashed(PixelSprite sprite)
        {
            if (!_flashed.TryGetValue(sprite, out var white))
            {
                white = sprite.Tinted(_ => KnightmareArt.Color('F'));
                _flashed[sprite] = white;
            }
            return white;
        }

        private static void DrawBossBar(Span<uint> px, double fraction)
        {
            const int x0 = 64, y0 = 4, w = 128, h = 4;
            int filled = (int)(w * Math.Clamp(fraction, 0, 1));
            for (int y = y0 - 1; y <= y0 + h; y++)
            {
                for (int x = x0 - 1; x <= x0 + w; x++)
                {
                    bool border = y == y0 - 1 || y == y0 + h || x == x0 - 1 || x == x0 + w;
                    px[y * Width + x] = border ? KnightmareArt.Color('F')
                        : x - x0 < filled ? KnightmareArt.Color('R') : KnightmareArt.Color('K');
                }
            }
        }

        private static void Blit(Span<uint> px, PixelSprite s, int x0, int y0)
        {
            for (int y = 0; y < s.Height; y++)
            {
                int sy = y0 + y;
                if ((uint)sy >= Height) continue;
                int row = y * s.Width, dest = sy * Width;
                for (int x = 0; x < s.Width; x++)
                {
                    int sx = x0 + x;
                    if ((uint)sx >= Width) continue;
                    uint c = s.Pixels[row + x];
                    if (c != 0) px[dest + sx] = c;
                }
            }
        }

        private static void BlitOpaque(Span<uint> px, PixelSprite s, int x0, int y0)
        {
            for (int y = 0; y < s.Height; y++)
            {
                int sy = y0 + y;
                if ((uint)sy >= Height) continue;
                s.Pixels.AsSpan(y * s.Width, s.Width).CopyTo(px.Slice(sy * Width + x0, s.Width));
            }
        }
    }
}
