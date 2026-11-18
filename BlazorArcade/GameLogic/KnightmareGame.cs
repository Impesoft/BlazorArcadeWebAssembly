using System;
using System.Collections.Generic;

namespace BlazorArcade.GameLogic
{
    public enum KnightmareState { Title, Playing, StageClear, GameOver }

    [Flags]
    public enum KnightmareInput
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,
        Fire = 16,
        Pause = 32,
    }

    public enum KnightmareWeapon { Arrow, DoubleArrow, Fire, Boomerang }

    /// <summary>Power-up crystal types, in the order shooting a crystal cycles through them.</summary>
    public enum KnightmareItem { DoubleArrow, Fire, Boomerang, Shield, Speed, Bonus }

    public enum KnightmareEnemyKind { Bat, Skeleton, Knight, Wisp, Boss }

    public sealed class KnightmareShot
    {
        public double X, Y, VX, VY, Age;
        public KnightmareWeapon Weapon;
        public bool Returning;
        public bool Dead;
        public double W => Weapon switch { KnightmareWeapon.Fire => 6, KnightmareWeapon.Boomerang => 8, _ => 3 };
        public double H => Weapon switch { KnightmareWeapon.Fire => 6, KnightmareWeapon.Boomerang => 8, _ => 8 };
    }

    public sealed class KnightmareBullet
    {
        public double X, Y, VX, VY;
        public bool Dead;
    }

    public sealed class KnightmareEnemy
    {
        public KnightmareEnemyKind Kind;
        public double X, Y, VX, VY, Age, BaseX, FireTimer, Flash;
        public int Health;
        public bool Dead;
        public int Size => Kind == KnightmareEnemyKind.Boss ? 32 : 16;
    }

    public sealed class KnightmarePickup
    {
        public double X, Y, Cooldown;
        public KnightmareItem Type;
        public bool Taken;
    }

    public sealed class KnightmareExplosion
    {
        public double X, Y, Age;
    }

    /// <summary>
    /// A tribute to Konami's Knightmare (MSX, 1986): a vertically scrolling shooter on foot.
    /// The screen scrolls on its own; terrain blocks the knight, and being pushed off the
    /// bottom edge by the scroll is fatal. Shoot "?" blocks for crystals, and shoot crystals
    /// to change what they give you.
    /// </summary>
    public sealed class KnightmareGame
    {
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 192;
        private const int T = KnightmareArt.Tile;
        private const double ScrollSpeed = 24;
        private const double PlayerHitInset = 4;

        public KnightmareState State { get; private set; } = KnightmareState.Title;
        public bool IsPaused { get; private set; }
        public int StageIndex { get; private set; }
        public int Loop { get; private set; }
        public string StageName => KnightmareStages.All[StageIndex].Name;

        public char[,] Map { get; private set; } = new char[1, 1];
        public int MapRows { get; private set; }
        public double CameraY { get; private set; }
        public double Time { get; private set; }
        public double StageProgress => MapRows <= 12 ? 1 : 1 - CameraY / (MapRows * T - ScreenHeight);

        public double PlayerX, PlayerY;
        public bool PlayerDying => _deathTimer > 0;
        public double Invulnerable { get; private set; }
        public double WalkPhase { get; private set; }
        public KnightmareWeapon Weapon { get; private set; }
        public int Shield { get; private set; }
        public int SpeedLevel { get; private set; }

        public int Score { get; private set; }
        public int HighScore { get; set; }
        public int Lives { get; private set; }

        public List<KnightmareShot> Shots { get; } = new();
        public List<KnightmareBullet> Bullets { get; } = new();
        public List<KnightmareEnemy> Enemies { get; } = new();
        public List<KnightmarePickup> Pickups { get; } = new();
        public List<KnightmareExplosion> Explosions { get; } = new();
        public KnightmareEnemy? Boss { get; private set; }
        public int BossMaxHealth { get; private set; }

        public string? Message { get; private set; }
        private double _messageTimer;

        /// <summary>Sound effect names raised during the last update, drained by the page.</summary>
        public List<string> Sounds { get; } = new();

        private readonly Random _rng = new();
        private KnightmareInput _prevInput;
        private double _fireCooldown;
        private double _deathTimer;
        private double _ambientTimer;
        private double _clearTimer;
        private bool _bossSpawned;
        private readonly List<(double Y, double X, char Kind)> _spawns = new();
        private int _nextSpawn;

        public KnightmareGame()
        {
            LoadStage(0);
        }

        private double Difficulty => 1 + StageIndex * 0.25 + Loop * 0.5;

        // ---------- Game flow ----------

        public void StartNewGame()
        {
            Score = 0;
            Lives = 3;
            Loop = 0;
            LoadStage(0);
            State = KnightmareState.Playing;
            IsPaused = false;
        }

        public void NextStage()
        {
            if (State != KnightmareState.StageClear) return;
            int next = StageIndex + 1;
            if (next >= KnightmareStages.All.Length)
            {
                next = 0;
                Loop++;
            }
            LoadStage(next);
            State = KnightmareState.Playing;
        }

        public void TogglePause()
        {
            if (State == KnightmareState.Playing) IsPaused = !IsPaused;
        }

        private void LoadStage(int index)
        {
            StageIndex = index;
            var rows = KnightmareStages.All[index].Rows;
            MapRows = rows.Length;
            Map = new char[16, MapRows];
            _spawns.Clear();
            for (int y = 0; y < MapRows; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    char c = rows[y][x];
                    if (c is 'b' or 's' or 'k' or 'w')
                    {
                        _spawns.Add((y * T, x * T, c));
                        c = '.';
                    }
                    Map[x, y] = c;
                }
            }
            _spawns.Sort((a, b) => b.Y.CompareTo(a.Y)); // the scroll meets the bottom ones first
            _nextSpawn = 0;

            CameraY = MapRows * T - ScreenHeight;
            Shots.Clear();
            Bullets.Clear();
            Enemies.Clear();
            Pickups.Clear();
            Explosions.Clear();
            Boss = null;
            _bossSpawned = false;
            _clearTimer = 0;
            _ambientTimer = 3;
            Time = 0;
            PlacePlayer();
            Invulnerable = 1.5;
            SetMessage($"STAGE {index + 1 + Loop * KnightmareStages.All.Length}");
        }

        private void PlacePlayer()
        {
            PlayerX = 120;
            PlayerY = CameraY + 150;
            // Nudge sideways until the knight stands on open ground.
            for (int dx = 0; dx < ScreenWidth && Blocked(PlayerX, PlayerY); dx += 8)
            {
                PlayerX = 120 + (dx / 8 % 2 == 0 ? dx / 2 : -dx / 2);
            }
        }

        private void SetMessage(string text)
        {
            Message = text;
            _messageTimer = 2;
        }

        // ---------- Map queries ----------

        public char TileAt(int col, int row) =>
            (uint)col < 16 && (uint)row < (uint)MapRows ? Map[col, row] : (col < 0 || col >= 16 ? '#' : '.');

        public static bool IsSolid(char c) => c is '#' or 'C' or 'T' or '~' or '?';

        /// <summary>True when the knight's feet-and-body box at (x, y) overlaps solid terrain.</summary>
        private bool Blocked(double x, double y)
        {
            double left = x + PlayerHitInset, right = x + T - PlayerHitInset - 0.01;
            double top = y + 6, bottom = y + T - 0.01;
            for (int row = (int)Math.Floor(top / T); row <= (int)Math.Floor(bottom / T); row++)
                for (int col = (int)Math.Floor(left / T); col <= (int)Math.Floor(right / T); col++)
                    if (IsSolid(TileAt(col, row))) return true;
            return false;
        }

        private static bool Overlaps(double ax, double ay, double aw, double ah, double bx, double by, double bw, double bh) =>
            ax < bx + bw && ax + aw > bx && ay < by + bh && ay + ah > by;

        // ---------- Update ----------

        public void Update(double dt, KnightmareInput input)
        {
            var pressed = input & ~_prevInput;
            _prevInput = input;
            Sounds.Clear();

            if (State == KnightmareState.Title)
            {
                Time += dt;
                return;
            }
            if (State != KnightmareState.Playing) return;
            if ((pressed & KnightmareInput.Pause) != 0) IsPaused = !IsPaused;
            if (IsPaused) return;

            Time += dt;
            if (_messageTimer > 0 && (_messageTimer -= dt) <= 0) Message = null;

            double scroll = 0;
            if (CameraY > 0)
            {
                scroll = Math.Min(CameraY, ScrollSpeed * dt);
                CameraY -= scroll;
            }
            else if (!_bossSpawned)
            {
                SpawnBoss();
            }

            if (_deathTimer > 0)
            {
                if ((_deathTimer -= dt) <= 0) Respawn();
            }
            else
            {
                UpdatePlayer(dt, input, scroll);
            }

            SpawnFromMap();
            SpawnAmbient(dt);
            UpdateShots(dt);
            UpdateEnemies(dt);
            UpdateBullets(dt);
            UpdatePickups(dt);
            CheckPlayerHits();

            foreach (var e in Explosions) e.Age += dt;
            Explosions.RemoveAll(e => e.Age > 0.4);
            Shots.RemoveAll(s => s.Dead);
            Bullets.RemoveAll(b => b.Dead);
            Enemies.RemoveAll(e => e.Dead);
            Pickups.RemoveAll(p => p.Taken || p.Y > CameraY + ScreenHeight + 16);

            if (Score > HighScore) HighScore = Score;

            if (_clearTimer > 0 && (_clearTimer -= dt) <= 0)
            {
                State = KnightmareState.StageClear;
                Sounds.Add("stageClear");
            }
        }

        private void UpdatePlayer(double dt, KnightmareInput input, double scroll)
        {
            Invulnerable = Math.Max(0, Invulnerable - dt);

            double speed = 85 + SpeedLevel * 25;
            double dx = ((input & KnightmareInput.Right) != 0 ? 1 : 0) - ((input & KnightmareInput.Left) != 0 ? 1 : 0);
            double dy = ((input & KnightmareInput.Down) != 0 ? 1 : 0) - ((input & KnightmareInput.Up) != 0 ? 1 : 0);
            if (dx != 0 && dy != 0)
            {
                dx *= 0.7071;
                dy *= 0.7071;
            }
            if (dx != 0 || dy != 0) WalkPhase += dt * 8;

            // Ride along with the scroll, unless terrain is in the way.
            if (scroll > 0 && !Blocked(PlayerX, PlayerY - scroll)) PlayerY -= scroll;

            double nx = Math.Clamp(PlayerX + dx * speed * dt, 0, ScreenWidth - T);
            if (!Blocked(nx, PlayerY)) PlayerX = nx;
            double ny = Math.Max(CameraY, PlayerY + dy * speed * dt);
            ny = Math.Min(ny, CameraY + ScreenHeight - T);
            if (!Blocked(PlayerX, ny)) PlayerY = ny;

            // Pinned against terrain while the screen keeps scrolling: crushed.
            if (PlayerY > CameraY + ScreenHeight - T + 0.5)
            {
                Die();
                return;
            }

            _fireCooldown -= dt;
            if ((input & KnightmareInput.Fire) != 0 && _fireCooldown <= 0) FireWeapon();
        }

        private void FireWeapon()
        {
            int Count(KnightmareWeapon w) => Shots.FindAll(s => s.Weapon == w).Count;
            double cx = PlayerX + T / 2.0, top = PlayerY - 4;

            switch (Weapon)
            {
                case KnightmareWeapon.Arrow:
                    if (Count(Weapon) >= 3) return;
                    AddShot(cx - 1.5, top, 0, -300);
                    _fireCooldown = 0.16;
                    break;
                case KnightmareWeapon.DoubleArrow:
                    if (Count(Weapon) >= 6) return;
                    AddShot(cx - 6, top, 0, -300);
                    AddShot(cx + 3, top, 0, -300);
                    _fireCooldown = 0.16;
                    break;
                case KnightmareWeapon.Fire:
                    if (Count(Weapon) >= 9) return;
                    AddShot(cx - 3, top, -70, -220);
                    AddShot(cx - 3, top, 0, -240);
                    AddShot(cx - 3, top, 70, -220);
                    _fireCooldown = 0.26;
                    break;
                case KnightmareWeapon.Boomerang:
                    if (Count(Weapon) >= 2) return;
                    AddShot(cx - 4, top, 0, -280);
                    _fireCooldown = 0.3;
                    break;
            }
            Sounds.Add("arrow");
        }

        private void AddShot(double x, double y, double vx, double vy) =>
            Shots.Add(new KnightmareShot { X = x, Y = y, VX = vx, VY = vy, Weapon = Weapon });

        private void UpdateShots(double dt)
        {
            foreach (var s in Shots)
            {
                s.Age += dt;
                if (s.Weapon == KnightmareWeapon.Boomerang)
                {
                    if (!s.Returning)
                    {
                        s.VY += 420 * dt;
                        if (s.VY >= 0) s.Returning = true;
                    }
                    else
                    {
                        // Home back in on the knight and get caught.
                        double tx = PlayerX + 4 - s.X, ty = PlayerY + 4 - s.Y;
                        double len = Math.Max(1, Math.Sqrt(tx * tx + ty * ty));
                        s.VX = tx / len * 300;
                        s.VY = ty / len * 300;
                        if (len < 10 || PlayerDying) s.Dead = true;
                    }
                }
                s.X += s.VX * dt;
                s.Y += s.VY * dt;

                if (s.Y < CameraY - 16 || s.Y > CameraY + ScreenHeight || s.X < -16 || s.X > ScreenWidth) s.Dead = true;
                if (s.Weapon == KnightmareWeapon.Fire && s.Age > 0.55) s.Dead = true;
                if (s.Dead) continue;

                HitBlock(s);
                if (!s.Dead) HitPickup(s);
                if (!s.Dead) HitEnemy(s);
            }
        }

        private void HitBlock(KnightmareShot s)
        {
            int col = (int)Math.Floor((s.X + s.W / 2) / T), row = (int)Math.Floor((s.Y + s.H / 2) / T);
            if (TileAt(col, row) != '?') return;
            Map[col, row] = ',';
            Pickups.Add(new KnightmarePickup { X = col * T + 1, Y = row * T + 1, Type = KnightmareItem.DoubleArrow, Cooldown = 0.25 });
            Score += 50;
            Sounds.Add("block");
            if (s.Weapon != KnightmareWeapon.Boomerang) s.Dead = true;
        }

        private void HitPickup(KnightmareShot s)
        {
            foreach (var p in Pickups)
            {
                if (p.Cooldown > 0 || !Overlaps(s.X, s.Y, s.W, s.H, p.X, p.Y, 14, 14)) continue;
                p.Type = (KnightmareItem)(((int)p.Type + 1) % 6);
                p.Cooldown = 0.2;
                Sounds.Add("cycle");
                if (s.Weapon != KnightmareWeapon.Boomerang) s.Dead = true;
                return;
            }
        }

        private void HitEnemy(KnightmareShot s)
        {
            foreach (var e in Enemies)
            {
                if (e.Dead || !Overlaps(s.X, s.Y, s.W, s.H, e.X + 2, e.Y + 2, e.Size - 4, e.Size - 4)) continue;
                if (s.Weapon == KnightmareWeapon.Boomerang)
                {
                    if (e.Flash > 0) continue; // a piercing boomerang only hits each pass once
                }
                else
                {
                    s.Dead = true;
                }
                Damage(e, s.Weapon == KnightmareWeapon.Boomerang ? 2 : 1);
                return;
            }
        }

        private void Damage(KnightmareEnemy e, int amount)
        {
            e.Health -= amount;
            e.Flash = 0.12;
            if (e.Health > 0)
            {
                if (e.Kind == KnightmareEnemyKind.Boss) Sounds.Add("bossHit");
                return;
            }

            e.Dead = true;
            Score += e.Kind switch
            {
                KnightmareEnemyKind.Bat => 100,
                KnightmareEnemyKind.Wisp => 150,
                KnightmareEnemyKind.Skeleton => 200,
                KnightmareEnemyKind.Knight => 500,
                _ => 5000 * (1 + Loop),
            };
            if (e.Kind == KnightmareEnemyKind.Boss)
            {
                for (int i = 0; i < 8; i++)
                    Explosions.Add(new KnightmareExplosion { X = e.X + _rng.Next(-4, 20), Y = e.Y + _rng.Next(-4, 20), Age = -i * 0.12 });
                Bullets.Clear();
                Sounds.Add("bossDie");
                _clearTimer = 2.5;
                Boss = null;
            }
            else
            {
                Explosions.Add(new KnightmareExplosion { X = e.X, Y = e.Y });
                Sounds.Add("explode");
            }
        }

        // ---------- Enemies ----------

        private void SpawnFromMap()
        {
            while (_nextSpawn < _spawns.Count && _spawns[_nextSpawn].Y >= CameraY - T)
            {
                var (y, x, kind) = _spawns[_nextSpawn++];
                Spawn(kind switch
                {
                    'b' => KnightmareEnemyKind.Bat,
                    's' => KnightmareEnemyKind.Skeleton,
                    'k' => KnightmareEnemyKind.Knight,
                    _ => KnightmareEnemyKind.Wisp,
                }, x, Math.Min(y, CameraY - T));
            }
        }

        private void SpawnAmbient(double dt)
        {
            if (_bossSpawned || (_ambientTimer -= dt) > 0) return;
            _ambientTimer = Math.Max(1.2, 3.2 / Difficulty) + _rng.NextDouble();
            var kind = _rng.NextDouble() < 0.75 ? KnightmareEnemyKind.Bat : KnightmareEnemyKind.Wisp;
            Spawn(kind, _rng.Next(8, ScreenWidth - 24), CameraY - T);
        }

        private void Spawn(KnightmareEnemyKind kind, double x, double y)
        {
            Enemies.Add(new KnightmareEnemy
            {
                Kind = kind,
                X = x,
                Y = y,
                BaseX = x,
                Health = kind switch
                {
                    KnightmareEnemyKind.Skeleton => 2,
                    KnightmareEnemyKind.Knight => 5 + Loop * 2,
                    _ => 1,
                },
                FireTimer = 1 + _rng.NextDouble() * 1.5,
                Age = _rng.NextDouble() * 3,
            });
        }

        private void SpawnBoss()
        {
            _bossSpawned = true;
            BossMaxHealth = 40 + StageIndex * 15 + Loop * 25;
            Boss = new KnightmareEnemy
            {
                Kind = KnightmareEnemyKind.Boss,
                X = ScreenWidth / 2 - 16,
                Y = CameraY - 40,
                Health = BossMaxHealth,
                FireTimer = 2,
            };
            Enemies.Add(Boss);
            SetMessage("BOSS!");
        }

        private void UpdateEnemies(double dt)
        {
            double px = PlayerX + 8, py = PlayerY + 8;
            foreach (var e in Enemies)
            {
                e.Age += dt;
                e.Flash = Math.Max(0, e.Flash - dt);
                double d = Difficulty;

                switch (e.Kind)
                {
                    case KnightmareEnemyKind.Bat:
                        e.Y += 70 * d * dt;
                        e.X = e.BaseX + Math.Sin(e.Age * 3) * 40;
                        break;

                    case KnightmareEnemyKind.Skeleton:
                        e.Y += 32 * dt;
                        e.X += Math.Sign(px - (e.X + 8)) * 18 * dt;
                        if ((e.FireTimer -= dt) <= 0)
                        {
                            e.FireTimer = 2.2 / d;
                            Aim(e, px, py, 90 * d);
                        }
                        break;

                    case KnightmareEnemyKind.Knight:
                        e.Y += 22 * dt;
                        e.X = e.BaseX + Math.Sin(e.Age * 1.3) * 30;
                        if ((e.FireTimer -= dt) <= 0)
                        {
                            e.FireTimer = 2.4 / d;
                            for (int i = -1; i <= 1; i++) Aim(e, px + i * 40, py, 85 * d);
                        }
                        break;

                    case KnightmareEnemyKind.Wisp:
                        if (e.Age < 7)
                        {
                            double tx = px - (e.X + 8), ty = py - (e.Y + 8);
                            double len = Math.Max(1, Math.Sqrt(tx * tx + ty * ty));
                            e.VX += tx / len * 110 * dt;
                            e.VY += ty / len * 110 * dt;
                        }
                        else
                        {
                            e.VY -= 120 * dt; // gives up and drifts away
                        }
                        double speed = Math.Sqrt(e.VX * e.VX + e.VY * e.VY), max = 60 * d;
                        if (speed > max) { e.VX *= max / speed; e.VY *= max / speed; }
                        e.X += e.VX * dt;
                        e.Y += e.VY * dt;
                        break;

                    case KnightmareEnemyKind.Boss:
                        UpdateBoss(e, dt, px, py);
                        break;
                }

                if (e.Kind != KnightmareEnemyKind.Boss &&
                    (e.Y > CameraY + ScreenHeight + 8 || e.Y < CameraY - 64 || e.X < -32 || e.X > ScreenWidth + 16))
                {
                    e.Dead = true;
                }
            }
        }

        private void UpdateBoss(KnightmareEnemy e, double dt, double px, double py)
        {
            double homeY = CameraY + 22;
            if (e.Y < homeY) e.Y = Math.Min(homeY, e.Y + 40 * dt);
            e.X = ScreenWidth / 2 - 16 + Math.Sin(e.Age * 0.9) * 90;

            if ((e.FireTimer -= dt) > 0 || e.Y < homeY) return;

            double rage = 1 + (1 - e.Health / (double)BossMaxHealth); // fires faster as it weakens
            e.FireTimer = 1.4 / rage;
            if (((int)(e.Age / 3)) % 2 == 0)
            {
                for (int i = -2; i <= 2; i++) Aim(e, px + i * 28, py, 100 * Difficulty);
            }
            else
            {
                double offset = e.Age;
                for (int i = 0; i < 10; i++)
                {
                    double a = offset + i * Math.PI / 5;
                    Bullets.Add(new KnightmareBullet { X = e.X + 14, Y = e.Y + 20, VX = Math.Cos(a) * 75, VY = Math.Abs(Math.Sin(a)) * 75 + 20 });
                }
            }
        }

        private void Aim(KnightmareEnemy e, double tx, double ty, double speed)
        {
            double sx = e.X + e.Size / 2 - 2, sy = e.Y + e.Size / 2;
            double dx = tx - sx, dy = ty - sy;
            double len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            Bullets.Add(new KnightmareBullet { X = sx, Y = sy, VX = dx / len * speed, VY = dy / len * speed });
        }

        private void UpdateBullets(double dt)
        {
            foreach (var b in Bullets)
            {
                b.X += b.VX * dt;
                b.Y += b.VY * dt;
                if (b.Y > CameraY + ScreenHeight || b.Y < CameraY - 16 || b.X < -8 || b.X > ScreenWidth) b.Dead = true;
            }
        }

        // ---------- Pickups and damage ----------

        private void UpdatePickups(double dt)
        {
            foreach (var p in Pickups)
            {
                p.Cooldown = Math.Max(0, p.Cooldown - dt);
                if (PlayerDying || !Overlaps(PlayerX + 2, PlayerY + 2, 12, 12, p.X, p.Y, 14, 14)) continue;
                p.Taken = true;
                Sounds.Add("powerup");
                switch (p.Type)
                {
                    case KnightmareItem.DoubleArrow: Weapon = KnightmareWeapon.DoubleArrow; SetMessage("DOUBLE ARROW"); break;
                    case KnightmareItem.Fire: Weapon = KnightmareWeapon.Fire; SetMessage("FIRE"); break;
                    case KnightmareItem.Boomerang: Weapon = KnightmareWeapon.Boomerang; SetMessage("BOOMERANG"); break;
                    case KnightmareItem.Shield: Shield = 3; SetMessage("SHIELD"); break;
                    case KnightmareItem.Speed: SpeedLevel = Math.Min(2, SpeedLevel + 1); SetMessage("SPEED UP"); break;
                    case KnightmareItem.Bonus: Score += 1000; SetMessage("1000 PTS"); break;
                }
            }
        }

        private void CheckPlayerHits()
        {
            if (PlayerDying || Invulnerable > 0 || State != KnightmareState.Playing || _clearTimer > 0) return;
            double hx = PlayerX + PlayerHitInset, hy = PlayerY + 3, hw = T - PlayerHitInset * 2, hh = T - 4;

            foreach (var b in Bullets)
            {
                // The shield stops shots coming at the knight from the front.
                if (Shield > 0 && b.VY > 0 && Overlaps(b.X, b.Y, 4, 4, PlayerX + 1, PlayerY - 4, 14, 5))
                {
                    b.Dead = true;
                    Sounds.Add("shield");
                    if (--Shield == 0) SetMessage("SHIELD BROKEN");
                    continue;
                }
                if (Overlaps(b.X, b.Y, 4, 4, hx, hy, hw, hh))
                {
                    Die();
                    return;
                }
            }
            foreach (var e in Enemies)
            {
                if (Overlaps(e.X + 3, e.Y + 3, e.Size - 6, e.Size - 6, hx, hy, hw, hh))
                {
                    Die();
                    return;
                }
            }
        }

        private void Die()
        {
            if (PlayerDying) return;
            Explosions.Add(new KnightmareExplosion { X = PlayerX, Y = PlayerY });
            Sounds.Add("death");
            _deathTimer = 1.6;
            Lives--;
            Weapon = KnightmareWeapon.Arrow;
            Shield = 0;
            SpeedLevel = 0;
        }

        private void Respawn()
        {
            if (Lives < 0)
            {
                Lives = 0;
                State = KnightmareState.GameOver;
                return;
            }
            Bullets.Clear();
            PlacePlayer();
            Invulnerable = 2.5;
        }
    }
}
