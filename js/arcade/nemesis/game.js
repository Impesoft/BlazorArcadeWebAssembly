// A tribute to Konami's Nemesis (Gradius on the MSX, 1986): a horizontally scrolling shooter.
// Red capsules light up the next slot of the power meter; POWER buys whatever is lit. Touching
// the terrain is fatal, and dying costs every power-up. No DOM access, so it runs under Node for tests.
import { STAGES } from './stages.js';

export const SCREEN_WIDTH = 256;
export const SCREEN_HEIGHT = 192;
export const TILE = 8;
const SCROLL_SPEED = 30;
const SHIP_WIDTH = 16, SHIP_HEIGHT = 8;
const OPTION_LAG = 12; // trail points between the ship and each option
const MAX_OPTIONS = 2;
const MAX_SPEED = 5;
const SHIELD_HITS = 5;

export const State = { Title: 'Title', Playing: 'Playing', StageClear: 'StageClear', GameOver: 'GameOver' };
export const Input = { Up: 1, Down: 2, Left: 4, Right: 8, Fire: 16, Power: 32, Pause: 64 };
/** Power meter slots, in the order capsules light them. */
export const Power = { Speed: 0, Missile: 1, Double: 2, Laser: 3, Option: 4, Shield: 5 };
const POWER_NAMES = ['SPEED UP', 'MISSILE', 'DOUBLE', 'LASER', 'OPTION', 'FORCE FIELD'];
export const Weapon = { Normal: 'Normal', Double: 'Double', Laser: 'Laser' };
export const Enemy = {
    Fan: 'Fan', Jet: 'Jet', Red: 'Red', Mini: 'Mini', Turret: 'Turret', Ducker: 'Ducker',
    Volcano: 'Volcano', Rock: 'Rock', Moai: 'Moai', Hatch: 'Hatch', Boss: 'Boss',
};

export const ENEMY_SIZE = {
    Fan: [8, 8], Jet: [16, 8], Red: [16, 8], Mini: [8, 8], Turret: [8, 8], Ducker: [8, 8],
    Volcano: [0, 0], Rock: [8, 8], Moai: [16, 24], Hatch: [16, 8], Boss: [48, 32],
};
const HEALTH = { Fan: 1, Jet: 1, Red: 1, Mini: 1, Turret: 2, Ducker: 2, Volcano: Infinity, Rock: 2, Moai: 10, Hatch: 8, Boss: 1 };
const POINTS = { Fan: 100, Jet: 100, Red: 200, Mini: 50, Turret: 300, Ducker: 200, Rock: 100, Moai: 1000, Hatch: 800 };
/** Things fixed to the terrain: they scroll away with it rather than flying off. */
const GROUNDED = new Set([Enemy.Turret, Enemy.Ducker, Enemy.Volcano, Enemy.Moai, Enemy.Hatch]);

const overlaps = (ax, ay, aw, ah, bx, by, bw, bh) => ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;
const rand = (min, max) => min + Math.random() * (max - min);
const clamp = (v, min, max) => Math.min(max, Math.max(min, v));

export function shotBox(s) {
    switch (s.type) {
        case 'laser': return [s.x - s.length, s.y, s.length, 2];
        case 'double': return [s.x, s.y, 6, 6];
        case 'missile': return [s.x, s.y, 4, 4];
        default: return [s.x, s.y, 8, 2];
    }
}

export function bulletBox(b) {
    return b.laser ? [b.x, b.y, 16, 2] : b.ring ? [b.x, b.y, 6, 6] : [b.x, b.y, 4, 4];
}

export class Game {
    constructor() {
        this.state = State.Title;
        this.isPaused = false;
        this.loop = 0;
        this.score = 0;
        this.highScore = 0;
        this.lives = 3;
        this.message = null;
        this.messageTimer = 0;
        this.sounds = [];
        this.prevInput = 0;
        this.fireCooldown = 0;
        this.deathTimer = 0;
        this.invulnerable = 0;
        this.resetPowers();
        this.loadStage(0);
    }

