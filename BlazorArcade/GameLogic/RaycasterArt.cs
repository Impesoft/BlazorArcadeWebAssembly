using System;

namespace BlazorArcade.GameLogic
{
    /// <summary>
    /// A 64x64 RGBA texture (little-endian ABGR uints, so the bytes land in canvas RGBA order)
    /// with precomputed distance-shaded copies, so the renderer's inner loops are a single lookup.
    /// A pixel value of 0 is fully transparent.
    /// </summary>
    public sealed class RaycasterTexture
    {
        public const int Size = 64;
        public const int Mask = Size - 1;
        public const int ShadeLevels = 16;

        public readonly uint[] Pixels = new uint[Size * Size];
        public uint[][] Shades { get; private set; } = Array.Empty<uint[]>();

        private static readonly int[] ShadeFactors = BuildShadeFactors();

        private static int[] BuildShadeFactors()
        {
            var factors = new int[ShadeLevels];
            for (int i = 0; i < ShadeLevels; i++)
            {
                factors[i] = (int)Math.Round(256 * Math.Pow(0.83, i));
            }
            return factors;
        }

        public uint this[int x, int y]
        {
            get => Pixels[(y & Mask) * Size + (x & Mask)];
            set
            {
                if ((uint)x < Size && (uint)y < Size)
                {
                    Pixels[y * Size + x] = value;
                }
            }
        }

        public RaycasterTexture Finish()
        {
            Shades = new uint[ShadeLevels][];
            for (int level = 0; level < ShadeLevels; level++)
            {
                int f = ShadeFactors[level];
                var shaded = new uint[Pixels.Length];
                for (int i = 0; i < Pixels.Length; i++)
                {
                    shaded[i] = Scale(Pixels[i], f);
                }
                Shades[level] = shaded;
            }
            return this;
        }

        public static uint Scale(uint c, int factor256)
        {
            if (c == 0) return 0;
            uint r = (uint)(((c & 0xFF) * factor256) >> 8);
            uint g = (uint)((((c >> 8) & 0xFF) * factor256) >> 8);
            uint b = (uint)((((c >> 16) & 0xFF) * factor256) >> 8);
            return (c & 0xFF000000) | (b << 16) | (g << 8) | r;
        }
    }

    /// <summary>
    /// Procedurally generated walls, flats and sprites. Everything is drawn in code from
    /// seeded noise, so the game ships without a single image asset.
    /// </summary>
    public static class RaycasterArt
    {
        public static uint Rgb(double r, double g, double b)
        {
            uint R = (uint)Math.Clamp((int)r, 0, 255);
            uint G = (uint)Math.Clamp((int)g, 0, 255);
            uint B = (uint)Math.Clamp((int)b, 0, 255);
            return 0xFF000000u | (B << 16) | (G << 8) | R;
        }

