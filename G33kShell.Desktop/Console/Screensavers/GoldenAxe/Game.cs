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
    internal const int FireballImpactTicks = 14;
    internal const int FlameFadeTicks = 12;
    internal const int HeroFadeTicks = 18;
    internal const int LandingDustTicks = 150;
    internal const int PickupFadeTicks = 15;
    internal const int ClayHeningerPalette = 0x1f;
    internal const int ClayLongmoanPalette = 0x7d;
    internal const int StageFiveDoorPalette = 0x68;
    internal const int StageFiveDoorAjarFrame = 770;
    internal const int StageFiveDoorOpenFrame = 771;
    internal const int StageFiveDoorOpeningTicks = 8;
    internal const int DoorRevealStart = StageFiveDoorOpeningTicks + 8;
    internal const int DoorRevealEnd = DoorRevealStart + 30;
    private const int DoorInteriorDepth = 24;
    internal const int DoorCloseStart = DoorRevealEnd + 40;
    internal const int DoorEntranceDuration = DoorCloseStart + StageFiveDoorOpeningTicks;
    private const int BrotherChuckleTicks = 100;
    internal const int LightningPalette = 0x61;
    internal const int LightningTrailPalette = 0x60;
    internal const int LightningTrailTicks = 180;
    private const double JumpVelocity = -5.75;
    private const double RunningJumpVelocity = -7.875;
    private const int BargeSpeed = 4;
    private const int HeroBargeTravelTicks = 23;
    internal const int HeroBargeDistance = HeroBargeTravelTicks * BargeSpeed;
    private const int KnockdownTicks = 36;
    private const int EnemyKnockdownVariation = 2;
    private const int ArcadeLightHitTicks = 81;
    internal readonly record struct DustPuff(double X, double Z, double Y, double Spread, int Tick, bool Heavy, bool Running = false);
    internal readonly record struct LightningMark(double X, double Z, double Y, int Facing, int Tick);

    private readonly ArcadeData m_data;
    private readonly Random m_random;
    private readonly List<DustPuff> m_landingDust = [];
    private readonly List<LightningMark> m_lightningTrail = [];
    private readonly List<SmokePuff> m_dragonSmoke = [];
    private readonly List<MagicGroundGlow> m_magicGroundGlow = [];
    private readonly List<PotionPickup> m_potionPickups = [];
    private readonly List<Fighter> m_fadingPickups = [];
    private readonly List<Fighter> m_magicTargets = [];
    private readonly List<Fighter> m_stoneBodies = [];
    private readonly List<SpawnDefinition> m_openedSkeletonMounds = [];
    private int m_nextId;
    private int m_spawnIndex;
    private int m_stopIndex;
    private int m_clearTicks;
    private int m_magicPeak;
    private int m_pendingHero = -1;
    private int? m_surfStartTick;
    private int? m_forestPaletteStartTick;
    private int? m_eagleSunsetStartTick;
    private bool m_deathAdderEntered;
    private Fighter m_meleeLeft;
    private Fighter m_meleeRight;
    private readonly Queue<(SpawnDefinition Spawn, int Tick)> m_brotherEntrances = new();
    private int? m_openingFinishedTick;
    public byte HudOpacity
    {
        get
        {
            if (RescuingCaptives)
                return CastleRescue.HudOpacity(EndStateTicks);
            if (m_openingFinishedTick is not { } start)
                return byte.MaxValue;
            var t = Math.Clamp((TickCount - start) / 45.0, 0, 1);
            return (byte)Math.Round(255 * t * t * (3 - 2 * t));
        }
    }

    public ForestOpening Opening { get; private set; }
    public HeroDefinition Character { get; private set; }
    public int[][][][] MagicEffects => m_data.Magic[Character.MagicKey];
    public List<Fighter> Actors { get; } = [];
    public IReadOnlyList<DustPuff> LandingDust => m_landingDust;
    public IReadOnlyList<LightningMark> LightningTrail => m_lightningTrail;
    public IReadOnlyList<SmokePuff> DragonSmoke => m_dragonSmoke;
    public IReadOnlyList<MagicGroundGlow> MagicGroundGlows => m_magicGroundGlow;
    public IReadOnlyList<PotionPickup> PotionPickups => m_potionPickups;
    public IReadOnlyList<Fighter> FadingPickups => m_fadingPickups;
    public IReadOnlyList<Fighter> MagicTargets => m_magicTargets;
    public IReadOnlyList<Fighter> StoneBodies => m_stoneBodies;
    public IReadOnlyList<SpawnDefinition> OpenedSkeletonMounds => m_openedSkeletonMounds;
    public IEnumerable<SpawnDefinition> SkeletonMounds => IsCamp || AreaIndex != 3 ? [] : m_data.Spawns.Skip(m_spawnIndex).Where(IsSkeletonMound);

    private bool IsSkeletonMound(SpawnDefinition spawn) =>
        AreaIndex == 3 && spawn.Routine == 0x23bda || AreaIndex == 4 && spawn.Routine == 0x2538a;
    public Fighter Hero { get; }
    public int TickCount { get; private set; }
    public bool RescuingCaptives => Complete && AreaIndex == ArcadeData.AreaCount - 1 && !IsCamp;
    public int EndStateTicks { get; private set; }
    public int? SurfAge => !IsCamp && m_surfStartTick is { } start ? TickCount - start : null;
    public int ForestPaletteAge => TickCount - (m_forestPaletteStartTick ?? TickCount);
    public int EagleSunsetAge => TickCount - (m_eagleSunsetStartTick ?? TickCount);
    public int EagleSkyTick { get; private set; } = -1;
    public double CameraX { get; private set; }
    public double CameraY { get; private set; }
    public FollowCamera ViewCamera { get; } = new();
    public int AreaIndex => m_data.AreaIndex;
    public double Progress => 4096 - CameraX - 7;
    public int Potions { get; private set; } = 1;
    public int Lives { get; private set; } = 2;
    public int Kills { get; private set; }
    public int Throws { get; private set; }
    public bool Complete { get; private set; }
    public CampRound Camp { get; private set; }
    public bool IsCamp => Camp != null;
    internal int BottomFeetLimit => IsCamp ? 196 : ArcadeData.Height - 2;
    public bool GameOver { get; private set; }
    public int MagicTick { get; private set; } = -1;
    public int MagicLevel { get; private set; }
    public int MagicDuration => Math.Max(60, ActiveMagicEffects.Length);
    public int MagicShakeStart { get; private set; }
    public bool MagicTargetsFlashing => MagicTick >= MagicShakeStart &&
        (Character.MagicKey == "gilius" ? LightningCage.SampleTick(this) >= 66 : MagicTick >= m_magicPeak);
    public int[][][] ActiveMagicEffects { get; private set; } = [];
    private bool FinalWaveSpawned => m_stopIndex == m_data.Stops.Length - 1 && m_spawnIndex == m_data.Spawns.Length;
    public bool CanAdvance => !IsCamp && !Complete && !GameOver && Hero.Health > 0 && !FinalWaveSpawned && Progress < m_data.Stops[m_stopIndex] &&
        m_brotherEntrances.Count == 0 && !Actors.Any(a => a.IsEnemy && !a.IsGone);
    public int BossPaletteTick { get; private set; } = -1;
    public bool Boss => Progress >= m_data.Stops[^1];
    // A familiar player can anticipate the next nearby wave, but not beyond a scroll lock.
    internal bool HasUpcomingEnemies => !IsCamp && (m_brotherEntrances.Count != 0 ||
        m_data.Spawns.Skip(m_spawnIndex).TakeWhile(s => s.Trigger <= Math.Min(Progress + 64, m_data.Stops[m_stopIndex]))
            .Any(s => s.IsEnemy));

    public Game(ArcadeData data, int character = 0, Random random = null, bool startInCamp = false, bool playOpening = false)
    {
        m_data = data;
        m_random = random ?? Random.Shared;
        Character = HeroDefinition.All[Math.Clamp(character, 0, 2)];
        CameraX = data.InitialCameraX;
        CameraY = m_data.CameraRange(CameraX, extendLower: false).Low;
        Hero = Add(ActorKind.Hero, Character.Moves, Character.Palette, 48, 4096 - CameraX + 60, 4240);
        Hero.Facing = 1;
        Hero.Z = FindFloor(Hero.X, Hero.Z, keepVisible: true);
        Hero.Y = Ground(Hero.X, Hero.Z);
        ViewCamera.Reset(CameraX, CameraY);
        if (playOpening && !startInCamp && AreaIndex == 0)
        {
            Opening = new ForestOpening(this, data.Spawns[0], data.Spawns[1]);
            m_spawnIndex = 2;
        }
        if (startInCamp)
        {
            Complete = true;
            StartCamp();
        }
    }

    public Game ContinueIn(ArcadeData nextArea)
    {
        var next = new Game(nextArea, Array.IndexOf(HeroDefinition.All, Character), m_random);
        next.Hero.Health = Hero.Health;
        next.Lives = Lives;
        next.Potions = Potions;
        return next;
    }

    public void StartCamp()
    {
        if (!Complete || IsCamp || AreaIndex == ArcadeData.AreaCount - 1)
            return;
        CameraX = 4096 - 7 - m_data.Stops[^1];
        Camp = new CampRound(this, m_data);
        Complete = false;
        TickCount = 0;
        m_openingFinishedTick = null;
        EndStateTicks = 0;
        // The camp has its own clock; a previous respawn must not hide the sleeping hero.
        Hero.RespawnTick = -1;
        m_landingDust.Clear();
        m_lightningTrail.Clear();
        m_dragonSmoke.Clear();
        m_magicGroundGlow.Clear();
        m_potionPickups.Clear();
        m_fadingPickups.Clear();
        m_magicTargets.Clear();
        m_stoneBodies.Clear();
        m_openedSkeletonMounds.Clear();
        m_brotherEntrances.Clear();
        var range = CameraRange();
        CameraY = Math.Clamp(150 - (Project(Hero.X, Hero.Z, Hero.Y).Y - CameraY), range.Low, range.High);
        ViewCamera.Reset(CameraX, CameraY);
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
        Potions = Math.Min(Potions, Character.PotionCapacity);
        if (Hero.Mount != null)
        {
            body.Moves = Character.RidingMoves(body.Kind);
            body.Combo = body.ComboTimer = 0;
            body.SetState(FighterState.Idle, body.Moves.Idle);
        }
    }
    internal Fighter Add(ActorKind kind, Moves moves, int palette, int hp, double x, double z, int spawnParameter = 0)
    {
        var actor = new Fighter(++m_nextId, m_data, kind, moves, palette, hp) { X = x, Z = z, SpawnParameter = spawnParameter, Facing = -1, Cooldown = 35 + m_nextId * 13 % 45 };
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
        var cameraY = keepVisible ? m_data.CameraRange(CameraX, extendLower: false).Low : CameraY;
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

    public (double X, double Y) Project(double x, double z, double y) => Project(x, z, y, CameraX, CameraY);

    public (double X, double Y) ProjectView(double x, double z, double y) => Project(x, z, y, ViewCamera.X, ViewCamera.Y);

    private static (double X, double Y) Project(double x, double z, double y, double cameraX, double cameraY) =>
        (x - 4096 + cameraX + 8, z - 4096 - Math.Floor((z - 4096) / 4) + y - 4096 + cameraY);

    private (int Low, int High) CameraRange() => m_data.CameraRange(CameraX, extendLower: !IsCamp);

    public void Tick(GameInput input)
    {
        TickSimulation(input);
        if (AreaIndex == TurtleVillageSurf.Area && !IsCamp && Progress >= TurtleVillageSurf.StartProgress)
            m_surfStartTick ??= TickCount;
        if (!IsCamp && Progress >= m_data.ForestPaletteTrigger)
            m_forestPaletteStartTick ??= TickCount;
        if (AreaIndex == 3 && !IsCamp && Progress >= EagleSky.SunsetTrigger)
            m_eagleSunsetStartTick ??= TickCount;
        if (!IsCamp && Boss && BossPaletteTick < 0)
            BossPaletteTick = TickCount;
        ViewCamera.Tick(this, m_data);
    }

    private void TickSimulation(GameInput input)
    {
        ApplyCharacterSelection();
        if (Complete || GameOver)
        {
            // End messages keep animating while the gameplay clock is stopped.
            EndStateTicks++;
            if (RescuingCaptives)
            {
                CameraY = Math.Min(CameraRange().High, CameraY + 0.65);
                var witness = Hero.Mount ?? Hero;
                var dx = Math.Clamp(Progress + 64 - witness.X, -1, 1);
                var dz = Math.Clamp(4112 - witness.Z, -1.25, 1.25);
                var previous = (witness.X, witness.Z);
                Move(witness, dx, dz);
                witness.Y = Ground(witness.X, witness.Z);
                var walking = previous != (witness.X, witness.Z);
                witness.Facing = walking && dx < 0 ? -1 : 1;
                witness.Animation.Play(walking ? witness.Moves.Walk : witness.Moves.Idle);
                witness.Animation.Tick();
                if (Hero.Mount != null)
                    (Hero.X, Hero.Z, Hero.Y) = (witness.X, witness.Z, witness.Y);
            }
            return;
        }
        TickCount++;
        if (Opening != null)
        {
            if (Opening.Tick(this, input.AdvanceDialogue))
            {
                Opening = null;
                m_openingFinishedTick = TickCount;
            }
            return;
        }
        if (m_lightningTrail.Count != 0)
            m_lightningTrail.RemoveAll(mark => TickCount - mark.Tick >= LightningTrailTicks);
        if (AreaIndex == 3 && !IsCamp && Progress >= EagleSky.FirstCapturedProgress)
            EagleSkyTick++;
        m_stoneBodies.RemoveAll(body => body.X < Progress - 100);
        m_dragonSmoke.RemoveAll(puff => TickCount - puff.Tick >= puff.Duration);
        m_magicGroundGlow.RemoveAll(glow => TickCount - glow.Tick >= MagicGroundGlow.FadeTicks);
        m_potionPickups.RemoveAll(pickup => TickCount - pickup.Tick >= PotionPickup.Lifetime);
        m_fadingPickups.RemoveAll(pickup => TickCount - pickup.PickupTick >= PickupFadeTicks);
        if (m_landingDust.Count > 0)
            m_landingDust.RemoveAll(puff => TickCount - puff.Tick >= LandingDustTicks);
        Camp?.BeforeTick();
        if (MagicTick >= 0)
        {
            MagicTick++;
            AddMagicSmoke();
            AddMagicGroundGlow();
            Hero.Animation.Tick();
            if (MagicTick == m_magicPeak)
            {
                foreach (var thief in Actors.Where(a => a.Kind == ActorKind.Thief && !a.IsGone).ToArray())
                {
                    var point = Project(thief.X, thief.Z, thief.Y);
                    if (point.X is >= 0 and < ArcadeData.Width && point.Y is >= 0 and <= ArcadeData.Height)
                        Hit(Hero, thief, 0, false);
                }
                foreach (var enemy in Actors.Where(a => a.IsEnemy && a.State is not (FighterState.Emerging or FighterState.Entering) && a.Health > 0 && a.HeldBy == null && Project(a.X, a.Z, a.Y).X is >= -20 and <= 340).ToArray())
                {
                    Hit(Hero, enemy, new[] { 0, 4, 8, 16, 24, 32, 48 }[MagicLevel], true);
                    // Grounded victims crumple after the spell; airborne victims still fall naturally.
                    if (enemy.Y + 1 >= Ground(enemy.X, enemy.Z))
                    {
                        enemy.Y = Ground(enemy.X, enemy.Z);
                        enemy.Vx = enemy.Vy = 0;
                        enemy.SetState(FighterState.Collapsing, enemy.Moves.Collapse);
                        // Some arcade scripts lead with the resting prone pose before the fall.
                        // Starting there makes the victim lie down, stand up, then collapse again.
                        var proneFrame = m_data.Animations[enemy.Moves.GetUp][1].Frame;
                        if (enemy.Animation.Current.Frame == proneFrame)
                            enemy.Animation.Seek(1);
                    }
                }
            }
            if (MagicTick >= MagicDuration)
            {
                MagicTick = -1;
                ActiveMagicEffects = [];
                m_magicTargets.Clear();
                Hero.SetState(FighterState.Idle, Hero.Moves.Idle);
            }
            return;
        }
        if (Hero.Mount is { IsFree: true } mount && TryCastMagic(input))
        {
            mount.Vx = 0;
            mount.SetState(FighterState.Idle, mount.Moves.Idle);
            return;
        }
        while (!IsCamp && m_spawnIndex < m_data.Spawns.Length && m_data.Spawns[m_spawnIndex].Trigger <= Progress)
            Spawn(m_data.Spawns[m_spawnIndex++]);
        if (!IsCamp && AreaIndex == 4 && !m_deathAdderEntered && Progress >= m_data.Stops[^1])
            EnterDeathAdder();
        while (m_brotherEntrances.TryPeek(out var brother) && TickCount >= brother.Tick)
        {
            m_brotherEntrances.Dequeue();
            EnterBadBrother(brother.Spawn);
        }
        if (!IsCamp && AreaIndex == 0 && m_stopIndex == 0 && !Actors.Any(a => a.IsEnemy && !a.IsGone && a.State != FighterState.Sleeping))
        {
            foreach (var enemy in Actors.Where(a => a.IsEnemy && a.State == FighterState.Sleeping))
                enemy.SetState(FighterState.Idle, enemy.Moves.Idle);
        }
        if (!IsCamp)
            UpdateMeleeAssignments(Hero.Mount ?? Hero);
        foreach (var actor in Actors.ToArray())
        {
            if (actor.IsGone || actor.Mount != null)
                continue;
            actor.Age++;
            if (actor.SpawnParameter != 0 && (actor.IsFree || actor.State is FighterState.Emerging or FighterState.Entering) &&
                !AdvanceSpawnParameter(actor))
                continue;
            actor.StateTicks++;
            if (actor.Kind == ActorKind.BodyPile)
            {
                if (actor.StateTicks >= DeathAdderEntrance.WalkTick + DeathAdderEntrance.PileFadeTicks)
                    actor.SetState(FighterState.Gone);
                continue;
            }
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
                AddDragonSmoke(actor);
                UpdateFireball(actor);
                AddDragonGroundHeat(actor);
                continue;
            }
            if (actor.Kind == ActorKind.Lightning)
            {
                UpdateLightning(actor);
                continue;
            }
            if (actor.Kind == ActorKind.LightningCharge)
            {
                UpdateLightningCharge(actor);
                continue;
            }
            if (actor.Kind == ActorKind.Flame)
            {
                if (actor.State == FighterState.Dying)
                {
                    if (actor.StateTicks >= FlameFadeTicks)
                        actor.SetState(FighterState.Gone);
                    continue;
                }
                var owner = actor.Owner;
                if (owner.Rider == null || owner.State != FighterState.Attacking)
                {
                    actor.SetState(FighterState.Dying);
                    continue;
                }
                actor.X = owner.X;
                actor.Z = owner.Z + 1;
                actor.Y = owner.Y;
                actor.Facing = owner.Facing;
                AddDragonSmoke(actor);
                AddDragonGroundHeat(actor);
                // Retain the last visible pose instead of wrapping to the start as it fades.
                if (actor.Animation.Index == actor.Animation.Steps.Length - 1 &&
                    actor.Animation.StepTicks >= actor.Animation.Current.Duration - 1)
                    actor.SetState(FighterState.Dying);
                else
                    actor.Animation.Tick();
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
            var previousX = actor.X;
            UpdateFighter(actor, command);
            if (TickCount % 10 == 0 && Math.Abs(actor.X - previousX) >= 2 &&
                actor.State is FighterState.Walking or FighterState.RunningToPosition or FighterState.Dashing &&
                Math.Abs(actor.Y - Ground(actor.X, actor.Z)) < 1 &&
                Project(actor.X, actor.Z, actor.Y).X is > 16 and < 300)
                m_landingDust.Add(new DustPuff(actor.X - actor.Facing * 5, actor.Z, actor.Y, 2, TickCount, false, true));
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
        if (Hero.Mount != null)
            Hero.X = body.X;
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
        var floor = Ground(body.X, body.Z);
        // Frame the player higher as they approach the front of the opening ledge,
        // while keeping the initial view close to the arcade framing.
        var feetTarget = m_data.UsesCastleOpeningFraming(Progress) ? 188 : 150;
        // Do not chase the jump arc. Falls still lower the view to reveal the landing.
        var desiredY = body.State == FighterState.Jumping
            ? CameraY + Math.Max(0, 64 - Project(body.X, body.Z, body.Y).Y)
            : feetTarget - (z - Math.Floor(z / 4) + Math.Min(body.Y, floor) - 4096);
        CameraY += Math.Clamp(Math.Clamp(desiredY, cameraRange.Low, cameraRange.High) - CameraY, -2, 2);
        var enemies = m_brotherEntrances.Count != 0 || Actors.Any(a => a.IsEnemy && !a.IsGone);
        if (!enemies && (Progress >= stop || FinalWaveSpawned))
        {
            if (++m_clearTicks > 30)
            {
                if (m_stopIndex < m_data.Stops.Length - 1)
                    m_stopIndex++;
                else if (FinalWaveSpawned)
                    Complete = true;
                m_clearTicks = 0;
            }
        }
        else
            m_clearTicks = 0;
    }

    private bool AdvanceSpawnParameter(Fighter actor)
    {
        // The set-6 spawner reserves the actor slot immediately. The actor reads its word at +0x74.
        if (actor.SpawnParameter < 0x1000)
        {
            actor.SpawnParameter--;
            // Mound and door entrances start on the decrement to zero. Walking enemies resume on the following update.
            return actor.SpawnParameter == 0 && (actor.State == FighterState.Emerging || actor.EntranceDoor != null);
        }

        // $ffed5a counts allocated encounter actors, including this actor and other waiting reinforcements.
        // A rider and its beast occupy one encounter slot. Spell effects, pickups and stone bodies occupy none.
        var slots = m_brotherEntrances.Count + Actors.Count(a => !a.IsGone &&
            (a.IsEnemy || a.Kind == ActorKind.Thief || a.Kind == ActorKind.Villager && a.Animation.Address != 0xa1ae));
        // The castle script reserves Adder at scroll $1325, before his throne sequence starts at $14c0.
        if (!IsCamp && AreaIndex == 4 && Progress >= 0x1325 && !m_deathAdderEntered)
            slots++;
        if (slots > (actor.SpawnParameter & 15))
            return false;
        actor.SpawnParameter = 0;
        return true;
    }

    private void EnterDeathAdder()
    {
        m_deathAdderEntered = true;
        var x = m_data.Stops[^1] + ArcadeData.Width / 2;
        var boss = Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 64, x, FindFloor(x, 4112));
        boss.SetState(FighterState.Entering, Moves.DeathAdder.Idle);
        var bodies = Add(ActorKind.BodyPile, Moves.DeathAdder, DeathAdderEntrance.BodyPilePalette, 0, x, boss.Z + 23);
        bodies.Animation.HoldFrame(1128);
    }

    private void SpawnClayEnemy(ActorKind kind, Moves moves, int palette, SpawnDefinition spawn)
    {
        var enemy = Add(kind, moves, palette, 16, spawn.X, spawn.Z);
        enemy.SpawnParameter = spawn.Parameter;
        enemy.SetState(FighterState.Entering, kind == ActorKind.Heninger ? 0x1c164 : 0x2111e);
    }

    internal void Spawn(SpawnDefinition spawn)
    {
        if (IsCamp)
            spawn = spawn with { Z = spawn.Z + Camp.DepthOffset };
        switch (spawn.Routine)
        {
            case 0x1b454: SpawnClayEnemy(ActorKind.Heninger, Moves.StrongHeninger, ClayHeningerPalette, spawn); break;
            case 0x20414: SpawnClayEnemy(ActorKind.Longmoan, Moves.StrongLongmoan, ClayLongmoanPalette, spawn); break;
            case 0x23bda: SpawnSkeletonMound(spawn); break;
            case 0x2536e: Add(ActorKind.Skeleton, Moves.Skeleton, 0x6f, 24, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x2538a: SpawnSkeletonMound(spawn); break;
            case 0x25352: Add(ActorKind.Skeleton, Moves.Skeleton, 0x70, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x2a338: Add(ActorKind.Knight, Moves.Knight, 0x87, 32, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x29364: AddBadBrother(spawn, 0x39).SpawnParameter = spawn.Parameter; break;
            case 0x1a0a8: Add(ActorKind.Heninger, Moves.Heninger, 0x1a, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x1f016: Add(ActorKind.Longmoan, Moves.FastLongmoan, 0x7b, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x21854: Add(ActorKind.Amazon, Moves.Amazon, 0x63, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x21876: Add(ActorKind.Amazon, Moves.Amazon, 0x64, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x229c8: Add(ActorKind.Amazon, Moves.Amazon, 0x65, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x2f60a: case 0x2f61c: case 0x306aa: SpawnMount(ActorKind.BlueDragon, spawn); break;
            case 0x317b8: SpawnMount(ActorKind.RedDragon, spawn); break;
            case 0x2a358:
                var knight = Add(ActorKind.Knight, Moves.Knight, 0x86, 32, spawn.X, spawn.Z, spawn.Parameter);
                EnterDoor(knight, spawn);
                break;
            case 0x18b50: Add(ActorKind.Heninger, Moves.Heninger, 0x1d, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x18b28: Add(ActorKind.Heninger, Moves.Heninger, 0x1b, AreaIndex == 0 && m_stopIndex == 0 ? 8 : 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x18b3c: case 0x18ba6: SpawnForestReinforcement(spawn, ActorKind.Heninger, Moves.Heninger, 0x18); break;
            case 0x1c846: Add(ActorKind.Longmoan, Moves.Longmoan, 0x79, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x1c868: Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, spawn.X, spawn.Z, spawn.Parameter); break;
            case 0x1dc3e: SpawnForestReinforcement(spawn, ActorKind.Longmoan, Moves.FastLongmoan, 0x77); break;
            case 0x282ec:
                // The two Stage 5 records share a trigger and doorway, but carry separate delays.
                m_brotherEntrances.Enqueue((spawn, TickCount + spawn.Parameter));
                break;
            case 0x282d4: AddBadBrother(spawn, 0x38).SpawnParameter = spawn.Parameter; break;
            case 0x2c264: case 0x2c276: case 0x2b10c: SpawnMount(ActorKind.Chicken, spawn); break;
            case 0x17c7c: case 0x17d00: case 0x17d0e: case 0x17d24:
                var food = spawn.Routine == 0x17d00;
                var direction = spawn.X < Progress + 160 ? 1 : -1;
                var thief = Add(ActorKind.Thief, Moves.Thief, food ? 0x3c : 0x3d, 99, spawn.X, FindFloor(spawn.X + direction * 60, spawn.Z));
                thief.FoodThief = food;
                thief.Facing = direction;
                thief.Items = Math.Max(1, spawn.Parameter >> 12);
                if (IsCamp)
                {
                    // Enter from the edge on the same floor as the walk-in lane, not the
                    // off-map height at the original spawn coordinate.
                    var entryX = Progress + (direction > 0 ? 24 : 296);
                    thief.Z = FindFloor(entryX, spawn.Z + Camp.DepthOffset);
                    thief.X = Progress + (direction > 0 ? -24 : 344);
                    thief.Y = Ground(entryX, thief.Z);
                }
                thief.SetState(FighterState.Walking, thief.Moves.Walk);
                break;
            case 0x99c0: case 0x99c8: case 0x99da: case 0x99ec: case 0x99fe: case 0x9a9a:
            case 0x9c08: case 0x9eb6: case 0x9a5a:
                var villagerPalette = spawn.Routine switch
                {
                    0x99da => 0x4e,
                    0x99ec => 0x4b,
                    0x99fe => 0x4c,
                    0x9a9a => 0x4c,
                    0x9a5a or 0x9c08 => 0x42,
                    0x9eb6 => 0x18,
                    _ => 0x4d
                };
                var villager = Add(ActorKind.Villager, Moves.Thief, villagerPalette, 1, spawn.X, spawn.Z);
                // MAME traces match the civilian parameter's low byte in sixteenths of a pixel per tick.
                if (spawn.Routine is 0x99c0 or 0x99c8 or 0x99da or 0x99ec or 0x99fe)
                {
                    villager.VillagerSpeed = (spawn.Parameter & 255) / 16.0;
                    // MAME's opening and late village crowds settle around these measured lanes.
                    // The high byte offsets each actor's lane; retain collision-aware detours.
                    if (AreaIndex == 1 && spawn.Trigger is 4120 or 4768)
                        villager.VillagerTargetZ = (spawn.Trigger == 4120 ? 4192 : 4320) + unchecked((sbyte)(spawn.Parameter >> 8));
                }
                // Distinct extracted civilian run sequences; routine-to-variant mapping still needs MAME verification.
                villager.Animation.Play(spawn.Routine switch
                {
                    0x99da => 0xa04a,
                    0x99ec => 0xa0ac,
                    0x99fe => 0xa10e,
                    0x9a5a => 0xa170,
                    0x9a9a => 0xa35c,
                    0x9c08 => 0xa1ae,
                    0x9eb6 => 0xa228,
                    _ => 0x9fe8
                });
                if (spawn.Routine == 0x9c08)
                {
                    // First observed forest pose is 611 for eight ticks, then the fallen 612 holds.
                    villager.X -= 147;
                    villager.Animation.Seek(villager.Animation.Steps.Length - 1);
                }
                else if (spawn.Routine == 0x9eb6)
                {
                    villager.X -= 87;
                    villager.Facing = 1;
                    villager.VillagerSpeed = 4;
                }
                else if (spawn.Routine == 0x9a9a)
                {
                    villager.VillagerSpeed = (spawn.Parameter & 255) / 16.0;
                    villager.Animation.Seek(1);
                }
                break;
        }
    }

    private void EnterBadBrother(SpawnDefinition spawn)
    {
        var brother = AddBadBrother(spawn, 0x38);
        EnterDoor(brother, spawn);
    }

    private static void EnterDoor(Fighter actor, SpawnDefinition spawn)
    {
        actor.EntranceDoor = spawn;
        actor.Z = spawn.Z - DoorInteriorDepth;
        actor.SetState(FighterState.Entering, actor.Moves.Walk);
    }

    private Fighter AddBadBrother(SpawnDefinition spawn, int palette)
    {
        var brother = Add(ActorKind.BadBrother, Moves.Brother, palette, 32, spawn.X, spawn.Z);
        brother.NextChuckleAge = 0;
        return brother;
    }

    private void SpawnSkeletonMound(SpawnDefinition spawn)
    {
        var skeleton = Add(ActorKind.Skeleton, Moves.Skeleton, spawn.Routine == 0x2538a ? 0x6f : 0x73, 16, spawn.X, spawn.Z);
        // Only authored rise spawns use cracks; the final castle guards arrive standing.
        if (!IsSkeletonMound(spawn) || !m_data.Spawns.Contains(spawn))
            return;

        skeleton.SpawnParameter = spawn.Parameter;
        skeleton.Palette = 0x6f;
        skeleton.Facing = Hero.X < spawn.X ? -1 : 1;
        var animation = AreaIndex == 4 ? SkeletonEmergence.CastleAnimation : SkeletonEmergence.Animation;
        skeleton.SetState(FighterState.Emerging, animation);
        skeleton.Animation.Seek(1); // The script starts with a standing setup pose before the buried skull.
    }

    private void SpawnForestReinforcement(SpawnDefinition spawn, ActorKind kind, Moves moves, int palette)
    {
        var enemy = Add(kind, moves, palette, 16, spawn.X, spawn.Z);
        if (spawn.Routine == 0x18ba6)
        {
            var civilian = Actors.LastOrDefault(a => a.Kind == ActorKind.Villager && a.Owner == null &&
                a.Animation.Address is 0xa170 or 0xa35c && a.X == spawn.X && a.Z == spawn.Z);
            if (civilian != null)
            {
                // Both pairs in each stage share the spawn coordinate. The soldier starts 28 pixels to its right.
                enemy.X += 28;
                enemy.Facing = -1;
                enemy.SetState(FighterState.Stamping, 0x199bc);
                civilian.Owner = enemy;
                if (civilian.Animation.Address == 0xa170)
                {
                    civilian.Palette = 0x4b;
                    civilian.Animation.Play(0xa342);
                }
            }
        }
        // Both entries at the forest's first stop wait for the preceding enemies to clear.
        if (!IsCamp && AreaIndex == 0 && spawn.Trigger == m_data.Stops[0] && spawn.Parameter == 0x1002)
            enemy.SetState(FighterState.Sleeping);
    }

    private void SpawnMount(ActorKind kind, SpawnDefinition spawn)
    {
        var (riderKind, moves, palette) = (AreaIndex, spawn.Routine) switch
        {
            (0, 0x2c264) => (ActorKind.Amazon, Moves.Amazon, 0x63),
            (0, 0x2b10c) => (ActorKind.Heninger, Moves.Heninger, 0x1b),
            (0, _) => (ActorKind.Heninger, Moves.Heninger, 0x1a),
            (1, 0x2f61c) => (ActorKind.Amazon, Moves.Amazon, 0x65),
            (1, _) => (ActorKind.Amazon, Moves.Amazon, 0x64),
            (2, 0x317b8) => (ActorKind.Heninger, Moves.Heninger, 0x1b),
            (2, 0x2c276) => (ActorKind.Amazon, Moves.Amazon, 0x64),
            (3, _) => (ActorKind.Longmoan, Moves.FastLongmoan, 0x7b),
            (4, _) => (ActorKind.Amazon, Moves.Amazon, 0x65),
            _ => (ActorKind.Longmoan, Moves.Longmoan, 0x79)
        };
        var rider = Add(riderKind, moves, palette, 16, spawn.X, spawn.Z);
        var beast = Add(kind, kind == ActorKind.Chicken ? Moves.Chicken : Moves.BlueDragon,
            kind switch { ActorKind.Chicken => 0x23, ActorKind.BlueDragon => 0x43, _ => 0x44 }, 99, spawn.X, spawn.Z);
        Mount(rider, beast);
        beast.SpawnParameter = spawn.Parameter;
    }

    private void UpdateFireball(Fighter fireball)
    {
        if (fireball.State == FighterState.Dying)
        {
            fireball.Animation.Tick();
            if (fireball.StateTicks >= FireballImpactTicks)
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
        // Set 6 accelerates a fleeing beast by half a pixel per tick to four times its walk speed.
        var speed = Math.Min(6, (beast.StateTicks + 1) * 0.5);
        var direction = beast.EscapeDirection;
        var oldX = beast.X;
        var oldZ = beast.Z;
        if (direction < 0 && screenX < 24 || direction > 0 && screenX > 296)
        {
            // Keep running beyond the visible collision map until the whole sprite exits.
            beast.X += direction * speed;
            beast.Facing = direction;
        }
        else
        {
            var ahead = Ground(beast.X + direction * 12, beast.Z);
            var input = ahead < beast.Y - 12 || ahead >= 5248
                ? TerrainNavigator.Towards(this, beast, Progress + (direction < 0 ? 12 : 305), beast.Z)
                : new GameInput(direction);
            var step = input.X != 0 && input.Z != 0 ? Math.Min(speed, 4.25) : speed;
            Move(beast, input.X * step, input.Z * step);
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

    private void UpdateLightningCharge(Fighter charge)
    {
        if (charge.Owner.Health > 0 && !charge.Owner.IsGone)
        {
            if (!charge.Animation.Tick())
                return;
            var target = Hero.Mount ?? Hero;
            var distance = target.X - charge.X;
            var facing = distance == 0 ? charge.Facing : Math.Sign(distance);
            var launchOffset = Math.Clamp(Math.Abs(distance) - 12, 8, 56);
            var bolt = Add(ActorKind.Lightning, Moves.Lightning, LightningPalette, 1,
                charge.X + facing * launchOffset, charge.Z);
            bolt.Owner = charge.Owner;
            charge.Owner.Facing = bolt.Facing = facing;
        }
        charge.SetState(FighterState.Gone);
    }

    private void UpdateLightning(Fighter bolt)
    {
        if (bolt.Owner.Health <= 0 || bolt.Owner.IsGone || bolt.Age > 180)
        {
            bolt.SetState(FighterState.Gone);
            return;
        }
        const int hopTicks = 8;
        if (bolt.Age % hopTicks == 0)
        {
            m_lightningTrail.Add(new LightningMark(bolt.X, bolt.Z, bolt.Y, bolt.Facing, TickCount));
            var target = Hero.Mount ?? Hero;
            var hopsToTarget = Math.Max(1, Math.Ceiling(Math.Abs(target.X - bolt.X) / 16));
            bolt.X += bolt.Facing * 16;
            bolt.Z += Math.Clamp((target.Z - bolt.Z) / hopsToTarget, -12, 12);
        }
        var floor = Ground(bolt.X, bolt.Z);
        var screenX = Project(bolt.X, bolt.Z, bolt.Y).X;
        if (floor is <= 3840 or >= 5248 || screenX is < -32 or > 352)
        {
            bolt.SetState(FighterState.Gone);
            return;
        }
        bolt.Y = floor;
        // Each of the four ROM scripts makes a different phase the damaging frame.
        var phase = bolt.Age / hopTicks % 4;
        bolt.Animation.Play(0x3618e + phase * 0x3a);
        bolt.Animation.Seek(phase);
        ResolveAttack(bolt);
        if (bolt.Connected)
        {
            m_lightningTrail.Add(new LightningMark(bolt.X, bolt.Z, bolt.Y, bolt.Facing, TickCount));
            bolt.SetState(FighterState.Gone);
        }
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
        if (TryStartBrotherChuckle(actor, target, dx, dz))
            return default;
        return AssignedMeleeInput(actor, target, dx, dz);
    }

    // Set-6 approach positions are separate from the reach of the selected attack.
    private static int MeleeSlotDistance(ActorKind kind) => kind switch
    {
        ActorKind.Knight => 52,
        ActorKind.Amazon or ActorKind.BadBrother or ActorKind.DeathAdder or ActorKind.Chicken => 48,
        ActorKind.BlueDragon or ActorKind.RedDragon => 64,
        _ => 40
    };

    private bool CanAssignMeleeAgainst(Fighter target) => Hero.Health > 0 &&
        target.State is not (FighterState.Flying or FighterState.Down or FighterState.GettingUp or
            FighterState.Falling or FighterState.Dying or FighterState.Respawning);

    private void UpdateMeleeAssignments(Fighter target)
    {
        Fighter first = null, second = null;
        var firstDistance = double.PositiveInfinity;
        var secondDistance = double.PositiveInfinity;
        if (CanAssignMeleeAgainst(target))
        {
            foreach (var actor in Actors)
            {
                if ((!actor.IsEnemy && actor.Rider is not { IsEnemy: true, Health: > 0 }) ||
                    actor.Health <= 0 || actor.SpawnParameter != 0 || actor.Mount != null ||
                    actor.State is not (FighterState.Idle or FighterState.Walking or FighterState.Attacking or
                        FighterState.Dashing or FighterState.Crouching or FighterState.Jumping or
                        FighterState.RunningToPosition or FighterState.Chuckling))
                    continue;
                var dx = actor.X - target.X;
                var dz = actor.Z - target.Z;
                var distance = dx * dx + dz * dz;
                if (distance < firstDistance)
                {
                    (second, secondDistance) = (first, firstDistance);
                    (first, firstDistance) = (actor, distance);
                }
                else if (distance < secondDistance)
                    (second, secondDistance) = (actor, distance);
            }
        }

        // The set-6 allocator selects the nearest two, then uses the one closer in depth
        // to choose a side. It retains the previous sides if the same pair reverses order.
        if (second != null && Math.Abs(second.Z - target.Z) < Math.Abs(first.Z - target.Z))
            (first, second) = (second, first);
        var (left, right) = first != null && first.X >= target.X ? (second, first) : (first, second);
        if (left != null && right != null && left == m_meleeRight && right == m_meleeLeft)
            return;
        (m_meleeLeft, m_meleeRight) = (left, right);
    }

    private GameInput AssignedMeleeInput(Fighter actor, Fighter target, double dx, double dz)
    {
        if (!actor.IsFree)
            return default;

        var canEngage = CanAssignMeleeAgainst(target);
        // A pursuing enemy can follow the player into a mapped drop. Limit the probe to
        // the space between them so a player still on this bank doesn't lure it past them.
        if (actor.IsEnemy && actor.Kind != ActorKind.DeathAdder && Hero.Health > 0 &&
            (canEngage || target.State == FighterState.Falling) &&
            Ground(actor.X + Math.Clamp(dx, -16, 16), actor.Z + Math.Clamp(dz, -16, 16)) >= 5248)
        {
            var followX = Math.Sign(dx);
            var followZ = Math.Sign(dz);
            if (followX != 0)
                actor.Facing = followX;
            return new GameInput(followX, followZ, FollowOverPit: true);
        }
        if (!canEngage)
            return default;

        var side = actor == m_meleeLeft ? -1 : actor == m_meleeRight ? 1 : 0;
        var slotDistance = MeleeSlotDistance(actor.Kind);
        var sameSide = dx * side < 0;
        if (side != 0 && actor.Kind is ActorKind.BadBrother or ActorKind.DeathAdder &&
            TryStartHeavyRun(actor, target, side, slotDistance, dx, dz))
            return default;
        if (sameSide && EnemyCharge(actor, target, dx, dz) is { } charge)
            return charge;

        if (side == 0 || dx * side <= -28)
            actor.FlankSide = 0;
        else if (!sameSide)
            actor.FlankSide = side;
        if (side != 0 && actor.FlankSide == side &&
            FlankInput(actor, target, side, target.X + side * slotDistance) is { } flank)
            return flank;

        actor.Facing = dx < 0 ? -1 : 1;
        var attackRange = actor.Kind switch
        {
            ActorKind.RedDragon => 146,
            ActorKind.BlueDragon => 76,
            ActorKind.Chicken or ActorKind.DeathAdder => 48,
            ActorKind.Amazon or ActorKind.Skeleton => 36,
            _ => 44
        };
        var phaseMask = actor.Kind is ActorKind.Amazon or ActorKind.Skeleton or ActorKind.Knight or ActorKind.DeathAdder ? 3 : 7;
        var aligned = Math.Abs(dz) < 6 && Math.Abs(target.Y - actor.Y) <= 8;
        if (sameSide && Math.Abs(dx) <= attackRange && aligned &&
            actor.Cooldown == 0 && (TickCount & phaseMask) == (actor.Id & phaseMask))
            return new GameInput(Attack: true);

        // Unassigned enemies approach with room for the pair in front.
        var reach = side == 0 ? slotDistance + 8 : Math.Min(slotDistance - 8, attackRange - 4);
        var x = Math.Abs(dx) > reach || side != 0 && !sameSide ? Math.Sign(dx + side * reach) : 0;
        var ahead = Ground(actor.X + x * 16, actor.Z + Math.Sign(dz) * 4);
        if (ahead < actor.Y - 12 || ahead >= 5248 || Math.Abs(target.Y - actor.Y) > 24)
            return TerrainNavigator.Towards(this, actor, target.X + side * reach, target.Z);
        return new GameInput(x, Math.Abs(dz) > 2 ? Math.Sign(dz) : 0);
    }

    private bool TryStartHeavyRun(Fighter actor, Fighter target, int side, int distance, double dx, double dz)
    {
        if (Hero.Health <= 0 || Math.Abs(target.Y - actor.Y) > 8 || dx == 0)
            return false;

        var direction = Math.Sign(dx);
        var slotDistance = (dx + side * distance) * direction;
        var across = side == direction;
        var attackRun = !across && slotDistance >= 80 && Math.Abs(dz) < 3;
        if (attackRun)
        {
            var brother = actor.Kind == ActorKind.BadBrother;
            if (actor.Cooldown != 0 || !target.Vulnerable || Math.Abs(actor.X - (Progress + 160)) >= (brother ? 128 : 160))
                return false;
            actor.Facing = direction;
            if (!brother)
            {
                actor.FlameStarted = false;
                actor.SetState(FighterState.Attacking, Moves.DeathAdderCast, true);
                return true;
            }
        }
        else if (!across || slotDistance < 128 || Math.Abs(dz) < 16)
            return false;

        var destination = target.X + direction * (attackRun ? -96 : 32);
        if (!FlankPathIsWalkable(actor.Y, actor.X, actor.Z, destination, actor.Z))
            return false;
        actor.ChargeAttackX = destination;
        actor.Facing = direction;
        actor.Vx = 0;
        actor.SetState(FighterState.RunningToPosition, attackRun ? Moves.BrotherAttackRun : actor.Moves.Run);
        return true;
    }

    private bool TryStartBrotherChuckle(Fighter actor, Fighter target, double dx, double dz)
    {
        if (actor.NextChuckleAge < 0 || actor.Age < actor.NextChuckleAge || !actor.IsFree ||
            actor.FlankSide != 0 || Hero.Health <= 0 || Math.Abs(target.Y - actor.Y) > 12 ||
            Project(actor.X, actor.Z, actor.Y) is not { X: >= 20 and <= 300, Y: >= 64 and <= 220 })
            return false;

        // Start with the standing laugh. Later pauses depend on spacing and alternate between brothers.
        if (actor.NextChuckleAge != 0 &&
            (Math.Abs(dx) > 104 || Math.Abs(dz) > 32 ||
             actor.Cooldown == 0 && Math.Abs(dx) < 44 && Math.Abs(dz) < 6 ||
             Actors.Any(other => other.State == FighterState.Chuckling)))
            return false;

        actor.Facing = dx < 0 ? -1 : 1;
        actor.Vx = 0;
        actor.NextChuckleAge = actor.Age + BrotherChuckleTicks + 300 + actor.Id % 4 * 36;
        actor.SetState(FighterState.Chuckling, actor.Moves.Idle);
        return true;
    }

    private GameInput? FlankInput(Fighter actor, Fighter target, int side, double destinationX)
    {
        const int bypass = 24;
        var preferredLane = actor.Z < target.Z ? -1 : 1;
        foreach (var lane in new[] { preferredLane, -preferredLane })
        {
            var laneZ = target.Z + lane * bypass;
            if (destinationX < Progress + 12 || destinationX > Progress + 305 ||
                !FlankPathIsWalkable(actor.Y, actor.X, actor.Z, actor.X, laneZ) ||
                !FlankPathIsWalkable(actor.Y, actor.X, laneZ, destinationX + side * 8, laneZ) ||
                !FlankPathIsWalkable(actor.Y, destinationX, laneZ, destinationX, target.Z))
                continue;

            var depth = laneZ - actor.Z;
            var moveX = Math.Abs(depth) < 4 ? side : 0;
            if (moveX == 0)
                actor.Facing = actor.X < target.X ? 1 : -1;
            return new GameInput(moveX, Math.Abs(depth) > 1 ? Math.Sign(depth) : 0);
        }
        return null;
    }

    private bool FlankPathIsWalkable(double height, double x, double z, double endX, double endZ)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(endX - x), Math.Abs(endZ - z)) / 8));
        for (var i = 0; i <= steps; i++)
        {
            var floor = Ground(x + (endX - x) * i / steps, z + (endZ - z) * i / steps);
            if (Math.Abs(floor - height) > 12)
                return false;
        }
        return true;
    }

    internal bool HasImminentStrike(Fighter actor) => actor.State == FighterState.Attacking &&
        m_data.Attacks.TryGetValue(actor.Animation.Address, out var attack) && actor.Animation.TicksUntilStep(attack[2]) is >= 0 and <= 12;

    internal bool HasImminentStrikeAgainst(Fighter attacker, Fighter target) =>
        attacker.State == FighterState.Attacking && Math.Abs(attacker.Z - target.Z) < 7 && !attacker.Struck.Contains(target.Id) &&
        m_data.Attacks.TryGetValue(attacker.Animation.Address, out var attack) &&
        attacker.Animation.TicksUntilStep(attack[2]) is >= 0 and <= 4 &&
        AttackOverlapsBody(attacker, attacker.Animation.Steps[attack[2]], attack, target, target.Animation.Current);

    private GameInput? EnemyCharge(Fighter actor, Fighter target, double dx, double dz)
    {
        var distance = Math.Abs(dx);
        var soldier = actor.Moves == Moves.Heninger || actor.Moves == Moves.Longmoan;
        var running = actor.State == FighterState.Walking && actor.Animation.Address == actor.Moves.Run;
        var committed = soldier && running;
        if (!actor.IsEnemy || !actor.IsFree || actor.Cooldown != 0 || !committed && !target.Vulnerable ||
            actor.Moves.Dash == 0 && actor.Moves.JumpAttack == 0 || !committed && (soldier ? Math.Abs(dz) >= 3 : Math.Abs(dz) > 5) ||
            Math.Abs(target.Y - actor.Y) > 8 || Project(actor.X, actor.Z, actor.Y).X is < 16 or > 304)
            return null;

        var leap = actor.Moves.Dash == 0;
        var attackRange = leap ? 85 : 48;
        if (soldier)
        {
            // The attack slot is 40 pixels from the target. Start 96 pixels outside it.
            if (!running && (distance < 136 || Math.Abs(actor.X - (Progress + 160)) >= 128))
                return null;
        }
        else if (distance < 32 || !running && (distance < 140 || target.State != FighterState.Idle || target.StateTicks < 36))
            return null;

        if (!running && Actors.Any(other => other != actor && other.IsEnemy && other.Health > 0 &&
            (Math.Abs(other.X - target.X) < 56 && Math.Abs(other.Z - target.Z) < 20 ||
             other.State is FighterState.Dashing or FighterState.Crouching or FighterState.Jumping ||
             other.State == FighterState.Walking && other.Animation.Address == other.Moves.Run)))
            return null;

        var direction = committed ? Math.Sign(actor.Vx) : Math.Sign(dx);
        if (soldier && !running)
            actor.ChargeAttackX = target.X - direction * 96;
        var attack = running && Math.Abs(actor.Vx) >= 2 &&
            (soldier ? (actor.ChargeAttackX - actor.X) * direction <= 0 : distance <= attackRange);
        var pathLength = attack && leap ? distance + 16 : Math.Min(distance, 48);
        for (var offset = 8; offset <= pathLength; offset += 8)
        {
            var floor = Ground(actor.X + direction * offset, actor.Z);
            if (floor is <= 3840 or >= 5248 || Math.Abs(floor - actor.Y) > 8)
                return null;
        }

        return new GameInput(direction, Attack: attack, Jump: attack && leap, Run: true);
    }

    private void UpdateFighter(Fighter actor, GameInput input)
    {
        if (actor.Rider == Hero && actor.IsFree && input.Dismount)
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
        if (actor.Rider == Hero && actor.IsFree && input.Jump)
        {
            if (input.X != 0)
                actor.Facing = Math.Sign(input.X);
            var runningJump = input.Run && input.X != 0;
            actor.Vx = input.X * (runningJump ? 3 : 1.5);
            actor.Vy = runningJump ? RunningJumpVelocity : JumpVelocity;
            actor.SetState(FighterState.Jumping, actor.Moves.Jump);
            return;
        }
        switch (actor.State)
        {
            case FighterState.RunningToPosition:
                var attackRun = actor.Animation.Address == Moves.BrotherAttackRun;
                var remaining = (actor.ChargeAttackX - actor.X) * actor.Facing;
                actor.Vx = actor.Facing * Math.Min(attackRun ? 4 : 3, Math.Abs(actor.Vx) + 0.5);
                var oldX = actor.X;
                Move(actor, actor.Facing * Math.Min(Math.Abs(actor.Vx), Math.Max(0, remaining)), 0);
                actor.Animation.Tick();
                if (actor.X == oldX || actor.X == actor.ChargeAttackX)
                {
                    if (attackRun && actor.X == actor.ChargeAttackX)
                    {
                        actor.Vy = -3;
                        actor.SetState(FighterState.Jumping, Moves.BrotherChargeImpact);
                    }
                    else
                    {
                        actor.Vx = 0;
                        actor.SetState(FighterState.Idle, actor.Moves.Idle);
                    }
                }
                return;
            case FighterState.Stamping:
                actor.Animation.Tick();
                if (actor.Animation.Index == 0 && (actor.StateTicks > 480 || Progress + 188 >= actor.X))
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Chuckling:
                actor.Animation.Tick();
                if (actor.StateTicks >= BrotherChuckleTicks)
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Entering when actor.Moves == Moves.StrongHeninger || actor.Moves == Moves.StrongLongmoan:
                // Both clay initializers hold their entrance pose for 0x3c updates after the gate opens.
                if (actor.StateTicks >= 60)
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Entering when actor.Kind == ActorKind.DeathAdder:
                if (actor.StateTicks >= DeathAdderEntrance.WalkTick)
                {
                    actor.Animation.Play(actor.Moves.Walk);
                    actor.Animation.Tick();
                    Move(actor, 0, 1);
                    if (actor.StateTicks >= DeathAdderEntrance.WalkTick + 48)
                        actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Entering:
                if (actor.StateTicks < DoorRevealStart)
                    return;
                actor.Animation.Tick();
                if (actor.StateTicks <= DoorRevealEnd && actor.EntranceDoor is { } door)
                {
                    // The door interior is outside the walkable height map. Keep the sill elevation until the feet reach it.
                    actor.Z = door.Z - DoorInteriorDepth * (DoorRevealEnd - actor.StateTicks) / (double)(DoorRevealEnd - DoorRevealStart);
                }
                else
                    Move(actor, -0.5, 0.75);
                if (actor.StateTicks >= DoorEntranceDuration)
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Emerging:
                if (actor.StateTicks is 8 or 24)
                    AddLandingDust(actor, heavy: true);
                actor.Animation.Tick();
                if (actor.StateTicks >= SkeletonEmergence.Duration)
                {
                    m_openedSkeletonMounds.Add(m_data.Spawns.First(s => IsSkeletonMound(s) && s.X == actor.X && s.Z == actor.Z));
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                    actor.Cooldown = 30;
                }
                return;
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
                if (actor.StateTicks >= (actor.Moves.FollowUpHurt != 0 ? ArcadeLightHitTicks + 1 : 18))
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            case FighterState.Collapsing:
                actor.Animation.Tick();
                if (actor.Animation.Current.Duration == 255)
                    LandKnockdown(actor);
                return;
            case FighterState.Flying:
                var knockedX = Math.Clamp(actor.X + actor.Vx, Progress + 12, Progress + 296);
                if (Ground(knockedX, actor.Z) > 3840)
                    actor.X = knockedX;
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                if (FellIntoPit(actor))
                    return;
                if (actor.StateTicks % 8 == 0)
                    actor.Animation.Seek(Math.Min(actor.Animation.Index + 1, actor.Animation.Steps.Length - 2));
                var knockdownFloor = Ground(actor.X, actor.Z);
                if (knockdownFloor < 5248 && actor.Y >= knockdownFloor)
                {
                    actor.Y = knockdownFloor;
                    actor.Animation.Seek(actor.Animation.Steps.Length - 1);
                    LandKnockdown(actor);
                }
                return;
            case FighterState.Down:
                if (actor.StateTicks >= KnockdownTicks + (actor.Health > 0 ? actor.KnockdownVariation : 0))
                {
                    if (actor.Health <= 0)
                        Die(actor);
                    else
                    {
                        actor.SetState(FighterState.GettingUp, actor.Moves.GetUp);
                        if (actor.IsEnemy || actor.Kind == ActorKind.Hero)
                            actor.Animation.Seek(2);
                    }
                }
                return;
            case FighterState.GettingUp:
                // Hero recovery runs from sitting up (last step) to crouching (step zero), then stands.
                // Stop before the script loops back through its flat-on-back hold.
                if (actor.Kind == ActorKind.Hero ? actor.Animation.Index != 0 :
                    !actor.IsEnemy || actor.Animation.Index < actor.Animation.Steps.Length - 1)
                    actor.Animation.Tick();
                if (actor.StateTicks >= 24)
                {
                    actor.Invulnerable = actor == Hero ? 70 : 20;
                    if (actor.Kind == ActorKind.Knight)
                        actor.Combo = Array.IndexOf(actor.Moves.Attacks, Moves.KnightOverheadSwing);
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
            case FighterState.StruckByOwnAxe:
                if (actor.StateTicks == DeathAdderDefeat.ImpactTick)
                    actor.Animation.Play(DeathAdderDefeat.EmbeddedPose);
                if (actor.StateTicks >= DeathAdderDefeat.Duration)
                    Petrify(actor);
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
                    actor.RespawnTick = TickCount;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Attacking:
                if (actor.Kind == ActorKind.DeathAdder && actor.Animation.Address == Moves.DeathAdderCast)
                {
                    if (actor.Animation.Index == 0)
                    {
                        actor.Animation.Tick();
                        if (actor.Animation.Index != 0)
                        {
                            actor.Cooldown = 45;
                            actor.SetState(FighterState.Idle, actor.Moves.Idle);
                        }
                        return;
                    }
                    if (actor.Animation.Index >= 3 && !actor.FlameStarted)
                    {
                        actor.FlameStarted = true;
                        actor.Facing = Hero.X < actor.X ? -1 : 1;
                        var charge = Add(ActorKind.LightningCharge, Moves.Lightning, LightningPalette, 1, actor.X, actor.Z + 1);
                        charge.Owner = actor;
                        charge.Facing = actor.Facing;
                        charge.Animation.Play(Moves.LightningDescent);
                    }
                    actor.Animation.Tick();
                    return;
                }
                if (actor.Kind == ActorKind.BlueDragon && actor.Animation.Current.Frame == 0x277 && !actor.FlameStarted)
                {
                    actor.FlameStarted = true;
                    var flame = Add(ActorKind.Flame, Moves.BlueDragon, 0x45, 1, actor.X, actor.Z + 1);
                    flame.Owner = actor;
                    flame.Facing = actor.Facing;
                    flame.Y = actor.Y;
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
                if (actor.Kind != ActorKind.Knight)
                    ResolveAttack(actor);
                var attackFinished = actor.Animation.Tick();
                if (actor.Kind == ActorKind.Knight && !attackFinished)
                    ResolveAttack(actor);
                if (attackFinished)
                {
                    actor.ComboTimer = 40;
                    actor.Combo = actor.Connected ? (actor.Combo + 1) % actor.Moves.Attacks.Length : 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                    actor.Cooldown = actor == Hero || actor.Rider == Hero ? 0 : 45;
                }
                return;
            case FighterState.Dashing:
                var advancing = actor.StateTicks <= (actor == Hero ? HeroBargeTravelTicks : 17);
                Move(actor, advancing ? actor.Facing * BargeSpeed : 0, 0);
                if (actor.IsMount)
                {
                    // Reuse the paired beast/rider take-off pose as a grounded charge brace.
                    // This is a native presentation choice pending mounted-charge MAME verification.
                    if (actor.Moves.Jump == 0)
                        actor.Animation.Tick();
                    else if (actor.StateTicks == 18)
                        actor.Animation.Play(actor.Moves.Idle);
                    var chargeFloor = Ground(actor.X, actor.Z);
                    if (chargeFloor - actor.Y > 12)
                    {
                        actor.Vy = 0;
                        actor.SetState(FighterState.Falling, actor.Moves.Walk);
                        return;
                    }
                }
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                var dashFloor = Ground(actor.X, actor.Z);
                if (actor == Hero && dashFloor >= 5248)
                {
                    if (FellIntoPit(actor))
                        return;
                    if (!advancing)
                    {
                        actor.Vx = 0;
                        actor.SetState(FighterState.Falling, actor.Moves.Jump);
                        actor.Animation.Seek(2);
                        return;
                    }
                }
                else
                    actor.Y = Math.Min(actor.Y, dashFloor);
                if (advancing)
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
                    var jumpAttack = actor.IsEnemy && actor.Animation.Address == actor.Moves.JumpAttack;
                    actor.Vy = jumpAttack ? -4.25 : Math.Abs(actor.Vx) > 2 ? RunningJumpVelocity : JumpVelocity;
                    actor.SetState(FighterState.Jumping, jumpAttack ? actor.Moves.JumpAttack : actor.Moves.Jump);
                    actor.Animation.Seek(2);
                }
                else if (actor.IsEnemy && actor.Animation.Address == actor.Moves.JumpAttack)
                    actor.Animation.Tick();
                return;
            case FighterState.Falling:
                Move(actor, input.X * 1.5, input.Z * 0.5);
                actor.Vy += 0.25;
                actor.Y += actor.Vy;
                if (FellIntoPit(actor))
                    return;
                var fallingFloor = Ground(actor.X, actor.Z);
                if (fallingFloor < 5248 && actor.Y >= fallingFloor)
                {
                    actor.Y = fallingFloor;
                    if (actor.Vy >= 3)
                        AddLandingDust(actor);
                    actor.Vy = 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
            case FighterState.Jumping:
                // A running start raises the hero's jump arc, while air steering
                // stays at one pixel per tick, as in the arcade.
                var dx = actor != Hero && Math.Abs(actor.Vx) > 2 ? actor.Vx : input.X;
                if (actor.IsMount && Math.Abs(actor.Vx) <= 2)
                    dx = input.X == 0 ? actor.Vx : input.X * 1.5;
                Move(actor, dx, input.Z * 0.5);
                actor.Y += actor.Vy;
                actor.Vy += 0.25;
                if (FellIntoPit(actor))
                    return;
                if (input.Attack && actor.Moves.JumpAttack != 0 && actor.Animation.Address != actor.Moves.JumpAttack)
                {
                    actor.Animation.Play(actor.Moves.JumpAttack, true, true);
                    actor.Struck.Clear();
                }
                if (actor.Animation.Address == actor.Moves.JumpAttack || actor.Animation.Address == Moves.BrotherChargeImpact)
                {
                    ResolveAttack(actor);
                    actor.Animation.Tick();
                }
                var landingY = Ground(actor.X, actor.Z);
                if (actor.IsMount)
                    AdvanceMountedJumpPose(actor, landingY);
                if (landingY < 5248 && actor.Y >= landingY && actor.Vy > 0)
                {
                    actor.Y = landingY;
                    AddLandingDust(actor);
                    if (actor.IsMount)
                        actor.Vx = actor.Vy = 0;
                    actor.SetState(FighterState.Idle, actor.Moves.Idle);
                }
                return;
        }
        if (actor == Hero && TryCastMagic(input))
            return;
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
            if (actor.IsMount && actor.Rider != null && input.Run && Math.Abs(actor.Vx) >= 3)
            {
                actor.Vy = 0;
                actor.Connected = false;
                actor.Struck.Clear();
                actor.SetState(FighterState.Dashing, actor.Moves.Jump != 0 ? actor.Moves.Jump : actor.Moves.Run);
                return;
            }
            if (actor.IsEnemy && input.Run)
            {
                actor.Cooldown = 105;
                actor.Vx = actor.Facing * 2.75;
                actor.Vy = -2.6;
                if (input.Jump)
                    actor.SetState(FighterState.Crouching, actor.Moves.JumpAttack);
                else
                    actor.SetState(FighterState.Dashing, actor.Moves.Dash);
                return;
            }
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
            if (actor.Kind == ActorKind.Knight)
                actor.Animation.Seek(1);
            return;
        }
        if (input.Jump && actor.Moves.Jump != 0)
        {
            if (actor == Hero && input.Run && input.X != 0)
                actor.Vx = Math.Sign(input.X) * 3;
            actor.SetState(FighterState.Crouching, actor.Moves.Jump);
            return;
        }
        var speed = actor == Hero || actor.Rider != null ? 1.5 : actor.Kind switch
        {
            ActorKind.BadBrother => 0.5,
            ActorKind.Knight or ActorKind.DeathAdder => 1,
            ActorKind.Skeleton => 2,
            _ => actor.Moves.WalkSpeed
        };
        var run = (actor == Hero || actor.IsEnemy || actor.Rider != null) && input.Run && input.X != 0 && ((actor != Hero && actor.Rider == null) || input.Z == 0);
        if (run)
        {
            actor.Vx = Math.Sign(input.X) * Math.Min(actor == Hero || actor.Rider != null ? 4 : actor.Moves.RunSpeed, Math.Abs(actor.Vx) + 0.5);
            speed = Math.Abs(actor.Vx);
        }
        else
            actor.Vx = 0;
        // Set-6 direction table 0x198a4 uses 90/128 of straight speed on each diagonal axis.
        if (!run && input.X != 0 && input.Z != 0)
        {
            if (actor.Rider != null)
                speed = 272.0 / 256;
            else if (actor.IsEnemy || actor == Hero)
                speed *= 90.0 / 128;
        }
        Move(actor, input.X * speed, input.Z * speed, input.FollowOverPit);
        if (input.X != 0 || input.Z != 0)
        {
            if (actor.State != FighterState.Walking)
                actor.SetState(FighterState.Walking);
            actor.Animation.Play(run ? actor.Moves.Run : input.Z < 0 ? actor.Moves.WalkUp : actor.Moves.Walk);
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

    internal bool CanHitWithMagic(Fighter actor) => actor.IsEnemy &&
        actor.State is not (FighterState.Emerging or FighterState.Entering) && actor.Health > 0 &&
        actor.HeldBy == null && ProjectView(actor.X, actor.Z, actor.Y) is { X: >= -20 and <= 340, Y: >= 0 and <= 224 };

    private bool TryCastMagic(GameInput input)
    {
        if (IsCamp || !input.Magic || Potions == 0 || Hero.Health <= 0)
            return false;

        m_potionPickups.Clear();
        m_magicTargets.Clear();
        m_magicTargets.AddRange(Actors.Where(CanHitWithMagic));
        MagicLevel = Character.GetMagicLevel(Potions);
        // The export indexes recordings by the potion count fed to the arcade.
        var recordings = MagicEffects;
        ActiveMagicEffects = recordings[Math.Min(Potions, recordings.Length) - 1];
        // Wait until the last casting potion has disappeared, including any recorded flicker gaps.
        MagicShakeStart = Array.FindLastIndex(ActiveMagicEffects, frame => frame.Any(sprite => sprite[0] == 517)) + 1;
        var potionCount = Potions;
        Potions = 0;
        MagicTick = 0;
        m_magicPeak = Math.Max(30, Enumerable.Range(0, ActiveMagicEffects.Length)
            .OrderByDescending(i => ActiveMagicEffects[i].Length).FirstOrDefault());
        // The larger potion fan must not apply damage before the reconstructed spell appears.
        if (Character.MagicKey == "tyris" && potionCount >= 7)
            m_magicPeak = potionCount == 9 ? 106 : potionCount == 8 ? 54 : 48;
        Hero.SetState(FighterState.Magic, Character.Magic);
        return true;
    }

    private void AddMagicGroundGlow()
    {
        if (MagicTick >= ActiveMagicEffects.Length)
            return;
        var hero = ProjectView(Hero.X, Hero.Z, Ground(Hero.X, Hero.Z));
        // The core keeps the same patch of ground hot while the last flames are still overhead.
        if (Character.MagicKey == "tyris" && MagicLevel == 5 && MagicTick >= 48)
        {
            var center = Hero.X + TyrisMagic.MeteorCenterX - hero.X;
            var depth = Hero.Z + (TyrisMagic.MeteorFloorY - hero.Y) / 0.75;
            for (var offset = -48; offset <= 48; offset += 48)
                RefreshGroundHeat(center + offset, depth, 1, "tyris");
        }
        foreach (var effect in ActiveMagicEffects[MagicTick])
        {
            // Physical props and the airborne sections of the dwarf's cages do not light the floor.
            if (effect[1] is 0x4a or 0x51 or 0x47 || effect[0] == 518 ||
                Character.MagicKey == "gilius" && LightningCage.IsSprite(effect))
                continue;
            var frame = m_data.Frames[effect[0]];
            var screenSpace = effect.Length > 6 && effect[6] == 1;
            var point = screenSpace
                ? (X: effect[3], Y: effect[5])
                : ProjectView(Hero.X + effect[3], Hero.Z + effect[4], Hero.Y + effect[5]);
            var x = Hero.X + point.X + (frame.Ox + frame.Width * 0.5) * (effect[2] == 1 ? -1 : 1) - hero.X;
            double z;
            if (screenSpace)
            {
                // Dragon fire sweeps over this fixed floor lane even as its head rises away.
                var floorY = effect[1] == 0x52 ? 176 : point.Y + frame.Oy + frame.Height;
                if (floorY is < 160 or > 210)
                    continue;
                z = Hero.Z + (floorY - hero.Y) / 0.75;
            }
            else
            {
                z = Hero.Z + effect[4];
                var bottom = Hero.Y + effect[5] + frame.Oy + frame.Height;
                if (Math.Abs(bottom - Ground(x, z)) > 20)
                    continue;
            }
            var first = 0;
            while (first < MagicTick && !ActiveMagicEffects[first].Any(s => s[1] == effect[1]))
                first++;
            RefreshGroundHeat(x, z, Math.Clamp((MagicTick - first + 1) / 12.0, 0, 1), Character.MagicKey);
        }

        var cageTick = LightningCage.SampleTick(this);
        if (cageTick >= 0 && MagicEffects[0][cageTick].Any(s => s[0] is >= 569 and <= 572))
        {
            // The renderer attaches a separate cage to every victim, not just the recorded caster.
            foreach (var target in LightningCage.Targets(this))
                RefreshGroundHeat(target.X, target.Z, Math.Clamp((cageTick - 65) / 12.0, 0, 1), Character.MagicKey);
        }

    }

    private void AddDragonGroundHeat(Fighter flame)
    {
        if (flame.IsGone)
            return;
        var step = flame.Animation.Current;
        var frame = m_data.Frames[step.Frame];
        var direction = ((step.Flags & 1) != 0) ^ flame.Facing < 0 ? -1 : 1;
        double offset = step.Dx * flame.Facing;
        if (flame.Kind == ActorKind.Flame)
            offset += (frame.Ox + frame.Width * 0.5) * direction;
        var x = flame.X + offset;
        var ground = Ground(x, flame.Z);
        var height = Math.Max(0, ground - flame.Y);
        var strength = flame.Kind == ActorKind.Flame ? Math.Min(1, frame.Width / 42.0) : 1 / (1 + height / 65);
        RefreshGroundHeat(x, flame.Z, strength, "tyris");
    }

    private void RefreshGroundHeat(double x, double z, double strength, string magicKey)
    {
        var ground = Ground(x, z);
        if (ground is <= 3840 or >= 5248)
            return;
        var index = m_magicGroundGlow.FindIndex(glow => glow.MagicKey == magicKey &&
            Math.Abs(glow.X - x) < 28 && Math.Abs(glow.Z - z) < 12);
        if (index >= 0)
            m_magicGroundGlow[index] = m_magicGroundGlow[index] with { Tick = TickCount, Strength = strength };
        else
            m_magicGroundGlow.Add(new MagicGroundGlow(x, z, ground, TickCount, strength, magicKey, TickCount));
    }

    private void AddMagicSmoke()
    {
        if (Character.MagicKey != "tyris" || MagicTick >= ActiveMagicEffects.Length || MagicTick % 3 != 0)
            return;
        var flames = ActiveMagicEffects[MagicTick].Where(s => s[1] is 0x26 or 0x52 or 0x35 or 0x24).ToArray();
        if (flames.Length == 0)
            return;
        var hero = ProjectView(Hero.X, Hero.Z, Hero.Y);
        // Keep cosmetic randomness independent of combat and sample the actual flame silhouette.
        var random = new Random(unchecked(TickCount * 397 ^ MagicTick));
        var dragonFlames = flames.Where(s => s[1] == 0x52).ToArray();
        var lowerFlameY = double.NegativeInfinity;
        if (dragonFlames.Length > 0)
        {
            var top = dragonFlames.Min(s => s[5] + m_data.Frames[s[0]].Oy);
            var bottom = dragonFlames.Max(s => s[5] + m_data.Frames[s[0]].Oy + m_data.Frames[s[0]].Height);
            lowerFlameY = top + (bottom - top) * 0.6;
        }
        foreach (var effect in flames)
        {
            // Favor the lower part of the assembled breath, not the bottom of each mouth piece.
            var lowerOnly = effect[1] == 0x52 && random.NextDouble() < 0.85;
            var frame = m_data.Frames[effect[0]];
            var point = effect.Length > 6 && effect[6] == 1
                ? (X: effect[3], Y: effect[5])
                : ProjectView(Hero.X + effect[3], Hero.Z + effect[4], Hero.Y + effect[5]);
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var pixelX = random.Next(frame.Width);
                var pixelY = random.Next(frame.Height);
                var pen = m_data.SpritePens[(frame.Y + pixelY) * m_data.SpriteStride + frame.X + pixelX];
                if (pen is 0 or 15)
                    continue;
                var x = point.X + (effect[2] == 1 ? -frame.Ox - 1 - pixelX : frame.Ox + pixelX);
                var y = point.Y + frame.Oy + pixelY;
                if (lowerOnly && y < lowerFlameY)
                    continue;
                if (x is > -16 and < 336 && y is > -24 and < 224)
                {
                    // Snapshot the emission position: existing puffs never follow the moving spell.
                    var scale = 1.3 + random.NextDouble() * 0.7;
                    var soot = random.NextDouble() < 0.5 ? 0.45 : 0.95;
                    m_dragonSmoke.Add(new SmokePuff(Hero.X + x - hero.X, Hero.Z,
                        Hero.Y + y - hero.Y, TickCount, scale, soot, 0.8, 240));
                }
                break;
            }
        }
    }

    private static void AdvanceMountedJumpPose(Fighter mount, double ground)
    {
        // The two long holds in the mount script are the rising and descending poses.
        if (mount.Animation.Index < 2)
            mount.Animation.Tick();
        else if (mount.Vy >= 0)
        {
            var pose = ground - mount.Y <= 8 ? 4 : 3;
            if (mount.Animation.Index != pose)
                mount.Animation.Seek(pose);
        }
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
        if (actor.Rider is { } rider)
        {
            actor.Rider = null;
            rider.Mount = null;
            rider.Health = 0;
            actor.SetState(FighterState.Gone);
            Die(rider);
        }
        else
            Die(actor);
        return true;
    }

    private void Move(Fighter actor, double dx, double dz, bool followOverPit = false)
    {
        var airborne = actor.State is FighterState.Jumping or FighterState.Falling ||
            actor == Hero && actor.State == FighterState.Dashing;
        var canEnterPit = airborne || !IsCamp && actor.IsFree && (actor == Hero || actor.Rider == Hero || followOverPit);
        var next = Ground(actor.X + dx + Math.Sign(dx) * 8, actor.Z);
        if (next >= actor.Y - 12 && (next < 5248 || canEnterPit))
            actor.X += dx;
        next = Ground(actor.X, actor.Z + dz);
        if (next >= actor.Y - 12 && (next < 5248 || canEnterPit) && (dz <= 0 || airborne && next >= 5248 || CanMoveDown(actor, actor.Z + dz, next)))
            actor.Z += dz;
    }

    private bool CanMoveDown(Fighter actor, double z, double ground)
    {
        if (actor != Hero && actor.Rider != Hero)
            return true;
        var lowestCamera = IsCamp ? CameraY : CameraRange().Low;
        if (Project(actor.X, z, ground).Y + lowestCamera - CameraY <= BottomFeetLimit)
            return true;
        if (IsCamp)
            return false;

        // The opening forest has a wide front lane. Elsewhere, the HUD must not fence off a real drop.
        if (AreaIndex == 0 && Progress <= m_data.Stops[0] || ground >= 5248)
            return true;
        for (var depth = 8; depth <= 64; depth += 8)
        {
            var ahead = Ground(actor.X, z + depth);
            if (ahead <= 3840 || ahead < ground - 12)
                break;
            if (ahead >= 5248)
                return true;
        }
        return false;
    }

    private void ResolveAttack(Fighter actor)
    {
        var mountedCharge = actor.IsMount && actor.State == FighterState.Dashing;
        // No separate beast-charge script is mapped: use a body impact while its run poses play.
        var attack = mountedCharge ? new[] { 5, 4, 0, 8, 44, -42, 0 } : m_data.Attacks.GetValueOrDefault(actor.Animation.Address);
        if (attack == null || !mountedCharge && actor.Animation.Index != attack[2])
            return;
        var step = actor.Animation.Current;
        var friendly = actor == Hero || actor.Rider == Hero || actor.Kind == ActorKind.Fireball && actor.Owner == Hero;
        foreach (var target in Actors.ToArray())
        {
            // A chicken's tail swipe can catch a thief slightly across the lane.
            var depthReach = actor.Kind == ActorKind.Chicken && actor.Rider == Hero && target.Kind == ActorKind.Thief ? 14 : 7;
            if (target == actor || !target.Vulnerable || actor.Struck.Contains(target.Id) || Math.Abs(target.Z - actor.Z) >= depthReach)
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
            if (!AttackOverlapsBody(actor, step, attack, target, body))
                continue;
            actor.Struck.Add(target.Id);
            actor.Connected = true;
            Hit(actor, target, attack[1], attack[0] >= 5);
        }
    }

    private static bool AttackOverlapsBody(Fighter actor, AnimationStep step, int[] attack, Fighter target, AnimationStep body)
    {
        var origin = actor.X + step.Dx * actor.Facing;
        var left = origin + (actor.Facing > 0 ? attack[3] : -attack[4]);
        var right = origin + (actor.Facing > 0 ? attack[4] : -attack[3]);
        var bx = target.X + body.Dx * target.Facing;
        var bl = bx + (target.Facing > 0 ? body.Body[0] : -body.Body[1]);
        var br = bx + (target.Facing > 0 ? body.Body[1] : -body.Body[0]);
        return br >= left && bl <= right && target.Y + body.Dy + body.Body[3] >= actor.Y + step.Dy + attack[5] &&
            target.Y + body.Dy + body.Body[2] <= actor.Y + step.Dy + attack[6];
    }

    private void Hit(Fighter source, Fighter target, int damage, bool knock)
    {
        if (target.Mount != null)
            target = target.Mount;
        if (target.Rider != null)
        {
            var rider = target.Rider;
            target.Rider = null;
            target.SpawnParameter = 0;
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
            target.SetState(FighterState.Hurt, target.Items == 0 ? Moves.ThiefEmptyHurt : target.Moves.Hurt);
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
        {
            var followUp = target.Moves.FollowUpHurt;
            target.SetState(FighterState.Hurt, followUp != 0 && target.Hits == 2 ? followUp : target.Moves.Hurt);
            // The long hold at step zero plays after the arcade recoil poses.
            if (followUp != 0)
                target.Animation.Seek(1);
        }
    }

    private void LandKnockdown(Fighter actor)
    {
        if (actor.IsEnemy || actor.Kind == ActorKind.Hero)
        {
            if (actor.IsEnemy && actor.Health > 0)
                actor.KnockdownVariation = m_random.Next(-EnemyKnockdownVariation, EnemyKnockdownVariation + 1);
            actor.SetState(FighterState.Down, actor.Moves.GetUp);
            // Recovery scripts hold their grounded back pose in the second frame.
            actor.Animation.Seek(1);
        }
        else
            actor.SetState(FighterState.Down);

        AddLandingDust(actor, heavy: true);
    }

    private void AddDragonSmoke(Fighter actor)
    {
        if (actor.Age % 4 != 0 || actor.State is FighterState.Dying or FighterState.Gone)
            return;
        var step = actor.Animation.Current;
        var frame = m_data.Frames[step.Frame];
        var direction = ((step.Flags & 1) != 0) ^ actor.Facing < 0 ? -1 : 1;
        m_dragonSmoke.Add(new SmokePuff(
            actor.X + step.Dx * actor.Facing + (frame.Ox + frame.Width * 0.5) * direction,
            actor.Z, actor.Y + step.Dy + frame.Oy + frame.Height * 0.35, TickCount));
    }

    private void AddLandingDust(Fighter actor, bool heavy = false)
    {
        if (actor.Y is <= 3840 or >= 5248)
            return;

        var pose = actor.Animation.Current;
        var sprite = m_data.Frames[pose.Frame];
        m_landingDust.Add(new DustPuff(
            actor.X + (heavy ? pose.Dx * actor.Facing : 0),
            actor.Z,
            actor.Y + (heavy ? pose.Dy : 0) + (actor.State == FighterState.Down ? m_data.LandingContactY(sprite) : 0),
            heavy ? sprite.Width * 0.34 : 5,
            TickCount,
            heavy));
    }

    private void Die(Fighter actor)
    {
        if (actor == Hero)
            actor.SetState(FighterState.Respawning);
        else
        {
            Kills++;
            if (actor.IsEnemy)
            {
                actor.Flash = actor.Invulnerable = 0;
                if (actor.Kind == ActorKind.DeathAdder && AreaIndex == ArcadeData.AreaCount - 1)
                    actor.SetState(FighterState.StruckByOwnAxe, DeathAdderDefeat.FallenPose);
                else
                    Petrify(actor);
            }
            else
                actor.SetState(FighterState.Dying);
        }
    }

    private void Petrify(Fighter actor)
    {
        actor.PetrifiedTick = TickCount;
        actor.SetState(FighterState.Petrified);
        m_stoneBodies.Add(actor);
    }

    private void MoveVillager(Fighter actor)
    {
        // The injured man's animation carries travel offsets; collide at his displayed position.
        var poseX = actor.Animation.Address == 0xa170 ? actor.Animation.Current.Dx * actor.Facing : 0;
        actor.X += poseX;
        var screenX = Project(actor.X, actor.Z, actor.Y).X;
        if (actor.Facing < 0 && screenX < 8 || actor.Facing > 0 && screenX > 312)
            actor.X += actor.Facing * actor.VillagerSpeed;
        else
        {
            var ground = Ground(actor.X, actor.Z);
            var ahead = Ground(actor.X + actor.Facing * 12, actor.Z);
            bool Walkable(double floor) => floor is > 3840 and < 5248 && Math.Abs(floor - ground) <= 12;
            var desiredDepth = actor.VillagerTargetZ ?? actor.Z;
            var clearAhead = Walkable(ahead);
            var route = clearAhead
                ? new GameInput(X: actor.Facing, Z: Math.Sign(desiredDepth - actor.Z))
                : TerrainNavigator.Towards(this, actor, Progress + (actor.Facing < 0 ? -64 : 384), desiredDepth, allowExit: true);
            var dx = route.X * actor.VillagerSpeed;
            var dz = route.Z * (clearAhead ? Math.Min(1, Math.Abs(desiredDepth - actor.Z)) : 1.2);
            if (Walkable(Ground(actor.X + dx + Math.Sign(dx) * 8, actor.Z)))
                actor.X += dx;
            if (Walkable(Ground(actor.X, actor.Z + dz)))
                actor.Z += dz;
            var floor = Ground(actor.X, actor.Z);
            if (Walkable(floor))
                actor.Y = floor;
        }
        actor.X -= poseX;
    }

    private void MoveThief(Fighter actor, bool fleeingCamp = false)
    {
        var screenX = Project(actor.X, actor.Z, actor.Y).X;
        var running = actor.Items == 0 || fleeingCamp;
        if (running && (actor.Facing < 0 && screenX < 24 || actor.Facing > 0 && screenX > 296))
        {
            // A fleeing thief must be allowed beyond the captured collision map to leave.
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
        Move(actor, actor.Facing * (running ? 3.0 : 1.0), 0);
        actor.Y += actor.Vy;
        actor.Vy += 0.25;
        ground = Ground(actor.X, actor.Z);
        if (actor.Y >= ground && actor.Vy >= 0)
        {
            actor.Y = ground;
            actor.Vy = 0;
        }
    }

    private void MoveCampThief(Fighter actor)
    {
        // Provisional stop/start chase rhythm; exact arcade timings await MAME comparison.
        if (actor.ThiefPauseTicks > 0)
        {
            actor.ThiefPauseTicks--;
            return;
        }
        if (actor.ThiefRunTicks == 0)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var x = Progress + m_random.Next(40, 281);
                var z = FindFloor(x, Hero.Z + m_random.Next(-28, 29));
                if (Math.Abs(x - actor.X) + Math.Abs(z - actor.Z) < 32 ||
                    Enumerable.Range(1, 24).Any(step =>
                        Math.Abs(Ground(actor.X + (x - actor.X) * step / 24,
                            actor.Z + (z - actor.Z) * step / 24) - actor.Y) > 8))
                    continue;
                actor.ThiefTargetX = x;
                actor.ThiefTargetZ = z;
                actor.ThiefRunTicks = m_random.Next(30, 61);
                actor.SetState(FighterState.Walking, actor.Moves.Run);
                break;
            }
            if (actor.ThiefRunTicks == 0)
            {
                actor.ThiefPauseTicks = 20;
                actor.SetState(FighterState.Idle, actor.Moves.Idle);
                return;
            }
        }
        var dx = actor.ThiefTargetX - actor.X;
        var dz = actor.ThiefTargetZ - actor.Z;
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var speed = Math.Min(1.8, distance);
        if (dx != 0)
            actor.Facing = Math.Sign(dx);
        if (distance > 0)
            Move(actor, dx / distance * speed, dz / distance * speed);
        actor.Y = Ground(actor.X, actor.Z);
        actor.Vy = 0;
        if (--actor.ThiefRunTicks == 0 || distance <= speed)
        {
            actor.ThiefRunTicks = 0;
            actor.ThiefPauseTicks = m_random.Next(45, 81);
            actor.SetState(FighterState.Idle, actor.Moves.Idle);
        }
    }

    private void RunThiefOutOfCamp(Fighter actor)
    {
        var ticksSinceTimeout = Camp.Ticks - CampRound.TimeLimit;
        var screenX = Project(actor.X, actor.Z, actor.Y).X;
        if (actor.State != FighterState.Fleeing || ticksSinceTimeout is 0 or CampRound.ExitRoutingTicks)
        {
            actor.Facing = screenX < ArcadeData.Width / 2 ? -1 : 1;
            actor.SetState(FighterState.Fleeing, actor.Items == 0 ? Moves.ThiefEmptyRun : actor.Moves.Run);
        }

        if (ticksSinceTimeout < CampRound.ExitRoutingTicks)
            MoveThief(actor, fleeingCamp: true);
        else
            // Finish the exit even if the captured terrain blocked the normal route.
            actor.X += actor.Facing * 3;

        screenX = Project(actor.X, actor.Z, actor.Y).X;
        if (screenX < -60 || screenX > ArcadeData.Width + 60)
            actor.SetState(FighterState.Gone);
    }

    private void FadePickup(Fighter actor)
    {
        // Collected or stolen items stop participating in gameplay immediately.
        actor.PickupTick = TickCount;
        m_fadingPickups.Add(actor);
        actor.SetState(FighterState.Gone);
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
            var persistentPickup = !IsCamp || actor.Kind == ActorKind.Food;
            actor.Y += actor.Vy;
            actor.Vy += 0.25;
            var floor = Ground(actor.X, actor.Z);
            if (actor.Y >= floor)
            {
                actor.Y = floor;
                if (actor.Kind == ActorKind.Potion && !actor.PotionBounced && actor.Vy >= 1.5)
                {
                    actor.PotionBounced = true;
                    actor.Vy = -Math.Min(1.5, actor.Vy * 0.35);
                    actor.Vx *= 0.55;
                }
                else
                {
                    actor.Vy = 0;
                    actor.Vx = 0;
                }
            }
            var nextFloor = Ground(actor.X + actor.Vx, actor.Z);
            // Persistent pickups stay where they landed as the camera scrolls past.
            if ((!persistentPickup || actor.Vx != 0) && nextFloor > 3840 && nextFloor < 5248)
                actor.X = Math.Clamp(actor.X + actor.Vx, Progress + 16, Progress + 296);
            if (persistentPickup ? actor.X < Progress - 16 : actor.Age > 600)
            {
                actor.SetState(FighterState.Gone);
                return;
            }
            if (Hero.Health > 0 && Hero.State != FighterState.Sleeping && Math.Abs(actor.Y - Hero.Y) < 8 && Math.Abs(actor.X - Hero.X) < 16 && Math.Abs(actor.Z - Hero.Z) < 10)
            {
                if (actor.Kind == ActorKind.Food)
                    Hero.Health = Math.Min(48, Hero.Health + 16);
                else
                {
                    var point = ProjectView(actor.X, actor.Z, actor.Y);
                    var slot = Potions < Character.PotionCapacity ? Potions : -1;
                    m_potionPickups.Add(new PotionPickup(actor.X, actor.Z, actor.Y, point.X, point.Y, TickCount, slot));
                    Potions = Math.Min(Character.PotionCapacity, Potions + 1);
                }
                FadePickup(actor);
            }
            return;
        }
        if (actor.Kind == ActorKind.Villager && actor.Animation.Address == 0xa1ae)
        {
            // This injured villager remains in the scenery; the falling pose must not loop into a run.
            if (actor.Animation.Index != 0)
                actor.Animation.Tick();
            return;
        }
        if (actor.Kind == ActorKind.Villager && actor.Animation.Address is 0xa342 or 0xa35c)
        {
            actor.Animation.Tick();
            if (actor.Owner?.State == FighterState.Stamping)
                return;
            if (actor.Owner == null && actor.Animation.Address == 0xa35c && Math.Abs(Hero.X - actor.X) >= 128)
                return;
            actor.Owner = null;
            actor.Animation.Play(actor.Animation.Address == 0xa342 ? 0xa0ac : 0xa10e);
            actor.Facing = -1;
        }
        var previousPoseX = actor.Animation.Current.Dx;
        actor.Animation.Tick();
        // The injured villager's script contains cumulative travel (0..36), not ordinary pose bobbing.
        // Our prop movement already supplies travel, so cancel frame changes, including the loop reset.
        if (actor.Kind == ActorKind.Villager && actor.Animation.Address == 0xa170)
            actor.X += (previousPoseX - actor.Animation.Current.Dx) * actor.Facing;
        if (actor.Kind == ActorKind.Thief && Camp?.TimeExpired == true)
        {
            RunThiefOutOfCamp(actor);
            return;
        }
        if (actor.State == FighterState.Hurt)
        {
            if (actor.StateTicks > 24)
                actor.SetState(FighterState.Walking, actor.Kind == ActorKind.Thief && actor.Items == 0 ? Moves.ThiefEmptyRun : actor.Moves.Walk);
            return;
        }
        if (actor.Kind == ActorKind.Thief)
        {
            if (actor.Items == 0 && actor.State != FighterState.Fleeing)
            {
                actor.Facing = Project(actor.X, actor.Z, actor.Y).X < 160 ? -1 : 1;
                actor.SetState(FighterState.Fleeing, Moves.ThiefEmptyRun);
            }
            var jar = IsCamp && !actor.FoodThief && actor.Items > 0
                ? Actors.FirstOrDefault(a => a.Kind == ActorKind.CampPotion && !a.IsGone) : null;
            if (IsCamp && !actor.Entered && actor.Items > 0)
            {
                actor.X += actor.Facing;
                var entryX = Math.Clamp(actor.X, Progress + 24, Progress + 296);
                actor.Y = Ground(entryX, actor.Z);
                actor.Vy = 0;
                if (Project(actor.X, actor.Z, actor.Y).X is >= 24 and <= 296)
                    actor.Entered = true;
                return;
            }
            if (jar != null)
            {
                var dx = jar.X - actor.X;
                var dz = jar.Z - actor.Z;
                actor.Facing = dx < 0 ? -1 : 1;
                Move(actor, Math.Sign(dx) * Math.Min(1.5, Math.Abs(dx)), Math.Sign(dz) * Math.Min(1, Math.Abs(dz)));
                if (Math.Abs(dx) < 4 && Math.Abs(dz) < 4)
                {
                    FadePickup(jar);
                    Potions = Math.Max(0, Potions - 1);
                    actor.Items++;
                }
            }
            else if (IsCamp && actor.Items > 0)
                MoveCampThief(actor);
            else
                MoveThief(actor);
        }
        else
        {
            var previousX = actor.X;
            MoveVillager(actor);
            // Stagger lighter footfalls so a fleeing crowd does not emit one synchronized cloud.
            if ((TickCount + actor.Id * 5) % 18 == 0 && Math.Abs(actor.X - previousX) >= 1 &&
                actor.Animation.Address is 0x9fe8 or 0xa04a or 0xa0ac or 0xa10e or 0xa228 &&
                Math.Abs(actor.Y - Ground(actor.X, actor.Z)) < 1 &&
                ProjectView(actor.X, actor.Z, actor.Y) is { X: > 16 and < 304, Y: > 0 and < 224 })
                m_landingDust.Add(new DustPuff(actor.X - actor.Facing * 5, actor.Z, actor.Y, 2, TickCount, false, true));
        }
        var sx = Project(actor.X, actor.Z, actor.Y).X;
        if (sx is > 0 and < 320)
            actor.Entered = true;
        if (actor.Kind == ActorKind.Thief && actor.Items > 0 && actor.Age < 700 && (sx < 20 || sx > 300) && actor.Entered)
            actor.Facing = sx < 20 ? 1 : -1;
        if ((actor.Entered || actor.Kind == ActorKind.Villager && actor.Animation.Address == 0xa228) && (sx < -60 || sx > 380))
            actor.SetState(FighterState.Gone);
    }
}
