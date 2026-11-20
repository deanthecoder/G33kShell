// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Runs an arcade area using the extracted animation and collision data.
/// </summary>
/// <remarks>
/// Enemy decisions are approximations; rendering and simulation have independent clocks.
/// </remarks>
internal sealed class Game
{
    private readonly ArcadeData m_data;
    private int m_nextId;
    private int m_spawnIndex;
    private int m_stopIndex;
    private int m_clearTicks;
    private int m_magicPeak;
    private int m_pendingHero = -1;
    public HeroDefinition Character { get; private set; }
    public int[][][][] MagicEffects => m_data.Magic[Character.MagicKey];
    public List<Fighter> Actors { get; } = [];
    public Fighter Hero { get; }
    public int TickCount { get; private set; }
    public double CameraX { get; private set; }
    public double CameraY { get; private set; }
    public int AreaIndex => m_data.AreaIndex;
    public double Progress => 4096 - CameraX - 7;
    public int Potions { get; private set; } = 1;
    public int Lives { get; private set; } = 2;
    public int Kills { get; private set; }
    public int Throws { get; private set; }
    public bool Complete { get; private set; }
    public CampRound Camp { get; private set; }
    public bool IsCamp => Camp != null;
    public bool GameOver { get; private set; }
    public int MagicTick { get; private set; } = -1;
    public int MagicLevel { get; private set; }
    public bool CanAdvance => !IsCamp && !Complete && !GameOver && Hero.Health > 0 && Progress < m_data.Stops[m_stopIndex] &&
        !Actors.Any(a => a.IsEnemy && !a.IsGone);
    public bool Boss => Progress >= m_data.Stops[^1];

    public Game(ArcadeData data, int character = 0)
    {
        m_data = data;
        Character = HeroDefinition.All[Math.Clamp(character, 0, 2)];
        CameraX = data.InitialCameraX;
        CameraY = CameraRange()[0];
        Hero = Add(ActorKind.Hero, Character.Moves, Character.Palette, 48, 4096 - CameraX + 60, 4240);
        Hero.Facing = 1;
        Hero.Z = FindFloor(Hero.X, Hero.Z, keepVisible: true);
        Hero.Y = Ground(Hero.X, Hero.Z);
    }

    public Game ContinueIn(ArcadeData nextArea)
    {
        var next = new Game(nextArea, Array.IndexOf(HeroDefinition.All, Character));
        next.Hero.Health = Hero.Health;
        next.Lives = Lives;
        next.Potions = Potions;
        return next;
    }

    public void StartCamp()
    {
        if (!Complete || IsCamp || AreaIndex == ArcadeData.AreaCount - 1)
            return;
        Camp = new CampRound(this, m_data);
        Complete = false;
        TickCount = 0;
        var range = CameraRange();
        CameraY = Math.Clamp(150 - (Project(Hero.X, Hero.Z, Hero.Y).Y - CameraY), range[0], range[1]);
    }

    public void SelectCharacter(int character) => m_pendingHero = Math.Clamp(character, 0, 2);

    private void ApplyCharacterSelection()
    {
        // Finish an in-flight action before swapping scripts, so a grab cannot strand its victim.
        var body = Hero.Mount ?? Hero;
        if (m_pendingHero < 0 || MagicTick >= 0 || !body.IsFree)
            return;
        Character = HeroDefinition.All[m_pendingHero];
        m_pendingHero = -1;
        Hero.Moves = Character.Moves;
        Hero.Palette = Character.Palette;
        Hero.Combo = Hero.ComboTimer = 0;
        Hero.SetState(FighterState.Idle, Character.Moves.Idle);
        Potions = Math.Min(Potions, Character.MagicLevels);
        if (Hero.Mount != null)
        {
            body.Moves = Character.RidingMoves(body.Kind);
            body.Combo = body.ComboTimer = 0;
            body.SetState(FighterState.Idle, body.Moves.Idle);
        }
    }
    internal Fighter Add(ActorKind kind, Moves moves, int palette, int hp, double x, double z)
    {
        var actor = new Fighter(++m_nextId, m_data, kind, moves, palette, hp) { X = x, Z = z, Facing = -1, Cooldown = 35 + m_nextId * 13 % 45 };
        actor.Y = Ground(x, z);
        Actors.Add(actor);
        return actor;
    }

    public double Ground(double x, double z)
    {
        var column = (int)Math.Floor((x - 4096) / 8);
        var row = (int)Math.Floor((z - 3968) / 8);
        return column < 0 || column >= 256 || row < 0 || row >= 64 ? 3584 : m_data.HeightMap[column * 64 + row] * 8 + 3584;
    }

    public double FindFloor(double x, double z, bool keepVisible = false)
    {
        var cameraY = keepVisible ? CameraRange()[0] : CameraY;
        for (var d = 0; d < 160; d += 2)
        foreach (var candidate in new[] { z + d, z - d })
        {
            var ground = Ground(x, candidate);
            if (ground > 3840 && ground < 5248 && Ground(x, candidate + 6) == ground && Ground(x, candidate - 6) == ground &&
                (!keepVisible || Project(x, candidate, ground).Y + cameraY - CameraY is >= 48 and <= 196))
                return candidate;
        }
        return z;
    }

    public (double X, double Y) Project(double x, double z, double y) => (x - 4096 + CameraX + 8, z - 4096 - Math.Floor((z - 4096) / 4) + y - 4096 + CameraY);

    private int[] CameraRange() => m_data.CameraPath.OrderBy(p => Math.Abs(p.Key - (int)Math.Floor(CameraX / 8))).First().Value;

