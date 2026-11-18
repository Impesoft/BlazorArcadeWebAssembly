// Draws Penguin Adventure into a 256x192 framebuffer: a pseudo-3D road, one scanline at a time,
// with the ground hazards painted into it, then scaled sprites from the back to the front.
import * as Art from './art.js';
import { Kind, SCREEN_WIDTH as WIDTH, SCREEN_HEIGHT as HEIGHT, ROAD_EDGE } from './game.js';

export { WIDTH, HEIGHT };

export const HORIZON = 64;
/** Screen row of a point d metres ahead of the camera is HORIZON + DEPTH / d. */
const DEPTH = 400;
/** One road half-width is LATERAL / d pixels wide, d metres ahead. */
const LATERAL = 400;
/** The camera follows this far behind Penta. */
export const PLAYER_DISTANCE = 4;
const DRAW_DISTANCE = 90;
const NEAREST = DEPTH / (HEIGHT - HORIZON);
const EDGE_WIDTH = 0.08;

export class Renderer {
    /** @param {Uint32Array} [pixels] framebuffer to draw into; one is allocated when omitted. */
    constructor(pixels) {
        this.pixels = pixels ?? new Uint32Array(WIDTH * HEIGHT);
        this.offsets = new Float32Array(DRAW_DISTANCE + 2);
        this.track = null;
    }

    render(g) {
        if (this.track !== g.track) this.prepare(g.track);
        const theme = Art.themes[g.stageTheme];
        const camera = g.pos - PLAYER_DISTANCE;

        // Where the road's centre line runs, metre by metre ahead: bends bend it ever further.
        let slope = 0, offset = 0;
        for (let m = 0; m < this.offsets.length; m++) {
            this.offsets[m] = offset;
            slope += g.curveAt(camera + m);
            offset += slope;
        }

        this.drawSky(g, theme, camera);
        this.drawGround(g, theme, camera);
        this.drawSprites(g, camera);
    }

    /** How far each point of the road has turned from the start, to pan the horizon in bends. */
    prepare(track) {
        this.track = track;
        this.heading = new Float32Array(track.curve.length);
        let h = 0;
        for (let s = 0; s < track.curve.length; s++) this.heading[s] = h += track.curve[s];
    }

    offsetAt(d) {
        const m = Math.min(this.offsets.length - 2, Math.max(0, d));
        const i = Math.floor(m);
        return this.offsets[i] + (this.offsets[i + 1] - this.offsets[i]) * (m - i);
    }

    project(camera, s, x, z = 0) {
        const d = s - camera;
        if (d < NEAREST * 0.8 || d > DRAW_DISTANCE) return null;
        const scale = LATERAL / d;
        return {
            x: WIDTH / 2 + (this.offsetAt(d) + x) * scale,
            y: HORIZON + DEPTH / d - z * scale,
            ground: HORIZON + DEPTH / d,
            scale,
        };
    }

    drawSky(g, theme, camera) {
        const px = this.pixels;
        const [top, bottom] = theme.sky;
        for (let y = 0; y < HORIZON; y++) {
            const c = mix(top, bottom, y / HORIZON);
            px.fill(c, y * WIDTH, (y + 1) * WIDTH);
        }

        const heading = this.heading[Math.max(0, Math.min(this.heading.length - 1, Math.floor(g.pos)))];
        const pan = Math.round(heading * 160);
        const cave = g.stageTheme === 3;
        for (let x = 0; x < WIDTH; x++) {
            const w = ((x + pan) % WIDTH + WIDTH) % WIDTH;
            const a = w / WIDTH * Math.PI * 2;
            // Sums of whole waves wrap around seamlessly as the view pans.
            const far = 14 + 9 * Math.sin(a * 3 + 1) + 5 * Math.sin(a * 7 + 2) + 3 * Math.sin(a * 13);
            const near = 7 + 5 * Math.sin(a * 5 + 4) + 3 * Math.sin(a * 11 + 1);
            if (cave) {
                // Stalactites hang from the roof instead of mountains rising from the ground.
                const drip = Math.max(0, 22 + 18 * Math.sin(a * 9) + 12 * Math.sin(a * 23 + 2));
                for (let y = 0; y < Math.min(HORIZON, drip); y++) px[y * WIDTH + x] = theme.hills[1];
            }
            for (let y = HORIZON - Math.round(far); y < HORIZON; y++) px[y * WIDTH + x] = theme.hills[0];
            for (let y = HORIZON - Math.round(near); y < HORIZON; y++) px[y * WIDTH + x] = theme.hills[1];
        }
    }

