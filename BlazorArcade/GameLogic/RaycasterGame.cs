using System;
using System.Collections.Generic;

namespace BlazorArcade.GameLogic
{
    public enum RaycasterState { Title, Playing, LevelComplete, GameOver, Victory }

    public enum ActorKind { Ghoul, Health, Ammo, Treasure, Pillar, Barrel, Lamp }

    public enum GhoulState { Idle, Chase, Attack, Hurt, Dead }

    [Flags]
    public enum RaycasterInput
    {
        None = 0,
        Forward = 1,
        Back = 2,
        StrafeLeft = 4,
        StrafeRight = 8,
        TurnLeft = 16,
        TurnRight = 32,
        Fire = 64,
        Use = 128,
        Run = 256,
        Map = 512,
        Pause = 1024,
    }

    public sealed class RaycasterActor
    {
        public ActorKind Kind;
        public double X, Y;
        public bool Removed;
        public GhoulState State;
        public int Health;
        public double StateTimer;
        public double AttackCooldown;
        public double AnimTime;
        public bool AttackDealt;

        public bool IsBlocking =>
            !Removed && (Kind is ActorKind.Pillar or ActorKind.Barrel || (Kind == ActorKind.Ghoul && State != GhoulState.Dead));
    }

    public sealed class RaycasterDoor
    {
        public int X, Y;
        /// <summary>True when the jambs are above and below, so the panel spans the Y axis and is hit by rays crossing X lines.</summary>
        public bool AlongY;
        public double Open;
        public int Direction;
        public double HoldTimer;
        public bool IsPassable => Open > 0.85;
    }

    /// <summary>
    /// Crypt Crawler 3D: a Wolfenstein-style first-person maze shooter. This class owns the world
    /// and the rules; <see cref="RaycasterRenderer"/> turns it into pixels.
    /// </summary>
    public sealed class RaycasterGame
    {
        public const double PlaneLength = 0.66; // ~66 degree field of view
        private const double PlayerRadius = 0.25;
        private const double GhoulRadius = 0.3;

        public char[,] Map { get; private set; } = new char[1, 1];
        public RaycasterDoor?[,] Doors { get; private set; } = new RaycasterDoor?[1, 1];
        public int MapWidth { get; private set; }
        public int MapHeight { get; private set; }
        public List<RaycasterActor> Actors { get; } = new();

        public double PosX, PosY, Angle;
        public double DirX => Math.Cos(Angle);
        public double DirY => Math.Sin(Angle);

        public RaycasterState State { get; private set; } = RaycasterState.Title;
        public bool IsPaused { get; private set; }
        public bool ShowMap { get; private set; }

        public int LevelIndex { get; private set; }
        public string LevelName => RaycasterLevels.All[LevelIndex].Name;
        public int Score { get; private set; }
        public int HighScore { get; set; }
        public int Health { get; private set; } = 100;
        public int Ammo { get; private set; } = 16;
        public int Kills { get; private set; }
        public int TotalKills { get; private set; }
        public int Treasures { get; private set; }
        public int TotalTreasures { get; private set; }
        public double LevelTime { get; private set; }
        public int LevelBonus { get; private set; }

        public double WeaponCooldown { get; private set; }
        public double MuzzleFlash { get; private set; }
        public double DamageFlash { get; private set; }
        public double PickupFlash { get; private set; }
        public double BobPhase { get; private set; }
        public double BobAmount { get; private set; }

        public string? Message { get; private set; }
        private double _messageTimer;

        /// <summary>Sound effect names raised during the last update, drained by the page.</summary>
        public List<string> Sounds { get; } = new();

        private readonly Random _rng = new();
        private RaycasterInput _prevInput;
        private int[,] _flow = new int[1, 1];
        private int _flowCellX = -1, _flowCellY = -1;

        public RaycasterGame()
        {
            LoadLevel(0);
        }

        // ---------- Game flow ----------

        public void StartNewGame()
        {
            Score = 0;
            Health = 100;
            Ammo = 16;
            LoadLevel(0);
            State = RaycasterState.Playing;
            IsPaused = false;
        }

        public void NextLevel()
        {
            if (State != RaycasterState.LevelComplete) return;
            if (LevelIndex + 1 >= RaycasterLevels.All.Length)
            {
                State = RaycasterState.Victory;
                return;
            }
            LoadLevel(LevelIndex + 1);
            State = RaycasterState.Playing;
        }

