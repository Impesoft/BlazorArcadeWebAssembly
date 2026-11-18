using BlazorArcade.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorArcade.Pages
{
    /// <summary>
    /// Penguin Adventure, a tribute to Konami's MSX classic. The game loop and renderer run in the
    /// browser (wwwroot/js/arcade/penguin); this component shows the HUD, overlays and leaderboard
    /// from the snapshots the game sends whenever they change.
    /// </summary>
    public partial class PenguinAdventure : IAsyncDisposable
    {
        [Inject] private NavigationManager _nav { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        public sealed record Hud(
            string State, bool Paused, int Stage, string StageName, int Score, int HighScore, int Lives,
            int Time, int Distance, int Speed, int Fish, int Bonus, int Progress, string? Message);

        private Hud _hud = new("Title", false, 1, "", 0, 0, 3, 0, 0, 0, 0, 0, 0, null);
        private ElementReference _canvas, _controls;
        private IJSObjectReference? _module, _game;
        private DotNetObjectReference<PenguinAdventure>? _self;

        private bool IsPlaying => _hud.State == "Playing";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender) return;
            _self = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "/js/arcade/penguin/main.js");
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
