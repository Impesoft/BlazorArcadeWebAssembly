// Pixel art for Nemesis in the MSX1 (TMS9918) palette: sprites are drawn as text, terrain tiles
// and Big Core are generated.
import { Sprite, hash } from '../shared/pixel-art.js';
import { color, parse } from '../shared/msx.js';

export { color };

const flipV = s => {
    const out = new Sprite(s.width, s.height);
    for (let y = 0; y < s.height; y++) out.pixels.set(s.pixels.subarray(y * s.width, (y + 1) * s.width), (s.height - 1 - y) * s.width);
    return out;
};

// ---------- The Vic Viper and its options ----------

export const ship = [
    parse(
        '..EE............',
        '..EbE...........',
        '.EEbbEEEEEE.....',
        'rEEEEEEEEEEEEE..',
        'oEEEEEbbbbEEEEEE',
        '.EEEEEEEEEEEE...',
        '..EbE...........',
        '..EE............',
    ),
    parse(
        '..EE............',
        '..EbE...........',
        '.EEbbEEEEEE.....',
        'YEEEEEEEEEEEEE..',
        'rEEEEEbbbbEEEEEE',
        '.EEEEEEEEEEEE...',
        '..EbE...........',
        '..EE............',
    ),
];

export const option = [
    parse('..oooo..', '.oYYYYo.', 'oYFFYYYo', 'oYFYYYYo', 'oYYYYYro', 'oYYYYrro', '.orrrro.', '..oooo..'),
    parse('..rrrr..', '.roooor.', 'roYYooor', 'roYoooor', 'rooooorr', 'roooorrr', '.rrrrrr.', '..rrrr..'),
];

export const missile = parse('.YY.', 'YFFY', 'YFFY', '.YY.');

export const capsule = [
    parse('.kkkkkk.', 'kRRRRRRk', 'kRpFFpRk', 'kRpFpRRk', 'kRRRRRRk', 'kRRRRRRk', 'kkRRRRkk', '.kkkkkk.'),
    parse('.kkkkkk.', 'krrrrrrk', 'krRpprrk', 'krRprrrk', 'krrrrrrk', 'krrrrrrk', 'kkrrrrkk', '.kkkkkk.'),
];

// ---------- Enemies ----------

export const fan = [
    parse('...cc...', '..cFFc..', '.cFbbFc.', 'cFbKKbFc', 'cFbKKbFc', '.cFbbFc.', '..cFFc..', '...cc...'),
    parse('c......c', '.cF..Fc.', '..cbbc..', '..bKKb..', '..bKKb..', '..cbbc..', '.cF..Fc.', 'c......c'),
];

export const jet = parse(
    '..........kk....',
    '.........kEEk...',
    '....kkkkkEEEEkk.',
    '.kkEEEEEEEEEEEEk',
    'kEEbbEEEEEEEEEEk',
    '.kkEEEEEEEEEEEEk',
    '....kkkkkEEEEkk.',
    '.........kkkk...',
);

export const redJet = jet.tinted(c => (c === color('E') ? color('R') : c === color('b') ? color('Y') : c));

export const mini = [
    parse('..mmmm..', '.mmFFmm.', 'mmFmmFmm', 'mmmmmmmm', 'mmmmmmmm', 'mmFmmFmm', '.mmFFmm.', '..mmmm..'),
    parse('..mmmm..', '.mmmmmm.', 'mmmFFmmm', 'mmFmmFmm', 'mmFmmFmm', 'mmmFFmmm', '.mmmmmm.', '..mmmm..'),
];

const turretFloor = parse('........', '........', '...EE...', '..EEEE..', '.EbbbbE.', '.EbEEbE.', 'kkkkkkkk', 'kEEEEEEk');
export const turret = { floor: turretFloor, ceiling: flipV(turretFloor) };

export const ducker = [
    parse('..mmmm..', '.mFmmFm.', '.mmmmmm.', '..mmmm..', '...mm...', '..m..m..', '.m....m.', 'mm....mm'),
    parse('..mmmm..', '.mFmmFm.', '.mmmmmm.', '..mmmm..', '...mm...', '...mm...', '..m..m..', '..m..m..'),
];

export const rock = parse('..nnnn..', '.nooonn.', 'noooonnn', 'nooonnnn', 'nnonnnnk', 'nnnnnnnk', '.nnnnkk.', '..kkkk..');

export const crater = parse('.R.YY.R.', 'RoYRRYoR');

