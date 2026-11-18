// Pixel helpers shared by the arcade's procedural art. Colours are little-endian ABGR words,
// so they can be written straight into a canvas framebuffer; 0 means transparent.

export function rgb(r, g, b) {
    const R = Math.min(255, Math.max(0, r | 0));
    const G = Math.min(255, Math.max(0, g | 0));
    const B = Math.min(255, Math.max(0, b | 0));
    return (0xFF000000 | (B << 16) | (G << 8) | R) >>> 0;
}

export const red = c => c & 0xFF;
export const green = c => (c >>> 8) & 0xFF;
export const blue = c => (c >>> 16) & 0xFF;

/** Deterministic 0..1 hash of a lattice point. */
export function hash(x, y, seed) {
    let h = (Math.imul(x, 374761393) + Math.imul(y, 668265263) + Math.imul(seed, 2147483647)) >>> 0;
    h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return (h & 0xFFFFFF) / 0xFFFFFF;
}

/** Smooth value noise that tiles every `period` pixels. */
export function noise(x, y, cell, seed, period = 64) {
    const cells = period / cell;
    const fx = x / cell, fy = y / cell;
    const x0 = Math.floor(fx), y0 = Math.floor(fy);
    let tx = fx - x0, ty = fy - y0;
    tx = tx * tx * (3 - 2 * tx);
    ty = ty * ty * (3 - 2 * ty);
    const wrap = v => ((v % cells) + cells) % cells;
    const a = hash(wrap(x0), wrap(y0), seed), b = hash(wrap(x0 + 1), wrap(y0), seed);
    const c = hash(wrap(x0), wrap(y0 + 1), seed), d = hash(wrap(x0 + 1), wrap(y0 + 1), seed);
    const top = a + (b - a) * tx, bottom = c + (d - c) * tx;
    return top + (bottom - top) * ty;
}

/** A small RGBA image with drawing helpers. */
export class Sprite {
    constructor(width, height) {
        this.width = width;
        this.height = height;
        this.pixels = new Uint32Array(width * height);
    }

    get(x, y) { return this.pixels[y * this.width + x]; }

    set(x, y, c) {
        if (x >= 0 && x < this.width && y >= 0 && y < this.height) this.pixels[y * this.width + x] = c;
    }

    fill(x0, y0, x1, y1, c) {
        for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) this.set(x, y, c);
    }

    /** colorAt(x, y, d) gets the normalised squared distance from the centre (0..1). */
    ellipse(cx, cy, rx, ry, colorAt) {
        const color = typeof colorAt === 'function' ? colorAt : () => colorAt;
        for (let y = Math.trunc(cy - ry) - 1; y <= Math.trunc(cy + ry) + 1; y++) {
            for (let x = Math.trunc(cx - rx) - 1; x <= Math.trunc(cx + rx) + 1; x++) {
                const dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                const d = dx * dx + dy * dy;
                if (d <= 1) this.set(x, y, color(x, y, d));
            }
        }
    }

    line(x0, y0, x1, y1, thickness, c) {
        const steps = Math.trunc(Math.max(Math.abs(x1 - x0), Math.abs(y1 - y0)) * 2) + 1;
        for (let i = 0; i <= steps; i++) {
            this.ellipse(x0 + (x1 - x0) * i / steps, y0 + (y1 - y0) * i / steps, thickness, thickness, c);
        }
    }

    tinted(map) {
        const copy = new Sprite(this.width, this.height);
        for (let i = 0; i < this.pixels.length; i++) copy.pixels[i] = this.pixels[i] === 0 ? 0 : map(this.pixels[i]);
        return copy;
    }
}
