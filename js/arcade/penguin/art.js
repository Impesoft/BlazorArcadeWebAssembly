// Penguin Adventure sprites, drawn as text in the MSX palette (see shared/msx.js).
import { parse, color } from '../shared/msx.js';
import { rgb } from '../shared/pixel-art.js';

export { color, rgb };

/** Re-colours a text sprite before parsing it, e.g. { E: 'c' } turns grey stone into ice. */
const recolor = (rows, map) => parse(...rows.map(row => [...row].map(c => map[c] ?? c).join('')));

// Penta from behind.
const PENTA_BODY = [
    '......kkkk......',
    '....kkKKKKkk....',
    '...kKKbKKKKKk...',
    '...kKbKKKKKKk...',
    '...kKKKKKKKKk...',
    '....kKKKKKKk....',
    '...kKKKKKKKKk...',
    '..kKKKKKKKKKKk..',
];

export const penta = {
    run: [
        parse(...PENTA_BODY,
            '.kKkKKKKKKKKkKk.',
            'kKk.kKKKKKKk.kKk',
            'kk..kKKKKKKk..kk',
            '....kKKKKKKk....',
            '....kKKKKKKk....',
            '.....kKKKKk.....',
            '......kkkk......',
            '.....oo..ooo....',
            '.....oo.........'),
        parse(...PENTA_BODY,
            '.kKkKKKKKKKKkKk.',
            '.kk.kKKKKKKk.kk.',
            '.k..kKKKKKKk..k.',
            '....kKKKKKKk....',
            '....kKKKKKKk....',
            '.....kKKKKk.....',
            '......kkkk......',
            '....ooo..oo.....',
            '.........oo.....'),
    ],
    jump: parse(
        '......kkkk......',
        'kk..kkKKKKkk..kk',
        'kKk.kKbKKKKk.kKk',
        '.kKkKbKKKKKKkKk.',
        '..kKKKKKKKKKKk..',
        '....kKKKKKKk....',
        '...kKKKKKKKKk...',
        '..kKKKKKKKKKKk..',
        '..kKKKKKKKKKKk..',
        '...kKKKKKKKKk...',
        '....kKKKKKKk....',
        '....kKKKKKKk....',
        '.....kKKKKk.....',
        '......kkkk......',
        '.....oo..oo.....'),
};

export const stars = parse(
    'y.....y',
    '.......',
    '...y...');

const SEAL = [
    '.....kEEEk......',
    '....kEEEEEk.....',
    '....kEKEKEk.....',
    '....kEEkEEk.....',
    '.....kEEEk......',
    '....kEEEEEk.....',
    '...kEEEFFEEk....',
    '...kEEFFFFEk....',
    '..kEEEFFFFEEk...',
    '..kEEEFFFFEEk...',
    '.kkEEEEFFEEEkk..',
    'kEkkEEEEEEEEkEk.',
    '.....kkkkkk.....',
];
export const seal = parse(...SEAL);

const ROCK = [
    '......kkkk......',
    '....kkEEEEkk....',
    '...kEEFEEEEEk...',
    '..kEEFEEEEEEEk..',
    '.kEEEEEEEEkEEEk.',
    '.kEEEEEEEkEEEEk.',
    'kEEEEEEEEEEEkEEk',
    'kEEEkEEEEEEEEEEk',
    '.kkkkkkkkkkkkkk.',
];
const STUMP = [
    '..nnnnnnnnnnnn..',
    '.noooooooooooon.',
    '.nonnnnnnnnnnon.',
    '.noooooooooooon.',
    '..nnnnnnnnnnnn..',
    '..nnonnnnnnonn..',
    '..nnonnnnnnonn..',
    '.nnnonnnnnnonnn.',
    'nnnnnnnnnnnnnnnn',
];
/** Rock by theme: an ice block, a tree stump, an ice block, a boulder. */
export const rock = [
    recolor(ROCK, { E: 'c', k: 'b' }),
    parse(...STUMP),
    recolor(ROCK, { E: 'c', k: 'b' }),
    recolor(ROCK, { E: 'm', F: 'p' }),
];

const FISH = [
    '....cccc...c',
    '..ccFccccccc',
    '.cKcccccccc.',
    '..cccccc.ccc',
    '....cccc...c',
];
export const fish = parse(...FISH);
export const goldFish = recolor(FISH, { c: 'y', F: 'F' });

const BAT = [
    'k..............k',
    'kk....k..k....kk',
    'kmk...kkkk...kmk',
    'kmmk.kKrrKk.kmmk',
    '.kmmkKKKKKKkmmk.',
    '..kmmkKKKKkmmk..',
    '...kk.kKKk.kk...',
    '.......kk.......',
];
const BAT_UP = [
    '.......kk.......',
    '.kk..k.kk.k..kk.',
    'kmmk.kkkkkk.kmmk',
    'kmmmkKrKKrKkmmmk',
    'kmmkKKKKKKKKkmmk',
    '.kk..kKKKKk..kk.',
    '......kKKk......',
    '.......kk.......',
];
/** Bats in the cave, birds in the forest. */
export const bat = [[parse(...BAT), parse(...BAT_UP)], [recolor(BAT, { m: 'n', r: 'y' }), recolor(BAT_UP, { m: 'n', r: 'y' })]];