        public static double Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (double)0xFFFFFF;
            }
        }

        /// <summary>Smooth value noise that tiles every 64 pixels.</summary>
        public static double Noise(double x, double y, int cell, int seed)
        {
            int period = RaycasterTexture.Size / cell;
            double fx = x / cell, fy = y / cell;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            int Wrap(int v) => ((v % period) + period) % period;
            double a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            double c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
        }

        private static double Fbm(int x, int y, int seed) =>
            Noise(x, y, 16, seed) * 0.5 + Noise(x, y, 8, seed + 1) * 0.3 + Noise(x, y, 4, seed + 2) * 0.2;

        // ---------- Walls ----------

        public static RaycasterTexture Bricks(int r, int g, int b, int seed, bool moss = false)
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                int row = y / 8;
                int offset = (row % 2) * 8;
                for (int x = 0; x < 64; x++)
                {
                    int bx = (x + offset) & 63;
                    int col = bx / 16;
                    double n = Fbm(x, y, seed);
                    double k;
                    if (y % 8 == 7 || bx % 16 == 15)
                    {
                        k = 0.35 + n * 0.15;
                    }
                    else
                    {
                        k = 0.85 + (Hash(col, row, seed) - 0.5) * 0.3 + (n - 0.5) * 0.35;
                        if (y % 8 == 0 || bx % 16 == 0) k += 0.15;
                        if (y % 8 == 6 || bx % 16 == 14) k -= 0.12;
                    }
                    double cr = r * k, cg = g * k, cb = b * k;
                    if (moss)
                    {
                        double m = Noise(x, y, 16, seed + 7) * 0.7 + Noise(x, y, 4, seed + 9) * 0.3;
                        if (m > 0.55)
                        {
                            double a = Math.Min(1, (m - 0.55) * 4);
                            cr += (50 - cr) * a;
                            cg += (110 * (0.7 + n * 0.5) - cg) * a;
                            cb += (35 - cb) * a;
                        }
                    }
                    t[x, y] = Rgb(cr, cg, cb);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture Wood(int seed)
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int plank = x / 16;
                    double plankVar = (Hash(plank, 0, seed) - 0.5) * 0.25;
                    double grain = Math.Sin((x % 16) * 0.9 + Noise(x, y, 8, seed) * 6 + plank * 3) * 0.5 + 0.5;
                    double k = 0.7 + grain * 0.25 + plankVar + (Hash(x, y, seed) - 0.5) * 0.08;
                    if (x % 16 == 0) k = 0.25;
                    if (x % 16 == 1) k *= 1.2;
                    if (y % 32 == 0 && x % 16 != 0) k = 0.35; // board joints
                    if ((y % 32 == 4 || y % 32 == 27) && (x % 16 == 3 || x % 16 == 12)) k = 0.2; // nails
                    t[x, y] = Rgb(125 * k, 82 * k, 42 * k);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture Door()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    double k = 0.85 + (Fbm(x, y, 41) - 0.5) * 0.2;
                    bool border = x < 3 || x > 60 || y < 3 || y > 60;
                    bool band = (y >= 14 && y <= 18) || (y >= 45 && y <= 49);
                    if (border) k = 0.5;
                    else if (band) k = y is 14 or 45 ? 1.25 : (y is 18 or 49 ? 0.6 : 1.05);
                    if (x == 31 || x == 32) k = 0.35;
                    if (!border && (x % 12 == 6) && (y % 12 == 6)) k = 1.4; // rivets
                    t[x, y] = Rgb(88 * k, 104 * k, 122 * k);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture DoorFrame()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    double k = 0.8 + (Hash(x / 2, y, 77) - 0.5) * 0.15;
                    if (x >= 26 && x <= 37) k = 0.3 + (x is 26 or 37 ? 0.4 : 0);
                    t[x, y] = Rgb(78 * k, 84 * k, 92 * k);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture Exit()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    uint c;
                    if (y < 8 || y > 55)
                    {
                        c = ((x + y) / 6) % 2 == 0 ? Rgb(230, 190, 20) : Rgb(25, 25, 25);
                    }
                    else
                    {
                        double k = 0.8 + (Fbm(x, y, 5) - 0.5) * 0.3;
                        c = Rgb(32 * k, 40 * k, 34 * k);
                        bool shaft = x >= 12 && x <= 36 && y >= 28 && y <= 36;
                        bool head = x > 36 && x <= 52 && Math.Abs(y - 32) <= (52 - x) * 0.8;
                        if (shaft || head) c = Rgb(70, 255, 110);
                        else if (x >= 8 && x <= 55 && (y == 12 || y == 51) || (y >= 12 && y <= 51 && (x == 8 || x == 55)))
                            c = Rgb(40, 170, 70);
                    }
                    t[x, y] = c;
                }
            }
            return t.Finish();
        }

        // ---------- Flats ----------

        public static RaycasterTexture Floor()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int tileX = x / 32, tileY = y / 32;
                    double tileVar = (Hash(tileX, tileY, 3) - 0.5) * 0.2;
                    double k = 0.8 + tileVar + (Fbm(x, y, 13) - 0.5) * 0.35;
                    if (x % 32 == 0 || y % 32 == 0) k = 0.3;
                    else if (x % 32 == 1 || y % 32 == 1) k += 0.12;
                    t[x, y] = Rgb(84 * k, 78 * k, 70 * k);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture Ceiling()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int v = y % 32;
                    if (v < 6)
                    {
                        double k = 0.75 + Math.Sin(x * 0.7 + Noise(x, y, 8, 21) * 5) * 0.12;
                        if (v == 0 || v == 5) k *= 0.6;
                        t[x, y] = Rgb(78 * k, 54 * k, 32 * k);
                    }
                    else
                    {
                        double k = 0.75 + (Fbm(x, y, 17) - 0.5) * 0.3;
                        t[x, y] = Rgb(46 * k, 44 * k, 50 * k);
                    }
                }
            }
            return t.Finish();
        }

        // ---------- Sprite helpers ----------

        private static void Ellipse(RaycasterTexture t, double cx, double cy, double rx, double ry, Func<int, int, double, uint> color)
        {
            for (int y = (int)(cy - ry); y <= (int)(cy + ry) + 1; y++)
            {
                for (int x = (int)(cx - rx); x <= (int)(cx + rx) + 1; x++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    double d = dx * dx + dy * dy;
                    if (d <= 1) t[x, y] = color(x, y, d);
                }
            }
        }

        private static void Rect(RaycasterTexture t, int x0, int y0, int x1, int y1, uint c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    t[x, y] = c;
        }

        private static void Line(RaycasterTexture t, double x0, double y0, double x1, double y1, double thickness, uint c)
        {
            int steps = (int)(Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) * 2) + 1;
            for (int i = 0; i <= steps; i++)
            {
                double px = x0 + (x1 - x0) * i / steps, py = y0 + (y1 - y0) * i / steps;
                Ellipse(t, px, py, thickness, thickness, (_, _, _) => c);
            }
        }

        // ---------- Sprites ----------

        public enum GhoulPose { Walk0, Walk1, Attack, Hurt, Dead }

        public static RaycasterTexture Ghoul(GhoulPose pose)
        {
            var t = new RaycasterTexture();
            if (pose == GhoulPose.Dead)
            {
                Ellipse(t, 32, 60, 22, 3.5, (_, _, d) => Rgb(70 - d * 30, 20, 18));
                Ellipse(t, 36, 57, 14, 5, (x, y, d) => Rgb(60 * (1 - d * 0.4), 78 * (1 - d * 0.4), 60 * (1 - d * 0.4)));
                Ellipse(t, 22, 56, 6, 4.5, (x, y, d) => Rgb(150 - d * 40, 165 - d * 40, 125 - d * 40));
                t[20, 55] = Rgb(30, 10, 10);
                t[24, 55] = Rgb(30, 10, 10);
                return t.Finish();
            }

            uint claw = Rgb(220, 215, 180);
            uint arm = Rgb(120, 140, 105);
            if (pose == GhoulPose.Attack)
            {
                Line(t, 23, 28, 13, 12, 2.2, arm);
                Line(t, 41, 28, 51, 12, 2.2, arm);
                Line(t, 13, 12, 10, 6, 0.8, claw); Line(t, 13, 12, 14, 5, 0.8, claw);
                Line(t, 51, 12, 54, 6, 0.8, claw); Line(t, 51, 12, 50, 5, 0.8, claw);
            }
            else
            {
                double swing = pose == GhoulPose.Walk1 ? 3 : -3;
                Line(t, 23, 28, 17 + swing, 45, 2.2, arm);
                Line(t, 41, 28, 47 + swing, 45, 2.2, arm);
                Line(t, 17 + swing, 45, 15 + swing, 50, 0.8, claw);
                Line(t, 47 + swing, 45, 49 + swing, 50, 0.8, claw);
            }

            // Legs
            uint leg = Rgb(45, 55, 45);
            if (pose == GhoulPose.Walk1)
            {
                Rect(t, 23, 56, 27, 63, leg);
                Rect(t, 37, 54, 41, 61, leg);
            }
            else
            {
                Rect(t, 26, 56, 30, 63, leg);
                Rect(t, 34, 56, 38, 63, leg);
            }

            // Ragged robe: a trapezoid with a torn hem
            for (int y = 24; y <= 59; y++)
            {
                double half = 8 + (y - 24) * 0.2;
                for (int x = (int)(32 - half); x <= (int)(32 + half); x++)
                {
                    if (y > 54 && Hash(x, 0, 99) * 6 < y - 54) continue;
                    double edge = Math.Abs(x + 0.5 - 32) / half;
                    double k = 1 - edge * 0.45 + (Hash(x, y, 12) - 0.5) * 0.12;
                    if ((x + y * 3) % 11 == 0) k *= 0.8;
                    t[x, y] = Rgb(72 * k, 92 * k, 70 * k);
                }
            }

            // Head
            Ellipse(t, 32, 15, 8, 9, (x, y, d) => Rgb(155 - d * 50, 172 - d * 50, 132 - d * 50));
            Ellipse(t, 28.5, 14, 2.4, 2, (_, _, _) => Rgb(20, 10, 10));
            Ellipse(t, 35.5, 14, 2.4, 2, (_, _, _) => Rgb(20, 10, 10));
            t[28, 14] = Rgb(255, 60, 20);
            t[35, 14] = Rgb(255, 60, 20);
            for (int x = 28; x <= 36; x++)
            {
                t[x, 20] = Rgb(30, 15, 15);
                t[x, 21] = x % 2 == 0 ? Rgb(230, 225, 200) : Rgb(30, 15, 15);
            }

            if (pose == GhoulPose.Hurt)
            {
                for (int i = 0; i < t.Pixels.Length; i++)
                {
                    uint c = t.Pixels[i];
                    if (c == 0) continue;
                    t.Pixels[i] = Rgb(Math.Min(255, (c & 0xFF) + 140), ((c >> 8) & 0xFF) * 0.6, ((c >> 16) & 0xFF) * 0.6);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture HealthKit()
        {
            var t = new RaycasterTexture();
            Rect(t, 20, 44, 44, 63, Rgb(120, 120, 120));
            Rect(t, 21, 45, 43, 62, Rgb(232, 232, 228));
            Rect(t, 25, 51, 39, 56, Rgb(210, 25, 30));
            Rect(t, 29, 47, 34, 60, Rgb(210, 25, 30));
            Rect(t, 21, 45, 43, 45, Rgb(255, 255, 255));
            return t.Finish();
        }

        public static RaycasterTexture AmmoBox()
        {
            var t = new RaycasterTexture();
            for (int i = 0; i < 4; i++)
            {
                int bx = 24 + i * 5;
                Rect(t, bx, 43, bx + 3, 52, Rgb(200, 160, 60));
                Rect(t, bx, 43, bx, 52, Rgb(245, 210, 110));
                Rect(t, bx + 1, 41, bx + 2, 42, Rgb(170, 110, 50));
            }
            Rect(t, 21, 51, 43, 63, Rgb(70, 74, 40));
            Rect(t, 22, 52, 42, 62, Rgb(96, 100, 52));
            Rect(t, 27, 55, 37, 58, Rgb(180, 170, 90));
            return t.Finish();
        }

        public static RaycasterTexture Treasure()
        {
            var t = new RaycasterTexture();
            uint Gold(int x, int y, double d) => Rgb(240 - d * 90, 195 - d * 90, 60 - d * 40);
            Ellipse(t, 32, 61, 9, 2.5, Gold);
            Rect(t, 30, 50, 33, 59, Rgb(200, 160, 40));
            Ellipse(t, 32, 44, 10, 7, Gold);
            Rect(t, 22, 37, 42, 40, 0); // cut the top off to make a cup
            Ellipse(t, 32, 41, 10, 2, (_, _, d) => Rgb(120, 80, 20));
            t[27, 44] = Rgb(255, 255, 230);
            t[28, 45] = Rgb(255, 255, 230);
            Ellipse(t, 32, 46, 2, 2, (_, _, _) => Rgb(220, 30, 60));
            return t.Finish();
        }

        public static RaycasterTexture Pillar()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 64; y++)
            {
                bool cap = y < 6 || y > 57;
                double half = cap ? 13 : 9.5;
                for (int x = (int)(32 - half); x < (int)(32 + half); x++)
                {
                    double u = (x + 0.5 - 32) / half;
                    double k = 0.45 + Math.Cos(u * Math.PI / 2) * 0.65 + (Hash(x, y, 33) - 0.5) * 0.1;
                    if (!cap && (x - 23) % 5 == 0) k *= 0.8; // fluting
                    if (y == 6 || y == 57) k *= 0.5;
                    t[x, y] = Rgb(150 * k, 145 * k, 135 * k);
                }
            }
            return t.Finish();
        }

        public static RaycasterTexture Barrel()
        {
            var t = new RaycasterTexture();
            for (int y = 30; y < 64; y++)
            {
                for (int x = 19; x < 45; x++)
                {
                    double u = (x + 0.5 - 32) / 13;
                    double k = 0.4 + Math.Cos(u * Math.PI / 2) * 0.7;
                    bool hoop = (y >= 34 && y <= 36) || (y >= 57 && y <= 59);
                    if (hoop) t[x, y] = Rgb(110 * k, 110 * k, 115 * k);
                    else t[x, y] = Rgb(120 * k, 75 * k, 38 * k * (x % 6 == 0 ? 0.7 : 1));
                }
            }
            Ellipse(t, 32, 30.5, 12.5, 2.5, (_, _, d) => Rgb(150 - d * 40, 100 - d * 30, 55));
            return t.Finish();
        }

        public static RaycasterTexture Lamp()
        {
            var t = new RaycasterTexture();
            for (int y = 0; y < 10; y++)
            {
                t[31, y] = Rgb(90, 90, 90);
                t[32, y] = y % 2 == 0 ? Rgb(140, 140, 140) : Rgb(60, 60, 60);
            }
            for (int y = 10; y < 18; y++)
            {
                double half = 3 + (y - 10) * 1.1;
                for (int x = (int)(32 - half); x < (int)(32 + half); x++)
                    t[x, y] = Rgb(70, 60, 40);
            }
            Ellipse(t, 32, 20, 6, 4, (_, _, d) => Rgb(255, 245 - d * 60, 180 - d * 120));
            return t.Finish();
        }

        public static RaycasterTexture Pistol(bool firing)
        {
            var t = new RaycasterTexture();
            if (firing)
            {
                for (int i = 0; i < 10; i++)
                {
                    double a = i * Math.PI / 5;
                    double len = i % 2 == 0 ? 13 : 8;
                    Line(t, 32, 16, 32 + Math.Cos(a) * len, 16 + Math.Sin(a) * len, 1.5, Rgb(255, 160, 30));
                }
                Ellipse(t, 32, 16, 6, 6, (_, _, d) => Rgb(255, 255, 200 - d * 150));
            }
            // Slide and barrel
            Rect(t, 28, 24, 36, 44, Rgb(52, 54, 58));
            Rect(t, 29, 24, 29, 44, Rgb(100, 104, 110));
            Rect(t, 31, 22, 33, 24, Rgb(40, 40, 44)); // front sight
            Rect(t, 30, 28, 34, 29, Rgb(30, 30, 34));
            // Grip and hand
            Rect(t, 27, 44, 37, 52, Rgb(40, 32, 26));
            Ellipse(t, 32, 56, 11, 9, (x, y, d) => Rgb(225 - d * 60, 172 - d * 50, 135 - d * 45));
            Rect(t, 24, 58, 40, 63, Rgb(200, 150, 115));
            Ellipse(t, 23, 50, 4, 3, (x, y, d) => Rgb(215 - d * 50, 165 - d * 45, 128 - d * 40)); // thumb
            return t.Finish();
        }
    }
}
