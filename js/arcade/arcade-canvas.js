// Shared browser layer for the canvas arcade games (Crypt Crawler 3D, Knightmare, Nemesis,
// Penguin Adventure): a 32-bit framebuffer that blits to a canvas, keyboard/mouse/touch input
// as a bitmask, a requestAnimationFrame loop, and synthesized sound effects.

/** A width x height RGBA framebuffer. `pixels` holds little-endian ABGR words (canvas byte order). */
export function createFramebuffer(canvas, width, height) {
    const ctx = canvas.getContext('2d');
    const image = ctx.createImageData(width, height);
    return {
        pixels: new Uint32Array(image.data.buffer),
        present: () => ctx.putImageData(image, 0, 0),
    };
}

/** Calls frame(dtSeconds) once per animation frame until stopped; long pauses are clamped. */
export function startLoop(frame) {
    let id = 0, last = 0;
    const tick = time => {
        id = requestAnimationFrame(tick);
        const dt = last ? Math.min(time - last, 50) / 1000 : 1 / 60;
        last = time;
        frame(dt);
    };
    id = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(id);
}

const isTyping = e => e.target?.closest?.('input, textarea, select, [contenteditable]');

/**
 * Input as a bitmask.
 * options.keys: { [KeyboardEvent.code]: bit }
 * options.pointerLock: clicking the canvas captures the mouse for looking around; touch-drag turns too.
 * options.fireBit: bit set while the left mouse button is held (pointer-lock games).
 * On-screen buttons anywhere in options.buttonRoot declare their bit with data-input-bit.
 */
export function createInput(canvas, options) {
    const keys = options.keys ?? {};
    const fireBit = options.fireBit ?? 0;
    const buttonRoot = options.buttonRoot ?? document;
    // `tapped` latches key-downs until the next frame reads them, so a press shorter than a frame still counts.
    let keyMask = 0, tapped = 0, touchMask = 0, mouseFire = false, mouseDx = 0, lookId = null, lookX = 0;

    const onKeyDown = e => {
        const bit = keys[e.code];
        if (!bit || isTyping(e)) return;
        keyMask |= bit;
        tapped |= bit;
        e.preventDefault();
    };
    const onKeyUp = e => {
        const bit = keys[e.code];
        if (!bit) return;
        keyMask &= ~bit;
        if (!isTyping(e)) e.preventDefault();
    };
    const onBlur = () => { keyMask = 0; touchMask = 0; mouseFire = false; };

    const onMouseDown = e => {
        if (e.button !== 0) return;
        if (document.pointerLockElement === canvas) mouseFire = true;
        else canvas.requestPointerLock?.();
    };
    const onMouseUp = e => { if (e.button === 0) mouseFire = false; };
    const onMouseMove = e => { if (document.pointerLockElement === canvas) mouseDx += e.movementX; };

    const onTouchStart = e => {
        const t = e.changedTouches[0];
        lookId = t.identifier;
        lookX = t.clientX;
        e.preventDefault();
    };
    const onTouchMove = e => {
        for (const t of e.changedTouches) {
            if (t.identifier !== lookId) continue;
            mouseDx += (t.clientX - lookX) * 2;
            lookX = t.clientX;
        }
        e.preventDefault();
    };
    const onTouchEnd = e => {
        for (const t of e.changedTouches) if (t.identifier === lookId) lookId = null;
    };

    const onButtonDown = e => {
        const el = e.target.closest?.('[data-input-bit]');
        if (!el) return;
        touchMask |= Number(el.dataset.inputBit);
        el.setPointerCapture?.(e.pointerId);
        e.preventDefault();
    };
    const onButtonUp = e => {
        const el = e.target.closest?.('[data-input-bit]');
        if (el) touchMask &= ~Number(el.dataset.inputBit);
    };

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', onBlur);
    buttonRoot.addEventListener('pointerdown', onButtonDown);
    buttonRoot.addEventListener('pointerup', onButtonUp);
    buttonRoot.addEventListener('pointercancel', onButtonUp);
    if (options.pointerLock) {
        canvas.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mouseup', onMouseUp);
        window.addEventListener('mousemove', onMouseMove);
        canvas.addEventListener('touchstart', onTouchStart, { passive: false });
        canvas.addEventListener('touchmove', onTouchMove, { passive: false });
        canvas.addEventListener('touchend', onTouchEnd);
    }

    return {
        mask() {
            const mask = keyMask | tapped | touchMask | (mouseFire ? fireBit : 0);
            tapped = 0;
            return mask;
        },
        takeMouseDx: () => { const dx = mouseDx; mouseDx = 0; return dx; },
        releasePointer: () => { if (document.pointerLockElement === canvas) document.exitPointerLock?.(); },
        dispose() {
            window.removeEventListener('keydown', onKeyDown);
            window.removeEventListener('keyup', onKeyUp);
            window.removeEventListener('blur', onBlur);
            buttonRoot.removeEventListener('pointerdown', onButtonDown);
            buttonRoot.removeEventListener('pointerup', onButtonUp);
            buttonRoot.removeEventListener('pointercancel', onButtonUp);
            canvas.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mouseup', onMouseUp);
            window.removeEventListener('mousemove', onMouseMove);
            canvas.removeEventListener('touchstart', onTouchStart);
            canvas.removeEventListener('touchmove', onTouchMove);
            canvas.removeEventListener('touchend', onTouchEnd);
            if (document.pointerLockElement === canvas) document.exitPointerLock?.();
        },
    };
}

