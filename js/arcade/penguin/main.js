// Penguin Adventure in the browser: runs the game loop and renderer locally, and only tells the
// Blazor component (HUD, overlays, leaderboard) when what it shows has changed.
import { Game, State, Input } from './game.js';
import { Renderer, WIDTH, HEIGHT } from './renderer.js';
import { createFramebuffer, createInput, startLoop, play, loadHighScore, saveHighScore, createHudPublisher } from '../arcade-canvas.js';

const HIGH_SCORE_KEY = 'penguinHighScore';

const KEYS = {
    ArrowUp: Input.Up, KeyW: Input.Up,
    ArrowDown: Input.Down, KeyS: Input.Down,
    ArrowLeft: Input.Left, KeyA: Input.Left,
    ArrowRight: Input.Right, KeyD: Input.Right,
    Space: Input.Jump, KeyZ: Input.Jump, KeyJ: Input.Jump,
    KeyP: Input.Pause,
};

/**
 * Starts the game on `canvas`. `dotnet` receives OnHud(hud) calls; `controls` is the element
 * holding the on-screen touch buttons. Returns the controller the component drives.
 */
export function mount(canvas, dotnet, controls) {
    canvas.width = WIDTH;
    canvas.height = HEIGHT;
    const framebuffer = createFramebuffer(canvas, WIDTH, HEIGHT);
    const renderer = new Renderer(framebuffer.pixels);
    const game = new Game();
    game.highScore = loadHighScore(HIGH_SCORE_KEY);
    let savedHighScore = game.highScore;

    const input = createInput(canvas, { keys: KEYS, buttonRoot: controls ?? document });
    const hud = createHudPublisher(dotnet);

    const stopLoop = startLoop(dt => {
        game.update(dt, input.mask());
        renderer.render(game);
        framebuffer.present();

        if (game.sounds.length) play(game.sounds);
        if (game.highScore > savedHighScore && game.state !== State.Playing) {
            savedHighScore = game.highScore;
            saveHighScore(HIGH_SCORE_KEY, savedHighScore);
        }

        hud.publish({
            state: game.state,
            paused: game.isPaused,
            stage: game.stageNumber,
            stageName: game.stageName,
            score: game.score,
            highScore: Math.max(game.highScore, game.score),
            lives: game.lives,
            time: Math.ceil(game.timeLeft),
            distance: game.distanceLeft,
            speed: Math.round(game.speed * 3.6),
            fish: game.fish,
            bonus: game.bonus,
            progress: Math.round(game.progress * 100),
            message: game.message,
        });
    });

    return {
        newGame: () => game.startNewGame(),
        nextStage: () => game.nextStage(),
        togglePause: () => game.togglePause(),
        dispose() {
            stopLoop();
            input.dispose();
            hud.dispose();
        },
    };
}
