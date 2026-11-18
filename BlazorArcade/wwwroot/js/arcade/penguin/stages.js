// Penguin Adventure stages. Each stage is a road of `length` metres with bends, laid out from a
// seed so every run of a stage is the same. Lateral positions are in road half-widths: the road
// runs from -1 to 1 and the verge from there to about 1.35.
//
// Things on the road, by kind:
//   Hole      an ice hole; fall in and climbing out costs time     (x, r: half-width)
//   Crevasse  a crack across the whole road; jump it                (len: metres)
//   Water     open sea between floes, the same as a crevasse        (len)
//   Seal      bobs up out of a hole: a hole when down, a wall when up
//   Rock      trip over it unless you jump
//   Bat       swoops up and down and side to side
//   Fish      100 points; a GoldFish is worth 1000 and five seconds  (z: height above the road)

export const Kind = {
    Hole: 'Hole', Crevasse: 'Crevasse', Water: 'Water', Seal: 'Seal', Rock: 'Rock',
    Bat: 'Bat', Fish: 'Fish', GoldFish: 'GoldFish',
};

export const Theme = { Ice: 0, Forest: 1, Floes: 2, Cave: 3 };

export const STAGES = [
    { name: 'Ice Field', theme: Theme.Ice, length: 2200, seed: 11, weights: { hole: 4, holes: 2, crevasse: 3, seal: 3, rock: 2, fish: 3 } },
    { name: 'Forest', theme: Theme.Forest, length: 2500, seed: 23, weights: { hole: 3, holes: 2, crevasse: 2, seal: 1, rock: 4, bat: 2, fish: 3 } },
    { name: 'Ice Floes', theme: Theme.Floes, length: 2600, seed: 37, weights: { hole: 2, holes: 2, crevasse: 5, seal: 4, rock: 1, fish: 4 } },
    { name: 'Ice Cave', theme: Theme.Cave, length: 2800, seed: 53, weights: { hole: 3, holes: 3, crevasse: 3, seal: 2, rock: 2, bat: 5, fish: 3 } },
];

/** Room to keep clear before and after a crevasse, so there's space to line up and to land. */
const CREVASSE_RUNUP = 16, CREVASSE_LANDING = 10;

/** A seeded random generator (mulberry32). */
export function random(seed) {
    let a = seed >>> 0;
    return () => {
        a = (a + 0x6D2B79F5) >>> 0;
        let t = a;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

/**
 * Lays out stage `index` for its `loop`-th time round (0 the first time): bends, hazards, fish
 * and roadside scenery. Later loops are longer, busier and allow less spare time.
 */
export function buildStage(index, loop = 0) {
    const def = STAGES[index];
    const rnd = random(def.seed + loop * 7919);
    const range = (lo, hi) => lo + rnd() * (hi - lo);
    const length = def.length + loop * 300;

    // Bends: straights and curves, eased in and out so the road never kinks.
    const curve = new Float32Array(length + 200);
    for (let s = 200; s < length - 150;) {
        const straight = Math.round(range(60, 160));
        const bend = Math.round(range(120, 260));
        const strength = range(0.005, 0.012) * (rnd() < 0.5 ? -1 : 1);
        const start = s + straight;
        for (let i = 0; i < bend && start + i < length; i++) {
            const t = i / bend;
            curve[start + i] = strength * Math.sin(Math.PI * t);
        }
        s = start + bend;
    }

    const objects = [];
    const add = (s, kind, props = {}) => objects.push({ s, kind, x: 0, ...props });
    const lane = (margin = 0.75) => range(-margin, margin);
    const kinds = Object.entries(def.weights);
    const total = kinds.reduce((sum, [, w]) => sum + w, 0);
    const pick = () => {
        let r = rnd() * total;
        for (const [kind, w] of kinds) if ((r -= w) < 0) return kind;
        return kinds[0][0];
    };

    const gapScale = 1 / (1 + loop * 0.2);
    let lastCrevasse = -Infinity;
    for (let s = 160; s < length - 120; s += range(16, 32) * gapScale) {
        let kind = pick();
        // Crevasses need clear road either side; anything else must stay out of a landing zone.
        if (kind === 'crevasse' && s - lastCrevasse < 70) kind = 'hole';
        if (s - lastCrevasse < CREVASSE_LANDING + 4) s = lastCrevasse + CREVASSE_LANDING + 4;

        switch (kind) {
            case 'hole':
                add(s, Kind.Hole, { x: lane(), r: range(0.2, 0.3), depth: 1.4 });
                break;
            case 'holes': {
                // Two holes with a gap between or beside them that is always wide enough to pass.
                const gapAt = lane(0.6);
                add(s, Kind.Hole, { x: Math.max(-0.85, gapAt - 0.55), r: 0.22, depth: 1.4 });
                add(s, Kind.Hole, { x: Math.min(0.85, gapAt + 0.55), r: 0.22, depth: 1.4 });
                break;
            }
            case 'crevasse': {
                s += CREVASSE_RUNUP;
                const len = range(2.6, 3.6);
                add(s, def.theme === 2 ? Kind.Water : Kind.Crevasse, { len });
                // Fish hanging over the gap reward a well-timed jump.
                if (rnd() < 0.5) add(s + len / 2, Kind.Fish, { x: lane(0.5), z: 0.3 });
                lastCrevasse = s + len;
                s += len + CREVASSE_LANDING;
                break;
            }
            case 'seal':
                add(s, Kind.Seal, { x: lane(0.7), r: 0.2, depth: 1.4, phase: rnd() * 3 });
                break;
            case 'rock':
                add(s, Kind.Rock, { x: lane(0.8) });
                break;
            case 'bat':
                add(s, Kind.Bat, { x: lane(0.4), phase: rnd() * 6, speed: range(1.5, 2.5) });
                break;
            case 'fish': {
                const x = lane(0.7), count = 1 + Math.floor(rnd() * 3);
                const gold = rnd() < 0.12;
                for (let i = 0; i < count; i++) add(s + i * 4, gold && i === 0 ? Kind.GoldFish : Kind.Fish, { x, z: 0 });
                s += count * 4;
                break;
            }
        }
    }
    objects.sort((a, b) => a.s - b.s);

    // Roadside scenery, purely for show.
    const scenery = [];
    for (let s = 20; s < length + 150; s += range(10, 24)) {
        const side = rnd() < 0.5 ? -1 : 1;
        scenery.push({ s, x: side * range(1.5, 3.2), variant: Math.floor(rnd() * 2) });
    }

    // Par: the time it takes at nearly full speed, plus some slack that shrinks every loop.
    const time = Math.round(length / 22.5 + 8 - loop * 3);
    return { name: def.name, theme: def.theme, length, time, curve, objects, scenery };
}