export function loadHighScore(key) {
    try { return Number(localStorage.getItem(key)) || 0; } catch { return 0; }
}

export function saveHighScore(key, score) {
    try { localStorage.setItem(key, String(score)); } catch { /* storage unavailable */ }
}

/**
 * Pushes a HUD snapshot to the Blazor component, but only when it changed and at most
 * every 80 ms: on the Server runtime each call is a round trip over the circuit.
 */
export function createHudPublisher(dotnet) {
    let last = '', pending = null, timer = 0;
    const flush = () => {
        timer = 0;
        if (pending === null) return;
        const json = pending;
        pending = null;
        dotnet.invokeMethodAsync('OnHud', JSON.parse(json)).catch(() => { /* circuit gone */ });
    };
    return {
        publish(hud) {
            const json = JSON.stringify(hud);
            if (json === last) return;
            last = json;
            pending = json;
            if (!timer) timer = setTimeout(flush, 80);
        },
        dispose() { clearTimeout(timer); pending = null; },
    };
}

// ---------- Synthesized sound effects ----------

let audio = null;

function ac() {
    audio ??= new (window.AudioContext || window.webkitAudioContext)();
    if (audio.state === 'suspended') audio.resume();
    return audio;
}

function tone(type, from, to, duration, volume = 0.15, delay = 0) {
    const a = ac();
    const t = a.currentTime + delay;
    const osc = a.createOscillator();
    const gain = a.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(from, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(1, to), t + duration);
    gain.gain.setValueAtTime(volume, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + duration);
    osc.connect(gain).connect(a.destination);
    osc.start(t);
    osc.stop(t + duration);
}

function noise(duration, filterFrom, filterTo, volume = 0.3) {
    const a = ac();
    const t = a.currentTime;
    const buffer = a.createBuffer(1, Math.ceil(a.sampleRate * duration), a.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    const src = a.createBufferSource();
    src.buffer = buffer;
    const filter = a.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(filterFrom, t);
    filter.frequency.exponentialRampToValueAtTime(filterTo, t + duration);
    const gain = a.createGain();
    gain.gain.setValueAtTime(volume, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + duration);
    src.connect(filter).connect(gain).connect(a.destination);
    src.start(t);
}

const arpeggio = (type, notes, step, length, volume) =>
    notes.forEach((f, i) => tone(type, f, f, length, volume, i * step));

const sounds = {
    // Crypt Crawler 3D
    shoot: () => { noise(0.18, 4000, 200, 0.35); tone('square', 160, 50, 0.1, 0.12); },
    empty: () => tone('square', 900, 700, 0.03, 0.08),
    hit: () => tone('square', 260, 110, 0.12, 0.12),
    die: () => { tone('sawtooth', 320, 50, 0.45, 0.14); noise(0.3, 900, 100, 0.12); },
    hurt: () => tone('square', 150, 70, 0.22, 0.16),
    alert: () => tone('sawtooth', 90, 190, 0.25, 0.1),
    door: () => noise(0.5, 500, 80, 0.18),
    pickup: () => { tone('triangle', 660, 660, 0.06, 0.15); tone('triangle', 990, 990, 0.08, 0.15, 0.06); },
    treasure: () => arpeggio('triangle', [880, 1100, 1320, 1760], 0.06, 0.08, 0.13),
    exit: () => arpeggio('square', [440, 554, 659, 880, 1109], 0.1, 0.12, 0.08),
    gameover: () => [392, 330, 262, 196].forEach((f, i) => tone('sawtooth', f, f * 0.95, 0.3, 0.1, i * 0.25)),

    // Knightmare (PSG-style square waves)
    arrow: () => tone('square', 1400, 700, 0.05, 0.05),
    explode: () => { noise(0.25, 3000, 100, 0.2); tone('square', 200, 40, 0.15, 0.06); },
    block: () => tone('square', 300, 600, 0.08, 0.08),
    cycle: () => tone('square', 1200, 1500, 0.05, 0.07),
    powerup: () => arpeggio('square', [523, 659, 784, 1047], 0.05, 0.07, 0.08),
    shield: () => tone('triangle', 900, 300, 0.1, 0.12),
    death: () => { noise(0.6, 2000, 60, 0.25); arpeggio('square', [440, 370, 311, 262, 220], 0.09, 0.1, 0.08); },
    bossHit: () => tone('square', 180, 120, 0.06, 0.08),
    bossDie: () => { noise(1.2, 5000, 50, 0.35); tone('sawtooth', 300, 30, 1.0, 0.1); },
    stageClear: () => arpeggio('square', [523, 523, 659, 784, 659, 784, 1047], 0.12, 0.14, 0.08),


    // Nemesis (also uses explode, powerup, shield, death, bossHit, bossDie, block, empty, stageClear)
    shot: () => tone('square', 1800, 900, 0.04, 0.04),
    laser: () => tone('sawtooth', 2400, 600, 0.12, 0.05),
    capsule: () => arpeggio('square', [880, 1320], 0.04, 0.05, 0.08),

    // Penguin Adventure (also uses pickup, treasure, hurt, stageClear, gameover)
    jump: () => tone('square', 400, 1200, 0.15, 0.06),
    fall: () => tone('square', 900, 120, 0.5, 0.08),
    splash: () => { noise(0.4, 2500, 200, 0.25); tone('triangle', 600, 150, 0.3, 0.06); },
    tick: () => tone('square', 1000, 1000, 0.05, 0.06),
};

export function play(names) {
    for (const name of names) sounds[name]?.();
}
