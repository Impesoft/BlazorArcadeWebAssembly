// Shared browser layer for the framebuffer games (Crypt Crawler 3D, Knightmare).
// The game renders every frame in C#; this module only blits the pixels to a canvas,
// collects input as a bitmask and plays synthesized sound effects.

let canvas = null, ctx = null, image = null, imageBytes = null;
let onFrame = null, rafId = 0, lastTime = 0;
let keyBits = {}, pointerLock = false, fireBit = 0;
let keyMask = 0, touchMask = 0, mouseFire = false, mouseDx = 0;
let touchLookId = null, touchLookX = 0;

function loop(time) {
    rafId = requestAnimationFrame(loop);
    const dt = lastTime ? Math.min(time - lastTime, 50) : 16;
    lastTime = time;
    const dx = mouseDx;
    mouseDx = 0;
    onFrame(dt, keyMask | touchMask | (mouseFire ? fireBit : 0), dx);
}

const isTyping = e => e.target?.closest?.('input, textarea, select, [contenteditable]');

function onKeyDown(e) {
    const bit = keyBits[e.code];
    if (!bit || isTyping(e)) return;
    keyMask |= bit;
    e.preventDefault();
}

function onKeyUp(e) {
    const bit = keyBits[e.code];
    if (!bit) return;
    keyMask &= ~bit;
    if (!isTyping(e)) e.preventDefault();
}

function onBlur() {
    keyMask = 0;
    touchMask = 0;
    mouseFire = false;
}

function onMouseDown(e) {
    if (e.button !== 0) return;
    if (document.pointerLockElement === canvas) mouseFire = true;
    else canvas.requestPointerLock?.();
}

function onMouseUp(e) {
    if (e.button === 0) mouseFire = false;
}

function onMouseMove(e) {
    if (document.pointerLockElement === canvas) mouseDx += e.movementX;
}

// Dragging a finger across the view turns the camera (pointer-lock games only).
function onTouchStart(e) {
    const t = e.changedTouches[0];
    touchLookId = t.identifier;
    touchLookX = t.clientX;
    e.preventDefault();
}

function onTouchMove(e) {
    for (const t of e.changedTouches) {
        if (t.identifier !== touchLookId) continue;
        mouseDx += (t.clientX - touchLookX) * 2;
        touchLookX = t.clientX;
    }
    e.preventDefault();
}

function onTouchEnd(e) {
    for (const t of e.changedTouches) {
        if (t.identifier === touchLookId) touchLookId = null;
    }
}

// On-screen buttons declare their input bit with data-input-bit.
function onButtonDown(e) {
    const el = e.target.closest?.('[data-input-bit]');
    if (!el) return;
    touchMask |= Number(el.dataset.inputBit);
    el.setPointerCapture?.(e.pointerId);
    e.preventDefault();
}

function onButtonUp(e) {
    const el = e.target.closest?.('[data-input-bit]');
    if (!el) return;
    touchMask &= ~Number(el.dataset.inputBit);
}

/**
 * options: { keys: { [KeyboardEvent.code]: bit }, pointerLock: bool, fireBit: number }
 * frameCallback(dtMs, inputMask, mouseDx) is called once per animation frame.
 */
export function start(canvasId, width, height, optionsJson, frameCallback) {
    stop();
    canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const options = JSON.parse(optionsJson);
    keyBits = options.keys ?? {};
    pointerLock = !!options.pointerLock;
    fireBit = options.fireBit ?? 0;

    ctx = canvas.getContext('2d');
    image = ctx.createImageData(width, height);
    imageBytes = new Uint8Array(image.data.buffer); // .NET's copyTo wants a plain Uint8Array
    onFrame = frameCallback;
    lastTime = 0;

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', onBlur);
    document.addEventListener('pointerdown', onButtonDown);
    document.addEventListener('pointerup', onButtonUp);
    document.addEventListener('pointercancel', onButtonUp);
    if (pointerLock) {
        canvas.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mouseup', onMouseUp);
        window.addEventListener('mousemove', onMouseMove);
        canvas.addEventListener('touchstart', onTouchStart, { passive: false });
        canvas.addEventListener('touchmove', onTouchMove, { passive: false });
        canvas.addEventListener('touchend', onTouchEnd);
    }

    rafId = requestAnimationFrame(loop);
}

export function stop() {
    cancelAnimationFrame(rafId);
    rafId = 0;
    onFrame = null;
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    window.removeEventListener('blur', onBlur);
    window.removeEventListener('mouseup', onMouseUp);
    window.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('pointerdown', onButtonDown);
    document.removeEventListener('pointerup', onButtonUp);
    document.removeEventListener('pointercancel', onButtonUp);
    if (canvas) {
        canvas.removeEventListener('mousedown', onMouseDown);
        canvas.removeEventListener('touchstart', onTouchStart);
        canvas.removeEventListener('touchmove', onTouchMove);
        canvas.removeEventListener('touchend', onTouchEnd);
        if (document.pointerLockElement === canvas) document.exitPointerLock?.();
    }
    keyMask = touchMask = 0;
    mouseFire = false;
    canvas = ctx = image = imageBytes = null;
}

// Receives a view straight onto the .NET framebuffer; copy it out before returning.
export function present(pixels) {
    if (!ctx) return;
    pixels.copyTo(imageBytes);
    ctx.putImageData(image, 0, 0);
}

export function releasePointer() {
    if (canvas && document.pointerLockElement === canvas) document.exitPointerLock?.();
}

export function loadHighScore(key) {
    try { return Number(localStorage.getItem(key)) || 0; } catch { return 0; }
}

export function saveHighScore(key, score) {
    try { localStorage.setItem(key, String(score)); } catch { /* storage unavailable */ }
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
};

export function play(names) {
    for (const name of names.split(',')) sounds[name]?.();
}