    get stageName() { return STAGES[this.stageIndex].name; }
    get stageTheme() { return STAGES[this.stageIndex].theme; }
    get stageNumber() { return this.stageIndex + 1 + this.loop * STAGES.length; }
    get playerDying() { return this.deathTimer > 0; }
    get difficulty() { return 1 + this.stageIndex * 0.2 + this.loop * 0.4; }
    get stageProgress() { return this.maxCam <= 0 ? 1 : Math.min(1, this.camX / this.maxCam); }

    resetPowers() {
        this.speedLevel = 0;
        this.missile = false;
        this.weapon = Weapon.Normal;
        this.options = 0;
        this.shield = 0;
        this.meter = -1;
        this.trail = [];
    }

    // ---------- Game flow ----------

    startNewGame() {
        this.score = 0;
        this.lives = 3;
        this.loop = 0;
        this.deathTimer = 0;
        this.resetPowers();
        this.loadStage(0);
        this.state = State.Playing;
        this.isPaused = false;
    }

    nextStage() {
        if (this.state !== State.StageClear) return;
        let next = this.stageIndex + 1;
        if (next >= STAGES.length) {
            next = 0;
            this.loop++;
        }
        this.loadStage(next);
        this.state = State.Playing;
    }

    togglePause() {
        if (this.state === State.Playing) this.isPaused = !this.isPaused;
    }

    loadStage(index) {
        this.stageIndex = index;
        const stage = STAGES[index];
        this.floor = stage.floor;
        this.ceil = stage.ceil;
        this.columns = stage.floor.length;
        this.events = stage.events;
        this.nextEvent = 0;

        this.camX = 0;
        this.maxCam = this.columns * TILE - SCREEN_WIDTH;
        this.shots = [];
        this.bullets = [];
        this.enemies = [];
        this.capsules = [];
        this.explosions = [];
        this.formations = new Map();
        this.nextFormation = 0;
        this.boss = null;
        this.bossMaxHealth = 1;
        this.bossSpawned = false;
        this.clearTimer = 0;
        this.ambientTimer = 5;
        this.time = 0;
        this.trail = [];
        this.placePlayer();
        this.invulnerable = 1.5;
        this.setMessage(`STAGE ${this.stageNumber}`);
    }

    /** Puts the ship near the left edge, halfway between floor and ceiling. */
    placePlayer() {
        this.playerX = this.camX + 32;
        let top = 0, bottom = SCREEN_HEIGHT;
        for (let x = this.playerX; x < this.playerX + SHIP_WIDTH; x += TILE) {
            const col = Math.floor(x / TILE);
            top = Math.max(top, this.ceilY(col));
            bottom = Math.min(bottom, this.floorY(col));
        }
        this.playerY = Math.round((top + bottom) / 2 - SHIP_HEIGHT / 2);
    }

    setMessage(text) {
        this.message = text;
        this.messageTimer = 2;
    }

    // ---------- Terrain ----------

    floorAt(col) { return this.floor[clamp(col, 0, this.columns - 1)]; }
    ceilAt(col) { return this.ceil[clamp(col, 0, this.columns - 1)]; }
    /** Top edge of the floor in a column, in pixels. */
    floorY(col) { return SCREEN_HEIGHT - this.floorAt(col) * TILE; }
    /** Bottom edge of the ceiling in a column, in pixels. */
    ceilY(col) { return this.ceilAt(col) * TILE; }

    solidAt(x, y) {
        const col = Math.floor(x / TILE);
        return y >= this.floorY(col) || y < this.ceilY(col);
    }

    hitsTerrain(x, y) {
        return [[3, 2], [13, 2], [3, 6], [13, 6], [8, 4]].some(([dx, dy]) => this.solidAt(x + dx, y + dy));
    }

    // ---------- Update ----------