    public void Tick(GameInput input)
    {
        ApplyCharacterSelection();
        if (Complete || GameOver)
            return;
        TickCount++;
        Camp?.BeforeTick();
        if (MagicTick >= 0)
        {
            MagicTick++;
            Hero.Animation.Tick();
            if (MagicTick == m_magicPeak)
            {
                foreach (var enemy in Actors.Where(a => a.IsEnemy && a.Health > 0 && a.HeldBy == null && Project(a.X, a.Z, a.Y).X is >= -20 and <= 340).ToArray())
                {
                    Hit(Hero, enemy, new[] { 0, 4, 8, 16, 24, 32, 48 }[MagicLevel], true);
                    // Grounded victims crumple after the spell; airborne victims still fall naturally.
                    if (enemy.Y + 1 >= Ground(enemy.X, enemy.Z))
                    {
                        enemy.Y = Ground(enemy.X, enemy.Z);
                        enemy.Vx = enemy.Vy = 0;
                        enemy.SetState(FighterState.Collapsing, enemy.Moves.Collapse);
                    }
                }
            }
            if (MagicTick >= Math.Max(60, MagicEffects[MagicLevel - 1].Length))
            {
                MagicTick = -1;
                Hero.SetState(FighterState.Idle, Hero.Moves.Idle);
            }
            return;
        }
        while (!IsCamp && m_spawnIndex < m_data.Spawns.Length && m_data.Spawns[m_spawnIndex].Trigger <= Progress)
            Spawn(m_data.Spawns[m_spawnIndex++]);
        foreach (var actor in Actors.ToArray())
        {
            if (actor.IsGone || actor.Mount != null)
                continue;
            actor.Age++;
            actor.StateTicks++;
            actor.Invulnerable = Math.Max(0, actor.Invulnerable - 1);
            actor.Cooldown = Math.Max(0, actor.Cooldown - 1);
            actor.MountCooldown = Math.Max(0, actor.MountCooldown - 1);
            actor.Flash = Math.Max(0, actor.Flash - 1);
            if (actor.HitTimer > 0 && --actor.HitTimer == 0)
                actor.Hits = 0;
            if (actor.ComboTimer > 0 && --actor.ComboTimer == 0)
                actor.Combo = 0;
            if (actor.Kind == ActorKind.Fireball)
            {
                UpdateFireball(actor);
                continue;
            }
            if (actor.Kind == ActorKind.Lightning)
            {
                UpdateLightning(actor);
                continue;
            }
            if (actor.Kind == ActorKind.Flame)
            {
                var owner = actor.Owner;
                actor.X = owner.X;
                actor.Z = owner.Z + 1;
                actor.Y = owner.Y;
                actor.Facing = owner.Facing;
                if (actor.Animation.Tick() || owner.Rider == null || owner.State != FighterState.Attacking)
                    actor.SetState(FighterState.Gone);
                continue;
            }
            if (actor.Kind is ActorKind.Villager or ActorKind.Thief or ActorKind.Potion or ActorKind.Food or ActorKind.CampPotion or ActorKind.Campfire)
            {
                UpdateProp(actor);
                continue;
            }
            if (actor.IsMount && actor.Rider == null)
            {
                UpdateFreeMount(actor);
                continue;
            }
            var command = actor == Hero || actor.Rider == Hero ? input : EnemyInput(actor);
            UpdateFighter(actor, command);
            if (actor.Rider != null)
            {
                actor.Rider.X = actor.X;
                actor.Rider.Y = actor.Y;
                actor.Rider.Z = actor.Z;
            }
        }
        Actors.RemoveAll(a => a.IsGone && a != Hero);
        var body = Hero.Mount ?? Hero;
        body.X = Math.Clamp(body.X, Progress + 12, Progress + 305);
        if (IsCamp)
        {
            Camp.AfterTick();
            Complete = Camp.Complete;
            return;
        }
        var screenX = Project(body.X, body.Z, body.Y).X;
        var stop = m_data.Stops[m_stopIndex];
        if (screenX > 160)
            CameraX = Math.Max(4096 - 7 - stop, CameraX - Math.Min(4, screenX - 160));
        var cameraRange = CameraRange();
        var z = body.Z - 4096;
        var desiredY = 150 - (z - Math.Floor(z / 4) + Math.Min(body.Y, Ground(body.X, body.Z)) - 4096);
        CameraY += Math.Clamp(Math.Clamp(desiredY, cameraRange[0], cameraRange[1]) - CameraY, -2, 2);
        var enemies = Actors.Any(a => a.IsEnemy && !a.IsGone);
        if (Progress >= stop && !enemies)
        {
            if (++m_clearTicks > 30)
            {
                if (m_stopIndex < m_data.Stops.Length - 1)
                    m_stopIndex++;
                else if (m_spawnIndex == m_data.Spawns.Length)
                    Complete = true;
                m_clearTicks = 0;
            }
        }
        else
            m_clearTicks = 0;
    }

