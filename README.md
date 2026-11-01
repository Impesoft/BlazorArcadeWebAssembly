# BlazorArcade

**BlazorArcade** is a comprehensive, feature-rich web application built with Blazor WebAssembly (.NET 9) that brings classic retro, puzzle, and arcade games directly to your browser with offline PWA support and modern, responsive design.

---

## 🎮 Featured Games & Tools

- **Tetris**: Classic tile-matching puzzle with responsive controls and ghost piece preview.
- **Breakout**: Arcade brick-breaker with paddle control, power-ups, and level progressions.
- **Sudoku**: Interactive grid generator with full solver capabilities (step solver & instant solve).
- **Pacman**: Classic maze runner featuring ghost AI, power pellets, and score tracking.
- **2048**: Sliding tile puzzle with smooth animations and high-score tracking.
- **Platformer**: Action platforming game engine with jumping physics and obstacle courses.
- **Donkey Kong**: Arcade platformer clone featuring jump mechanics, ladders, and barrels.
- **King's Valley**: Retro MSX puzzle-platformer clone with pyramid maze navigation and item collection.
- **Chess**: Classic 2-player chess board implementation with full piece movement and Staunton visuals.
- **Connect 4**: Drop-piece strategy game supporting 2-player local gameplay.
- **Bubble Shooter**: Match-3 bubble launcher with angle preview line and physics bouncing.
- **Eggerland / Lolo**: Grid-based puzzle game with enemy AI, egg shots, and map obstacles.
- **Tangram Master**: Geometric shape-fitting puzzle with SVG pixel-checking validation algorithms.
- **Block Dude**: TI-83 calculator classic puzzle-platformer where you pick up and stack blocks to escape.
- **Flappy Bird**: Arcade side-scroller with physics tap-jumping and pipe collision detection.
- **Block Blast!**: Authentic 8x8 block puzzle with pre-placed starter blocks, combo streak multipliers, and Web Audio sound synthesis.
- **Crypt Crawler 3D**: Wolfenstein-style first-person shooter on a software raycaster written in C#: textured walls, floors and ceilings, sliding doors, z-buffered sprites, ghoul AI with BFS pathfinding, all art generated procedurally, blitted to a canvas through zero-copy `[JSImport]` interop.
- **Knightmare**: Tribute to Konami's 1986 MSX vertical scroller at the MSX's native 256x192 in the TMS9918 palette: forced scrolling that can crush you against terrain, "?" blocks dropping crystals you shoot to cycle (double arrow, fire, boomerang, shield, speed), and a boss at the end of each stage.

---

## 🚀 Key Features

- **PWA & Offline Support**: Service worker integration allowing full offline playability.
- **Web Audio API Synthesis**: Retro sound effects generated directly via JavaScript Web Audio API.
- **Responsive Layouts**: Glassmorphic modern design optimized for desktop, tablet, and mobile touch screens.
- **Local Storage**: High-score persistence across sessions.

---

## 🌐 Online Play & Leaderboards (`BlazorArcade.Server`)

An optional ASP.NET Core backend adds online two-player **Chess** and **Connect 4** and shared **high-score boards**. The arcade stays a static PWA; it only shows online features when `ArcadeBackend:BaseUrl` is set in `wwwroot/appsettings.json` (Development uses `http://localhost:5280/`).

- **Match hub** (SignalR, `/hubs/match`): rooms with 4-letter codes and invite links, nickname-only players, a per-tab token that reclaims your seat after a dropped connection, rematches that swap colours. The server is the referee: every move is checked against the real rules (ChessDotNet, and the same `Connect4Logic` the client uses, shared through `BlazorArcade.Shared`). New turn-based games plug in by implementing `IGameRules`.
- **Scores API**: `GET /api/scores/{game}` and `POST /api/scores/{game}` (SQLite via EF Core), with name clean-up, per-game plausibility caps and per-IP rate limiting. Any game listed in `ScoreGames.MaxScore` can use the `<Leaderboard Game="..." Score="..." />` component.

Run locally (two terminals):
```bash
dotnet run --project BlazorArcade.Server
dotnet run --project BlazorArcade
```

Deploy the server with `BlazorArcade.Server/Dockerfile` (mount a volume at `/app/data` for the scores), add the arcade's origin to `Arcade:AllowedOrigins`, and put the server's URL in the arcade's `wwwroot/appsettings.json`.

---

## 🛠️ Prerequisites

Ensure you have the following installed:
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A modern web browser (e.g., Chrome, Edge, Safari, or Firefox)

---

## 🏃 Setup & Run Instructions

1. **Clone the Repository**
   ```bash
   git clone https://github.com/your-repo/BlazorArcade.git
   cd BlazorArcade
   ```

2. **Restore Dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the Project**
   ```bash
   dotnet build
   ```

4. **Run the Application**
   ```bash
   dotnet run --project BlazorArcade
   ```
   Open your browser and navigate to `http://localhost:5000` (or `https://localhost:5001`).

---

## 🧪 Testing

Run the test suite using:
```bash
dotnet test
```

---

## 📦 Deployment

To publish the application for static hosting (e.g., GitHub Pages, Azure Static Web Apps, Cloudflare Pages):
```bash
dotnet publish -c Release -o publish
```

---

## 📄 License

This project is licensed under the [MIT License](./LICENSE).