    update(dt, input) {
        const pressed = input & ~this.prevInput;
        this.prevInput = input;
        this.sounds = [];

        if (this.state === State.Title) {
            this.time += dt;
            return;
        }
        if (this.state !== State.Playing) return;
        if (pressed & Input.Pause) this.isPaused = !this.isPaused;
        if (this.isPaused) return;

        this.time += dt;
        if (this.messageTimer > 0 && (this.messageTimer -= dt) <= 0) this.message = null;

        let scroll = 0;
        if (this.camX < this.maxCam) {
            scroll = Math.min(this.maxCam - this.camX, SCROLL_SPEED * dt);
            this.camX += scroll;
        } else if (!this.bossSpawned) {
            this.spawnBoss();
        }

        if (this.deathTimer > 0) {
            if ((this.deathTimer -= dt) <= 0) this.respawn();
        } else {
            this.updatePlayer(dt, input, pressed, scroll);
        }

        this.spawnEvents();
        this.spawnAmbient(dt);
        this.updateShots(dt);
        this.updateEnemies(dt);
        this.updateBullets(dt);
        this.updateCapsules();
        this.checkPlayerHits();

        for (const e of this.explosions) e.age += dt;
        this.explosions = this.explosions.filter(e => e.age <= 0.4);
        this.shots = this.shots.filter(s => !s.dead);
        this.bullets = this.bullets.filter(b => !b.dead);
        this.enemies = this.enemies.filter(e => !e.dead);
        this.capsules = this.capsules.filter(c => !c.taken && c.x > this.camX - 8);

        if (this.score > this.highScore) this.highScore = this.score;

        if (this.clearTimer > 0 && (this.clearTimer -= dt) <= 0) {
            this.state = State.StageClear;
            this.sounds.push('stageClear');
        }
    }

    updatePlayer(dt, input, pressed, scroll) {
        this.invulnerable = Math.max(0, this.invulnerable - dt);
        this.playerX += scroll;

        const speed = 70 + this.speedLevel * 22;
        let dx = (input & Input.Right ? 1 : 0) - (input & Input.Left ? 1 : 0);
        let dy = (input & Input.Down ? 1 : 0) - (input & Input.Up ? 1 : 0);
        if (dx !== 0 && dy !== 0) {
            dx *= 0.7071;
            dy *= 0.7071;
        }
        this.playerX = clamp(this.playerX + dx * speed * dt, this.camX, this.camX + SCREEN_WIDTH - SHIP_WIDTH);
        this.playerY = clamp(this.playerY + dy * speed * dt, 0, SCREEN_HEIGHT - SHIP_HEIGHT);

        // Options retrace the ship's path across the screen, so they only follow when it moves.
        if (dx !== 0 || dy !== 0) {
            this.trail.push({ x: this.playerX - this.camX, y: this.playerY });
            if (this.trail.length > OPTION_LAG * MAX_OPTIONS + 1) this.trail.shift();
        }

        if (this.hitsTerrain(this.playerX, this.playerY)) {
            this.die();
            return;
        }

        if (pressed & Input.Power) this.activatePower();
        this.fireCooldown -= dt;
        if ((input & Input.Fire) && this.fireCooldown <= 0) this.fire();
    }

    /** Top-left corners of the options, in world coordinates. */
    optionPositions() {
        const positions = [];
        for (let i = 0; i < this.options; i++) {
            const p = this.trail[Math.max(0, this.trail.length - 1 - OPTION_LAG * (i + 1))] ?? { x: this.playerX - this.camX, y: this.playerY };
            positions.push({ x: this.camX + p.x + 4, y: p.y });
        }
        return positions;
    }

    activatePower() {
        if (this.meter < 0) return;
        let gained = true;
        switch (this.meter) {
            case Power.Speed: gained = this.speedLevel < MAX_SPEED; if (gained) this.speedLevel++; break;
            case Power.Missile: gained = !this.missile; this.missile = true; break;
            case Power.Double: gained = this.weapon !== Weapon.Double; this.weapon = Weapon.Double; break;
            case Power.Laser: gained = this.weapon !== Weapon.Laser; this.weapon = Weapon.Laser; break;
            case Power.Option: gained = this.options < MAX_OPTIONS; if (gained) this.options++; break;
            case Power.Shield: gained = this.shield === 0; if (gained) this.shield = SHIELD_HITS; break;
        }
        if (!gained) {
            this.sounds.push('empty');
            return;
        }
        this.setMessage(POWER_NAMES[this.meter]);
        this.meter = -1;
        this.sounds.push('powerup');
    }