    internal void Spawn(SpawnDefinition spawn)
    {
        if (IsCamp)
            spawn = spawn with { Z = spawn.Z + Camp.DepthOffset };
        switch (spawn.Routine)
        {
            case 0x1b454: Add(ActorKind.Heninger, Moves.StrongHeninger, 0x1c, 16, spawn.X, spawn.Z); break;
            case 0x20414: Add(ActorKind.Longmoan, Moves.StrongLongmoan, 0x7a, 16, spawn.X, spawn.Z); break;
            case 0x23bda: Add(ActorKind.Skeleton, Moves.Skeleton, 0x73, 16, spawn.X, spawn.Z); break;
            case 0x2538a: Add(ActorKind.Skeleton, Moves.Skeleton, 0x6f, 16, spawn.X, spawn.Z); break;
            case 0x25352:
                if (spawn.Parameter == 0x1005)
                    Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 64, spawn.X, spawn.Z);
                else
                    Add(ActorKind.Skeleton, Moves.Skeleton, 0x70, 16, spawn.X, spawn.Z);
                break;
            case 0x2a338: Add(ActorKind.Knight, Moves.Knight, 0x87, 32, spawn.X, spawn.Z); break;
            case 0x29364: Add(ActorKind.BadBrother, Moves.Brother, 0x39, 32, spawn.X, spawn.Z); break;
            case 0x1a0a8: Add(ActorKind.Heninger, Moves.Heninger, 0x1a, 16, spawn.X, spawn.Z); break;
            case 0x1f016: Add(ActorKind.Longmoan, Moves.FastLongmoan, 0x7b, 16, spawn.X, spawn.Z); break;
            case 0x21854: Add(ActorKind.Amazon, Moves.Amazon, 0x63, 16, spawn.X, spawn.Z); break;
            case 0x21876: Add(ActorKind.Amazon, Moves.Amazon, 0x64, 16, spawn.X, spawn.Z); break;
            case 0x229c8: case 0x2536e: Add(ActorKind.Amazon, Moves.Amazon, 0x65, 16, spawn.X, spawn.Z); break;
            case 0x2f60a: case 0x2f61c: SpawnMount(ActorKind.BlueDragon, spawn); break;
            case 0x306aa: SpawnMount(ActorKind.RedDragon, spawn); break;
            case 0x2a358: Add(ActorKind.Knight, Moves.Knight, 0x86, 32, spawn.X, spawn.Z); break;
            case 0x18b50: Add(ActorKind.Heninger, Moves.Heninger, 0x1d, 16, spawn.X, spawn.Z); break;
            case 0x317b8: case 0x18b28: Add(ActorKind.Heninger, Moves.Heninger, 0x1b, 16, spawn.X, spawn.Z); break;
            case 0x18b3c: case 0x18ba6: Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, spawn.X, spawn.Z); break;
            case 0x1c846: Add(ActorKind.Longmoan, Moves.Longmoan, 0x79, 16, spawn.X, spawn.Z); break;
            case 0x1c868: Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, spawn.X, spawn.Z); break;
            case 0x1dc3e: Add(ActorKind.Longmoan, Moves.Longmoan, 0x77, 16, spawn.X, spawn.Z); break;
            case 0x282ec: case 0x282d4: Add(ActorKind.BadBrother, Moves.Brother, 0x38, 32, spawn.X, spawn.Z); break;
            case 0x2c264: case 0x2c276: case 0x2b10c: SpawnMount(ActorKind.Chicken, spawn); break;
            case 0x17c7c: case 0x17d00: case 0x17d0e: case 0x17d24:
                var food = spawn.Routine == 0x17d00;
                var direction = spawn.X < Progress + 160 ? 1 : -1;
                var thief = Add(ActorKind.Thief, Moves.Thief, food ? 0x3c : 0x3d, 99, spawn.X, FindFloor(spawn.X + direction * 60, spawn.Z));
                thief.FoodThief = food;
                thief.Facing = direction;
                thief.Items = Math.Max(1, spawn.Parameter >> 12);
                thief.SetState(FighterState.Walking, thief.Moves.Walk);
                break;
            case 0x99c0: case 0x99c8: case 0x99da: case 0x99ec: case 0x99fe: case 0x9a9a:
            case 0x9c08: case 0x9eb6: case 0x9a5a:
                var villager = Add(ActorKind.Villager, Moves.Thief, spawn.Routine == 0x9a9a ? 0x64 : 0x4d, 1, spawn.X, spawn.Z);
                villager.Animation.Play(spawn.Routine is 0x99fe or 0x9a9a ? 0xa10e : 0x9fe8);
                break;
        }
    }

    private void SpawnMount(ActorKind kind, SpawnDefinition spawn)
    {
        var riderKind = AreaIndex switch { 0 => ActorKind.Heninger, 1 or 4 => ActorKind.Amazon, _ => ActorKind.Longmoan };
        var moves = AreaIndex switch { 0 => Moves.Heninger, 1 or 4 => Moves.Amazon, 3 => Moves.FastLongmoan, _ => Moves.Longmoan };
        var palette = AreaIndex switch { 0 => 0x1a, 1 => 0x64, 3 => 0x7b, 4 => 0x65, _ => 0x79 };
        var rider = Add(riderKind, moves, palette, 16, spawn.X, spawn.Z);
        var beast = Add(kind, kind == ActorKind.Chicken ? Moves.Chicken : Moves.BlueDragon,
            kind switch { ActorKind.Chicken => 0x23, ActorKind.BlueDragon => 0x43, _ => 0x44 }, 99, spawn.X, spawn.Z);
        Mount(rider, beast);
    }

    private void UpdateFireball(Fighter fireball)
    {
        if (fireball.State == FighterState.Dying)
        {
            fireball.Animation.Tick();
            if (fireball.StateTicks >= 14)
                fireball.SetState(FighterState.Gone);
            return;
        }
        fireball.X += fireball.Facing * 4;
        var phase = (fireball.Age - 1) / 4 % 3;
        fireball.Animation.Play(0x32b00 + phase * 0x30);
        fireball.Animation.Seek(phase);
        ResolveAttack(fireball);
        var screenX = Project(fireball.X, fireball.Z, fireball.Y).X;
        if (fireball.Connected || screenX < -16 || screenX > 336 || fireball.Age >= 120)
        {
            fireball.SetState(FighterState.Dying, 0x32b90);
            fireball.Animation.Seek(1);
        }
    }

    private void UpdateFreeMount(Fighter beast)
    {
        const int idleTicks = 5 * 60;
        var floor = Ground(beast.X, beast.Z);
        if (floor is > 3840 and < 5248)
        {
            if (floor > beast.Y)
            {
                beast.Vy += 0.25;
                beast.Y = Math.Min(floor, beast.Y + beast.Vy);
            }
            else
            {
                beast.Y = floor;
                beast.Vy = 0;
            }
        }
        if (beast.State != FighterState.Fleeing && ++beast.UnmountedTicks < idleTicks)
        {
            // Hold the extracted seated pose until a rider mounts or the beast flees.
            beast.Animation.Seek(0);
            beast.Vx = 0;
            return;
        }
        var screenX = Project(beast.X, beast.Z, beast.Y).X;
        if (beast.State != FighterState.Fleeing)
        {
            beast.EscapeDirection = screenX < 160 ? -1 : 1;
            beast.SetState(FighterState.Fleeing, beast.FreeMoves.Walk);
        }
        var direction = beast.EscapeDirection;
        var oldX = beast.X;
        var oldZ = beast.Z;
        if (direction < 0 && screenX < 24 || direction > 0 && screenX > 296)
        {
            // Keep running beyond the visible collision map until the whole sprite exits.
            beast.X += direction * 3;
            beast.Facing = direction;
        }
        else
        {
            var ahead = Ground(beast.X + direction * 12, beast.Z);
            var input = ahead < beast.Y - 12 || ahead >= 5248
                ? TerrainNavigator.Towards(this, beast, Progress + (direction < 0 ? 12 : 305), beast.Z)
                : new GameInput(direction);
            Move(beast, input.X * 3, input.Z * 1.5);
            if (input.X != 0)
                beast.Facing = Math.Sign(input.X);
        }
        beast.Animation.Tick();
        beast.EscapeBlockedTicks = beast.X == oldX && beast.Z == oldZ ? beast.EscapeBlockedTicks + 1 : 0;
        if (beast.EscapeBlockedTicks >= 30)
        {
            beast.EscapeDirection = -direction;
            beast.EscapeBlockedTicks = 0;
        }
        screenX = Project(beast.X, beast.Z, beast.Y).X;
        if (screenX is < -64 or > 384)
            beast.SetState(FighterState.Gone);
    }

    private void UpdateLightning(Fighter bolt)
    {
        if (bolt.Owner.Health <= 0 || bolt.Owner.IsGone || bolt.Age > 180)
        {
            bolt.SetState(FighterState.Gone);
            return;
        }
        bolt.X += bolt.Facing * 3;
        bolt.Z += Math.Clamp(Hero.Z - bolt.Z, -0.35, 0.35);
        var floor = Ground(bolt.X, bolt.Z);
        var screenX = Project(bolt.X, bolt.Z, bolt.Y).X;
        if (floor is <= 3840 or >= 5248 || screenX is < -32 or > 352)
        {
            bolt.SetState(FighterState.Gone);
            return;
        }
        bolt.Y = floor;
        // Each of the four ROM scripts makes a different phase the damaging frame.
        var phase = (bolt.Age - 1) / 4 % 4;
        bolt.Animation.Play(0x3618e + phase * 0x3a);
        bolt.Animation.Seek(phase);
        ResolveAttack(bolt);
        if (bolt.Connected)
            bolt.SetState(FighterState.Gone);
    }

    private GameInput EnemyInput(Fighter actor)
    {
        var target = Hero.Mount ?? Hero;
        if (CanRide(actor) && actor.MountCooldown == 0 && actor.IsFree)
        {
            var beast = Actors.Where(a => a.IsMount && a.Rider == null && a.IsFree &&
                    Math.Abs(a.Y - actor.Y) < 12 && Math.Abs(a.Z - actor.Z) < 48 && Math.Abs(a.X - actor.X) < 120)
                .OrderBy(a => Math.Abs(a.X - actor.X) + Math.Abs(a.Z - actor.Z) * 2).FirstOrDefault();
            if (beast != null)
            {
                var mx = beast.X - actor.X;
                var mz = beast.Z - actor.Z;
                var floor = Ground(actor.X + Math.Sign(mx) * 16, actor.Z + Math.Sign(mz) * 4);
                if (floor < actor.Y - 12 || floor >= 5248)
                    return TerrainNavigator.Towards(this, actor, beast.X, beast.Z);
                return new GameInput(Math.Abs(mx) > 5 ? Math.Sign(mx) : 0, Math.Abs(mz) > 2 ? Math.Sign(mz) : 0);
            }
        }
        var dx = target.X - actor.X;
        var dz = target.Z - actor.Z;
        if (actor.IsFree)
            actor.Facing = dx < 0 ? -1 : 1;
        var reach = actor.Kind switch { ActorKind.RedDragon => 140, ActorKind.BlueDragon => 70, ActorKind.DeathAdder => 42, ActorKind.BadBrother or ActorKind.Knight => 38, _ => 30 };
        var ahead = Ground(actor.X + Math.Sign(dx) * 16, actor.Z + Math.Sign(dz) * 4);
        if (actor.IsFree && (Math.Abs(dx) > reach && (ahead < actor.Y - 12 || ahead >= 5248) || Math.Abs(target.Y - actor.Y) > 24))
            return TerrainNavigator.Towards(this, actor, target.X, target.Z);
        return new GameInput(Math.Abs(dx) > reach ? Math.Sign(dx) : 0, Math.Abs(dz) > 2 ? Math.Sign(dz) : 0,
            Math.Abs(dx) < reach + 6 && Math.Abs(dz) < 6 && actor.Cooldown == 0 && Hero.Health > 0);
    }

    private void UpdateFighter(Fighter actor, GameInput input)
    {
        if (actor.Rider == Hero && actor.IsFree && input.Jump)
        {
            actor.Rider = null;
            actor.Moves = actor.FreeMoves;
            actor.SetState(FighterState.Idle, actor.Moves.Idle);
            Hero.Mount = null;
            Hero.MountCooldown = 60;
            Hero.Y = actor.Y - 20;
            Hero.Vx = 0;
            Hero.Vy = -4;
            Hero.SetState(FighterState.Jumping, Hero.Moves.Jump);
            Hero.Animation.Seek(2);
            return;
        }
        switch (actor.State)
        {
            case FighterState.Sleeping:
                return;
            case FighterState.Held:
                return;
            case FighterState.Grabbing:
                UpdateGrab(actor);
                return;
            case FighterState.Throwing:
                UpdateThrow(actor);
                return;
            case FighterState.Hurt:
                actor.Animation.Tick();
                if (actor.StateTicks >= 18)
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Collapsing:
                actor.Animation.Tick();
                if (actor.Animation.Current.Duration == 255)
                    actor.SetState(FighterState.Down);
                return;
            case FighterState.Flying:
                var knockedX = Math.Clamp(actor.X + actor.Vx, Progress + 12, Progress + 296);
                if (Ground(knockedX, actor.Z) > 3840 && Ground(knockedX, actor.Z) < 5248)
                    actor.X = knockedX;
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                if (actor.StateTicks % 8 == 0)
                    actor.Animation.Seek(Math.Min(actor.Animation.Index + 1, actor.Animation.Steps.Length - 2));
                if (actor.Y >= Ground(actor.X, actor.Z))
                {
                    actor.Y = Ground(actor.X, actor.Z);
                    actor.Animation.Seek(actor.Animation.Steps.Length - 1);
                    actor.SetState(FighterState.Down);
                }
                return;
            case FighterState.Down:
                if (actor.StateTicks > 35)
                {
                    if (actor.Health <= 0)
                        Die(actor);
                    else
                        actor.SetState(FighterState.GettingUp, actor.Moves.GetUp);
                }
                return;
            case FighterState.GettingUp:
                actor.Animation.Tick();
                if (actor.StateTicks >= 24)
                {
                    actor.Invulnerable = actor == Hero ? 70 : 20;
                    if (actor.Kind == ActorKind.DeathAdder && Hero.Health > 0)
                    {
                        actor.Facing = Hero.X < actor.X ? -1 : 1;
                        actor.FlameStarted = false;
                        actor.SetState(FighterState.Attacking, Moves.DeathAdderCast, true);
                    }
                    else
                        actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Dying:
                if (actor.StateTicks > 45)
                    actor.SetState(FighterState.Gone);
                return;
            case FighterState.Respawning:
                if (actor.StateTicks > 90)
                {
                    if (Lives == 0)
                    {
                        GameOver = true;
                        return;
                    }
                    Lives--;
                    actor.Health = 48;
                    Potions = 1;
                    actor.Vx = actor.Vy = 0;
                    actor.Facing = 1;
                    actor.X = Progress + 70;
                    actor.Z = FindFloor(actor.X, 4240, keepVisible: true);
                    actor.Y = Ground(actor.X, actor.Z);
                    actor.Invulnerable = 120;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Attacking:
                if (actor.Kind == ActorKind.DeathAdder && actor.Animation.Address == Moves.DeathAdderCast &&
                    actor.Animation.Index >= 4 && !actor.FlameStarted)
                {
                    actor.FlameStarted = true;
                    var bolt = Add(ActorKind.Lightning, Moves.Lightning, 0x52, 1, actor.X + actor.Facing * 40, actor.Z);
                    bolt.Owner = actor;
                    bolt.Facing = actor.Facing;
                }
                if (actor.Kind == ActorKind.BlueDragon && actor.Animation.Current.Frame == 0x277 && !actor.FlameStarted)
                {
                    actor.FlameStarted = true;
                    var flame = Add(ActorKind.Flame, Moves.BlueDragon, 0x45, 1, actor.X, actor.Z + 1);
                    flame.Owner = actor;
                    flame.Animation.Play(0x2f422);
                }
                if (actor.Kind == ActorKind.RedDragon && actor.Animation.Current.Frame == 0x274 && !actor.FlameStarted)
                {
                    actor.FlameStarted = true;
                    var fireball = Add(ActorKind.Fireball, Moves.BlueDragon, 0x45, 1, actor.X + actor.Facing * 52, actor.Z);
                    fireball.Owner = actor.Rider;
                    fireball.Facing = actor.Facing;
                    fireball.Y = actor.Y - 28;
                    fireball.Animation.Play(0x32b00);
                }
                ResolveAttack(actor);
                if (actor.Animation.Tick())
                {
                    actor.ComboTimer = 40;
                    actor.Combo = actor.Connected ? (actor.Combo + 1) % actor.Moves.Attacks.Length : 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                    actor.Cooldown = actor == Hero || actor.Rider == Hero ? 0 : 45;
                }
                return;
            case FighterState.Dashing:
                Move(actor, actor.StateTicks < 18 ? actor.Facing * 4 : 0, 0);
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                actor.Y = Math.Min(actor.Y, Ground(actor.X, actor.Z));
                if (actor.StateTicks < 18)
                    ResolveAttack(actor);
                if (actor.StateTicks >= 34)
                {
                    actor.Vx = 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Crouching:
                if (actor.StateTicks >= 12)
                {
                    actor.Vy = Math.Abs(actor.Vx) > 2 ? -7.875 : -5.75;
                    actor.SetState(FighterState.Jumping, actor.Moves.Jump);
                    actor.Animation.Seek(2);
                }
                return;
            case FighterState.Falling:
                Move(actor, input.X * 1.5, input.Z * 0.5);
                actor.Vy += 0.25;
                actor.Y += actor.Vy;
                if (FellIntoPit(actor))
                    return;
                if (actor.Y >= Ground(actor.X, actor.Z))
                {
                    actor.Y = Ground(actor.X, actor.Z);
                    actor.Vy = 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Jumping:
                Move(actor, Math.Abs(actor.Vx) > 2 ? actor.Vx : input.X, input.Z * 0.5);
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                if (FellIntoPit(actor))
                    return;
                if (input.Attack && actor.Moves.JumpAttack != 0 && actor.Animation.Address != actor.Moves.JumpAttack)
                {
                    actor.Animation.Play(actor.Moves.JumpAttack, true, true);
                    actor.Struck.Clear();
                }
                if (actor.Animation.Address == actor.Moves.JumpAttack)
                {
                    ResolveAttack(actor);
                    actor.Animation.Tick();
                }
                if (actor.Y >= Ground(actor.X, actor.Z) && actor.Vy > 0)
                {
                    actor.Y = Ground(actor.X, actor.Z);
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
        }
        if (actor == Hero && !IsCamp && input.Magic && Potions > 0)
        {
            MagicLevel = Math.Min(Character.MagicLevels, Potions);
            Potions = 0;
            MagicTick = 0;
            var effects = MagicEffects[MagicLevel - 1];
            m_magicPeak = Math.Max(30, Enumerable.Range(0, effects.Length).OrderByDescending(i => effects[i].Length).FirstOrDefault());
            actor.SetState(FighterState.Magic, Character.Magic);
            return;
        }
        if (actor == Hero && input.Attack && input.Jump)
        {
            actor.Facing = -actor.Facing;
            actor.Connected = false;
            actor.SetState(FighterState.Attacking, Character.Back, true);
            return;
        }
        if (input.X != 0)
            actor.Facing = Math.Sign(input.X);
        if (input.Attack && actor.Moves.Attacks.Length > 0)
        {
            if (actor == Hero && input.Run && Math.Abs(actor.Vx) >= 3)
            {
                actor.Vy = -2.6;
                actor.SetState(FighterState.Dashing, Character.Dash);
                return;
            }
            if (actor == Hero && TryGrab(actor))
                return;
            actor.Connected = false;
            actor.FlameStarted = false;
            actor.SetState(FighterState.Attacking, actor.Moves.Attacks[actor.Combo % actor.Moves.Attacks.Length], true);
            return;
        }
        if (input.Jump && actor.Moves.Jump != 0)
        {
            if (actor == Hero && input.Run && input.X != 0)
                actor.Vx = Math.Sign(input.X) * 3;
            actor.SetState(FighterState.Crouching, actor.Moves.Jump);
            return;
        }
        var speed = actor == Hero || actor.Rider == Hero ? 1.5 : actor.Kind == ActorKind.BadBrother ? 0.5 : actor.Kind is ActorKind.Knight or ActorKind.DeathAdder ? 0.9 : actor.Kind == ActorKind.Skeleton ? 0.8 : 0.7;
        if (actor == Hero && input.Run && input.X != 0)
        {
            actor.Vx = Math.Sign(input.X) * Math.Min(4, Math.Abs(actor.Vx) + 0.5);
            speed = Math.Abs(actor.Vx);
        }
        else
            actor.Vx = 0;
        Move(actor, input.X * speed, input.Z * speed);
        if (input.X != 0 || input.Z != 0)
        {
            if (actor.State != FighterState.Walking)
                actor.SetState(FighterState.Walking);
            actor.Animation.Play(actor == Hero && input.Run && input.X != 0 ? actor.Moves.Run : input.Z < 0 ? actor.Moves.WalkUp : actor.Moves.Walk);
            actor.Animation.Tick();
        }
        else if (actor.State != FighterState.Idle)
            actor.SetState(FighterState.Idle, actor.Moves.Idle);
        var floor = Ground(actor.X, actor.Z);
        if (floor - actor.Y > 12)
        {
            actor.Vy = 0;
            actor.SetState(FighterState.Falling, actor.Moves.Jump != 0 ? actor.Moves.Jump : actor.Moves.Idle);
            if (actor.Moves.Jump != 0)
                actor.Animation.Seek(2);
            return;
        }
        actor.Y = floor;
        TryMount(actor);
    }

    private static bool CanRide(Fighter actor) => actor.Kind is ActorKind.Hero or ActorKind.Heninger or ActorKind.Longmoan or ActorKind.Amazon;

    private void TryMount(Fighter actor)
    {
        if (!CanRide(actor) || actor.MountCooldown != 0 || !actor.IsFree || actor.Health <= 0)
            return;
        var beast = Actors.FirstOrDefault(a => a.IsMount && a.Rider == null && a.IsFree &&
            Math.Abs(a.X - actor.X) < 14 && Math.Abs(a.Z - actor.Z) < 6 && Math.Abs(a.Y - actor.Y) < 8);
        if (beast != actor.PendingMount)
        {
            actor.PendingMount = beast;
            actor.MountWaitTicks = 0;
        }
        if (beast == null)
            return;
        // Enemies take a second to climb aboard; the hero can interrupt or claim the beast.
        if (actor != Hero && ++actor.MountWaitTicks < 60)
            return;
        actor.PendingMount = null;
        actor.MountWaitTicks = 0;
        Mount(actor, beast);
    }

    private void Mount(Fighter actor, Fighter beast)
    {
        beast.UnmountedTicks = beast.EscapeBlockedTicks = 0;
        beast.Rider = actor;
        actor.Mount = beast;
        beast.Facing = actor.Facing;
        beast.Combo = 0;
        beast.Cooldown = actor.Cooldown;
        beast.Moves = (actor.Kind, beast.Kind) switch
        {
            (ActorKind.Hero, _) => Character.RidingMoves(beast.Kind),
            (ActorKind.Amazon, ActorKind.RedDragon) => Moves.AmazonRedDragon,
            (ActorKind.Longmoan, ActorKind.RedDragon) => Moves.LongmoanRedDragon,
            (_, ActorKind.RedDragon) => Moves.HeningerRedDragon,
            (ActorKind.Amazon, ActorKind.BlueDragon) => Moves.AmazonDragon,
            (ActorKind.Amazon, _) => Moves.AmazonChicken,
            (ActorKind.Longmoan, ActorKind.BlueDragon) => Moves.LongmoanDragon,
            (ActorKind.Longmoan, _) => Moves.LongmoanChicken,
            (_, ActorKind.BlueDragon) => Moves.HeningerDragon,
            _ => Moves.EnemyChicken
        };
        beast.SetState(FighterState.Idle, beast.Moves.Idle);
    }

    private bool TryGrab(Fighter actor)
    {
        var victim = Actors.FirstOrDefault(a => a.Kind is ActorKind.Heninger or ActorKind.Longmoan or ActorKind.Amazon or ActorKind.Skeleton &&
            a.Vulnerable && (a.IsFree || a.State == FighterState.Hurt) &&
            (a.X - actor.X) * actor.Facing > 4 && (a.X - actor.X) * actor.Facing < 22 &&
            Math.Abs(a.Z - actor.Z) < 6 && Math.Abs(a.Y - actor.Y) < 8);
        if (victim == null)
            return false;
        actor.Victim = victim;
        actor.Knees = 0;
        victim.HeldBy = actor;
        victim.Facing = -actor.Facing;
        victim.X = actor.X + actor.Facing * 18;
        victim.Z = actor.Z;
        victim.SetState(FighterState.Held, victim.Moves.Hurt);
        actor.SetState(FighterState.Grabbing, Character.Knee, true);
        return true;
    }

    private void UpdateGrab(Fighter actor)
    {
        var victim = actor.Victim;
        if (victim == null)
        {
            actor.SetState(FighterState.Idle, actor.Moves.Idle);
            return;
        }
        var attack = m_data.Attacks[actor.Animation.Address];
        if (actor.Animation.Index == attack[2] && actor.Struck.Add(victim.Id))
        {
            victim.Health = Math.Max(0, victim.Health - attack[1]);
            victim.Flash = 3;
            actor.Knees++;
        }
        if (!actor.Animation.Tick())
            return;
        if (actor.Knees >= 2 || victim.Health == 0)
        {
            actor.SetState(FighterState.Throwing, Character.Throw, true);
            actor.Animation.Seek(1);
            victim.Animation.Play(victim.Moves.Fly, true);
            PositionCarriedVictim(actor);
        }
        else
            actor.SetState(FighterState.Grabbing, Character.Knee, true);
    }

    private void PositionCarriedVictim(Fighter actor)
    {
        // Recorded throw poses carry the victim up and over the hero's shoulder.
        (int X, int Y)[] offsets = [(28, 0), (1, -16), (-9, -18), (1, -10)];
        var step = actor.Animation.Index;
        if (actor.Victim == null || step < 1 || step > offsets.Length)
            return;
        var victim = actor.Victim;
        victim.X = actor.X + offsets[step - 1].X * actor.Facing;
        victim.Y = actor.Y + offsets[step - 1].Y;
        victim.Z = actor.Z;
        victim.Animation.Seek(step - 1);
    }

    private void UpdateThrow(Fighter actor)
    {
        var finished = actor.Animation.Tick();
        PositionCarriedVictim(actor);
        if (actor.Victim != null && (actor.Animation.Index >= 5 || finished))
        {
            var victim = actor.Victim;
            actor.Victim = null;
            victim.HeldBy = null;
            victim.X = actor.X + actor.Facing * 12;
            victim.Y = actor.Y - 48;
            victim.Facing = -actor.Facing;
            victim.Health = Math.Max(0, victim.Health - m_data.Attacks[Character.Throw][1]);
            victim.Vx = actor.Facing * 3;
            victim.Vy = -2;
            victim.Hits = 0;
            victim.SetState(FighterState.Flying, victim.Moves.Fly);
            victim.Animation.Seek(4);
            Throws++;
        }
        if (finished)
            actor.SetState(FighterState.Idle, actor.Moves.Idle);
    }

    private static void ReleaseGrab(Fighter actor)
    {
        // An interrupted throw must not leave its victim suspended or permanently held.
        var victim = actor.Victim;
        if (victim == null)
            return;
        actor.Victim = null;
        victim.HeldBy = null;
        victim.Vx = -actor.Facing;
        victim.Vy = 0;
        victim.SetState(FighterState.Flying, victim.Moves.Fly);
    }
    private bool FellIntoPit(Fighter actor)
    {
        if (Ground(actor.X, actor.Z) < 5248 || Project(actor.X, actor.Z, actor.Y).Y <= ArcadeData.Height + 40)
            return false;
        actor.Health = 0;
        Die(actor);
        return true;
    }

    private void Move(Fighter actor, double dx, double dz)
    {
        var airborne = actor.State is FighterState.Jumping or FighterState.Falling;
        var next = Ground(actor.X + dx + Math.Sign(dx) * 8, actor.Z);
        if (next >= actor.Y - 12 && (next < 5248 || airborne))
            actor.X += dx;
        next = Ground(actor.X, actor.Z + dz);
        if (next >= actor.Y - 12 && (next < 5248 || airborne) && (dz <= 0 || airborne && next >= 5248 || CanMoveDown(actor, actor.Z + dz, next)))
            actor.Z += dz;
    }

    private bool CanMoveDown(Fighter actor, double z, double ground)
    {
        if (actor != Hero && actor.Rider != Hero)
            return true;
        // Leave room above the HUD for the feet, allowing the camera to follow on taller maps.
        var lowestCamera = IsCamp ? CameraY : CameraRange()[0];
        return Project(actor.X, z, ground).Y + lowestCamera - CameraY <= 196;
    }

    private void ResolveAttack(Fighter actor)
    {
        if (!m_data.Attacks.TryGetValue(actor.Animation.Address, out var attack) || actor.Animation.Index != attack[2])
            return;
        var step = actor.Animation.Current;
        var origin = actor.X + step.Dx * actor.Facing;
        var left = origin + (actor.Facing > 0 ? attack[3] : -attack[4]);
        var right = origin + (actor.Facing > 0 ? attack[4] : -attack[3]);
        var friendly = actor == Hero || actor.Rider == Hero || actor.Kind == ActorKind.Fireball && actor.Owner == Hero;
        foreach (var target in Actors.ToArray())
        {
            if (target == actor || !target.Vulnerable || actor.Struck.Contains(target.Id) || Math.Abs(target.Z - actor.Z) >= 7)
                continue;
            var hostile = target.IsEnemy || target.Kind == ActorKind.Thief || target.Rider?.IsEnemy == true;
            if (friendly ? !hostile : target != Hero && target.Rider != Hero)
                continue;
            var body = target.Animation.Current;
            // Grab/throw scripts omit body bounds during the carried poses. Keep the
            // hero hittable there so another enemy can interrupt the grapple.
            if (target == Hero && target.State is FighterState.Grabbing or FighterState.Throwing &&
                (body.Body[1] <= body.Body[0] || body.Body[3] <= body.Body[2]))
                body = m_data.Animations[target.Moves.Idle][0];
            var bx = target.X + body.Dx * target.Facing;
            var bl = bx + (target.Facing > 0 ? body.Body[0] : -body.Body[1]);
            var br = bx + (target.Facing > 0 ? body.Body[1] : -body.Body[0]);
            if (br < left || bl > right || target.Y + body.Dy + body.Body[3] < actor.Y + step.Dy + attack[5] || target.Y + body.Dy + body.Body[2] > actor.Y + step.Dy + attack[6])
                continue;
            actor.Struck.Add(target.Id);
            actor.Connected = true;
            Hit(actor, target, attack[1], attack[0] >= 5);
        }
    }

    private void Hit(Fighter source, Fighter target, int damage, bool knock)
    {
        if (target.Mount != null)
            target = target.Mount;
        if (target.Rider != null)
        {
            var rider = target.Rider;
            target.Rider = null;
            target.Moves = target.FreeMoves;
            target.SetState(FighterState.Idle, target.Moves.Idle);
            rider.Mount = null;
            rider.MountCooldown = rider == Hero ? 60 : 180;
            target = rider;
            target.Invulnerable = 0;
            knock = true;
        }
        if (target.Kind == ActorKind.Thief)
        {
            if (target.Items <= 0 || target.State == FighterState.Hurt)
                return;
            target.Items--;
            var potion = Add(target.FoodThief ? ActorKind.Food : ActorKind.Potion, Moves.Thief, target.FoodThief ? 0x7f : 0x4a, 1,
                Math.Clamp(target.X, Progress + 16, Progress + 296), target.Z);
            potion.Animation.Play(target.FoodThief ? 0x18722 : 0x1870a);
            potion.Y -= 20;
            potion.Vy = -3;
            potion.Vx = source.Facing * 0.8;
            target.SetState(FighterState.Hurt, target.Moves.Hurt);
            return;
        }
        ReleaseGrab(target);
        target.Health = Math.Max(0, target.Health - damage);
        target.Flash = 3;
        target.Hits++;
        target.HitTimer = 45;
        target.Facing = source.X < target.X ? -1 : 1;
        if (knock || target.Health == 0 || target.Hits >= 3)
        {
            target.Hits = 0;
            target.Vx = -target.Facing * 2.5;
            target.Vy = -4.5;
            target.Y -= 1;
            target.SetState(FighterState.Flying, target.Moves.Fly);
            target.Animation.Seek(1);
        }
        else
            target.SetState(FighterState.Hurt, target.Moves.Hurt);
    }

    private void Die(Fighter actor)
    {
        if (actor == Hero)
            actor.SetState(FighterState.Respawning);
        else
        {
            Kills++;
            actor.SetState(FighterState.Dying);
        }
    }

    private void MoveThief(Fighter actor)
    {
        var screenX = Project(actor.X, actor.Z, actor.Y).X;
        if (actor.Items == 0 && (actor.Facing < 0 && screenX < 24 || actor.Facing > 0 && screenX > 296))
        {
            // An empty thief must be allowed beyond the captured collision map to leave.
            actor.X += actor.Facing * 3;
            return;
        }
        var ground = Ground(actor.X, actor.Z);
        var ahead = Ground(actor.X + actor.Facing * 12, actor.Z);
        var grounded = actor.Y >= ground - 1 && actor.Vy >= 0;
        if (grounded)
        {
            actor.Y = ground;
            actor.Vy = 0;
            if (ahead < ground - 12)
            {
                if (ahead > 3840 && ground - ahead <= 96)
                    actor.Vy = -7;
                else
                    actor.Facing = -actor.Facing;
            }
            else if (ahead >= 5248)
                actor.Facing = -actor.Facing;
        }
        Move(actor, actor.Facing * (actor.Items > 0 ? 1.0 : 3.0), 0);
        actor.Y += actor.Vy;
        actor.Vy += 0.25;
        ground = Ground(actor.X, actor.Z);
        if (actor.Y >= ground && actor.Vy >= 0)
        {
            actor.Y = ground;
            actor.Vy = 0;
        }
    }

    private void UpdateProp(Fighter actor)
    {
        if (actor.Kind is ActorKind.Campfire or ActorKind.CampPotion)
        {
            actor.Animation.Tick();
            return;
        }
        if (actor.Kind is ActorKind.Potion or ActorKind.Food)
        {
            actor.Y += actor.Vy;
            actor.Vy += 0.25;
            var floor = Ground(actor.X, actor.Z);
            if (actor.Y >= floor)
            {
                actor.Y = floor;
                actor.Vy = 0;
                actor.Vx = 0;
            }
            var nextFloor = Ground(actor.X + actor.Vx, actor.Z);
            if (nextFloor > 3840 && nextFloor < 5248)
                actor.X = Math.Clamp(actor.X + actor.Vx, Progress + 16, Progress + 296);
            if (actor.Age > 600)
                actor.SetState(FighterState.Gone);
            if (Hero.Health > 0 && Hero.State != FighterState.Sleeping && Math.Abs(actor.Y - Hero.Y) < 8 && Math.Abs(actor.X - Hero.X) < 16 && Math.Abs(actor.Z - Hero.Z) < 10)
            {
                if (actor.Kind == ActorKind.Food)
                    Hero.Health = Math.Min(48, Hero.Health + 16);
                else
                    Potions = Math.Min(Character.MagicLevels, Potions + 1);
                actor.SetState(FighterState.Gone);
            }
            return;
        }
        actor.Animation.Tick();
        if (actor.State == FighterState.Hurt)
        {
            if (actor.StateTicks > 24)
                actor.SetState(FighterState.Walking, actor.Moves.Walk);
            return;
        }
        if (actor.Kind == ActorKind.Thief)
        {
            if (actor.Items == 0 && actor.State != FighterState.Fleeing)
            {
                actor.Facing = Project(actor.X, actor.Z, actor.Y).X < 160 ? -1 : 1;
                actor.SetState(FighterState.Fleeing, actor.Moves.Run);
            }
            var jar = IsCamp && !actor.FoodThief && actor.Items > 0
                ? Actors.FirstOrDefault(a => a.Kind == ActorKind.CampPotion && !a.IsGone) : null;
            if (jar != null)
            {
                var dx = jar.X - actor.X;
                var dz = jar.Z - actor.Z;
                actor.Facing = dx < 0 ? -1 : 1;
                Move(actor, Math.Sign(dx) * Math.Min(1.5, Math.Abs(dx)), Math.Sign(dz) * Math.Min(1, Math.Abs(dz)));
                if (Math.Abs(dx) < 4 && Math.Abs(dz) < 4)
                {
                    jar.SetState(FighterState.Gone);
                    Potions = Math.Max(0, Potions - 1);
                    actor.Items++;
                }
            }
            else
                MoveThief(actor);
        }
        else
            actor.X += actor.Facing * 1.8;
        var sx = Project(actor.X, actor.Z, actor.Y).X;
        if (sx is > 0 and < 320)
            actor.Entered = true;
        if (actor.Kind == ActorKind.Thief && actor.Items > 0 && actor.Age < 700 && (sx < 20 || sx > 300) && actor.Entered)
            actor.Facing = sx < 20 ? 1 : -1;
        if (actor.Entered && (sx < -60 || sx > 380))
            actor.SetState(FighterState.Gone);
    }
}
