// Nemesis stages. The screen is 24 rows of 8-pixel tiles; a stage is a run of segments, each giving
// the floor and ceiling height in tiles at its start and end (interpolated in between) and the
// things that appear in it, by column offset:
//   fans y      a formation of five; shoot them all for a capsule
//   jet n       n jets at random heights        red y     a red jet that carries a capsule
//   turret F|C  a gun on the floor or ceiling   ducker    a walker on the floor
//   volcano h   raises the floor into a cone h tiles high that throws rocks
//   moai F|C    a stone head firing rings       hatch F|C a hangar that launches drones
// Things appear as their column scrolls in at the right edge, so a stage opens with a quiet
// screen's width (32 columns). When the scroll reaches the end, Big Core arrives.

const TILE = 8;

function build(name, theme, segments) {
    const floor = [], ceil = [], events = [];
    for (const [length, [f0, f1], [c0, c1], items = []] of segments) {
        const start = floor.length;
        for (let i = 0; i < length; i++) {
            const t = length === 1 ? 0 : i / (length - 1);
            floor.push(Math.round(f0 + (f1 - f0) * t));
            ceil.push(Math.round(c0 + (c1 - c0) * t));
        }
        for (const [offset, kind, param] of items) events.push({ x: (start + offset) * TILE, kind, param });
    }

    for (const e of events) {
        if (e.kind !== 'volcano') continue;
        const col = e.x / TILE, base = floor[col];
        for (let dx = -e.param; dx <= e.param; dx++) {
            if (col + dx >= 0 && col + dx < floor.length) floor[col + dx] = Math.max(floor[col + dx], base + e.param - Math.abs(dx));
        }
    }

    events.sort((a, b) => a.x - b.x);
    return { name, theme, floor, ceil, events };
}

export const STAGES = [
    build('Volcano', 0, [
        [56, [2, 2], [0, 0], [[36, 'fans', 60], [50, 'fans', 120]]],
        [30, [2, 4], [0, 0], [[10, 'turret', 'F'], [20, 'jet', 3]]],
        [40, [4, 3], [0, 0], [[8, 'ducker'], [20, 'fans', 50], [30, 'red', 90]]],
        [50, [3, 3], [0, 0], [[22, 'volcano', 8]]],
        [40, [3, 2], [0, 2], [[10, 'turret', 'F'], [15, 'turret', 'C'], [30, 'fans', 80]]],
        [50, [2, 2], [2, 2], [[22, 'volcano', 7], [42, 'red', 60]]],
        [40, [2, 3], [2, 3], [[10, 'ducker'], [20, 'jet', 4], [30, 'turret', 'C']]],
        [30, [3, 2], [3, 0], [[10, 'fans', 96], [25, 'red', 40]]],
        [60, [2, 2], [0, 0]],
    ]),
    build('Moai', 1, [
        [52, [2, 2], [0, 0], [[36, 'fans', 70], [48, 'red', 110]]],
        [30, [2, 5], [0, 0], [[20, 'moai', 'F']]],
        [40, [5, 5], [0, 3], [[6, 'moai', 'F'], [18, 'moai', 'C'], [30, 'fans', 100]]],
        [30, [5, 3], [3, 3], [[10, 'jet', 3], [24, 'moai', 'C']]],
        [40, [3, 3], [3, 5], [[8, 'moai', 'F'], [20, 'ducker'], [30, 'moai', 'C']]],
        [30, [3, 4], [5, 2], [[10, 'fans', 90], [24, 'red', 90]]],
        [40, [4, 4], [2, 2], [[6, 'moai', 'F'], [16, 'moai', 'C'], [26, 'moai', 'F'], [34, 'turret', 'C']]],
        [30, [4, 2], [2, 0], [[10, 'jet', 4]]],
        [60, [2, 2], [0, 0]],
    ]),
    build('Fortress', 2, [
        [52, [2, 2], [2, 2], [[36, 'fans', 96], [44, 'turret', 'F'], [48, 'turret', 'C']]],
        [30, [2, 6], [2, 2], [[10, 'hatch', 'F'], [22, 'red', 60]]],
        [40, [6, 6], [2, 6], [[8, 'turret', 'F'], [16, 'turret', 'C'], [26, 'hatch', 'C'], [34, 'jet', 3]]],
        [30, [6, 4], [6, 4], [[12, 'ducker'], [20, 'fans', 96]]],
        [40, [4, 4], [4, 4], [[6, 'hatch', 'F'], [14, 'hatch', 'C'], [28, 'turret', 'F'], [34, 'red', 96]]],
        [30, [4, 7], [4, 2], [[12, 'turret', 'C'], [20, 'jet', 4]]],
        [40, [7, 3], [2, 3], [[10, 'fans', 80], [24, 'hatch', 'F'], [30, 'turret', 'C']]],
        [60, [2, 2], [0, 0]],
    ]),
];
