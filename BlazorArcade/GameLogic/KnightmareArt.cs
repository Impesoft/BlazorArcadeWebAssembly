using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorArcade.GameLogic
{
    /// <summary>A small RGBA image (0 = transparent), in the same pixel format as the framebuffer.</summary>
    public sealed class PixelSprite
    {
        public int Width { get; }
        public int Height { get; }
        public uint[] Pixels { get; }

        public PixelSprite(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        public uint this[int x, int y]
        {
            get => Pixels[y * Width + x];
            set
            {
                if ((uint)x < (uint)Width && (uint)y < (uint)Height) Pixels[y * Width + x] = value;
            }
        }

        public void Fill(int x0, int y0, int x1, int y1, uint c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    this[x, y] = c;
        }

        public void Ellipse(double cx, double cy, double rx, double ry, uint c)
        {
            for (int y = (int)(cy - ry) - 1; y <= (int)(cy + ry) + 1; y++)
            {
                for (int x = (int)(cx - rx) - 1; x <= (int)(cx + rx) + 1; x++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) this[x, y] = c;
                }
            }
        }

        public PixelSprite Tinted(Func<uint, uint> map)
        {
            var copy = new PixelSprite(Width, Height);
            for (int i = 0; i < Pixels.Length; i++) copy.Pixels[i] = Pixels[i] == 0 ? 0 : map(Pixels[i]);
            return copy;
        }
    }

    /// <summary>
    /// Pixel art for Knightmare in the MSX1 (TMS9918) palette: sprites are drawn as text,
    /// terrain tiles are generated from seeded patterns.
    /// </summary>
    public static class KnightmareArt
    {
        public const int Tile = 16;

        // TMS9918 palette, plus a skin tone and a brown the MSX would have dithered.
        private static readonly Dictionary<char, uint> Palette = new()
        {
            ['K'] = Rgb(0, 0, 0),
            ['k'] = Rgb(60, 60, 72),
            ['E'] = Rgb(204, 204, 204),
            ['F'] = Rgb(255, 255, 255),
            ['r'] = Rgb(212, 82, 77),
            ['R'] = Rgb(252, 85, 84),
            ['p'] = Rgb(255, 121, 120),
            ['y'] = Rgb(212, 193, 84),
            ['Y'] = Rgb(230, 206, 128),
            ['g'] = Rgb(33, 176, 59),
            ['G'] = Rgb(94, 220, 120),
            ['b'] = Rgb(84, 85, 237),
            ['B'] = Rgb(125, 118, 252),
            ['c'] = Rgb(66, 235, 245),
            ['m'] = Rgb(201, 91, 186),
            ['n'] = Rgb(120, 70, 30),
            ['o'] = Rgb(200, 120, 40),
            ['s'] = Rgb(240, 190, 150),
        };

        public static uint Rgb(int r, int g, int b) => RaycasterArt.Rgb(r, g, b);
        public static uint Color(char c) => Palette[c];

        public static PixelSprite Parse(params string[] rows)
        {
            int width = rows.Max(r => r.Length);
            var sprite = new PixelSprite(width, rows.Length);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] != '.') sprite[x, y] = Palette[rows[y][x]];
            return sprite;
        }

        // ---------- Characters ----------

        public static readonly PixelSprite[] Player =
        {
            Parse(
                "......EEEE......",
                ".....EFFFFE.....",
                "....EFEEEEFE....",
                "....EEEEEEEE....",
                "....kEEEEEEk....",
                "...bbkEEEEkbb...",
                "..bBBbbbbbbBBb..",
                "..bBBBBBBBBBBb..",
                ".sbBBBBBBBBBBbs.",
                ".s.bBBBBBBBBb.s.",
                "...bbBBBBBBbb...",
                "....nnnnnnnn....",
                "....bBb..bBb....",
                "....bBb..bBb....",
                "....kkk..kkk....",
                "....kkk........."),
            Parse(
                "......EEEE......",
                ".....EFFFFE.....",
                "....EFEEEEFE....",
                "....EEEEEEEE....",
                "....kEEEEEEk....",
                "...bbkEEEEkbb...",
                "..bBBbbbbbbBBb..",
                "..bBBBBBBBBBBb..",
                ".sbBBBBBBBBBBbs.",
                ".s.bBBBBBBBBb.s.",
                "...bbBBBBBBbb...",
                "....nnnnnnnn....",
                "....bBb..bBb....",
                "....bBb..bBb....",
                "....kkk..kkk....",
                ".........kkk...."),
        };

        public static readonly PixelSprite[] Bat =
        {
            Parse(
                "................",
                "................",
                ".K............K.",
                ".KK..........KK.",
                ".KmK...KK...KmK.",
                ".KmmK.KRRK.KmmK.",
                "..KmmKKmmKKmmK..",
                "...KmmmmmmmmK...",
                "....KKmmmmKK....",
                "......KmmK......",
                ".......KK......."),
            Parse(
                "................",
                "................",
                "................",
                "................",
                ".......KK.......",
                "......KRRK......",
                "...KKKKmmKKKK...",
                "..KmmmmmmmmmmK..",
                ".KmmmKmmmmKmmmK.",
                ".KmmK.KmmK.KmmK.",
                ".KmK...KK...KmK.",
                ".KK..........KK.",
                ".K............K."),
        };

        public static readonly PixelSprite[] Skeleton =
        {
            Parse(
                "......FFFF......",
                ".....FFFFFF.....",
                ".....FKFFKF.....",
                ".....FFFFFF.....",
                "......FKKF......",
                ".......FF.......",
                "....FFFFFFFF....",
                "...F.F.FF.F.F...",
                "...F.FFFFFF.F...",
                "...E.F.FF.F.E...",
                "......FFFF......",
                ".......FF.......",
                "......F..F......",
                "......F..F......",
                ".....FF..FF....."),
            Parse(
                "......FFFF......",
                ".....FFFFFF.....",
                ".....FKFFKF.....",
                ".....FFFFFF.....",
                "......FKKF......",
                ".......FF.......",
                "....FFFFFFFF....",
                "...F.F.FF.F.F...",
                "...F.FFFFFF.F...",
                "...E.F.FF.F.E...",
                "......FFFF......",
                ".......FF.......",
                ".....F....F.....",
                ".....F....F.....",
                "....FF....FF...."),
        };

        public static readonly PixelSprite[] Knight =
        {
            Parse(
                "......rrrr......",
                ".....rRRRRr.....",
                ".....rKRRKr.....",
                ".....rRRRRr.....",
                "......rrrr......",
                "...EEErRRrEEE...",
                "..E.rRRRRRRr.E..",
                "..E.rRRRRRRr.E..",
                "..E.rRRrrRRr.E..",
                "..Y.rRRRRRRr.Y..",
                "....rrrrrrrr....",
                "....kk....kk....",
                "....kk....kk....",
                "...kkk....kkk..."),
            Parse(
                "......rrrr......",
                ".....rRRRRr.....",
                ".....rKRRKr.....",
                ".....rRRRRr.....",
                "......rrrr......",
                "...EEErRRrEEE...",
                "..E.rRRRRRRr.E..",
                "..E.rRRRRRRr.E..",
                "..E.rRRrrRRr.E..",
                "..Y.rRRRRRRr.Y..",
                "....rrrrrrrr....",
                "....kk....kk....",
                "...kkk....kk....",
                "..........kkk..."),
        };

        public static readonly PixelSprite[] Wisp = { MakeWisp(0), MakeWisp(1) };

        private static PixelSprite MakeWisp(int frame)
        {
            var s = new PixelSprite(16, 16);
            s.Ellipse(8, 9 + frame * 0.5, 6, 6, Color('r'));
            s.Ellipse(8, 7 - frame, 4.5, 6.5, Color('R'));
            s.Ellipse(8, 9, 3.5, 3.5, Color('o'));
            s.Ellipse(8, 9.5, 2, 2, Color('Y'));
            s[6, 8] = Color('K');
            s[9, 8] = Color('K');
            s[frame == 0 ? 5 : 10, 1] = Color('R');
            return s;
        }

        public static readonly PixelSprite Boss = MakeBoss();

        private static PixelSprite MakeBoss()
        {
            var s = new PixelSprite(32, 32);
            uint dark = Color('k'), steel = Color('E'), blue = Color('b'), light = Color('B'), red = Color('R'), gold = Color('y');
            s.Fill(4, 10, 27, 13, dark);        // shoulders
            s.Ellipse(5, 11, 4, 4, steel);
            s.Ellipse(26, 11, 4, 4, steel);
            s.Fill(9, 12, 22, 25, blue);        // torso
            s.Fill(10, 13, 21, 14, light);
            s.Fill(15, 15, 16, 24, light);
            s.Fill(9, 22, 22, 23, gold);        // belt
            s.Ellipse(16, 7, 6, 6, dark);       // helmet
            s.Fill(12, 6, 19, 8, steel);
            s.Fill(13, 7, 14, 7, red);          // eyes
            s.Fill(17, 7, 18, 7, red);
            for (int i = 0; i < 5; i++)         // horns
            {
                s[9 - i / 2, 4 - i] = steel;
                s[22 + i / 2, 4 - i] = steel;
            }
            s.Fill(1, 14, 4, 24, dark);         // arms
            s.Fill(27, 14, 30, 24, dark);
            s.Fill(28, 18, 29, 31, steel);      // sword
            s.Fill(26, 18, 31, 18, gold);
            s.Fill(10, 26, 14, 31, dark);       // legs
            s.Fill(17, 26, 21, 31, dark);
            return s;
        }

        // ---------- Projectiles and effects ----------

        public static readonly PixelSprite Arrow = Parse(
            ".F.",
            "FEF",
            ".E.",
            ".E.",
            ".E.",
            ".E.",
            ".y.",
            "y.y");

        public static readonly PixelSprite Flame = Parse(
            "..RR..",
            ".RooR.",
            "RoYYoR",
            "RoYYoR",
            ".RooR.",
            "..RR..");

        public static readonly PixelSprite[] Boomerang =
        {
            Parse("YYYYY...", "Yyyyy...", "Yy......", "Yy......", "Yy......"),
            Parse("...YYYYY", "...yyyyY", "......yY", "......yY", "......yY"),
            Parse("......yY", "......yY", "......yY", "...yyyyY", "...YYYYY"),
            Parse("Yy......", "Yy......", "Yy......", "Yyyyy...", "YYYYY..."),
        };

        public static readonly PixelSprite EnemyBullet = Parse(
            ".pp.",
            "pFFp",
            "pFFp",
            ".pp.");

        public static readonly PixelSprite Shield = Parse(
            "..cccccccccc..",
            ".cFFFFFFFFFFc.",
            "cFcccccccccFc.",
            ".ccccccccccc..");

        public static readonly PixelSprite[] Explosion = Enumerable.Range(0, 4).Select(MakeExplosion).ToArray();

        private static PixelSprite MakeExplosion(int frame)
        {
            var s = new PixelSprite(16, 16);
            double r = 3 + frame * 1.7;
            s.Ellipse(8, 8, r, r, Color(frame < 2 ? 'R' : 'r'));
            s.Ellipse(8, 8, r * 0.65, r * 0.65, Color(frame < 2 ? 'Y' : 'o'));
            if (frame >= 2) s.Ellipse(8, 8, r * 0.35, r * 0.35, 0);
            var rng = new Random(frame * 7 + 1);
            for (int i = 0; i < 6; i++) s[rng.Next(16), rng.Next(16)] = Color('Y');
            return s;
        }

        // ---------- Power-up crystals ----------

        private static readonly string[][] Glyphs =
        {
            new[] { "FF.", "..F", ".F.", "F..", "FFF" }, // 2  double arrow
            new[] { "FFF", "F..", "FF.", "F..", "F.." }, // F  fire
            new[] { "FF.", "F.F", "FF.", "F.F", "FF." }, // B  boomerang
            new[] { ".FF", "F..", ".F.", "..F", "FF." }, // S  shield
            new[] { ".F.", "FFF", ".F.", ".F.", ".F." }, // ↑  speed
            new[] { "F.F", ".F.", "FFF", ".F.", "F.F" }, // *  bonus
        };

        private static readonly (char Dark, char Light)[] CrystalColors =
        {
            ('b', 'B'), ('r', 'R'), ('y', 'Y'), ('c', 'F'), ('g', 'G'), ('m', 'p'),
        };

        public static readonly PixelSprite[] Crystals = Enumerable.Range(0, Glyphs.Length).Select(MakeCrystal).ToArray();

        private static PixelSprite MakeCrystal(int type)
        {
            var s = new PixelSprite(14, 14);
            var (dark, light) = CrystalColors[type];
            for (int y = 0; y < 14; y++)
            {
                int half = y < 7 ? y : 13 - y;
                for (int x = 6 - half; x <= 7 + half; x++)
                    s[x, y] = Color(x + y < 13 ? light : dark);
            }
            var glyph = Glyphs[type];
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 3; x++)
                    if (glyph[y][x] == 'F') s[5 + x, 4 + y] = Color('K');
            return s;
        }

        // ---------- Terrain ----------

        /// <summary>Tile frames by map character; water has two animation frames.</summary>
        public static Dictionary<char, PixelSprite[]> BuildTiles(int stage)
        {
            // Stage 2 is fought at dusk: same tiles, darker grass.
            (int r, int g, int b) grass = stage % 2 == 0 ? (33, 150, 50) : (24, 110, 60);
            return new Dictionary<char, PixelSprite[]>
            {
                ['.'] = new[] { Grass(grass, 1), Grass(grass, 2) },
                [','] = new[] { Path() },
                ['#'] = new[] { Wall() },
                ['C'] = new[] { Column(grass) },
                ['T'] = new[] { Tree(grass) },
                ['~'] = new[] { Water(0), Water(1) },
                ['='] = new[] { Bridge() },
                ['?'] = new[] { QuestionBlock() },
                ['f'] = new[] { Flowers(grass) },
                ['o'] = new[] { Rubble(grass) },
            };
        }

        private static PixelSprite Grass((int r, int g, int b) c, int seed)
        {
            var s = new PixelSprite(Tile, Tile);
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    double h = RaycasterArt.Hash(x, y, seed);
                    s[x, y] = h > 0.9 ? Color('G') : h < 0.12 ? Rgb(c.r / 2, c.g * 2 / 3, c.b / 2) : Rgb(c.r, c.g, c.b);
                }
            }
            return s;
        }

        private static PixelSprite Path()
        {
            var s = new PixelSprite(Tile, Tile);
            for (int y = 0; y < Tile; y++)
            {
                int row = y / 8, offset = row % 2 * 4;
                for (int x = 0; x < Tile; x++)
                {
                    int bx = (x + offset) % 8;
                    bool seam = y % 8 == 7 || bx == 7;
                    double h = RaycasterArt.Hash(x, y, 9);
                    s[x, y] = seam ? Rgb(150, 128, 80) : h > 0.85 ? Rgb(222, 205, 150) : Rgb(200, 180, 120);
                }
            }
            return s;
        }

        private static PixelSprite Wall()
        {
            var s = new PixelSprite(Tile, Tile);
            for (int y = 0; y < Tile; y++)
            {
                int row = y / 4, offset = row % 2 * 4;
                for (int x = 0; x < Tile; x++)
                {
                    bool seam = y % 4 == 3 || (x + offset) % 8 == 7;
                    bool top = y % 4 == 0;
                    s[x, y] = seam ? Rgb(64, 64, 76) : top ? Color('E') : Rgb(150, 150, 162);
                }
            }
            return s;
        }

        private static PixelSprite Column((int r, int g, int b) grass)
        {
            var s = Grass(grass, 3);
            s.Ellipse(8.5, 14, 7, 2.5, Rgb(20, 70, 30)); // shadow
            s.Fill(3, 1, 13, 2, Color('E'));
            s.Fill(4, 3, 12, 13, Color('E'));
            for (int x = 5; x <= 11; x += 2) s.Fill(x, 3, x, 13, Color('F'));
            s.Fill(4, 3, 12, 3, Color('k'));
            s.Fill(3, 14, 13, 15, Rgb(150, 150, 162));
            return s;
        }

        private static PixelSprite Tree((int r, int g, int b) grass)
        {
            var s = Grass(grass, 4);
            s.Ellipse(9, 13, 7, 3, Rgb(15, 60, 25));
            s.Fill(7, 11, 9, 15, Color('n'));
            s.Ellipse(8, 7, 7.5, 7, Rgb(16, 100, 36));
            s.Ellipse(7, 6, 5.5, 5, Color('g'));
            s.Ellipse(5.5, 4.5, 2.5, 2, Color('G'));
            return s;
        }

        private static PixelSprite Water(int frame)
        {
            var s = new PixelSprite(Tile, Tile);
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    int wave = (int)(Math.Sin((x + frame * 4) * 0.8) * 1.5);
                    bool crest = (y + wave + frame * 3) % 8 == 0;
                    s[x, y] = crest ? Color('B') : Color('b');
                }
            }
            return s;
        }

        private static PixelSprite Bridge()
        {
            var s = new PixelSprite(Tile, Tile);
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    uint c = y % 4 == 3 ? Color('n') : Color('o');
                    if ((x == 3 || x == 12) && y % 4 == 1) c = Color('k'); // nails
                    s[x, y] = c;
                }
            }
            return s;
        }

        private static PixelSprite QuestionBlock()
        {
            var s = new PixelSprite(Tile, Tile);
            s.Fill(0, 0, 15, 15, Rgb(150, 110, 60));
            s.Fill(0, 0, 15, 1, Color('Y'));
            s.Fill(0, 0, 1, 15, Color('Y'));
            s.Fill(0, 14, 15, 15, Color('n'));
            s.Fill(14, 0, 15, 15, Color('n'));
            string[] glyph = { ".YYY.", "Y...Y", "....Y", "...Y.", "..Y..", ".....", "..Y.." };
            for (int y = 0; y < glyph.Length; y++)
                for (int x = 0; x < 5; x++)
                    if (glyph[y][x] == 'Y') { s[6 + x, 4 + y] = Color('Y'); s[7 + x, 5 + y] = Color('K'); }
            for (int y = 0; y < glyph.Length; y++)
                for (int x = 0; x < 5; x++)
                    if (glyph[y][x] == 'Y') s[6 + x, 4 + y] = Color('Y');
            return s;
        }

        private static PixelSprite Flowers((int r, int g, int b) grass)
        {
            var s = Grass(grass, 5);
            foreach (var (x, y, c) in new[] { (3, 4, 'R'), (11, 3, 'Y'), (7, 9, 'F'), (13, 12, 'R'), (2, 12, 'Y') })
            {
                s[x, y] = Color(c);
                s[x - 1, y] = s[x + 1, y] = s[x, y - 1] = s[x, y + 1] = Color(c == 'F' ? 'Y' : 'F');
                s[x, y] = Color(c);
            }
            return s;
        }

        private static PixelSprite Rubble((int r, int g, int b) grass)
        {
            var s = Grass(grass, 6);
            s.Ellipse(5, 6, 3, 2.2, Rgb(150, 150, 162));
            s.Ellipse(11, 10, 3.5, 2.5, Rgb(130, 130, 140));
            s.Ellipse(6, 12, 2, 1.5, Color('E'));
            s[4, 5] = s[10, 9] = Color('F');
            return s;
        }
    }
}
