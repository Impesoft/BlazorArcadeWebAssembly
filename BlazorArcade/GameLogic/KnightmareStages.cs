using System.Collections.Generic;
using System.Linq;

namespace BlazorArcade.GameLogic
{
    /// <summary>
    /// Knightmare stages, assembled from 16-column map segments. Each segment is written top row
    /// first, and a stage lists its segments in the order the player meets them (bottom to top).
    /// Terrain: . grass  , path  # wall  C column  T tree  ~ water  = bridge  ? power-up block
    ///          f flowers  o rubble.
    /// Spawns (on grass): b bat  s skeleton  k knight  w wisp.
    /// </summary>
    public static class KnightmareStages
    {
        public sealed record Stage(string Name, string[] Rows);

        private static readonly Dictionary<string, string[]> Segments = new()
        {
            ["start"] = new[]
            {
                "................",
                "..f.......o.....",
                "......,,,,......",
                "......,,,,....f.",
                ".T....,,,,......",
                "......,,,,..T...",
                "..o...,,,,......",
                "......,,,,......",
                ".f....,,,,...o..",
                "......,,,,......",
                "..T...,,,,....T.",
                "......,,,,......",
            },
            ["meadow"] = new[]
            {
                "...b........b...",
                "................",
                ".T....,,,,...?..",
                "......,,,,......",
                "..b...,,,,..b...",
                "......,,,,......",
                "..?...,,,,....T.",
                "......,,,,......",
                "....s.,,,,.s....",
                "......,,,,......",
            },
            ["river"] = new[]
            {
                "....b......b....",
                "..T..........T..",
                "~~~~~~===~~~~~~~",
                "~~~~~~===~~~~~~~",
                "~~~~~~===~~~~~~~",
                "......,,,.......",
                "...o..,,,...f...",
                "......,,,.......",
            },
            ["twinBridge"] = new[]
            {
                "..s..........s..",
                "................",
                "~~==~~~~~~~~==~~",
                "~~==~~~~~~~~==~~",
                "~~==~~~?~~~~==~~",
                "~~==~~~~~~~~==~~",
                "................",
                "..w..........w..",
            },
            ["ruins"] = new[]
            {
                "##....,,,,....##",
                "##.b..,,,,..b.##",
                "#.....,,,,.....#",
                "#..C..,,,,..C..#",
                "#.....,,,,.....#",
                "#..C..,,,,..C..#",
                "#..s..,,,,..s..#",
                "####..,,,,..####",
                "......,,,,......",
                "..?..........?..",
                "................",
            },
            ["columns"] = new[]
            {
                "..C....,,....C..",
                ".......,,.......",
                "..C..b.,,.b..C..",
                ".......,,.......",
                "..C..?.,,.?..C..",
                ".......,,.......",
                "..C....,,....C..",
                ".....k.,,.......",
                "..C....,,....C..",
                ".......,,.......",
            },
            ["forest"] = new[]
            {
                "TT..TTT...TT..TT",
                "T......T.......T",
                "...T..b....T....",
                "TT....TT.T....TT",
                "....T.......T...",
                "T..w....T......T",
                "..TT..T....TT...",
                "................",
            },
            ["gate"] = new[]
            {
                "......,,,,......",
                "#######,,#######",
                "#..k...,,....k.#",
                "#......,,......#",
                "###....,,....###",
                "..#..?.,,.?..#..",
                "..#....,,....#..",
                "................",
            },
            ["swarm"] = new[]
            {
                ".b..b..b..b..b..",
                "................",
                "...w........w...",
                "......,,,,......",
                "..f...,,,,...o..",
                "......,,,,......",
                ".T..s.,,,,.s..T.",
                "......,,,,......",
            },
            ["arena"] = new[]
            {
                "################",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,C,,,,,,,,C,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,C,,,,,,,,C,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "#,,,,,,,,,,,,,,#",
                "......,,,,......",
                "......,,,,......",
            },
        };

        public static readonly Stage[] All =
        {
            Build("The Greek Ruins", "start", "meadow", "river", "ruins", "columns", "meadow", "forest", "river", "gate", "columns", "swarm", "arena"),
            Build("River of Dusk", "start", "swarm", "twinBridge", "forest", "gate", "river", "ruins", "twinBridge", "columns", "forest", "swarm", "gate", "arena"),
        };

        private static Stage Build(string name, params string[] segmentsBottomToTop) =>
            new(name, segmentsBottomToTop.Reverse().SelectMany(s => Segments[s]).ToArray());
    }
}
