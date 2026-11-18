// Penguin Adventure: game state and rules, with no browser dependencies so it can be tested in Node.
// Penta runs down a road into the screen. Distances along the road (s) are in metres; across it
// (x) in road half-widths; height (z) in the same units as x.
import { STAGES, Kind, buildStage } from './stages.js';

export { Kind, STAGES };

export const SCREEN_WIDTH = 256;
export const SCREEN_HEIGHT = 192;

export const State = { Title: 'Title', Playing: 'Playing', StageClear: 'StageClear', GameOver: 'GameOver' };

export const Input = { Up: 1, Down: 2, Left: 4, Right: 8, Jump: 16, Pause: 64 };

/** Running speeds in metres per second: holding back, left alone, and flat out. */
export const Speed = { Min: 10, Cruise: 16, Max: 26, OffRoad: 11 };

const ACCELERATION = 7, BRAKING = 14;
const STEER = 1.6, CURVE_PUSH = 2.2;
export const ROAD_EDGE = 1, VERGE_EDGE = 1.35;
export const JUMP_VELOCITY = 2.4, GRAVITY = 7.5;
const FALL_TIME = 1.6, STUN_TIME = 0.7, READY_TIME = 1.5, TIME_OVER_TIME = 2.5;
const SEAL_PERIOD = 2.4;
const START_LIVES = 3;

export class Game {
    constructor() {
        this.state = State.Title;
        this.highScore = 0;
        this.isPaused = false;
        this.sounds = [];
        this.time = 0;
        this.prevMask = 0;
        this.score = 0;
        this.fish = 0;
        this.lives = START_LIVES;
        this.loop = 0;
        this.loadStage(0);
    }

    startNewGame() {
        this.score = 0;
        this.lives = START_LIVES;
        this.fish = 0;
        this.loop = 0;
        this.isPaused = false;
        this.loadStage(0);
        this.state = State.Playing;
    }

    nextStage() {
        if (this.state !== State.StageClear) return;
        let index = this.stageIndex + 1;
        if (index === STAGES.length) {
            index = 0;
            this.loop++;
        }
        this.loadStage(index);
        this.state = State.Playing;
    }

    togglePause() {
        if (this.state === State.Playing) this.isPaused = !this.isPaused;
    }

    loadStage(index) {
        this.stageIndex = index;
        this.track = buildStage(index, this.loop);
        for (const o of this.track.objects) o.taken = false;
        this.timeLeft = this.track.time;
        this.pos = 0;
        this.x = 0;
        this.z = 0;
        this.vz = 0;
        this.speed = 0;
        this.falling = 0;
        this.fallExit = 0;
        this.stunned = 0;
        this.invulnerable = 0;
        this.ready = READY_TIME;
        this.timeOver = 0;
        this.cursor = 0;
        this.stride = 0;
        this.bonus = 0;
    }

    get stageNumber() { return this.loop * STAGES.length + this.stageIndex + 1; }
    get stageName() { return this.track.name; }
    get stageTheme() { return this.track.theme; }
    get distanceLeft() { return Math.max(0, Math.ceil(this.track.length - this.pos)); }
    get progress() { return Math.min(1, this.pos / this.track.length); }
    get isAirborne() { return this.z > 0; }

    get message() {
        if (this.state !== State.Playing) return null;
        if (this.timeOver > 0) return 'TIME OVER';
        if (this.ready > 0) return this.ready > 0.5 ? `STAGE ${this.stageNumber}` : 'GO!';
        return null;
    }

    curveAt(s) {
        const c = this.track.curve;
        return c[Math.max(0, Math.min(c.length - 1, Math.floor(s)))];
    }

    /** Whether a seal is out of its hole (a wall) rather than down in it (a hole). */
    sealUp(o) { return (this.time + o.phase) % SEAL_PERIOD < SEAL_PERIOD / 2; }

    /** Where a bat is: it swings across the road and swoops between head height and the ground. */
    batPosition(o) {
        const t = this.time * o.speed + o.phase;
        return { x: o.x + Math.sin(t) * 0.45, z: 0.35 + Math.sin(t * 1.7) * 0.3 };
    }

    update(dt, mask) {
        this.sounds = [];
        const pressed = mask & ~this.prevMask;
        this.prevMask = mask;
        if (pressed & Input.Pause) this.togglePause();
        if (this.state !== State.Playing || this.isPaused) return;
        this.time += dt;

        if (this.timeOver > 0) {
            this.timeOver -= dt;
            if (this.timeOver <= 0) this.loseLife();
            return;
        }
        if (this.ready > 0) {
            this.ready -= dt;
            return;
        }

        const before = Math.ceil(this.timeLeft);
        this.timeLeft = Math.max(0, this.timeLeft - dt);
        if (Math.ceil(this.timeLeft) < before && before <= 10) this.sounds.push('tick');
        if (this.timeLeft <= 0) {
            this.timeOver = TIME_OVER_TIME;
            this.speed = 0;
            this.sounds.push('gameover');
            return;
        }

        this.invulnerable = Math.max(0, this.invulnerable - dt);
        if (this.falling > 0) {
            this.falling -= dt;
            if (this.falling <= 0) {
                this.pos = this.fallExit;
                this.invulnerable = 0.8;
            }
            return;
        }

        this.move(dt, mask, pressed);
        if (this.pos >= this.track.length) this.clearStage();
    }