    // ---------- Weapons ----------

    fire() {
        const emitters = [{ x: this.playerX + SHIP_WIDTH, y: this.playerY + 3 }]
            .concat(this.optionPositions().map(o => ({ x: o.x + 8, y: o.y + 3 })));
        let fired = false;
        emitters.forEach((e, owner) => {
            const mine = this.shots.filter(s => s.owner === owner);
            const add = (type, x, y, vx, vy) => {
                this.shots.push({ type, owner, x, y, vx, vy, length: 0, grounded: false, hit: new Set(), dead: false });
                fired = true;
            };
            if (this.weapon === Weapon.Laser) {
                if (!mine.some(s => s.type === 'laser')) add('laser', e.x, e.y, 420, 0);
            } else if (mine.filter(s => s.type === 'shot').length < 2) {
                add('shot', e.x, e.y, 360, 0);
            }
            if (this.weapon === Weapon.Double && !mine.some(s => s.type === 'double')) add('double', e.x, e.y - 4, 260, -260);
            if (this.missile && !mine.some(s => s.type === 'missile')) add('missile', e.x - 8, e.y + 4, 110, 110);
        });
        this.fireCooldown = 0.1;
        if (fired) this.sounds.push(this.weapon === Weapon.Laser ? 'laser' : 'shot');
    }

    updateShots(dt) {
        for (const s of this.shots) {
            if (s.type === 'missile') {
                this.moveMissile(s, dt);
            } else {
                s.x += s.vx * dt;
                s.y += s.vy * dt;
                if (s.type === 'laser') s.length = Math.min(64, s.length + 600 * dt);
            }

            const [x, y, w, h] = shotBox(s);
            if (x > this.camX + SCREEN_WIDTH || y + h < 0 || y > SCREEN_HEIGHT) s.dead = true;
            // The laser cuts through everything; other shots stop at the terrain.
            if (!s.dead && s.type !== 'laser' && this.solidAt(x + w / 2, y + h / 2)) s.dead = true;
            if (!s.dead) this.hitEnemies(s);
        }
    }

    /** Missiles drop until they reach the floor, then run along it until a wall stops them. */
    moveMissile(s, dt) {
        if (!s.grounded) {
            s.x += s.vx * dt;
            s.y += s.vy * dt;
            const ground = this.floorY(Math.floor((s.x + 2) / TILE)) - 4;
            if (s.y >= ground) {
                s.y = ground;
                s.grounded = true;
            }
            return;
        }
        s.x += 140 * dt;
        const ground = this.floorY(Math.floor((s.x + 4) / TILE)) - 4;
        if (ground < s.y - 6) s.dead = true;
        else s.y = ground > s.y ? Math.min(ground, s.y + 150 * dt) : ground;
    }

    hitEnemies(s) {
        const [sx, sy, sw, sh] = shotBox(s);
        for (const e of this.enemies) {
            if (e.dead || e.kind === Enemy.Volcano) continue;
            const [w, h] = ENEMY_SIZE[e.kind];
            if (!overlaps(sx, sy, sw, sh, e.x + 1, e.y + 1, w - 2, h - 2)) continue;
            if (s.type === 'laser') {
                if (s.hit.has(e)) continue; // a laser hits each enemy once on its way through
                s.hit.add(e);
                this.damage(e);
                continue;
            }
            s.dead = true;
            this.damage(e);
            return;
        }
    }

    damage(e) {
        e.flash = 0.1;
        if (e.kind === Enemy.Boss) {
            this.hitBoss(e);
            return;
        }
        if (--e.health > 0) {
            this.sounds.push('bossHit');
            return;
        }
        this.kill(e);
    }

    kill(e) {
        e.dead = true;
        this.score += POINTS[e.kind] ?? 0;
        const [w, h] = ENEMY_SIZE[e.kind];
        this.explosions.push({ x: e.x + w / 2 - 8, y: e.y + h / 2 - 8, age: 0 });
        this.sounds.push('explode');

        if (e.kind === Enemy.Red) this.dropCapsule(e);
        const formation = e.formation != null ? this.formations.get(e.formation) : null;
        if (formation && ++formation.killed === formation.size) this.dropCapsule(e);
    }

