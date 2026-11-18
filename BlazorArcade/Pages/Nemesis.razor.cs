using BlazorArcade.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorArcade.Pages
{
    /// <summary>
    /// Nemesis, a tribute to Konami's MSX shooter (Gradius). The game loop and renderer run in the
    /// browser (wwwroot/js/arcade/nemesis); this component shows the HUD, the power meter, overlays
    /// and leaderboard from the snapshots the game sends whenever they change.
    /// </summary>
    public partial class Nemesis : IAsyncDisposable
    {
        [Inject] private NavigationManager _nav { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        public sealed record Hud(
            string State, bool Paused, int Stage, string StageName, int Score, int HighScore, int Lives,
            int Meter, int Speed, bool Missile, string Weapon, int Options, int Shield, int Progress, string? Message);

        private static readonly string[] Slots = { "SPEED", "MISSILE", "DOUBLE", "LASER", "OPTION", "?" };
        private const int MaxSpeed = 5, MaxOptions = 2;

        private Hud _hud = new("Title", false, 1, "", 0, 0, 3, -1, 0, false, "Normal", 0, 0, 0, null);
        private ElementReference _canvas, _controls;
        private IJSObjectReference? _module, _game;
        private DotNetObjectReference<Nemesis>? _self;

        private bool IsPlaying => _hud.State == "Playing";

        /// <summary>Lit when POWER would buy it; dimmed once there's nothing more to gain from it.</summary>
        private string SlotClass(int slot)
        {
            bool maxed = slot switch
            {
                0 => _hud.Speed >= MaxSpeed,
                1 => _hud.Missile,
                2 => _hud.Weapon == "Double",
                3 => _hud.Weapon == "Laser",
                4 => _hud.Options >= MaxOptions,
                _ => _hud.Shield > 0,
            };
            bool owned = slot switch
            {
                0 => _hud.Speed > 0,
                4 => _hud.Options > 0,
                _ => maxed,
            };
            return slot == _hud.Meter ? "lit" : maxed ? "maxed" : owned ? "owned" : "";
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "/js/arcade/nemesis/main.js");
            _game = await _module.InvokeAsync<IJSObjectReference>("mount", _canvas, _self, _controls);
        }

        [JSInvokable]
        public Task OnHud(Hud hud) => InvokeAsync(() =>
        {
            _hud = hud;
            StateHasChanged();
        });

        private Task Command(string name) => _game?.InvokeVoidAsync(name).AsTask() ?? Task.CompletedTask;

        private Task NewGame() => Command("newGame");

        private Task NextStage() => Command("nextStage");

        private Task TogglePause() => Command("togglePause");

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_game != null)
                {
                    await _game.InvokeVoidAsync("dispose");
                    await _game.DisposeAsync();
                }
                if (_module != null) await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser is already gone, and the game with it.
            }
            _self?.Dispose();
        }
    }
}