    drawGround(g, theme, camera) {
        const px = this.pixels;
        const objects = g.track.objects;
        const pits = objects.filter(o => o.s + (o.len ?? 1) > camera && o.s - 1 < camera + DRAW_DISTANCE &&
            (o.kind === Kind.Hole || o.kind === Kind.Crevasse || o.kind === Kind.Water || o.kind === Kind.Seal));

        for (let y = HORIZON; y < HEIGHT; y++) {
            const d = DEPTH / (y + 0.5 - HORIZON);
            const s = camera + d;
            const scale = LATERAL / d;
            const centre = WIDTH / 2 + this.offsetAt(d) * scale;
            const band = Math.floor(s / 3) & 1;
            const road = theme.road[band], edge = theme.edge[band], verge = theme.verge[band];
            const beyondEnd = s > g.track.length;

            // Pits crossing this row: a crevasse spans the road, a hole is an ellipse on it.
            let across = null;
            const holes = [];
            for (const o of pits) {
                if (o.len !== undefined) {
                    if (s >= o.s && s <= o.s + o.len) across = o;
                } else {
                    const t = (s - o.s) / (o.depth / 2);
                    if (Math.abs(t) < 1) holes.push({ x: o.x, w: o.r * Math.sqrt(1 - t * t), t });
                }
            }

            const row = y * WIDTH;
            for (let x = 0; x < WIDTH; x++) {
                const u = (x + 0.5 - centre) / scale;
                const a = Math.abs(u);
                let c = a <= ROAD_EDGE ? road : a <= ROAD_EDGE + EDGE_WIDTH ? edge : verge;
                if (beyondEnd && a <= ROAD_EDGE && ((Math.floor(u * 4) + Math.floor(s)) & 1)) c = Art.color('K');
                // A crevasse splits the whole ice field, verge and all.
                if (across) {
                    const t = (s - across.s) / across.len;
                    c = t < 0.15 || t > 0.85 ? theme.pit[1] : theme.pit[0];
                    if (across.kind === Kind.Water && ((x + Math.floor(g.time * 12)) & 15) < 3 && (y & 1)) c = theme.pit[1];
                }
                for (const h of holes) {
                    const e = Math.abs(u - h.x) / h.w;
                    if (e < 1) c = e > 0.75 || h.t < -0.6 ? theme.pit[1] : theme.pit[0];
                }
                px[row + x] = c;
            }
        }
    }

    drawSprites(g, camera) {
        const theme = g.stageTheme;
        const sprites = [];
        const near = camera + NEAREST * 0.8, far = camera + DRAW_DISTANCE;

        for (const s of g.track.scenery) {
            if (s.s < near || s.s > far) continue;
            const art = Art.scenery[theme];
            sprites.push({ s: s.s, x: s.x, z: 0, sprite: art.sprite, width: art.width * (s.variant ? 1.3 : 1) });
        }

        const frame = Math.floor(g.time * 6) & 1;
        for (const o of g.track.objects) {
            if (o.s < near || o.s > far || o.taken) continue;
            switch (o.kind) {
                case Kind.Seal:
                    if (g.sealUp(o)) sprites.push({ s: o.s, x: o.x, z: 0, sprite: Art.seal, width: 0.4 });
                    break;
                case Kind.Rock:
                    sprites.push({ s: o.s, x: o.x, z: 0, sprite: Art.rock[theme], width: 0.42 });
                    break;
                case Kind.Bat: {
                    const bat = g.batPosition(o);
                    sprites.push({ s: o.s, x: bat.x, z: bat.z, sprite: Art.bat[theme === 1 ? 1 : 0][frame], width: 0.42, shadow: true });
                    break;
                }
                case Kind.Fish:
                case Kind.GoldFish: {
                    const bob = Math.abs(Math.sin(g.time * 8 + o.s)) * 0.06;
                    sprites.push({ s: o.s, x: o.x, z: o.z + bob, sprite: o.kind === Kind.Fish ? Art.fish : Art.goldFish, width: 0.3, shadow: o.z > 0 });
                    break;
                }
            }
        }

        // The goal: a chequered banner across the road.
        if (g.track.length > near && g.track.length < far) sprites.push({ s: g.track.length, goal: true });

        sprites.push({ s: g.pos, player: true });
        sprites.sort((a, b) => b.s - a.s);

        for (const sp of sprites) {
            if (sp.player) this.drawPenta(g, camera);
            else if (sp.goal) this.drawGoal(camera, sp.s);
            else this.drawBillboard(camera, sp);
        }
    }

    drawBillboard(camera, sp) {
        const p = this.project(camera, sp.s, sp.x, sp.z);
        if (!p) return;
        if (sp.shadow) this.drawShadow(camera, sp.s, sp.x, sp.width * 0.4);
        const w = sp.width * p.scale;
        blitScaled(this.pixels, sp.sprite, p.x - w / 2, p.y - sp.sprite.height * (w / sp.sprite.width), w / sp.sprite.width);
    }

