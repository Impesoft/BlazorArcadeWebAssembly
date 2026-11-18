using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using BlazorArcade.GameLogic;
using BlazorArcade.Interop;

namespace BlazorArcade.Pages
{
    [SupportedOSPlatform("browser")]
    public partial class Raycaster : IDisposable
    {
        [Inject] private NavigationManager _nav { get; set; } = default!;

        private const string HighScoreKey = "cryptCrawlerHighScore";

        private static readonly string InputOptions = $$"""
            {
              "pointerLock": true,
              "fireBit": {{(int)RaycasterInput.Fire}},
              "keys": {
                "KeyW": {{(int)RaycasterInput.Forward}}, "ArrowUp": {{(int)RaycasterInput.Forward}},
                "KeyS": {{(int)RaycasterInput.Back}}, "ArrowDown": {{(int)RaycasterInput.Back}},
                "KeyA": {{(int)RaycasterInput.StrafeLeft}}, "KeyD": {{(int)RaycasterInput.StrafeRight}},
                "ArrowLeft": {{(int)RaycasterInput.TurnLeft}}, "ArrowRight": {{(int)RaycasterInput.TurnRight}},
                "ControlLeft": {{(int)RaycasterInput.Fire}}, "ControlRight": {{(int)RaycasterInput.Fire}}, "KeyF": {{(int)RaycasterInput.Fire}},
                "Space": {{(int)RaycasterInput.Use}}, "KeyE": {{(int)RaycasterInput.Use}},
                "ShiftLeft": {{(int)RaycasterInput.Run}}, "ShiftRight": {{(int)RaycasterInput.Run}},
                "KeyM": {{(int)RaycasterInput.Map}}, "Tab": {{(int)RaycasterInput.Map}},
                "KeyP": {{(int)RaycasterInput.Pause}}
              }
            }
            """;

        private readonly RaycasterGame _game = new();
        private readonly RaycasterRenderer _renderer = new();
        private HudSnapshot _hud;
        private bool _started;
        private int _savedHighScore;

        private readonly record struct HudSnapshot(
            RaycasterState State, bool Paused, int Level, int Score, int HighScore, int Health, int Ammo,
            int Kills, int TotalKills, int Treasures, int TotalTreasures, string? Message);

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || !OperatingSystem.IsBrowser()) return;

            await ArcadeCanvas.EnsureLoadedAsync();
            _game.HighScore = _savedHighScore = ArcadeCanvas.LoadHighScore(HighScoreKey);
            ArcadeCanvas.Start("rc-canvas", RaycasterRenderer.Width, RaycasterRenderer.Height, InputOptions, OnFrame);
            _started = true;
        }

        private void OnFrame(double dtMs, int input, double mouseDx)
        {
            _game.Update(dtMs / 1000.0, (RaycasterInput)input, mouseDx);
            _renderer.Render(_game);
            ArcadeCanvas.Present(_renderer.Buffer);

            if (_game.Sounds.Count > 0) ArcadeCanvas.Play(string.Join(',', _game.Sounds));
            if (_game.HighScore > _savedHighScore)
            {
                _savedHighScore = _game.HighScore;
                ArcadeCanvas.SaveHighScore(HighScoreKey, _savedHighScore);
            }
            if (_game.State != RaycasterState.Playing || _game.IsPaused) ArcadeCanvas.ReleasePointer();

            var hud = new HudSnapshot(_game.State, _game.IsPaused, _game.LevelIndex + 1, _game.Score, _game.HighScore,
                _game.Health, _game.Ammo, _game.Kills, _game.TotalKills, _game.Treasures, _game.TotalTreasures, _game.Message);
            if (hud != _hud)
            {
                _hud = hud;
                StateHasChanged();
            }
        }

        private void NewGame() => _game.StartNewGame();

        private void NextLevel() => _game.NextLevel();

        private void TogglePause() => _game.TogglePause();

        private string HealthClass => _game.Health switch
        {
            > 60 => "good",
            > 25 => "warn",
            _ => "danger",
        };

        public void Dispose()
        {
            if (_started) ArcadeCanvas.Stop();
        }
    }
}