    dropCapsule(e) {
        const [w, h] = ENEMY_SIZE[e.kind];
        this.capsules.push({ x: e.x + w / 2 - 4, y: e.y + h / 2 - 4, taken: false });
    }

    // ---------- Enemies ----------

    spawnEvents() {
        while (this.nextEvent < this.events.length && this.events[this.nextEvent].x <= this.camX + SCREEN_WIDTH + TILE) {
            this.spawnEvent(this.events[this.nextEvent++]);
        }
    }

    spawnEvent({ x, kind, param }) {
        const col = Math.floor(x / TILE);
        const onFloor = param !== 'C';
        switch (kind) {
            case 'fans': {
                const id = this.nextFormation++;
                this.formations.set(id, { size: 5, killed: 0 });
                for (let i = 0; i < 5; i++) {
                    const fan = this.spawn(Enemy.Fan, x + TILE + i * 14, param);
                    fan.baseY = param;
                    fan.age = -i * 0.3;
                    fan.formation = id;
                }
                break;
            }
            case 'jet':
                for (let i = 0; i < param; i++) this.spawn(Enemy.Jet, x + TILE + i * 28, this.randomHeight(col));
                break;
            case 'red':
                this.spawn(Enemy.Red, x + TILE, param);
                break;
            case 'turret':
                this.spawn(Enemy.Turret, x, onFloor ? this.floorY(col) - 8 : this.ceilY(col)).ceiling = !onFloor;
                break;
            case 'ducker':
                this.spawn(Enemy.Ducker, x, this.floorY(col) - 8);
                break;
            case 'volcano':
                this.spawn(Enemy.Volcano, x + 4, this.floorY(col)).fireTimer = 0.5;
                break;
            case 'moai':
                this.spawn(Enemy.Moai, x, onFloor ? this.floorY(col) - 24 : this.ceilY(col)).ceiling = !onFloor;
                break;
            case 'hatch':
                this.spawn(Enemy.Hatch, x, onFloor ? this.floorY(col) - 8 : this.ceilY(col)).ceiling = !onFloor;
                break;
        }
    }

    randomHeight(col) {
        return rand(this.ceilY(col) + 8, this.floorY(col) - 16);
    }

    spawnAmbient(dt) {
        if (this.bossSpawned || (this.ambientTimer -= dt) > 0) return;
        this.ambientTimer = 5 / this.difficulty + rand(0, 2);
        const x = this.camX + SCREEN_WIDTH + TILE;
        this.spawn(Enemy.Jet, x, this.randomHeight(Math.floor(x / TILE)));
    }

    spawn(kind, x, y) {
        const enemy = {
            kind, x, y, baseY: y, vx: 0, vy: 0, health: HEALTH[kind], flash: 0, dead: false,
            age: 0, fireTimer: rand(1, 2.5), ceiling: false, open: false, launched: 0, formation: null,
        };
        this.enemies.push(enemy);
        return enemy;
    }

    spawnBoss() {
        this.bossSpawned = true;
        this.bossMaxHealth = 12 + this.stageIndex * 4 + this.loop * 8;
        const col = Math.floor((this.camX + 200) / TILE);
        this.boss = this.spawn(Enemy.Boss, this.camX + SCREEN_WIDTH + 8, (this.ceilY(col) + this.floorY(col)) / 2 - 16);
        this.boss.health = this.bossMaxHealth;
        this.boss.walls = 4;
        this.boss.wallHealth = 3;
        this.boss.fireTimer = 2;
        this.setMessage('WARNING!');
    }

    onScreen(e) {
        const [w] = ENEMY_SIZE[e.kind];
        return e.x < this.camX + SCREEN_WIDTH && e.x + w > this.camX;
    }