    drawShadow(camera, s, x, radius) {
        const p = this.project(camera, s, x);
        if (!p) return;
        const rx = radius * p.scale, ry = Math.max(1, rx * 0.25);
        for (let y = Math.floor(p.ground - ry); y <= p.ground + ry; y++) {
            for (let px = Math.floor(p.x - rx); px <= p.x + rx; px++) {
                const dx = (px - p.x) / rx, dy = (y - p.ground) / ry;
                if (dx * dx + dy * dy <= 1 && px >= 0 && px < WIDTH && y >= 0 && y < HEIGHT) {
                    const i = y * WIDTH + px;
                    this.pixels[i] = darken(this.pixels[i]);
                }
            }
        }
    }

    drawGoal(camera, s) {
        const left = this.project(camera, s, -ROAD_EDGE - 0.15, 0), right = this.project(camera, s, ROAD_EDGE + 0.15, 0);
        if (!left || !right) return;
        const scale = left.scale, poleWidth = Math.max(1, Math.round(0.05 * scale));
        const top = Math.round(left.ground - 1.1 * scale), bannerBottom = Math.round(left.ground - 0.8 * scale);
        fill(this.pixels, left.x - poleWidth, top, poleWidth * 2, left.ground - top, Art.color('E'));
        fill(this.pixels, right.x - poleWidth, top, poleWidth * 2, right.ground - top, Art.color('E'));
        const cell = Math.max(1, (bannerBottom - top) / 2);
        for (let y = top; y < bannerBottom; y++) {
            for (let x = Math.ceil(left.x + poleWidth); x < right.x - poleWidth; x++) {
                const checker = (Math.floor((x - left.x) / cell) + Math.floor((y - top) / cell)) & 1;
                put(this.pixels, x, y, checker ? Art.color('K') : Art.color('F'));
            }
        }
    }

    drawPenta(g, camera) {
        if (g.invulnerable > 0 && g.falling <= 0 && Math.floor(g.time * 16) & 1) return;
        const sprite = g.isAirborne ? Art.penta.jump : Art.penta.run[g.speed > 0 ? Math.floor(g.stride / 1.6) & 1 : 0];
        const p = this.project(camera, g.pos, g.x, g.z);
        if (!p) return;
        if (g.isAirborne) this.drawShadow(camera, g.pos, g.x, 0.1);

        const width = 0.3 * p.scale, zoom = width / sprite.width;
        let visible = sprite.height;
        if (g.falling > 0) {
            // Down into the pit, a moment at the bottom, then a scramble out of it.
            const t = 1 - g.falling / 1.6;
            const shown = t < 0.25 ? 1 - t / 0.25 : t < 0.55 ? 0 : (t - 0.55) / 0.45;
            visible = Math.round(sprite.height * shown);
            if (visible <= 0) return;
        }
        const x = p.x - width / 2, y = p.y - visible * zoom;
        blitScaled(this.pixels, sprite, x, y, zoom, visible);
        if (g.stunned > 0) {
            const spin = Math.floor(g.time * 10) & 1;
            blitScaled(this.pixels, Art.stars, x + (spin ? 2 : 0), y - 5 * zoom, zoom);
        }
    }
}

function blitScaled(px, s, x0, y0, zoom, rows = s.height) {
    const w = Math.round(s.width * zoom), h = Math.round(rows * zoom);
    const left = Math.round(x0), top = Math.round(y0);
    for (let y = Math.max(0, -top); y < h; y++) {
        const dy = top + y;
        if (dy >= HEIGHT) break;
        const sy = Math.min(rows - 1, Math.floor(y / zoom));
        for (let x = Math.max(0, -left); x < w; x++) {
            const dx = left + x;
            if (dx >= WIDTH) break;
            const c = s.pixels[sy * s.width + Math.min(s.width - 1, Math.floor(x / zoom))];
            if (c) px[dy * WIDTH + dx] = c;
        }
    }
}

function fill(px, x0, y0, w, h, c) {
    for (let y = Math.round(y0); y < y0 + h; y++) for (let x = Math.round(x0); x < x0 + w; x++) put(px, x, y, c);
}

function put(px, x, y, c) {
    if (x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT) px[y * WIDTH + x] = c;
}

function mix(a, b, t) {
    const ch = (c, shift) => (c >>> shift) & 0xFF;
    const m = shift => Math.round(ch(a, shift) + (ch(b, shift) - ch(a, shift)) * t);
    return (0xFF000000 | (m(16) << 16) | (m(8) << 8) | m(0)) >>> 0;
}

function darken(c) {
    return (0xFF000000 | ((((c >>> 16) & 0xFF) * 0.6) << 16) | ((((c >>> 8) & 0xFF) * 0.6) << 8) | ((c & 0xFF) * 0.6)) >>> 0;
}