        public void TogglePause()
        {
            if (State == RaycasterState.Playing) IsPaused = !IsPaused;
        }

        private void LoadLevel(int index)
        {
            LevelIndex = index;
            var rows = RaycasterLevels.All[index].Rows;
            MapHeight = rows.Length;
            MapWidth = rows[0].Length;
            Map = new char[MapWidth, MapHeight];
            Doors = new RaycasterDoor?[MapWidth, MapHeight];
            _flow = new int[MapWidth, MapHeight];
            _flowCellX = _flowCellY = -1;
            Actors.Clear();
            Kills = TotalKills = Treasures = TotalTreasures = 0;
            LevelTime = 0;
            ShowMap = false;
            Angle = 0;

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    char c = rows[y][x];
                    Map[x, y] = c;
                    double cx = x + 0.5, cy = y + 0.5;
                    switch (c)
                    {
                        case 'P': PosX = cx; PosY = cy; Map[x, y] = '.'; break;
                        case 'g': AddActor(ActorKind.Ghoul, cx, cy); TotalKills++; break;
                        case 'h': AddActor(ActorKind.Health, cx, cy); break;
                        case 'a': AddActor(ActorKind.Ammo, cx, cy); break;
                        case 't': AddActor(ActorKind.Treasure, cx, cy); TotalTreasures++; break;
                        case 'c': AddActor(ActorKind.Pillar, cx, cy); break;
                        case 'b': AddActor(ActorKind.Barrel, cx, cy); break;
                        case 'l': AddActor(ActorKind.Lamp, cx, cy); break;
                    }
                    if (char.IsLower(c)) Map[x, y] = '.';
                }
            }

            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    if (Map[x, y] != 'D') continue;
                    Doors[x, y] = new RaycasterDoor
                    {
                        X = x,
                        Y = y,
                        AlongY = IsWallChar(CellAt(x, y - 1)) && IsWallChar(CellAt(x, y + 1)),
                    };
                }
            }
        }

        private void AddActor(ActorKind kind, double x, double y)
        {
            var actor = new RaycasterActor { Kind = kind, X = x, Y = y, AnimTime = _rng.NextDouble() };
            if (kind == ActorKind.Ghoul) actor.Health = 3 + LevelIndex;
            Actors.Add(actor);
        }

        // ---------- Map queries ----------

        public static bool IsWallChar(char c) => c is '#' or 'B' or 'M' or 'W' or 'E';

        public char CellAt(int x, int y) =>
            (uint)x < (uint)MapWidth && (uint)y < (uint)MapHeight ? Map[x, y] : '#';

        public RaycasterDoor? DoorAt(int x, int y) =>
            (uint)x < (uint)MapWidth && (uint)y < (uint)MapHeight ? Doors[x, y] : null;

        /// <summary>Walls and doors that are not open enough to walk or see through.</summary>
        public bool IsSolid(int x, int y)
        {
            char c = CellAt(x, y);
            if (c == 'D') return !Doors[x, y]!.IsPassable;
            return IsWallChar(c);
        }

        public bool HasLineOfSight(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            int steps = (int)(dist / 0.1);
            for (int i = 1; i < steps; i++)
            {
                double t = i / (double)steps;
                if (IsSolid((int)(x0 + dx * t), (int)(y0 + dy * t))) return false;
            }
            return true;
        }

        private bool CanOccupy(double x, double y, double radius, RaycasterActor? self, double fromX, double fromY)
        {
            for (int cy = (int)(y - radius); cy <= (int)(y + radius); cy++)
                for (int cx = (int)(x - radius); cx <= (int)(x + radius); cx++)
                    if (IsSolid(cx, cy)) return false;

            foreach (var a in Actors)
            {
                if (a == self || !a.IsBlocking) continue;
                if (Blocks(a.X, a.Y, radius + (a.Kind == ActorKind.Ghoul ? GhoulRadius : 0.35))) return false;
            }
            if (self != null && Blocks(PosX, PosY, radius + PlayerRadius)) return false;
            return true;

            // Only block movement that gets closer, so overlapping bodies can always separate.
            bool Blocks(double ox, double oy, double min)
            {
                double dNew = (x - ox) * (x - ox) + (y - oy) * (y - oy);
                double dOld = (fromX - ox) * (fromX - ox) + (fromY - oy) * (fromY - oy);
                return dNew < min * min && dNew < dOld;
            }
        }

        private bool TryMove(ref double x, ref double y, double dx, double dy, double radius, RaycasterActor? self)
        {
            bool moved = false;
            if (dx != 0 && CanOccupy(x + dx, y, radius, self, x, y)) { x += dx; moved = true; }
            if (dy != 0 && CanOccupy(x, y + dy, radius, self, x, y)) { y += dy; moved = true; }
            return moved;
        }

        // ---------- Update ----------

        public void Update(double dt, RaycasterInput input, double mouseDx)
        {
            var pressed = input & ~_prevInput;
            _prevInput = input;
            Sounds.Clear();

            if (State == RaycasterState.Title)
            {
                Angle += dt * 0.25; // attract mode: slowly look around the first room
                return;
            }
            if (State != RaycasterState.Playing) return;

            if ((pressed & RaycasterInput.Pause) != 0) IsPaused = !IsPaused;
            if (IsPaused) return;

            LevelTime += dt;
            if ((pressed & RaycasterInput.Map) != 0) ShowMap = !ShowMap;

            UpdatePlayer(dt, input, mouseDx);
            if ((input & RaycasterInput.Fire) != 0) Fire((pressed & RaycasterInput.Fire) != 0);
            if ((pressed & RaycasterInput.Use) != 0) Use();

            UpdateDoors(dt);
            UpdateFlowField();
            foreach (var a in Actors)
            {
                if (a.Kind == ActorKind.Ghoul) UpdateGhoul(a, dt);
            }
            CollectPickups();

            WeaponCooldown = Math.Max(0, WeaponCooldown - dt);
            MuzzleFlash = Math.Max(0, MuzzleFlash - dt);
            DamageFlash = Math.Max(0, DamageFlash - dt);
            PickupFlash = Math.Max(0, PickupFlash - dt);
            if (_messageTimer > 0 && (_messageTimer -= dt) <= 0) Message = null;

            if (Health <= 0)
            {
                Health = 0;
                State = RaycasterState.GameOver;
                Sounds.Add("gameover");
                SaveHighScore();
            }
        }

        private void UpdatePlayer(double dt, RaycasterInput input, double mouseDx)
        {
            double turn = ((input & RaycasterInput.TurnRight) != 0 ? 1 : 0) - ((input & RaycasterInput.TurnLeft) != 0 ? 1 : 0);
            Angle += turn * 2.6 * dt + mouseDx * 0.0028;

            double forward = ((input & RaycasterInput.Forward) != 0 ? 1 : 0) - ((input & RaycasterInput.Back) != 0 ? 1 : 0);
            double strafe = ((input & RaycasterInput.StrafeRight) != 0 ? 1 : 0) - ((input & RaycasterInput.StrafeLeft) != 0 ? 1 : 0);
            double dirX = DirX, dirY = DirY;
            double mx = dirX * forward - dirY * strafe;
            double my = dirY * forward + dirX * strafe;
            double len = Math.Sqrt(mx * mx + my * my);

            if (len > 0)
            {
                double speed = ((input & RaycasterInput.Run) != 0 ? 5.0 : 3.2) * dt / len;
                TryMove(ref PosX, ref PosY, mx * speed, my * speed, PlayerRadius, null);
                BobPhase += dt * ((input & RaycasterInput.Run) != 0 ? 14 : 10);
                BobAmount = Math.Min(1, BobAmount + dt * 4);
            }
            else
            {
                BobAmount = Math.Max(0, BobAmount - dt * 4);
            }
        }

        private void Fire(bool justPressed)
        {
            if (WeaponCooldown > 0) return;
            if (Ammo <= 0)
            {
                if (justPressed)
                {
                    Sounds.Add("empty");
                    SetMessage("Out of ammo!");
                    WeaponCooldown = 0.3;
                }
                return;
            }

            Ammo--;
            WeaponCooldown = 0.32;
            MuzzleFlash = 0.09;
            Sounds.Add("shoot");

            RaycasterActor? target = null;
            double best = double.MaxValue;
            double dirX = DirX, dirY = DirY;
            foreach (var a in Actors)
            {
                if (a.Kind != ActorKind.Ghoul || a.State == GhoulState.Dead) continue;
                double dx = a.X - PosX, dy = a.Y - PosY;

                // Gunfire wakes up anything within earshot.
                if (a.State == GhoulState.Idle && dx * dx + dy * dy < 100) a.State = GhoulState.Chase;

                double along = dx * dirX + dy * dirY;
                if (along <= 0) continue;

                double across = Math.Abs(dx * -dirY + dy * dirX);
                if (across > GhoulRadius + 0.05 || along >= best) continue;
                if (!HasLineOfSight(PosX, PosY, a.X, a.Y)) continue;
                best = along;
                target = a;
            }
            if (target == null) return;

            target.Health -= _rng.Next(1, 3);
            if (target.Health <= 0)
            {
                target.State = GhoulState.Dead;
                target.StateTimer = 0;
                Kills++;
                Score += 100;
                Sounds.Add("die");
            }
            else
            {
                target.State = GhoulState.Hurt;
                target.StateTimer = 0;
                Sounds.Add("hit");
            }
        }

        private void Use()
        {
            foreach (double reach in new[] { 0.6, 1.1 })
            {
                int cx = (int)(PosX + DirX * reach), cy = (int)(PosY + DirY * reach);
                char c = CellAt(cx, cy);
                if (c == 'D')
                {
                    var door = Doors[cx, cy]!;
                    if (door.Open >= 1 && !IsDoorOccupied(door)) door.Direction = -1;
                    else door.Direction = 1;
                    Sounds.Add("door");
                    return;
                }
                if (c == 'E')
                {
                    CompleteLevel();
                    return;
                }
                if (IsWallChar(c)) return;
            }
        }

        private void CompleteLevel()
        {
            int parSeconds = 90 + LevelIndex * 30;
            LevelBonus = Math.Max(0, (parSeconds - (int)LevelTime) * 10);
            if (TotalKills > 0 && Kills == TotalKills) LevelBonus += 1000;
            if (TotalTreasures > 0 && Treasures == TotalTreasures) LevelBonus += 1000;
            Score += LevelBonus;
            State = RaycasterState.LevelComplete;
            Sounds.Add("exit");
            SaveHighScore();
        }

        private void SaveHighScore()
        {
            if (Score > HighScore) HighScore = Score;
        }

        private void SetMessage(string text)
        {
            Message = text;
            _messageTimer = 1.8;
        }

        // ---------- Doors ----------

        private bool IsDoorOccupied(RaycasterDoor door)
        {
            if (Overlaps(PosX, PosY, PlayerRadius)) return true;
            foreach (var a in Actors)
            {
                if (a.IsBlocking && Overlaps(a.X, a.Y, GhoulRadius)) return true;
            }
            return false;

            bool Overlaps(double x, double y, double r) =>
                x + r > door.X && x - r < door.X + 1 && y + r > door.Y && y - r < door.Y + 1;
        }

        private void UpdateDoors(double dt)
        {
            foreach (var door in Doors)
            {
                if (door == null) continue;
                if (door.Direction != 0)
                {
                    if (door.Direction < 0 && IsDoorOccupied(door))
                    {
                        door.Direction = 1;
                    }
                    door.Open = Math.Clamp(door.Open + door.Direction * dt / 0.7, 0, 1);
                    if (door.Open >= 1 && door.Direction > 0) { door.Direction = 0; door.HoldTimer = 4; }
                    if (door.Open <= 0 && door.Direction < 0) door.Direction = 0;
                }
                else if (door.Open >= 1 && (door.HoldTimer -= dt) <= 0)
                {
                    if (IsDoorOccupied(door)) door.HoldTimer = 1;
                    else
                    {
                        door.Direction = -1;
                        if (Math.Abs(door.X + 0.5 - PosX) + Math.Abs(door.Y + 0.5 - PosY) < 10) Sounds.Add("door");
                    }
                }
            }
        }

        // ---------- Ghouls ----------

        /// <summary>Breadth-first distance map to the player's cell, through floors and doors, so ghouls can path around walls.</summary>
        private void UpdateFlowField()
        {
            int px = (int)PosX, py = (int)PosY;
            if (px == _flowCellX && py == _flowCellY) return;
            _flowCellX = px;
            _flowCellY = py;

            for (int y = 0; y < MapHeight; y++)
                for (int x = 0; x < MapWidth; x++)
                    _flow[x, y] = int.MaxValue;

            var queue = new Queue<(int X, int Y)>();
            _flow[px, py] = 0;
            queue.Enqueue((px, py));
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                int next = _flow[x, y] + 1;
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (IsWallChar(CellAt(nx, ny)) || _flow[nx, ny] <= next) continue;
                    _flow[nx, ny] = next;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        private void UpdateGhoul(RaycasterActor g, double dt)
        {
            g.AnimTime += dt;
            g.StateTimer += dt;
            g.AttackCooldown -= dt;
            if (g.State == GhoulState.Dead) return;

            double dx = PosX - g.X, dy = PosY - g.Y;
            double dist = Math.Max(0.001, Math.Sqrt(dx * dx + dy * dy));

            switch (g.State)
            {
                case GhoulState.Idle:
                    if (dist < 14 && HasLineOfSight(g.X, g.Y, PosX, PosY))
                    {
                        g.State = GhoulState.Chase;
                        Sounds.Add("alert");
                    }
                    break;

                case GhoulState.Hurt:
                    if (g.StateTimer > 0.22) g.State = GhoulState.Chase;
                    break;

                case GhoulState.Attack:
                    if (!g.AttackDealt && g.StateTimer >= 0.35)
                    {
                        g.AttackDealt = true;
                        if (dist < 1.4 && HasLineOfSight(g.X, g.Y, PosX, PosY))
                        {
                            Health -= _rng.Next(6, 14) + LevelIndex * 2;
                            DamageFlash = 0.35;
                            Sounds.Add("hurt");
                        }
                    }
                    if (g.StateTimer >= 0.6)
                    {
                        g.State = GhoulState.Chase;
                        g.AttackCooldown = 0.7;
                    }
                    break;

                case GhoulState.Chase:
                    bool canSee = HasLineOfSight(g.X, g.Y, PosX, PosY);
                    if (dist < 1.1 && canSee && g.AttackCooldown <= 0)
                    {
                        g.State = GhoulState.Attack;
                        g.StateTimer = 0;
                        g.AttackDealt = false;
                        break;
                    }
                    ChaseStep(g, dt, canSee, dx / dist, dy / dist);
                    break;
            }
        }

        private void ChaseStep(RaycasterActor g, double dt, bool canSee, double towardX, double towardY)
        {
            double speed = (1.5 + LevelIndex * 0.25) * dt;
            double mx = towardX, my = towardY;

            if (!canSee)
            {
                int cx = (int)g.X, cy = (int)g.Y;
                int best = _flow[cx, cy];
                int tx = cx, ty = cy;
                foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                {
                    if ((uint)nx < (uint)MapWidth && (uint)ny < (uint)MapHeight && _flow[nx, ny] < best)
                    {
                        best = _flow[nx, ny];
                        tx = nx;
                        ty = ny;
                    }
                }
                if (best == int.MaxValue) return;

                var door = DoorAt(tx, ty);
                if (door != null && !door.IsPassable)
                {
                    if (door.Direction <= 0) { door.Direction = 1; Sounds.Add("door"); }
                    return;
                }
                mx = tx + 0.5 - g.X;
                my = ty + 0.5 - g.Y;
                double len = Math.Sqrt(mx * mx + my * my);
                if (len < 0.001) return;
                mx /= len;
                my /= len;
            }

            TryMove(ref g.X, ref g.Y, mx * speed, my * speed, GhoulRadius, g);
        }

        // ---------- Pickups ----------

        private void CollectPickups()
        {
            foreach (var a in Actors)
            {
                if (a.Removed || a.Kind is not (ActorKind.Health or ActorKind.Ammo or ActorKind.Treasure)) continue;
                double dx = a.X - PosX, dy = a.Y - PosY;
                if (dx * dx + dy * dy > 0.55 * 0.55) continue;

                switch (a.Kind)
                {
                    case ActorKind.Health:
                        if (Health >= 100) continue;
                        Health = Math.Min(100, Health + 25);
                        SetMessage("Health +25");
                        Sounds.Add("pickup");
                        break;
                    case ActorKind.Ammo:
                        if (Ammo >= 99) continue;
                        Ammo = Math.Min(99, Ammo + 8);
                        SetMessage("Ammo +8");
                        Sounds.Add("pickup");
                        break;
                    case ActorKind.Treasure:
                        Score += 500;
                        Treasures++;
                        SetMessage("Treasure +500");
                        Sounds.Add("treasure");
                        break;
                }
                a.Removed = true;
                PickupFlash = 0.25;
            }
        }
    }
}