const PINE = [
    '.......FF.......',
    '......gFFg......',
    '.....gGGGGg.....',
    '......gGGg......',
    '.....gFGGFg.....',
    '....gGGGGGGg....',
    '...gGGGGGGGGg...',
    '.....gGGGGg.....',
    '....gFGGGGFg....',
    '...gGGGGGGGGg...',
    '..gGGGGGGGGGGg..',
    '.gGGGGGGGGGGGGg.',
    '....gGGGGGGg....',
    '...gFGGGGGGFg...',
    '..gGGGGGGGGGGg..',
    '.gGGGGGGGGGGGGg.',
    'gGGGGGGGGGGGGGGg',
    'gggggggggggggggg',
    '......nnnn......',
    '......nnnn......',
    '......nnnn......',
];
const SPIKE = [
    '.......c........',
    '......cFc.......',
    '......cFc.......',
    '.....cFcBc......',
    '.....cFcBc......',
    '....cFFcBBc.....',
    '....cFcBBBc.....',
    '...cFFcBBBBc....',
    '...cFcBBBBBc....',
    '..cFFcBBBBBBc...',
    '..cFcBBBBBBBc...',
    '.cFFcBBBBBBBBc..',
    '.cFcBBBBBBBBBc..',
    'cFFcBBBBBBBBBBc.',
    'cccccccccccccccc',
];
const BERG = [
    '........cccc............',
    '......ccFFFFcc..........',
    '....ccFFFFFFFFcc..cc....',
    '...cFFFFFFFFFFFFccFFcc..',
    '..cFFFFFFFFFFFFFFFFFFFc.',
    '.cFFFFFFFFFFFFFFFFFFFFFc',
    'cBBBBBBBBBBBBBBBBBBBBBBc',
];
const STALAGMITE = [
    '.....kk.....',
    '.....kE.....',
    '....kEE.....',
    '....kEEk....',
    '....kEEk....',
    '...kEEEEk...',
    '...kEkEEk...',
    '...kEEEEk...',
    '..kEEEkEEk..',
    '..kEEEEEEk..',
    '..kEkEEEEEk.',
    '.kEEEEEkEEk.',
    '.kEEEEEEEEEk',
    'kEEEkEEEEEEk',
    'kkkkkkkkkkkk',
];
/** Roadside scenery by theme, with how wide it stands in road half-widths. */
export const scenery = [
    { sprite: parse(...SPIKE), width: 0.7 },
    { sprite: parse(...PINE), width: 1.1 },
    { sprite: parse(...BERG), width: 1.6 },
    { sprite: recolor(STALAGMITE, { E: 'm' }), width: 0.7 },
];

/** Ground, sky and edge colours for each theme. */
export const themes = [
    { // Ice Field
        sky: [rgb(84, 140, 250), rgb(190, 220, 255)], hills: [rgb(235, 245, 255), rgb(170, 200, 240)],
        road: [color('F'), rgb(225, 238, 252)], edge: [color('b'), color('F')], verge: [rgb(150, 200, 250), rgb(135, 186, 242)],
        pit: [rgb(20, 40, 110), rgb(60, 100, 190)],
    },
    { // Forest
        sky: [rgb(60, 120, 230), rgb(170, 210, 255)], hills: [rgb(30, 110, 50), rgb(20, 80, 40)],
        road: [color('F'), rgb(225, 238, 252)], edge: [color('n'), color('o')], verge: [color('g'), rgb(28, 150, 52)],
        pit: [rgb(40, 30, 20), rgb(100, 70, 40)],
    },
    { // Ice Floes
        sky: [rgb(40, 90, 200), rgb(150, 200, 255)], hills: [rgb(220, 235, 255), rgb(150, 190, 240)],
        road: [color('F'), rgb(215, 232, 250)], edge: [color('c'), color('F')], verge: [rgb(30, 60, 170), rgb(25, 52, 150)],
        pit: [rgb(30, 60, 180), rgb(80, 130, 230)],
    },
    { // Ice Cave
        sky: [rgb(0, 0, 0), rgb(30, 10, 40)], hills: [rgb(60, 30, 70), rgb(40, 20, 50)],
        road: [rgb(150, 170, 210), rgb(135, 155, 198)], edge: [color('m'), color('B')], verge: [rgb(50, 35, 60), rgb(42, 30, 52)],
        pit: [rgb(0, 0, 0), rgb(40, 30, 60)],
    },
];
