using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using BlazorArcade.GameLogic;
using BlazorArcade.Interop;

namespace BlazorArcade.Pages
{
    [SupportedOSPlatform("browser")]
    public partial class Knightmare : IDisposable
    {
        [Inject] private NavigationManager _nav { get; set; } = default!;

        private const string HighScoreKey = "knightmareHighScore";

        private static readonly string InputOptions = $$"""
            {
              "keys": {
                "ArrowUp": {{(int)KnightmareInput.Up}}, "KeyW": {{(int)KnightmareInput.Up}},
                "ArrowDown": {{(int)KnightmareInput.Down}}, "KeyS": {{(int)KnightmareInput.Down}},
                "ArrowLeft": {{(int)KnightmareInput.Left}}, "KeyA": {{(int)KnightmareInput.Left}},
                "ArrowRight": {{(int)KnightmareInput.Right}}, "KeyD": {{(int)KnightmareInput.Right}},
                "Space": {{(int)KnightmareInput.Fire}}, "KeyZ": {{(int)KnightmareInput.Fire}}, "KeyJ": {{(int)KnightmareInput.Fire}},
                "KeyP": {{(int)KnightmareInput.Pause}}
              }
            }
            """;

        private readonly KnightmareGame _game = new();
        private readonly KnightmareRenderer _renderer = new();
        private HudSnapshot _hud;
        private bool _started;
        private int _savedHighScore;

        private readonly record struct HudSnapshot(
            KnightmareState State, bool Paused, int Stage, int Score, int HighScore, int Lives,
            KnightmareWeapon Weapon, int Shield, int Speed, int Progress, string? Message);

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || !OperatingSystem.IsBrowser()) return;

            await ArcadeCanvas.EnsureLoadedAsync();
            _game.HighScore = _savedHighScore = ArcadeCanvas.LoadHighScore(HighScoreKey);
            ArcadeCanvas.Start("km-canvas", KnightmareRenderer.Width, KnightmareRenderer.Height, InputOptions, OnFrame);
            _started = true;
        }

        private void OnFrame(double dtMs, int input, double mouseDx)
        {
            _game.Update(dtMs / 1000.0, (KnightmareInput)input);
            _renderer.Render(_game);
            ArcadeCanvas.Present(_renderer.Buffer);

            if (_game.Sounds.Count > 0) ArcadeCanvas.Play(string.Join(',', _game.Sounds));
            if (_game.HighScore > _savedHighScore && _game.State != KnightmareState.Playing)
            {
                _savedHighScore = _game.HighScore;
                ArcadeCanvas.SaveHighScore(HighScoreKey, _savedHighScore);
            }

            var hud = new HudSnapshot(_game.State, _game.IsPaused, StageNumber, _game.Score, _game.HighScore, _game.Lives,
                _game.Weapon, _game.Shield, _game.SpeedLevel, (int)(_game.StageProgress * 100), _game.Message);
            if (hud != _hud)
            {
                _hud = hud;
                StateHasChanged();
            }
        }

        private int StageNumber => _game.StageIndex + 1 + _game.Loop * KnightmareStages.All.Length;

        private string WeaponName => _game.Weapon switch
        {
            KnightmareWeapon.DoubleArrow => "DOUBLE",
            KnightmareWeapon.Fire => "FIRE",
            KnightmareWeapon.Boomerang => "BOOMERANG",
            _ => "ARROW",
        };

        private void NewGame() => _game.StartNewGame();

        private void NextStage() => _game.NextStage();

        private void TogglePause() => _game.TogglePause();

        public void Dispose()
        {
            if (_started) ArcadeCanvas.Stop();
        }
    }
}
