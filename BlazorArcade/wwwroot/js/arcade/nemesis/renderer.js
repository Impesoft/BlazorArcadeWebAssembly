// Draws Nemesis into a 256x192 framebuffer, the MSX's native resolution.
import * as Art from './art.js';
import { Enemy, State, TILE, shotBox, SCREEN_WIDTH as WIDTH, SCREEN_HEIGHT as HEIGHT } from './game.js';
import { hash } from '../shared/pixel-art.js';

export { WIDTH, HEIGHT };
const ROWS = HEIGHT / TILE;

export class Renderer {
    /** @param {Uint32Array} [pixels] framebuffer to draw into; one is allocated when omitted. */
    constructor(pixels) {
        this.pixels = pixels ?? new Uint32Array(WIDTH * HEIGHT);
        this.theme = -1;
        this.flashed = new Map();
    }

    render(g) {
        const px = this.pixels;
        if (this.theme !== g.stageTheme) {
            this.tiles = Art.buildTiles(g.stageTheme);
            this.theme = g.stageTheme;
        }
        const cam = Math.floor(g.camX);
        const frame = Math.trunc(g.time * 8) % 2;

        this.drawStars(px, g);
        this.drawTerrain(px, g, cam);

        for (const e of g.enemies) {
            if (e.kind === Enemy.Volcano) {
                blit(px, Art.crater, Math.trunc(e.x) - cam - 4, Math.trunc(e.y) - 2);
                continue;
            }
            const sprite = enemySprite(e, frame);
            blit(px, e.flash > 0 ? this.flash(sprite) : sprite, Math.trunc(e.x) - cam, Math.trunc(e.y));
            if (e.kind === Enemy.Boss) drawShieldWalls(px, e, cam);
        }

        for (const c of g.capsules) blit(px, Art.capsule[frame], Math.trunc(c.x) - cam, Math.trunc(c.y));

        const blinking = g.invulnerable > 0 && Math.trunc(g.invulnerable * 12) % 2 === 1;
        if (!g.playerDying && g.state !== State.Title) {
            for (const o of g.optionPositions()) blit(px, Art.option[frame], Math.trunc(o.x) - cam, Math.trunc(o.y));
            if (!blinking) {
                const x = Math.trunc(g.playerX) - cam, y = Math.trunc(g.playerY);
                blit(px, Art.ship[frame], x, y);
                if (g.shield > 0) drawForceField(px, x, y, g.time);
            }
        }

        for (const s of g.shots) {
            const [x, y, w, h] = shotBox(s);
            if (s.type === 'missile') blit(px, Art.missile, Math.trunc(x) - cam, Math.trunc(y));
            else if (s.type === 'double') drawLine(px, Math.trunc(x) - cam, Math.trunc(y) + 5, 6, Art.color('F'));
            else fillRect(px, Math.trunc(x) - cam, Math.trunc(y), Math.ceil(w), h, Art.color(s.type === 'laser' ? 'c' : 'F'));
        }

        for (const b of g.bullets) {
            const x = Math.trunc(b.x) - cam, y = Math.trunc(b.y);
            if (b.laser) fillRect(px, x, y, 16, 2, Art.color(frame ? 'F' : 'c'));
            else blit(px, b.ring ? Art.ring : Art.enemyBullet, x, y);
        }

        for (const e of g.explosions) {
            if (e.age >= 0) blit(px, Art.explosion[Math.min(3, Math.trunc(e.age * 10))], Math.trunc(e.x) - cam, Math.trunc(e.y));
        }

        if (g.boss) drawBossBar(px, g.boss.health / g.bossMaxHealth);
    }

    /** Two layers of stars drifting at different speeds behind the terrain. */
    drawStars(px, g) {
        px.fill(Art.color('K'));
        const dim = Art.color('k'), bright = Art.color('E');
        for (let i = 0; i < 48; i++) {
            const near = i % 3 === 0;
            const speed = near ? 0.5 : 0.25;
            const x = Math.floor(hash(i, 1, 5) * 512 - g.camX * speed) & 255;
            const y = Math.floor(hash(i, 2, 5) * HEIGHT);
            px[y * WIDTH + x] = near ? bright : dim;
        }
    }

