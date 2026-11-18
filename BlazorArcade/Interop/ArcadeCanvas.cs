using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace BlazorArcade.Interop
{
    /// <summary>
    /// [JSImport] bindings for wwwroot/js/arcade-canvas.js, shared by the games that render into
    /// a C# framebuffer. Present() hands JS a view straight onto .NET memory, so a frame costs one copy.
    /// </summary>
    [SupportedOSPlatform("browser")]
    internal static partial class ArcadeCanvas
    {
        private const string Module = "arcade-canvas";
        private static Task? _loading;

        public static Task EnsureLoadedAsync() =>
            _loading ??= JSHost.ImportAsync(Module, "../js/arcade-canvas.js");

        /// <param name="optionsJson">{ keys: { [KeyboardEvent.code]: bit }, pointerLock: bool, fireBit: number }</param>
        /// <param name="onFrame">Called every animation frame with (dtMs, inputMask, mouseDx).</param>
        [JSImport("start", Module)]
        public static partial void Start(string canvasId, int width, int height, string optionsJson,
            [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>] Action<double, int, double> onFrame);

        [JSImport("stop", Module)]
        public static partial void Stop();

        [JSImport("present", Module)]
        public static partial void Present([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

        /// <summary>Plays a comma-separated list of named sound effects.</summary>
        [JSImport("play", Module)]
        public static partial void Play(string names);

        [JSImport("releasePointer", Module)]
        public static partial void ReleasePointer();

        [JSImport("loadHighScore", Module)]
        public static partial int LoadHighScore(string key);

        [JSImport("saveHighScore", Module)]
        public static partial void SaveHighScore(string key, int score);
    }
}