    updateEnemies(dt) {
        const px = this.playerX + SHIP_WIDTH / 2, py = this.playerY + SHIP_HEIGHT / 2;
        const d = this.difficulty;
        const steer = (e, rate) => {
            const [, h] = ENEMY_SIZE[e.kind];
            e.y += clamp(py - (e.y + h / 2), -1, 1) * rate * dt;
        };

        for (const e of this.enemies) {
            e.age += dt;
            e.flash = Math.max(0, e.flash - dt);
            const visible = this.onScreen(e);

            switch (e.kind) {
                case Enemy.Fan:
                    e.x -= 90 * dt;
                    e.y = e.baseY + Math.sin(Math.max(0, e.age) * 3) * 22;
                    break;

                case Enemy.Jet:
                case Enemy.Red:
                    e.x -= 45 * dt;
                    steer(e, 30);
                    if (e.kind === Enemy.Jet && visible && (e.fireTimer -= dt) <= 0) {
                        e.fireTimer = 3.2 / d;
                        this.aim(e, 80 * d);
                    }
                    break;

                case Enemy.Mini:
                    e.x -= 60 * dt;
                    steer(e, 50);
                    break;

                case Enemy.Turret:
                    if (visible && (e.fireTimer -= dt) <= 0) {
                        e.fireTimer = 2.4 / d;
                        this.aim(e, 80 * d);
                    }
                    break;

                case Enemy.Ducker:
                    e.x -= 20 * dt;
                    e.y = this.floorY(Math.floor((e.x + 4) / TILE)) - 8;
                    if (visible && (e.fireTimer -= dt) <= 0) {
                        e.fireTimer = 2.6 / d;
                        this.aim(e, 75 * d);
                    }
                    break;

                case Enemy.Volcano:
                    if (visible && (e.fireTimer -= dt) <= 0) {
                        e.fireTimer = 0.8 / d;
                        const rock = this.spawn(Enemy.Rock, e.x - 4, e.y - 8);
                        rock.vx = rand(-70, 40);
                        rock.vy = rand(-170, -110);
                    }
                    break;

                case Enemy.Rock:
                    e.vy += 130 * dt;
                    e.x += e.vx * dt;
                    e.y += e.vy * dt;
                    break;

                case Enemy.Moai:
                    // Opens its mouth, then fires a pair of rings that straddle a ship in front of it:
                    // holding still is safe, flying into them isn't.
                    e.fireTimer -= dt;
                    e.open = visible && e.fireTimer < 0.8;
                    if (e.fireTimer <= 0) {
                        e.fireTimer = 4 / d;
                        if (visible && this.playerX < e.x) for (const spread of [-14, 14]) this.aim(e, 55 * d, { ring: true, spread });
                    }
                    break;

                case Enemy.Hatch:
                    if (visible && e.launched < 5 && (e.fireTimer -= dt) <= 0) {
                        e.fireTimer = 1.4;
                        e.launched++;
                        this.spawn(Enemy.Mini, e.x + 4, e.ceiling ? e.y + 8 : e.y - 8);
                    }
                    break;

                case Enemy.Boss:
                    this.updateBoss(e, dt, py);
                    break;
            }

            if (e.kind === Enemy.Boss) continue;
            const [w, h] = ENEMY_SIZE[e.kind];
            const gone = GROUNDED.has(e.kind)
                ? e.x + w < this.camX - 8
                : e.x + w < this.camX - 8 || e.x > this.camX + SCREEN_WIDTH + 160 || e.y > SCREEN_HEIGHT + 8 || e.y + h < -40;
            if (gone) e.dead = true;
        }
    }

    updateBoss(e, dt, py) {
        const homeX = this.camX + SCREEN_WIDTH - 60;
        if (e.x > homeX) e.x = Math.max(homeX, e.x - 40 * dt);

        const col = Math.floor((e.x + 24) / TILE);
        const rage = 1 + (1 - e.health / this.bossMaxHealth); // faster as the core weakens
        const target = clamp(py - 16, this.ceilY(col) + 2, this.floorY(col) - 34);
        e.y += clamp(target - e.y, -30 * rage * dt, 30 * rage * dt);

        if (e.x > homeX || (e.fireTimer -= dt) > 0) return;
        e.fireTimer = 1.6 / rage;
        for (const row of [4, 12, 20, 28]) {
            this.bullets.push({ x: e.x - 8, y: e.y + row - 1, vx: -200 * (1 + this.loop * 0.2), vy: 0, laser: true, ring: false, dead: false });
        }
        this.sounds.push('laser');
    }