    drawTerrain(px, g, cam) {
        const firstCol = Math.floor(cam / TILE), offset = cam - firstCol * TILE;
        for (let c = 0; c <= WIDTH / TILE; c++) {
            const col = firstCol + c, x = c * TILE - offset;
            const floor = g.floorAt(col), ceil = g.ceilAt(col);
            for (let r = 0; r < ceil; r++) blitOpaque(px, r === ceil - 1 ? this.tiles.bottom : this.tiles.fill, x, r * TILE);
            for (let r = ROWS - floor; r < ROWS; r++) blitOpaque(px, r === ROWS - floor ? this.tiles.top : this.tiles.fill, x, r * TILE);
        }
    }

    flash(sprite) {
        let white = this.flashed.get(sprite);
        if (!white) {
            white = sprite.tinted(() => Art.color('F'));
            this.flashed.set(sprite, white);
        }
        return white;
    }
}

function enemySprite(e, frame) {
    switch (e.kind) {
        case Enemy.Fan: return Art.fan[Math.trunc(e.age * 6) & 1];
        case Enemy.Jet: return Art.jet;
        case Enemy.Red: return Art.redJet;
        case Enemy.Mini: return Art.mini[frame];
        case Enemy.Turret: return e.ceiling ? Art.turret.ceiling : Art.turret.floor;
        case Enemy.Ducker: return Art.ducker[Math.trunc(e.age * 4) & 1];
        case Enemy.Rock: return Art.rock;
        case Enemy.Moai: return (e.ceiling ? Art.moai.ceiling : Art.moai.floor)[e.open ? 1 : 0];
        case Enemy.Hatch: return e.ceiling ? Art.hatch.ceiling : Art.hatch.floor;
        default: return Art.boss;
    }
}

/** The blue walls in front of Big Core's core; one disappears each time it is shot through. */
function drawShieldWalls(px, e, cam) {
    const x0 = Math.trunc(e.x) - cam, y0 = Math.trunc(e.y);
    for (let i = 0; i < e.walls; i++) fillRect(px, x0 + 12 - i * 3, y0 + 11, 2, 10, Art.color(i === e.walls - 1 && e.flash > 0 ? 'F' : 'b'));
}

function drawForceField(px, x, y, time) {
    const c = Art.color(Math.trunc(time * 10) % 2 ? 'c' : 'B');
    for (let a = 0; a < 40; a++) {
        const t = a / 40 * Math.PI * 2;
        put(px, Math.round(x + 8 + Math.cos(t) * 11), Math.round(y + 4 + Math.sin(t) * 8), c);
    }
}

function drawBossBar(px, fraction) {
    const x0 = 64, y0 = 4, w = 128, h = 4;
    const filled = Math.trunc(w * Math.min(1, Math.max(0, fraction)));
    const white = Art.color('F'), red = Art.color('R'), black = Art.color('K');
    for (let y = y0 - 1; y <= y0 + h; y++) {
        for (let x = x0 - 1; x <= x0 + w; x++) {
            const border = y === y0 - 1 || y === y0 + h || x === x0 - 1 || x === x0 + w;
            px[y * WIDTH + x] = border ? white : x - x0 < filled ? red : black;
        }
    }
}

function put(px, x, y, c) {
    if (x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT) px[y * WIDTH + x] = c;
}

function fillRect(px, x0, y0, w, h, c) {
    for (let y = y0; y < y0 + h; y++) for (let x = x0; x < x0 + w; x++) put(px, x, y, c);
}

/** A short diagonal going up and to the right. */
function drawLine(px, x0, y0, length, c) {
    for (let i = 0; i < length; i++) {
        put(px, x0 + i, y0 - i, c);
        put(px, x0 + i + 1, y0 - i, c);
    }
}

function blit(px, s, x0, y0) {
    for (let y = 0; y < s.height; y++) {
        const sy = y0 + y;
        if (sy < 0 || sy >= HEIGHT) continue;
        const row = y * s.width, dest = sy * WIDTH;
        for (let x = 0; x < s.width; x++) {
            const sx = x0 + x;
            if (sx < 0 || sx >= WIDTH) continue;
            const c = s.pixels[row + x];
            if (c !== 0) px[dest + sx] = c;
        }
    }
}

function blitOpaque(px, s, x0, y0) {
    for (let y = 0; y < s.height; y++) {
        const sy = y0 + y;
        if (sy < 0 || sy >= HEIGHT) continue;
        for (let x = 0; x < s.width; x++) {
            const sx = x0 + x;
            if (sx >= 0 && sx < WIDTH) px[sy * WIDTH + sx] = s.pixels[y * s.width + x];
        }
    }
}