const MOAI = [
    '....EEEEEEEE....',
    '...EEEEEEEEEE...',
    '..EEEEEEEEEEEE..',
    '..EkkkkkkkkkEE..',
    '..EEEEEEEEEEEE..',
    '.EEkkEEEEkkEEE..',
    '.EEkkEEEEkkEEE..',
    '.EEEEEEEEEEEEE..',
    'EEEEEEEEEEEEEE..',
    'EEEEEEEEEEEEE...',
    '.EEEEEEEEEEEE...',
    '.EEEEEEEEEEEE...',
    '..kkkkkkkEEEE...',
    '..EEEEEEEEEEE...',
    '...EEEEEEEEEE...',
    '...EEEEEEEEEE...',
    '....EEEEEEEEE...',
    '....EEEEEEEEE...',
    '...EEEEEEEEEEE..',
    '..EEEEEEEEEEEEE.',
    '..EEEEEEEEEEEEE.',
    '.EEEEEEEEEEEEEEE',
    '.EEEEEEEEEEEEEEE',
    'EEEEEEEEEEEEEEEE',
];
const moaiOpen = MOAI.map((row, y) => (y === 12 ? '..kkkkkkkEEEE...' : y === 13 ? '..kRRRRkkEEEE...' : row));
export const moai = {
    floor: [parse(...MOAI), parse(...moaiOpen)],
    ceiling: [flipV(parse(...MOAI)), flipV(parse(...moaiOpen))],
};

const hatchFloor = parse(
    '....kkkkkkkk....',
    '..kkEEEEEEEEkk..',
    '.kEEbbbbbbbbEEk.',
    'kEEbbBBBBBBbbEEk',
    'kEEEEEEEEEEEEEEk',
    'kkkkkkkkkkkkkkkk',
    'kEkEkEkEkEkEkEkE',
    'kkkkkkkkkkkkkkkk',
);
export const hatch = { floor: hatchFloor, ceiling: flipV(hatchFloor) };

/** Big Core: a grey hull with swept wings around a glowing blue core. */
export const boss = (() => {
    const s = new Sprite(48, 32);
    const hull = color('E'), shade = color('k'), dark = color('K');
    s.fill(20, 2, 44, 7, shade);
    s.fill(20, 24, 44, 29, shade);
    s.fill(24, 3, 46, 6, hull);
    s.fill(24, 25, 46, 28, hull);
    s.ellipse(30, 16, 16, 11, (x, y, d) => (d > 0.8 ? shade : hull));
    s.fill(4, 13, 30, 18, hull);
    s.fill(4, 15, 30, 16, shade);
    s.ellipse(22, 16, 6, 6, dark);
    s.ellipse(22, 16, 4.5, 4.5, (x, y, d) => (d < 0.3 ? color('F') : d < 0.6 ? color('c') : color('B')));
    return s;
})();

export const enemyBullet = parse('.pp.', 'pFFp', 'pFFp', '.pp.');
export const ring = parse('.cccc.', 'cc..cc', 'c....c', 'c....c', 'cc..cc', '.cccc.');

export const explosion = [0, 1, 2, 3].map(frame => {
    const s = new Sprite(16, 16);
    const r = 3 + frame * 1.7;
    s.ellipse(8, 8, r, r, color(frame < 2 ? 'R' : 'r'));
    s.ellipse(8, 8, r * 0.65, r * 0.65, color(frame < 2 ? 'Y' : 'o'));
    if (frame >= 2) s.ellipse(8, 8, r * 0.35, r * 0.35, 0);
    for (let i = 0; i < 6; i++) s.set(Math.floor(hash(i, frame, 7) * 16), Math.floor(hash(frame, i, 11) * 16), color('Y'));
    return s;
});

// ---------- Terrain ----------

/** Colours per stage theme: surface, body, speckle. */
const THEMES = [
    ['G', 'g', 'n'], // volcano: grass over earth
    ['Y', 'y', 'n'], // moai: sandstone
    ['B', 'b', 'E'], // fortress: metal plates
];

/** Eight-by-eight tiles for a theme: the body, the floor's top edge, and the ceiling's bottom edge. */
export function buildTiles(theme) {
    const [surface, body, speckle] = THEMES[theme].map(color);
    const fill = new Sprite(8, 8);
    for (let y = 0; y < 8; y++) {
        for (let x = 0; x < 8; x++) {
            const plate = theme === 2 && (x === 0 || y === 0);
            fill.set(x, y, plate || hash(x, y, theme + 3) > 0.82 ? speckle : body);
        }
    }
    const top = new Sprite(8, 8);
    top.pixels.set(fill.pixels);
    for (let x = 0; x < 8; x++) {
        const depth = 2 + (hash(x, 0, theme + 9) > 0.6 ? 1 : 0);
        for (let y = 0; y < depth; y++) top.set(x, y, surface);
    }
    return { fill, top, bottom: flipV(top) };
}
