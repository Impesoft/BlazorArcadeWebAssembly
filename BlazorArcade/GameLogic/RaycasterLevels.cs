namespace BlazorArcade.GameLogic
{
    /// <summary>
    /// Hand-made maps for Crypt Crawler 3D.
    /// Walls: # stone, B red brick, M mossy stone, W wood, E exit switch (a wall you "use"), D door.
    /// Floor: . empty, P player start (facing east), g ghoul, h health, a ammo, t treasure,
    /// c pillar, b barrel, l hanging lamp.
    /// </summary>
    public static class RaycasterLevels
    {
        public sealed record Level(string Name, string[] Rows);

        public static readonly Level[] All =
        {
            new("The Crypt Gate", new[]
            {
                "########################",
                "#P.....#........t#.....#",
                "#......#..g......#..h..#",
                "#..l...D....c....D..g..#",
                "#......#.........#.....#",
                "#.a....#.t.....g.#..a..#",
                "###D####BBBBDBBBB###D###",
                "#......B.........B.....#",
                "#..g...B..c...c..B..t..#",
                "#......D....l....D.....#",
                "#.h....B..c...c..B..g..#",
                "#......B....g....B.....#",
                "####D##BBBBBBBBBBBMMDMMM",
                "#......#..........M....#",
                "#..t...D.....g....D....E",
                "########################",
            }),
            new("Mossy Halls", new[]
            {
                "WWWWWWWWWWWWWWWWWWWWWWWW",
                "WP.......W.......W..t..W",
                "W...h....W..g.g..W.....W",
                "W........D...l...D..g..W",
                "W..c..c..W.......W.....W",
                "W........W..a....W..h..W",
                "WWWWDWWWWWMMMDMMMWWWDWWW",
                "M........M.......M.....M",
                "M..g.....M..c.c..M..g..M",
                "M....l...D.......D.....M",
                "M..b.....M..g.g..M..a..M",
                "M........M.......M.....M",
                "MMMMDMMMMMMMMDMMMMMMDMMM",
                "#........#.......#.....#",
                "#..t..g..#...g...#..t..#",
                "#........D...c...D.....#",
                "#..h.....#.g...g.#..g..#",
                "#.....a..#.......#.....#",
                "#........#...t...#.....E",
                "########################",
            }),
            new("The Ossuary", new[]
            {
                "BBBBBBBBBBBBBBBBBBBBBBEB",
                "BP.....B...............B",
                "B..h...B..c...c...c....B",
                "B......D......g........B",
                "B..a...B..c...c...c.g..B",
                "B......B.....l.....l...B",
                "BBBDBBBB..g.........g..B",
                "#......B..c...c...c....B",
                "#..g...B......t.....g..B",
                "#......D..c...c...c....B",
                "#.t....B.....g......t..B",
                "#..h...B..c...c...c....B",
                "#......B.g...l.....l.g.B",
                "#..a...B...............B",
                "#......B.....h.....a...B",
                "BBBBBBBBBBBBBBBBBBBBBBBB",
            }),
        };
    }
}
