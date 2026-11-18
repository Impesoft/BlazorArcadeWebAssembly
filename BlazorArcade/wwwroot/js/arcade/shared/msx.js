// The MSX1 (TMS9918) palette shared by the MSX tributes, and a parser for sprites drawn as text:
// one palette letter per pixel, '.' for transparent.
import { Sprite, rgb } from './pixel-art.js';

// TMS9918 palette, plus a skin tone and a brown the MSX would have dithered.
const PALETTE = {
    K: rgb(0, 0, 0),
    k: rgb(60, 60, 72),
    E: rgb(204, 204, 204),
    F: rgb(255, 255, 255),
    r: rgb(212, 82, 77),
    R: rgb(252, 85, 84),
    p: rgb(255, 121, 120),
    y: rgb(212, 193, 84),
    Y: rgb(230, 206, 128),
    g: rgb(33, 176, 59),
    G: rgb(94, 220, 120),
    b: rgb(84, 85, 237),
    B: rgb(125, 118, 252),
    c: rgb(66, 235, 245),
    m: rgb(201, 91, 186),
    n: rgb(120, 70, 30),
    o: rgb(200, 120, 40),
    s: rgb(240, 190, 150),
};

export const color = c => PALETTE[c];

export function parse(...rows) {
    const width = Math.max(...rows.map(r => r.length));
    const sprite = new Sprite(width, rows.length);
    rows.forEach((row, y) => [...row].forEach((c, x) => { if (c !== '.') sprite.set(x, y, PALETTE[c]); }));
    return sprite;
}