    move(dt, mask, pressed) {
        const offRoad = Math.abs(this.x) > ROAD_EDGE;
        if (this.stunned > 0) {
            this.stunned -= dt;
            this.speed = Math.max(0, this.speed - 30 * dt);
        } else {
            let target = mask & Input.Up ? Speed.Max : mask & Input.Down ? Speed.Min : Speed.Cruise;
            if (offRoad) target = Math.min(target, Speed.OffRoad);
            this.speed = this.speed < target
                ? Math.min(target, this.speed + ACCELERATION * dt)
                : Math.max(target, this.speed - BRAKING * dt);

            const steer = ((mask & Input.Right) ? 1 : 0) - ((mask & Input.Left) ? 1 : 0);
            this.x += steer * STEER * dt * (this.isAirborne ? 0.6 : 1);

            if (pressed & Input.Jump && !this.isAirborne) {
                this.vz = JUMP_VELOCITY;
                this.z = 0.001;
                this.sounds.push('jump');
            }
        }

        // Bends push Penta toward the outside.
        this.x -= this.curveAt(this.pos) * this.speed * CURVE_PUSH * dt;
        this.x = Math.max(-VERGE_EDGE, Math.min(VERGE_EDGE, this.x));

        if (this.isAirborne) {
            this.vz -= GRAVITY * dt;
            this.z += this.vz * dt;
            if (this.z <= 0) {
                this.z = 0;
                this.vz = 0;
            }
        }

        const before = this.pos;
        this.pos += this.speed * dt;
        this.stride += this.speed * dt;
        this.score += Math.floor(this.pos / 10) - Math.floor(before / 10);
        this.collide(before);
    }

    collide(before) {
        const objects = this.track.objects;
        while (this.cursor < objects.length && endOf(objects[this.cursor]) < before - 2) this.cursor++;

        for (let i = this.cursor; i < objects.length && objects[i].s - 2 < this.pos; i++) {
            const o = objects[i];
            if (o.taken) continue;
            const onGround = !this.isAirborne;
            switch (o.kind) {
                case Kind.Crevasse:
                case Kind.Water:
                    if (onGround && this.pos >= o.s && this.pos <= o.s + o.len) return this.fall(o.s + o.len + 0.5, o.kind);
                    break;
                case Kind.Hole:
                    if (onGround && Math.abs(this.pos - o.s) < o.depth / 2 && Math.abs(this.x - o.x) < o.r) return this.fall(o.s + o.depth, o.kind);
                    break;
                case Kind.Seal:
                    if (Math.abs(this.pos - o.s) > 0.7 || Math.abs(this.x - o.x) > o.r + 0.05) break;
                    if (!this.sealUp(o)) {
                        if (onGround) return this.fall(o.s + o.depth, o.kind);
                    } else if (this.z < 0.35 && this.invulnerable <= 0) {
                        return this.stumble();
                    }
                    break;
                case Kind.Rock:
                    if (Math.abs(this.pos - o.s) < 0.6 && Math.abs(this.x - o.x) < 0.3 && this.z < 0.3 && this.invulnerable <= 0) return this.stumble();
                    break;
                case Kind.Bat: {
                    const bat = this.batPosition(o);
                    if (Math.abs(this.pos - o.s) < 0.8 && Math.abs(this.x - bat.x) < 0.2 && Math.abs(this.z + 0.15 - bat.z) < 0.3 && this.invulnerable <= 0) return this.stumble();
                    break;
                }
                case Kind.Fish:
                case Kind.GoldFish:
                    if (Math.abs(this.pos - o.s) < 0.9 && Math.abs(this.x - o.x) < 0.25 && Math.abs(this.z - o.z) < 0.45) {
                        o.taken = true;
                        this.fish++;
                        if (o.kind === Kind.GoldFish) {
                            this.score += 1000;
                            this.timeLeft += 5;
                            this.sounds.push('treasure');
                        } else {
                            this.score += 100;
                            this.sounds.push('pickup');
                        }
                    }
                    break;
            }
        }
    }

    fall(exit, kind) {
        this.falling = FALL_TIME;
        this.fallKind = kind;
        this.fallExit = exit;
        this.speed = 0;
        this.z = 0;
        this.vz = 0;
        this.sounds.push(kind === Kind.Water ? 'splash' : 'fall');
    }

    stumble() {
        this.stunned = STUN_TIME;
        this.speed *= 0.3;
        this.invulnerable = STUN_TIME + 0.6;
        this.sounds.push('hurt');
    }

    clearStage() {
        this.pos = this.track.length;
        this.bonus = Math.ceil(this.timeLeft) * 100;
        this.score += this.bonus;
        this.highScore = Math.max(this.highScore, this.score);
        this.state = State.StageClear;
        this.sounds.push('stageClear');
    }

    loseLife() {
        this.lives--;
        if (this.lives <= 0) {
            this.highScore = Math.max(this.highScore, this.score);
            this.state = State.GameOver;
            return;
        }
        this.loadStage(this.stageIndex);
    }
}

const endOf = o => o.s + (o.len ?? 1);