    /** Shots wear down Big Core's four shield walls first, then the core itself. */
    hitBoss(e) {
        if (e.walls > 0) {
            if (--e.wallHealth <= 0) {
                e.walls--;
                e.wallHealth = 3;
                this.sounds.push('block');
            } else {
                this.sounds.push('bossHit');
            }
            return;
        }
        if (--e.health > 0) {
            this.sounds.push('bossHit');
            return;
        }

        e.dead = true;
        this.score += 10000 * (1 + this.loop);
        for (let i = 0; i < 8; i++) this.explosions.push({ x: e.x + rand(-4, 36), y: e.y + rand(-4, 20), age: -i * 0.12 });
        this.bullets = [];
        this.sounds.push('bossDie');
        this.clearTimer = 2.5;
        this.boss = null;
    }

    aim(e, speed, { ring = false, spread = 0 } = {}) {
        const [w, h] = ENEMY_SIZE[e.kind];
        const sx = e.x + w / 2 - 2, sy = e.y + h / 2 - 2;
        const dx = this.playerX + SHIP_WIDTH / 2 - sx, dy = this.playerY + SHIP_HEIGHT / 2 + spread - sy;
        const length = Math.max(1, Math.hypot(dx, dy));
        this.bullets.push({ x: sx, y: sy, vx: dx / length * speed, vy: dy / length * speed, laser: false, ring, dead: false });
    }

    updateBullets(dt) {
        for (const b of this.bullets) {
            b.x += b.vx * dt;
            b.y += b.vy * dt;
            const [x, y, w, h] = bulletBox(b);
            if (x + w < this.camX - 8 || x > this.camX + SCREEN_WIDTH + 8 || y + h < 0 || y > SCREEN_HEIGHT) b.dead = true;
            else if (!b.laser && this.solidAt(x + w / 2, y + h / 2)) b.dead = true;
        }
    }

    // ---------- Capsules and damage ----------

    updateCapsules() {
        if (this.playerDying) return;
        for (const c of this.capsules) {
            if (!overlaps(this.playerX, this.playerY, SHIP_WIDTH, SHIP_HEIGHT, c.x, c.y, 8, 8)) continue;
            c.taken = true;
            this.meter = (this.meter + 1) % POWER_NAMES.length;
            this.sounds.push('capsule');
        }
    }

    checkPlayerHits() {
        if (this.playerDying || this.invulnerable > 0 || this.state !== State.Playing || this.clearTimer > 0) return;
        const hx = this.playerX + 3, hy = this.playerY + 2, hw = 10, hh = 4;

        // The force field soaks up shots and collisions (but not the terrain).
        const absorb = () => {
            if (this.shield <= 0) return false;
            this.sounds.push('shield');
            if (--this.shield === 0) this.setMessage('FORCE FIELD DOWN');
            return true;
        };

        for (const b of this.bullets) {
            const [x, y, w, h] = bulletBox(b);
            if (!overlaps(x, y, w, h, hx, hy, hw, hh)) continue;
            b.dead = true;
            if (absorb()) continue;
            this.die();
            return;
        }
        for (const e of this.enemies) {
            if (e.dead || e.kind === Enemy.Volcano) continue;
            const [w, h] = ENEMY_SIZE[e.kind];
            if (!overlaps(e.x + 1, e.y + 1, w - 2, h - 2, hx, hy, hw, hh)) continue;
            if (absorb()) {
                if (e.kind !== Enemy.Boss) this.kill(e);
                continue;
            }
            this.die();
            return;
        }
    }

    die() {
        if (this.playerDying) return;
        this.explosions.push({ x: this.playerX, y: this.playerY - 4, age: 0 });
        this.sounds.push('death');
        this.deathTimer = 1.6;
        this.lives--;
        this.resetPowers();
    }

    respawn() {
        if (this.lives < 0) {
            this.lives = 0;
            this.state = State.GameOver;
            return;
        }
        this.bullets = [];
        this.placePlayer();
        this.invulnerable = 2.5;
    }
}
