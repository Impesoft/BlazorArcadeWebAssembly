using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BlazorArcade.GameLogic
{
    /// <summary>
    /// Software renderer for <see cref="RaycasterGame"/>: classic DDA raycasting into a 320x200
    /// RGBA framebuffer, which the page hands to a canvas each frame. All inner loops use
    /// 16.16 fixed point and pre-shaded textures to keep the WebAssembly interpreter fast.
    /// </summary>
    public sealed class RaycasterRenderer
    {
        public const int Width = 320;
        public const int Height = 200;
        private const int Half = Height / 2;
        private const int T = RaycasterTexture.Size;

        public readonly byte[] Buffer = new byte[Width * Height * 4];
        private readonly double[] _zBuffer = new double[Width];
        private readonly List<(RaycasterActor Actor, double Dist)> _sprites = new();

        private readonly RaycasterTexture _stone = RaycasterArt.Bricks(120, 118, 112, 1);
        private readonly RaycasterTexture _brick = RaycasterArt.Bricks(150, 62, 48, 2);
        private readonly RaycasterTexture _moss = RaycasterArt.Bricks(105, 108, 100, 3, moss: true);
        private readonly RaycasterTexture _wood = RaycasterArt.Wood(4);
        private readonly RaycasterTexture _door = RaycasterArt.Door();
        private readonly RaycasterTexture _doorFrame = RaycasterArt.DoorFrame();
        private readonly RaycasterTexture _exit = RaycasterArt.Exit();
        private readonly RaycasterTexture _floor = RaycasterArt.Floor();
        private readonly RaycasterTexture _ceiling = RaycasterArt.Ceiling();

        private readonly RaycasterTexture[] _ghoul =
        {
            RaycasterArt.Ghoul(RaycasterArt.GhoulPose.Walk0),
            RaycasterArt.Ghoul(RaycasterArt.GhoulPose.Walk1),
            RaycasterArt.Ghoul(RaycasterArt.GhoulPose.Attack),
            RaycasterArt.Ghoul(RaycasterArt.GhoulPose.Hurt),
            RaycasterArt.Ghoul(RaycasterArt.GhoulPose.Dead),
        };
        private readonly RaycasterTexture _health = RaycasterArt.HealthKit();
        private readonly RaycasterTexture _ammo = RaycasterArt.AmmoBox();
        private readonly RaycasterTexture _treasure = RaycasterArt.Treasure();
        private readonly RaycasterTexture _pillar = RaycasterArt.Pillar();
        private readonly RaycasterTexture _barrel = RaycasterArt.Barrel();
        private readonly RaycasterTexture _lamp = RaycasterArt.Lamp();
        private readonly RaycasterTexture _pistol = RaycasterArt.Pistol(false);
        private readonly RaycasterTexture _pistolFire = RaycasterArt.Pistol(true);

        private static int ShadeFor(double dist) => Math.Clamp((int)(dist * 1.1), 0, RaycasterTexture.ShadeLevels - 1);

        public void Render(RaycasterGame g)
        {
            Span<uint> px = MemoryMarshal.Cast<byte, uint>(Buffer.AsSpan());
            double dirX = g.DirX, dirY = g.DirY;
            double planeX = -dirY * RaycasterGame.PlaneLength, planeY = dirX * RaycasterGame.PlaneLength;

            DrawFlats(px, g, dirX, dirY, planeX, planeY);
            DrawWalls(px, g, dirX, dirY, planeX, planeY);
            DrawSprites(px, g, dirX, dirY, planeX, planeY);

            if (g.State == RaycasterState.Playing)
            {
                DrawWeapon(px, g);
                DrawCrosshair(px);
                if (g.ShowMap) DrawMinimap(px, g, dirX, dirY);
            }
            if (g.DamageFlash > 0) Tint(px, 200, 0, 0, (int)(g.DamageFlash / 0.35 * 140));
            if (g.PickupFlash > 0) Tint(px, 255, 210, 80, (int)(g.PickupFlash / 0.25 * 70));
        }

        // Floor and ceiling, one horizontal scanline at a time.
        private void DrawFlats(Span<uint> px, RaycasterGame g, double dirX, double dirY, double planeX, double planeY)
        {
            double rdx0 = dirX - planeX, rdy0 = dirY - planeY;
            double rdx1 = dirX + planeX, rdy1 = dirY + planeY;

            for (int y = Half; y < Height; y++)
            {
                double rowDist = Half / (y - Half + 0.5);
                double stepX = rowDist * (rdx1 - rdx0) / Width;
                double stepY = rowDist * (rdy1 - rdy0) / Width;
                int fx = (int)((g.PosX + rowDist * rdx0) * T * 65536);
                int fy = (int)((g.PosY + rowDist * rdy0) * T * 65536);
                int sx = (int)(stepX * T * 65536), sy = (int)(stepY * T * 65536);

                int level = ShadeFor(rowDist);
                uint[] floor = _floor.Shades[level], ceiling = _ceiling.Shades[level];
                int floorRow = y * Width, ceilingRow = (Height - 1 - y) * Width;

                for (int x = 0; x < Width; x++)
                {
                    int ti = (((fy >> 16) & 63) << 6) | ((fx >> 16) & 63);
                    px[floorRow + x] = floor[ti];
                    px[ceilingRow + x] = ceiling[ti];
                    fx += sx;
                    fy += sy;
                }
            }
        }

        private RaycasterTexture WallTexture(char c) => c switch
        {
            'B' => _brick,
            'M' => _moss,
            'W' => _wood,
            'E' => _exit,
            _ => _stone,
        };

        private void DrawWalls(Span<uint> px, RaycasterGame g, double dirX, double dirY, double planeX, double planeY)
        {
            for (int x = 0; x < Width; x++)
            {
                double cameraX = 2.0 * x / Width - 1;
                double rayX = dirX + planeX * cameraX, rayY = dirY + planeY * cameraX;
                int mapX = (int)g.PosX, mapY = (int)g.PosY;
                double deltaX = rayX == 0 ? 1e30 : Math.Abs(1 / rayX);
                double deltaY = rayY == 0 ? 1e30 : Math.Abs(1 / rayY);
                int stepX = rayX < 0 ? -1 : 1, stepY = rayY < 0 ? -1 : 1;
                double sideX = rayX < 0 ? (g.PosX - mapX) * deltaX : (mapX + 1 - g.PosX) * deltaX;
                double sideY = rayY < 0 ? (g.PosY - mapY) * deltaY : (mapY + 1 - g.PosY) * deltaY;

                RaycasterTexture tex = _stone;
                double perp = 64, wallU = 0;
                int side = 0;
                bool previousWasDoor = false;

                for (int i = 0; i < 64; i++)
                {
                    if (sideX < sideY) { sideX += deltaX; mapX += stepX; side = 0; }
                    else { sideY += deltaY; mapY += stepY; side = 1; }

                    char cell = g.CellAt(mapX, mapY);
                    if (cell == 'D')
                    {
                        // Doors sit in the middle of their cell and slide sideways as they open.
                        var door = g.Doors[mapX, mapY]!;
                        previousWasDoor = true;
                        if (door.AlongY != (side == 0)) continue;
                        double halfDist = side == 0 ? sideX - deltaX * 0.5 : sideY - deltaY * 0.5;
                        if (halfDist >= (side == 0 ? sideY : sideX)) continue;
                        double hit = side == 0 ? g.PosY + halfDist * rayY : g.PosX + halfDist * rayX;
                        hit -= Math.Floor(hit);
                        if (hit < door.Open) continue;
                        perp = halfDist;
                        wallU = hit - door.Open;
                        tex = _door;
                        break;
                    }
                    if (RaycasterGame.IsWallChar(cell))
                    {
                        perp = side == 0 ? sideX - deltaX : sideY - deltaY;
                        double hit = side == 0 ? g.PosY + perp * rayY : g.PosX + perp * rayX;
                        wallU = hit - Math.Floor(hit);
                        if ((side == 0 && rayX > 0) || (side == 1 && rayY < 0)) wallU = 1 - wallU;
                        tex = previousWasDoor ? _doorFrame : WallTexture(cell);
                        break;
                    }
                    previousWasDoor = false;
                }

                perp = Math.Max(perp, 0.05);
                _zBuffer[x] = perp;

                int lineHeight = Math.Max(1, (int)(Height / perp));
                int drawStart = Half - lineHeight / 2;
                int y0 = Math.Max(0, drawStart), y1 = Math.Min(Height, drawStart + lineHeight);
                int texX = Math.Min(T - 1, (int)(wallU * T));
                int texStep = (T << 16) / lineHeight;
                int texPos = (y0 - drawStart) * texStep;
                uint[] shade = tex.Shades[Math.Min(RaycasterTexture.ShadeLevels - 1, ShadeFor(perp) + side * 2)];

                for (int y = y0; y < y1; y++)
                {
                    px[y * Width + x] = shade[(((texPos >> 16) & 63) << 6) | texX];
                    texPos += texStep;
                }
            }
        }

        private RaycasterTexture SpriteTexture(RaycasterActor a) => a.Kind switch
        {
            ActorKind.Ghoul => a.State switch
            {
                GhoulState.Dead => _ghoul[4],
                GhoulState.Hurt => _ghoul[3],
                GhoulState.Attack => _ghoul[2],
                GhoulState.Chase => _ghoul[(int)(a.AnimTime * 4) % 2],
                _ => _ghoul[0],
            },
            ActorKind.Health => _health,
            ActorKind.Ammo => _ammo,
            ActorKind.Treasure => _treasure,
            ActorKind.Pillar => _pillar,
            ActorKind.Barrel => _barrel,
            _ => _lamp,
        };

        private void DrawSprites(Span<uint> px, RaycasterGame g, double dirX, double dirY, double planeX, double planeY)
        {
            _sprites.Clear();
            foreach (var a in g.Actors)
            {
                if (a.Removed) continue;
                double dx = a.X - g.PosX, dy = a.Y - g.PosY;
                _sprites.Add((a, dx * dx + dy * dy));
            }
            _sprites.Sort((p, q) => q.Dist.CompareTo(p.Dist)); // far to near

            double invDet = 1.0 / (planeX * dirY - dirX * planeY);
            foreach (var (a, _) in _sprites)
            {
                double sx = a.X - g.PosX, sy = a.Y - g.PosY;
                double tx = invDet * (dirY * sx - dirX * sy);
                double ty = invDet * (-planeY * sx + planeX * sy);
                if (ty < 0.2) continue;

                int screenX = (int)(Width / 2 * (1 + tx / ty));
                int size = (int)(Height / ty);
                if (size <= 0) continue;
                int startX = screenX - size / 2, startY = Half - size / 2;
                int x0 = Math.Max(0, startX), x1 = Math.Min(Width, startX + size);
                int y0 = Math.Max(0, startY), y1 = Math.Min(Height, startY + size);
                if (x0 >= x1) continue;

                int texStep = (T << 16) / size;
                int level = a.Kind == ActorKind.Lamp ? 0 : ShadeFor(ty);
                uint[] shade = SpriteTexture(a).Shades[level];

                for (int x = x0; x < x1; x++)
                {
                    if (ty >= _zBuffer[x]) continue;
                    int texX = (((x - startX) * texStep) >> 16) & 63;
                    int texPos = (y0 - startY) * texStep;
                    for (int y = y0; y < y1; y++)
                    {
                        uint c = shade[(((texPos >> 16) & 63) << 6) | texX];
                        if (c != 0) px[y * Width + x] = c;
                        texPos += texStep;
                    }
                }
            }
        }

        private void DrawWeapon(Span<uint> px, RaycasterGame g)
        {
            const int size = 96;
            int bobX = (int)(Math.Sin(g.BobPhase) * 6 * g.BobAmount);
            int bobY = (int)(Math.Abs(Math.Cos(g.BobPhase)) * 5 * g.BobAmount) + (g.WeaponCooldown > 0.2 ? 4 : 0);
            int originX = Width / 2 - size / 2 + bobX;
            int originY = Height - size + 6 + bobY;
            uint[] shade = (g.MuzzleFlash > 0 ? _pistolFire : _pistol).Shades[1];

            for (int dy = 0; dy < size; dy++)
            {
                int y = originY + dy;
                if ((uint)y >= Height) continue;
                int row = (dy * T / size) << 6;
                for (int dx = 0; dx < size; dx++)
                {
                    int x = originX + dx;
                    if ((uint)x >= Width) continue;
                    uint c = shade[row | (dx * T / size)];
                    if (c != 0) px[y * Width + x] = c;
                }
            }
        }

        private static void DrawCrosshair(Span<uint> px)
        {
            uint c = RaycasterArt.Rgb(230, 230, 230);
            int cx = Width / 2, cy = Half;
            foreach (var (dx, dy) in new[] { (-3, 0), (-2, 0), (2, 0), (3, 0), (0, -3), (0, -2), (0, 2), (0, 3) })
                px[(cy + dy) * Width + cx + dx] = c;
        }

        private static void DrawMinimap(Span<uint> px, RaycasterGame g, double dirX, double dirY)
        {
            const int cell = 3, ox = 4, oy = 4;
            for (int my = 0; my < g.MapHeight; my++)
            {
                for (int mx = 0; mx < g.MapWidth; mx++)
                {
                    char c = g.Map[mx, my];
                    uint color = c switch
                    {
                        'D' => g.Doors[mx, my]!.IsPassable ? RaycasterArt.Rgb(90, 140, 200) : RaycasterArt.Rgb(50, 90, 170),
                        'E' => RaycasterArt.Rgb(60, 230, 100),
                        '.' => 0,
                        _ => RaycasterArt.Rgb(150, 150, 160),
                    };
                    for (int y = 0; y < cell; y++)
                    {
                        for (int x = 0; x < cell; x++)
                        {
                            int i = (oy + my * cell + y) * Width + ox + mx * cell + x;
                            px[i] = color != 0 ? color : ((px[i] >> 2) & 0x3F3F3F) | 0xFF000000;
                        }
                    }
                }
            }

            foreach (var a in g.Actors)
            {
                if (a.Kind == ActorKind.Ghoul && a.State != GhoulState.Dead)
                    Dot(px, ox + (int)(a.X * cell), oy + (int)(a.Y * cell), RaycasterArt.Rgb(255, 50, 40));
            }

            int pxp = ox + (int)(g.PosX * cell), pyp = oy + (int)(g.PosY * cell);
            for (int i = 1; i <= 5; i++)
            {
                int lx = pxp + (int)(dirX * i), ly = pyp + (int)(dirY * i);
                if ((uint)lx < Width && (uint)ly < Height) px[ly * Width + lx] = RaycasterArt.Rgb(255, 230, 80);
            }
            Dot(px, pxp, pyp, RaycasterArt.Rgb(255, 230, 80));

            static void Dot(Span<uint> px, int x, int y, uint c)
            {
                for (int dy = -1; dy <= 0; dy++)
                    for (int dx = -1; dx <= 0; dx++)
                        if ((uint)(x + dx) < Width && (uint)(y + dy) < Height) px[(y + dy) * Width + x + dx] = c;
            }
        }

        private static void Tint(Span<uint> px, int r, int g, int b, int alpha256)
        {
            alpha256 = Math.Clamp(alpha256, 0, 255);
            int inv = 256 - alpha256;
            int ar = r * alpha256, ag = g * alpha256, ab = b * alpha256;
            for (int i = 0; i < px.Length; i++)
            {
                uint c = px[i];
                uint nr = (uint)(((int)(c & 0xFF) * inv + ar) >> 8);
                uint ng = (uint)(((int)((c >> 8) & 0xFF) * inv + ag) >> 8);
                uint nb = (uint)(((int)((c >> 16) & 0xFF) * inv + ab) >> 8);
                px[i] = 0xFF000000 | (nb << 16) | (ng << 8) | nr;
            }
        }
    }
}
