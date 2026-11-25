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
using System.IO;
using SkiaSharp;
using DTC.Core;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests;

/// <summary>
/// Checks reported gameplay faults against the real arcade terrain and artwork.
/// </summary>
/// <remarks>
/// Includes death and palette reuse, which a single successful autonomous run cannot exercise.
/// </remarks>
[TestFixture]
public class GoldenAxeGameplayTests
{
    private ArcadeData[] m_areas;

    [OneTimeSetUp]
    public void LoadAreas() => m_areas = Enumerable.Range(0, ArcadeData.AreaCount).Select(i => new ArcadeData(ArcadeData.AssetDirectory, i)).ToArray();

    [TestCase(168)]
    [TestCase(176)]
    [TestCase(192)]
    public void EagleHouseMeetsThePavementWithoutABlueSeam(int scrollY)
    {
        var data = m_areas[3];
        var game = new Game(data);
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, -207.0);
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, -512.0 - scrollY);
        game.ViewCamera.Reset(game.CameraX, game.CameraY);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game, effects: false);
        // The house ends at world Y=384. The original pavement tile must start on that same row.
        const int worldX = 416;
        const int worldY = 384;
        var layer = data.Background;
        var cell = layer.Cells[(worldY - layer.Y) / 8 * layer.Width + (worldX - layer.X) / 8];
        var tile = (int)(cell & 65535);
        var pen = data.TilePens[(tile / 128 * 8) * data.TileStride + tile % 128 * 8];
        var pavement = data.TilePalettes[data.DayPalette][(cell >> 16 & 127) * 8 + pen];
        Assert.That(screen.Pixels[(worldY - scrollY) * screen.Width + worldX - 200], Is.EqualTo(pavement));
    }

    [Test]
    public void LastReserveLifeDisplaysAHollowZero()
    {
        var data = m_areas[0];
        var game = new Game(data, character: 2);
        typeof(Game).GetProperty(nameof(Game.Lives))!.SetValue(game, 0);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        var topInk = Enumerable.Range(0, 64).First(i =>
            data.TilePens[(0x60 / 128 * 8 + i / 8) * data.TileStride + 0x60 % 128 * 8 + i % 8] != 0);
        var yellow = screen.Pixels[(200 + topInk / 8) * screen.Width + 128 + topInk % 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var pen = data.TilePens[(0x69 / 128 * 8 + y) * data.TileStride + 0x69 % 128 * 8 + x];
            Assert.That(screen.Pixels[(208 + y) * screen.Width + 128 + x] == yellow,
                Is.EqualTo(pen != 0), $"Zero lower half ({x}, {y})");
        }
        for (var y = 203; y < 214; y++)
            Assert.That(screen.Pixels[y * screen.Width + 131], Is.Not.EqualTo(yellow),
                "Zero must have an uninterrupted hollow center, without the crossbar from six or eight.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void AutoplaySavesMagicUntilEnemiesHaveEnteredTheVisibleBattle(int character)
    {
        var game = new Game(m_areas[0], character);
        game.Actors.RemoveAll(a => a != game.Hero);
        typeof(Game).GetField("m_spawnIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(game, m_areas[0].Spawns.Length);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, character == 1 ? 5 : 3);
        var enemies = Enumerable.Range(0, 3).Select(i =>
            game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Progress + 400 + i * 20, game.Hero.Z)).ToArray();
        var controller = new CodedController();
        Assert.That(controller.Read(game).Magic, Is.False, "Off-screen enemies must not trigger a cast.");
        foreach (var enemy in enemies)
        {
            enemy.X = game.Hero.X + 90;
            enemy.SetState(FighterState.Entering);
        }
        Assert.That(controller.Read(game).Magic, Is.False, "Entering enemies cannot yet be hit.");
        foreach (var enemy in enemies)
            enemy.SetState(FighterState.Idle);
        if (character == 1)
        {
            typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, 3);
            Assert.That(controller.Read(game).Magic, Is.False, "Tyris should save for her second spell level.");
            typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, 5);
        }
        Assert.That(controller.Read(game).Magic, Is.True, "A visible, hittable group warrants magic.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AutoplayWaitsForTheRestOfTheWaveButNotForever(bool entering)
    {
        var game = new Game(m_areas[0], 2);
        game.Actors.RemoveAll(a => a != game.Hero);
        typeof(Game).GetField("m_spawnIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(game, m_areas[0].Spawns.Length);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, 2);
        for (var i = 0; i < 2; i++)
            game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Hero.X + 90 + i * 10, game.Hero.Z);
        var late = game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16,
            entering ? game.Hero.X + 110 : game.Progress + 400, game.Hero.Z);
        if (entering)
            late.SetState(FighterState.Entering);
        var controller = new CodedController();
        Assert.That(controller.Read(game).Magic, Is.False, "Wait for the third enemy to become a valid target.");
        typeof(Game).GetProperty(nameof(Game.TickCount))!.SetValue(game, 119);
        Assert.That(controller.Read(game).Magic, Is.False);
        typeof(Game).GetProperty(nameof(Game.TickCount))!.SetValue(game, 120);
        Assert.That(controller.Read(game).Magic, Is.True, "A stuck straggler must not postpone magic indefinitely.");
        // A fresh decision casts immediately when the whole group has assembled.
        late.X = game.Hero.X + 110;
        late.SetState(FighterState.Idle);
        Assert.That(new CodedController().Read(game).Magic, Is.True);
    }

    [Test]
    public void AutoplayAnticipatesDelayedDoorEnemiesButCastsToSaveLowHealth()
    {
        var game = new Game(m_areas[2], 2);
        game.Actors.RemoveAll(a => a != game.Hero);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, 2);
        var first = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Hero.X + 40, game.Hero.Z);
        game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, game.Hero.X + 90, game.Hero.Z);
        game.Spawn(m_areas[2].Spawns.First(s => s.Routine == 0x282ec));
        Assert.That(game.HasUpcomingEnemies, Is.True);
        var controller = new CodedController();
        Assert.That(controller.Read(game).Magic, Is.False, "Do not spend the spell before the door wave arrives.");
        game.Hero.Health = 8;
        first.SetState(FighterState.Attacking, first.Moves.Attacks[0], true);
        first.Animation.Seek(m_areas[2].Attacks[first.Animation.Address][2]);
        Assert.That(game.CanHitWithMagic(first), Is.True);
        Assert.That(controller.Read(game).Magic, Is.True, "Low-health survival takes priority over waiting.");
    }

    [TestCase(7, 660, 170)]
    [TestCase(8, 518, 292)]
    [TestCase(9, 680, 366)]
    public void TyrisHigherPotionCountsSelectTheirDistinctSpell(int potions, int frame, int duration)
    {
        var game = new Game(m_areas[0], 1);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, potions);
        game.Tick(new GameInput(Magic: true));
        Assert.That(game.MagicLevel, Is.EqualTo(potions - 3));
        Assert.That(game.MagicDuration, Is.EqualTo(duration));
        Assert.That(game.ActiveMagicEffects[0].Count(s => s[0] == 517), Is.EqualTo(potions));
        Assert.That(game.ActiveMagicEffects.Take(48).SelectMany(s => s).All(s => s[1] == 0x4a), Is.True);
        Assert.That(game.ActiveMagicEffects[48].Any(s => s[0] == frame), Is.True);
        if (potions == 8)
        {
            var core = game.ActiveMagicEffects[100].Where(s => s[0] == 518).ToArray();
            Assert.That(core, Has.Length.EqualTo(2), "The molten core needs both halves, not a rectangular cut-off.");
            Assert.That(core.Select(s => s[2]), Is.EquivalentTo(new[] { 0, 1 }));
        }
    }

    [TestCase(-1, false)]
    [TestCase(1, false)]
    [TestCase(-1, true)]
    [TestCase(1, true)]
    public void AutoplayMakesRoomForOffscreenArrivals(int direction, bool mounted)
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        hero.X += (direction > 0 ? 300 : 20) - game.ProjectView(hero.X, hero.Z, hero.Y).X;
        hero.Y = game.Ground(hero.X, hero.Z);
        var controlled = hero;
        if (mounted)
        {
            controlled = game.Add(ActorKind.Chicken, game.Character.MountedMoves, 0x23, 99, hero.X, hero.Z);
            controlled.Rider = hero;
            hero.Mount = controlled;
        }
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 24, controlled.X + direction * 70, hero.Z);
        var controller = new CodedController();
        var retreat = controller.Read(game);
        Assert.That(retreat.X, Is.EqualTo(-direction));
        Assert.That(retreat.Attack || retreat.Run, Is.False);
        controlled.X -= direction * 32;
        hero.X = controlled.X;
        Assert.That(controller.Read(game), Is.EqualTo(default(GameInput)), "Wait inside the shot instead of walking back to the edge.");
        enemy.X = controlled.X + direction * 36;
        Assert.That(controller.Read(game).Attack, Is.True, "Resume combat once the opponent enters the shot.");
    }

    [TestCase(20, -1, false)]
    [TestCase(300, 1, false)]
    [TestCase(160, -1, true)]
    [TestCase(160, 1, true)]
    public void AutoplayOnlyBargesWhenTheChargeStaysOnScreen(int screenX, int direction, bool expectedRun)
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        hero.X += screenX - game.ProjectView(hero.X, hero.Z, hero.Y).X;
        hero.Y = game.Ground(hero.X, hero.Z);
        hero.Facing = direction;
        hero.Vx = direction * 3;
        game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 24, hero.X + direction * 36, hero.Z);
        var input = new CodedController().Read(game);
        Assert.That(input.Attack, Is.EqualTo(expectedRun), "Bring edge opponents into the shot before attacking.");
        if (!expectedRun)
            Assert.That(input.X, Is.EqualTo(-direction), "Walk inward instead of rolling into the boundary.");
        Assert.That(input.Attack && input.Run, Is.EqualTo(expectedRun), "Only commit to a rolling attack with room to finish it on screen.");
    }

    [Test]
    public void DragonFireLeavesGroundHeatAfterTheProjectileIsGone()
    {
        var game = new Game(m_areas[0]);
        var fireball = game.Add(ActorKind.Fireball, Moves.BlueDragon, 0x45, 1, game.Hero.X + 30, game.Hero.Z);
        fireball.Y -= 28;
        fireball.Animation.Play(0x32b00);
        for (var tick = 0; tick < 20; tick++)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows, Is.Not.Empty);
        var patch = game.MagicGroundGlows.Last();
        Assert.That(patch.Y, Is.EqualTo(game.Ground(patch.X, patch.Z)));
        fireball.SetState(FighterState.Gone);
        for (var tick = 0; tick < 60; tick++)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows, Does.Contain(patch), "The heat remains anchored after the projectile disappears.");
        for (var tick = 0; tick < 60; tick++)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows, Is.Empty);
    }

    [Test]
    public void InterruptedDragonBreathFadesAtItsLastPosition()
    {
        var game = new Game(m_areas[0]);
        var beast = game.Add(ActorKind.BlueDragon, Moves.BlueDragon, 0x23, 99, game.Hero.X + 60, game.Hero.Z);
        var flame = game.Add(ActorKind.Flame, Moves.BlueDragon, 0x45, 1, beast.X, beast.Z);
        flame.Owner = beast;
        flame.Animation.Play(0x2f422);
        var pose = flame.Animation.Current;
        var x = flame.X;
        game.Tick(default);
        Assert.That(flame.State, Is.EqualTo(FighterState.Dying));
        beast.X += 40;
        for (var tick = 0; tick < Game.FlameFadeTicks / 2; tick++)
            game.Tick(default);
        Assert.That(flame.X, Is.EqualTo(x));
        Assert.That(flame.Animation.Current, Is.EqualTo(pose));
        Assert.That(ArcadeRenderer.ActorFade(game, flame), Is.EqualTo(0.5));
        for (var tick = 0; tick < Game.FlameFadeTicks / 2; tick++)
            game.Tick(default);
        Assert.That(game.Actors, Does.Not.Contain(flame));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CampKeepsARespawnedHeroVisibleWhenTheClockRestarts(int character)
    {
        var game = new Game(m_areas[2], character);
        AdvanceTo(game, m_areas[2].Stops[^1]);
        for (var tick = 0; tick < 40; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        Assert.That(game.Complete, Is.True);
        // A death earlier in a long stage leaves a timestamp beyond the entire camp round.
        game.Hero.RespawnTick = 2000;
        game.StartCamp();
        Assert.That(game.IsCamp, Is.True);
        Assert.That(game.Hero.State, Is.EqualTo(FighterState.Sleeping));
        Assert.That(ArcadeRenderer.ActorFade(game, game.Hero), Is.EqualTo(1));
        for (var tick = 0; tick < 500; tick++)
        {
            game.Tick(default);
            Assert.That(ArcadeRenderer.ActorFade(game, game.Hero), Is.EqualTo(1),
                "The sleeper and waking hero must stay visible on the restarted camp clock.");
        }
        Assert.That(game.Hero.State, Is.Not.EqualTo(FighterState.Sleeping));
    }

    [Test]
    public void HeroFadesOutThenBackInAtRespawn()
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        hero.Health = 0;
        hero.SetState(FighterState.Respawning);
        Assert.That(ArcadeRenderer.ActorFade(game, hero), Is.EqualTo(1));
        for (var tick = 0; tick < Game.HeroFadeTicks / 2; tick++)
            game.Tick(default);
        Assert.That(ArcadeRenderer.ActorFade(game, hero), Is.EqualTo(0.5));
        while (hero.State == FighterState.Respawning)
            game.Tick(default);
        Assert.That(ArcadeRenderer.ActorFade(game, hero), Is.Zero);
        for (var tick = 0; tick < Game.HeroFadeTicks; tick++)
            game.Tick(default);
        Assert.That(ArcadeRenderer.ActorFade(game, hero), Is.EqualTo(1));
        Assert.That(hero.Health, Is.EqualTo(48));
    }

    [TestCase(0, 4)]
    [TestCase(1, 7)]
    [TestCase(1, 9)]
    [TestCase(2, 3)]
    public void MagicGroundGlowStaysOnTheFloorAndFadesAfterMagic(int character, int potions)
    {
        var game = new Game(m_areas[0], character);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, potions);
        game.Tick(new GameInput(Magic: true));
        while (game.MagicTick < 120)
            game.Tick(default);
        var patch = game.MagicGroundGlows.First();
        Assert.That(patch.Y, Is.EqualTo(game.Ground(patch.X, patch.Z)));
        for (var tick = 0; tick < 12; tick++)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows.Any(g => g.X == patch.X && g.Z == patch.Z && g.Y == patch.Y), Is.True);
        while (game.MagicTick >= 0)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows, Is.Not.Empty);
        for (var tick = 0; tick < MagicGroundGlow.FadeTicks; tick++)
            game.Tick(default);
        Assert.That(game.MagicGroundGlows, Is.Empty);
    }

    [Test]
    public void TyrisDragonBreathPrecedesDamageAndLeavesFadingSmoke()
    {
        var game = new Game(m_areas[0], 1);
        game.Actors.RemoveAll(a => a != game.Hero);
        typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, 9);
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 64, game.Hero.X + 90, game.Hero.Z);
        enemy.Cooldown = 10000;
        game.Tick(new GameInput(Magic: true));
        while (game.MagicTick < 105)
        {
            game.Tick(default);
            Assert.That(enemy.Health, Is.EqualTo(64), "Potion fan and dragon entrance must not deal premature damage.");
            Assert.That(game.DragonSmoke, Is.Empty, "The silver head is not a smoke source.");
        }
        game.Tick(default);
        Assert.That(game.ActiveMagicEffects[game.MagicTick].Any(s => s[0] == 681), Is.True);
        Assert.That(enemy.Health, Is.EqualTo(16));
        while (game.MagicTick >= 0)
            game.Tick(default);
        Assert.That(game.DragonSmoke, Is.Not.Empty);
        for (var tick = 0; tick < 121; tick++)
            game.Tick(default);
        Assert.That(game.DragonSmoke, Is.Not.Empty, "The plume should linger after the flame has gone.");
        for (var tick = 0; tick < 120; tick++)
            game.Tick(default);
        Assert.That(game.DragonSmoke, Is.Empty);
    }

    [TestCase(0, 0x1a0)]
    [TestCase(1, 0x1a4)]
    [TestCase(2, 0x1a8)]
    public void HudUsesTheArcadePortraitTilesAndLetsSceneryShowThrough(int character, int firstTile)
    {
        var data = m_areas[0];
        var game = new Game(data, character);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        var colors = data.PortraitPalettes[firstTile];
        var sceneryVisible = false;
        for (var y = 0; y < 16; y++)
        for (var x = 0; x < 16; x++)
        {
            var tile = firstTile + y / 8 * 2 + x / 8;
            var pen = data.TilePens[(tile / 128 * 8 + y % 8) * data.TileStride + tile % 128 * 8 + x % 8];
            var pixel = screen.Pixels[(200 + y) * screen.Width + 136 + x];
            if (pen == 0)
                sceneryVisible |= !colors.Contains(pixel);
            else
                Assert.That(pixel, Is.EqualTo(colors[pen]), $"Portrait pixel ({x}, {y})");
        }
        Assert.That(sceneryVisible, Is.True);
    }

    [Test]
    public void CastingClearsPickupSparklesAndCagesFollowPoseOffsets()
    {
        var data = m_areas[0];
        var game = new Game(data, character: 2);
        game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
        game.Tick(default);
        Assert.That(game.PotionPickups, Is.Not.Empty);
        game.Tick(new GameInput(Magic: true));
        Assert.That(game.PotionPickups, Is.Empty);

        var target = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Hero.X + 90, game.Hero.Z);
        target.Animation.HoldFrame(115);
        var renderer = new ArcadeRenderer(data);
        foreach (var facing in new[] { -1, 1 })
        {
            target.Facing = facing;
            target.Animation.Steps[0] = target.Animation.Current with { Dx = 0, Dy = 0 };
            var start = renderer.LightningAnchor(game, target);
            target.Animation.Steps[0] = target.Animation.Current with { Dx = 18, Dy = -7 };
            var shifted = renderer.LightningAnchor(game, target);
            Assert.That(shifted.X - start.X, Is.EqualTo(18 * facing));
            Assert.That(shifted.Y - start.Y, Is.EqualTo(-7));
        }
    }

    [Test]
    public void DwarfLightningKeepsVisibleVictimsUntilSpellEnds()
    {
        var game = new Game(m_areas[0], character: 2);
        game.Actors.RemoveAll(a => a != game.Hero);
        var victim = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 1, game.Hero.X + 90, game.Hero.Z);
        var offscreen = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Progress + 500, game.Hero.Z);
        game.Tick(new GameInput(Magic: true));
        Assert.That(game.MagicTargets, Does.Contain(victim));
        Assert.That(game.MagicTargets, Does.Not.Contain(offscreen));
        Assert.That(LightningCage.SampleTick(game), Is.EqualTo(-1));
        while (game.MagicTick < game.MagicDuration - 1)
            game.Tick(default);
        Assert.That(game.MagicTargets, Does.Contain(victim), "Lethal spell damage must not remove the visual target early.");
        Assert.That(victim.Health, Is.Zero);
        Assert.That(LightningCage.SampleTick(game), Is.EqualTo(game.MagicEffects[0].Length - 1));
        game.Tick(default);
        Assert.That(game.MagicTargets, Is.Empty);
        Assert.That(LightningCage.SampleTick(game), Is.EqualTo(-1));
    }

    [Test]
    public void CivilianSpawnParametersGiveEachVillagerTheirMeasuredSpeed()
    {
        var game = new Game(m_areas[0]);
        var opening = m_areas[1].Spawns.Where(s => s.Trigger == 4120).ToArray();
        // Measured base-X travel from ticks 1048 to 1080 of the MAME area1 trace.
        var travel = new[] { 80, 64, 48, 62, 60, 56 };
        Assert.That(opening, Has.Length.EqualTo(travel.Length));
        foreach (var spawn in opening)
            game.Spawn(spawn with { X = (int)game.Hero.X + 80, Z = (int)game.Hero.Z });
        var crowd = game.Actors.Where(a => a.Kind == ActorKind.Villager).ToArray();
        var startX = crowd.Select(a => a.X).ToArray();
        for (var tick = 0; tick < 32; tick++)
            game.Tick(default);
        for (var i = 0; i < crowd.Length; i++)
            Assert.That(startX[i] - crowd[i].X, Is.EqualTo(travel[i]).Within(0.001));
    }

    [Test]
    public void OpeningVillageCrowdMovesDownIntoTheMeasuredLanes()
    {
        var game = new Game(m_areas[1]);
        foreach (var spawn in m_areas[1].Spawns.Where(s => s.Trigger == 4120))
            game.Spawn(spawn);
        var crowd = game.Actors.Where(a => a.Kind == ActorKind.Villager).ToArray();
        var initialZ = crowd.Select(a => a.Z).ToArray();
        for (var tick = 0; tick < 12; tick++)
            game.Tick(default);
        Assert.That(crowd.Select((a, i) => a.Z - initialZ[i]), Is.All.EqualTo(12));
        for (var tick = 12; tick < 64; tick++)
            game.Tick(default);
        Assert.That(crowd.Select(a => a.Z), Is.EqualTo(new[] { 4192, 4168, 4208, 4195, 4186, 4179 }));
    }

    [Test]
    public void OpeningInjuredVillagerFallsAndRemainsInTheScenery()
    {
        var game = new Game(m_areas[0]);
        game.Spawn(m_areas[0].Spawns.Single(s => s.Routine == 0x9c08));
        var injured = game.Actors.Single(a => a.Kind == ActorKind.Villager);
        Assert.That((injured.Animation.Current.Frame, injured.Palette, injured.X), Is.EqualTo((611, 0x42, 4365.0)));
        for (var tick = 0; tick < 120; tick++)
            game.Tick(default);
        Assert.That((injured.Animation.Current.Frame, injured.X, injured.Z), Is.EqualTo((612, 4365.0, 4248.0)));
    }

    [Test]
    public void WaitingVillageChildUsesItsOwnPosesBeforeFleeing()
    {
        var game = new Game(m_areas[1]);
        var spawn = m_areas[1].Spawns.First(s => s.Routine == 0x9a9a);
        game.Spawn(spawn);
        var child = game.Actors.Single(a => a.Kind == ActorKind.Villager);
        for (var tick = 0; tick < 80; tick++)
            game.Tick(default);
        Assert.That((child.Animation.Address, child.Palette, child.X), Is.EqualTo((0xa35c, 0x4c, (double)spawn.X)));
        game.Hero.X = child.X - 100;
        game.Tick(default);
        Assert.That(child.Animation.Address, Is.EqualTo(0xa10e));
        Assert.That(child.X, Is.LessThan(spawn.X));
    }

    [Test]
    public void TurtleVillageCrowdRoutesAroundBuildingsAndLeavesTheScreen()
    {
        var data = m_areas[1];
        var game = new Game(data);
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, 4096.0 - 7 - 4768);
        game.Hero.X = game.Progress + 70;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero);
        foreach (var spawn in data.Spawns.Where(s => s.Trigger is >= 4768 and <= 4784 && s.Routine is >= 0x99c0 and <= 0x9a9a))
            game.Spawn(spawn);
        var crowd = game.Actors.Where(a => a.Kind == ActorKind.Villager && a.Animation.Address != 0xa35c).ToArray();
        var initialDepths = crowd.Select(a => a.Z).ToArray();
        Assert.That(crowd, Has.Length.EqualTo(9));
        for (var tick = 0; tick < 600; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero && a.Kind != ActorKind.Villager);
            var heights = crowd.Select(a => a.Y).ToArray();
            game.Tick(default);
            for (var i = 0; i < crowd.Length; i++)
                Assert.That(Math.Abs(crowd[i].Y - heights[i]), Is.LessThanOrEqualTo(12), "Walk around walls instead of teleporting onto their tops.");
        }
        Assert.That(crowd.Where((a, i) => Math.Abs(a.Z - initialDepths[i]) > 8), Is.Not.Empty, "The crowd must detour around the building.");
        Assert.That(crowd.All(a => a.IsGone), Is.True, "Routing must not leave the crowd stuck at the building.");
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void InjuredVillagerDoesNotSnapBackWhenHisWalkLoops(int facing)
    {
        var game = new Game(m_areas[0]);
        game.Spawn(new SpawnDefinition((int)game.Progress, (int)game.Progress + 160, (int)game.Hero.Z, 0, 0x9a5a));
        var villager = game.Actors.Single(a => a.Kind == ActorKind.Villager);
        villager.Facing = facing;
        var previous = villager.X + villager.Animation.Current.Dx * facing;
        for (var tick = 0; tick < 65; tick++)
        {
            game.Tick(default);
            var displayed = villager.X + villager.Animation.Current.Dx * facing;
            Assert.That(displayed - previous, Is.EqualTo(1.8 * facing).Within(0.001));
            previous = displayed;
        }
    }

    [Test]
    public void FullMagicMeterKeepsNumbersInFrontOfPotions()
    {
        var data = m_areas[0];
        var game = new Game(data, character: 1);
        for (var i = game.Potions; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
        game.Tick(default);
        Assert.That(game.Potions, Is.EqualTo(9));

        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        // The lower stroke of the first tier number overlaps the first potion's neck.
        var overlap = (ArcadeRenderer.MagicMeterTop + 7) * screen.Width + 108;
        renderer.Draw(screen, game);
        var withPotion = screen.Pixels[overlap];

        game.Tick(new GameInput(Magic: true));
        Assert.That(game.Potions, Is.Zero);
        renderer.Draw(screen, game);
        Assert.That(withPotion, Is.EqualTo(screen.Pixels[overlap]));
    }

    [Test]
    public void FilledMagicSlotsPulseWhileEmptySlotsAndBordersStayFixed()
    {
        var data = m_areas[0];
        var game = new Game(data, character: 2);
        var screen = new PixelScreenData(320, 224, data.Palette);
        var renderer = new ArcadeRenderer(data);
        var bright = new int[320 * 224];
        var dark = new int[320 * 224];
        for (var tick = 0; tick < 15; tick++)
            game.Tick(default);
        renderer.Draw(screen, game);
        screen.CopyToBgra(bright);
        for (var tick = 0; tick < 30; tick++)
            game.Tick(default);
        renderer.Draw(screen, game);
        screen.CopyToBgra(dark);
        var row = (ArcadeRenderer.MagicMeterTop + 9) * 320;
        Assert.That((bright[row + 108] >> 16) & 255, Is.GreaterThan((dark[row + 108] >> 16) & 255));
        Assert.That((dark[row + 108] >> 16) & 255, Is.GreaterThan((dark[row + 118] >> 16) & 255));
        Assert.That(bright[row + 118], Is.EqualTo(dark[row + 118]));
        Assert.That(bright[row + 104], Is.EqualTo(dark[row + 104]));
    }

    [Test]
    public void HudPotionUsesTheOriginalEightPixelTextTile()
    {
        var data = m_areas[0];
        var game = new Game(data);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        byte[] colors = new byte[8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            var pen = data.TilePens[y * data.TileStride + 0x1a * 8 + x];
            var pixel = screen.Pixels[(ArcadeRenderer.MagicMeterTop + 10 + y) * screen.Width + 105 + x];
            if (colors[pen] == 0)
                colors[pen] = pixel;
            Assert.That(pixel, Is.EqualTo(colors[pen]), "Preserve the original HUD bottle's pixel pattern.");
        }
        Assert.That(new[] { colors[3], colors[4], colors[5], colors[6] }.Distinct().Count(), Is.EqualTo(4));
        Assert.That(colors[3], Is.EqualTo(data.White), "The tile's bottom edge must join the meter's white border.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PotionSparklesSurviveCollectionThenExpireWithoutFoodSparkles(bool fullMeter)
    {
        var game = new Game(m_areas[0]);
        if (fullMeter)
        {
            for (var i = game.Potions; i < game.Character.PotionCapacity; i++)
                game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
            game.Tick(default);
            for (var tick = 0; tick < PotionPickup.Lifetime; tick++)
                game.Tick(default);
        }
        var count = game.Potions;
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
        game.Add(ActorKind.Food, Moves.Thief, 0x7f, 1, game.Hero.X, game.Hero.Z);
        game.Tick(default);
        Assert.That(game.Actors, Does.Not.Contain(potion));
        Assert.That(game.PotionPickups, Has.Count.EqualTo(1));
        Assert.That(game.PotionPickups[0].Slot, Is.EqualTo(fullMeter ? -1 : count));
        Assert.That(game.Potions, Is.EqualTo(Math.Min(count + 1, game.Character.PotionCapacity)));
        for (var tick = 0; tick < PotionPickup.Lifetime; tick++)
            game.Tick(default);
        Assert.That(game.PotionPickups, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CollectedItemsFadeWithoutBeingCollectedAgain(bool food)
    {
        var kind = food ? ActorKind.Food : ActorKind.Potion;
        var game = new Game(m_areas[0]);
        game.Hero.Health = 16;
        var item = game.Add(kind, Moves.Thief, kind == ActorKind.Food ? 0x7f : 0x4a, 1, game.Hero.X, game.Hero.Z);
        item.Animation.Play(kind == ActorKind.Food ? 0x18722 : 0x1870a);
        var frame = item.Animation.Current.Frame;
        game.Tick(default);
        Assert.That(game.Actors, Does.Not.Contain(item));
        Assert.That(game.FadingPickups, Does.Contain(item));
        Assert.That((game.Hero.Health, game.Potions), Is.EqualTo(kind == ActorKind.Food ? (32, 1) : (16, 2)));
        Assert.That(game.PotionPickups.Count, Is.EqualTo(kind == ActorKind.Food ? 0 : 1));
        for (var tick = 1; tick < Game.PickupFadeTicks; tick++)
            game.Tick(default);
        Assert.That(game.FadingPickups, Does.Contain(item));
        Assert.That(item.Animation.Current.Frame, Is.EqualTo(frame));
        game.Tick(default);
        Assert.That(game.FadingPickups, Is.Empty);
        Assert.That((game.Hero.Health, game.Potions), Is.EqualTo(kind == ActorKind.Food ? (32, 1) : (16, 2)));
    }

    [Test]
    public void VisualCameraSettlesAfterStoppingWithoutAdvancingTheLevel()
    {
        var game = new Game(m_areas[0]);
        for (var tick = 0; tick < 130; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(new GameInput(X: 1));
            Assert.That(game.ViewCamera.X, Is.InRange(Math.Floor(game.CameraX), Math.Ceiling(game.CameraX + 28)));
        }
        Assert.That(game.ViewCamera.X, Is.GreaterThan(game.CameraX + 2));
        var progress = game.Progress;
        var lag = Math.Abs(game.ViewCamera.X - game.CameraX);
        for (var tick = 0; tick < 90; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
            Assert.That(game.Progress, Is.EqualTo(progress));
            var nextLag = Math.Abs(game.ViewCamera.X - game.CameraX);
            Assert.That(nextLag, Is.LessThanOrEqualTo(lag));
            lag = nextLag;
        }
        Assert.That(lag, Is.LessThanOrEqualTo(0.5));
    }

    [Test]
    public void DroppedPotionBouncesOnceThenSettles()
    {
        var game = new Game(m_areas[0]);
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X + 30, game.Hero.Z);
        var floor = potion.Y;
        potion.Y -= 20;
        potion.Vy = -3;
        var bounces = 0;
        for (var tick = 0; tick < 90; tick++)
        {
            var wasDescending = potion.Vy > 0;
            game.Tick(default);
            if (wasDescending && potion.Vy < 0)
            {
                bounces++;
                Assert.That(potion.Y, Is.EqualTo(floor));
            }
        }

        Assert.That(bounces, Is.EqualTo(1));
        Assert.That(potion.Y, Is.EqualTo(floor));
        Assert.That(potion.Vy, Is.Zero);
    }

    [Test]
    public void RegularPotionAndFoodStayCollectibleAfterTheOldTimeout()
    {
        var game = new Game(m_areas[0]);
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X + 30, game.Hero.Z);
        var food = game.Add(ActorKind.Food, Moves.Thief, 0x7f, 1, potion.X, potion.Z);
        potion.Age = food.Age = 600;
        game.Hero.Health = 16;
        game.Tick(default);
        Assert.That(game.Actors, Does.Contain(potion));
        Assert.That(game.Actors, Does.Contain(food));

        game.Hero.X = potion.X;
        game.Hero.Y = potion.Y;
        game.Tick(default);
        Assert.That(game.Potions, Is.EqualTo(2));
        Assert.That(game.Hero.Health, Is.EqualTo(32));
        Assert.That(game.Actors, Does.Not.Contain(potion));
        Assert.That(game.Actors, Does.Not.Contain(food));
    }

    [Test]
    public void SettledPotionStaysInPlaceUntilItScrollsOut()
    {
        var game = new Game(m_areas[0]);
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X - 35, game.Hero.Z);
        var x = potion.X;
        AdvanceTo(game, (int)x - 1, potion);
        Assert.That(potion.X, Is.EqualTo(x));
        Assert.That(game.Actors, Does.Contain(potion));

        AdvanceTo(game, (int)x + 20, potion);
        game.Tick(default);
        Assert.That(game.Actors, Does.Not.Contain(potion));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HeningerFinishesHisSwingWithoutRepeatingTheStrikePose(bool strong)
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        var moves = strong ? Moves.StrongHeninger : Moves.Heninger;
        var enemy = game.Add(ActorKind.Heninger, moves, 0x18, 16, hero.X + 24, hero.Z);
        enemy.Cooldown = 0;
        game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Attacking));

        var frames = new List<int>();
        for (var tick = 0; tick < 100 && enemy.State == FighterState.Attacking; tick++)
        {
            var frame = enemy.Animation.Current.Frame;
            if (frames.Count == 0 || frames[^1] != frame)
                frames.Add(frame);
            game.Tick(default);
        }

        Assert.That(frames, Is.EqualTo(new[] { 124, 125, 126, 127, 125, 124 }));
        Assert.That(hero.Health, Is.EqualTo(46));
    }

    [Test]
    public void LongmoanSwingsWithoutFlashingHisIdlePoseOrRepeatingTheStrike()
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        var enemy = game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x77, 16, hero.X + 23, hero.Z);
        enemy.Cooldown = 0;
        game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Attacking));

        var frames = new List<int>();
        for (var tick = 0; tick < 100 && enemy.State == FighterState.Attacking; tick++)
        {
            var frame = enemy.Animation.Current.Frame;
            if (frames.Count == 0 || frames[^1] != frame)
                frames.Add(frame);
            game.Tick(default);
        }

        Assert.That(frames, Is.EqualTo(new[] { 1009, 1010, 1011, 1012, 1010, 1009 }));
        Assert.That(hero.Health, Is.EqualTo(46));
    }

    [Test]
    public void DistantEnemyRunsAndUsesItsClosingAttack([Values(false, true)] bool leap)
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        var moves = leap ? Moves.Longmoan : Moves.Heninger;
        var enemy = game.Add(leap ? ActorKind.Longmoan : ActorKind.Heninger, moves, 0x18, 16, hero.X + 175, hero.Z);
        enemy.Cooldown = 0;
        var ran = false;
        var topSpeed = 0.0;
        var usedClosingAttack = false;

        for (var tick = 0; tick < 130 && hero.Health == 48; tick++)
        {
            game.Tick(default);
            ran |= enemy.Animation.Address == moves.Run && Math.Abs(enemy.Vx) > 2;
            if (enemy.Animation.Address == moves.Run)
                topSpeed = Math.Max(topSpeed, Math.Abs(enemy.Vx));
            usedClosingAttack |= leap
                ? enemy.State == FighterState.Jumping && enemy.Animation.Address == moves.JumpAttack
                : enemy.State == FighterState.Dashing && enemy.Animation.Address == moves.Dash;
        }

        Assert.That(ran, Is.True);
        if (leap)
            Assert.That(topSpeed, Is.EqualTo(3));
        Assert.That(usedClosingAttack, Is.True);
        Assert.That(hero.Health, Is.EqualTo(44));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OpeningSoldierWalksAndRecoversFromALightHitAtArcadePace(bool longmoan)
    {
        var game = new Game(m_areas[0]);
        var kind = longmoan ? ActorKind.Longmoan : ActorKind.Heninger;
        var moves = longmoan ? Moves.Longmoan : Moves.Heninger;
        var hero = game.Hero;
        var enemy = game.Add(kind, moves, longmoan ? 0x78 : 0x18, 16, hero.X + 39, hero.Z);
        enemy.Cooldown = 1000;
        var startX = enemy.X;
        game.Tick(default);
        Assert.That(enemy.X, Is.EqualTo(startX - 0.5));

        for (var tick = 0; tick < 50 && enemy.State != FighterState.Hurt; tick++)
            game.Tick(new GameInput(Attack: true));
        Assert.That(enemy.Health, Is.LessThan(16));
        Assert.That(enemy.State, Is.EqualTo(FighterState.Hurt));
        Assert.That(enemy.Animation.Index, Is.EqualTo(1));
        var hitX = enemy.X;
        var hitZ = enemy.Z;
        for (var tick = 1; tick < 81; tick++)
        {
            game.Tick(default);
            Assert.That(enemy.State, Is.EqualTo(FighterState.Hurt));
            Assert.That((enemy.X, enemy.Z), Is.EqualTo((hitX, hitZ)));
        }
        Assert.That(enemy.Animation.Index, Is.Zero);
        game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Idle));
    }

    [Test]
    public void AutoplayWalksThroughClearSceneryAndWaitsForUnavailableTargets()
    {
        var game = new Game(m_areas[0]);
        var controller = new CodedController();
        for (var tick = 0; tick < 180; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            var input = controller.Read(game);
            Assert.That(input.Run, Is.False, "Ordinary stage advancement should be a walk.");
            game.Tick(input);
        }
        game.Actors.RemoveAll(a => a != game.Hero);
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, game.Hero.X + 90, game.Hero.Z);
        enemy.Invulnerable = 60;
        Assert.That(game.CanAdvance, Is.False);
        Assert.That(controller.Read(game), Is.EqualTo(default(GameInput)), "Wait for the fight instead of running to the screen edge.");
    }

    [Test]
    public void AutoplaySidestepsAnEnemyBarge()
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 175, hero.Z);
        enemy.Cooldown = 0;
        for (var tick = 0; tick < 120 && enemy.State != FighterState.Dashing; tick++)
            game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Dashing));

        var controller = new CodedController();
        var sidestep = 0.0;
        while (enemy.State == FighterState.Dashing && enemy.StateTicks < 18)
        {
            game.Tick(controller.Read(game));
            sidestep = Math.Max(sidestep, Math.Abs(hero.Z - enemy.Z));
        }

        Assert.That(sidestep, Is.GreaterThanOrEqualTo(7));
        Assert.That(hero.Health, Is.EqualTo(48));
    }

    [Test]
    public void AutoplayStepsOutOfTheCrowdedAttackLane()
    {
        var data = m_areas[0];
        var game = new Game(data);
        var hero = game.Hero;
        var attacker = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 24, hero.Z + 1);
        attacker.SetState(FighterState.Attacking, attacker.Moves.Attacks[0], true);
        attacker.Animation.Seek(data.Attacks[attacker.Animation.Address][2]);
        game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, hero.X + 35, hero.Z + 2);
        game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, hero.X + 45, hero.Z + 3);

        var input = new CodedController().Read(game);

        Assert.That(input, Is.EqualTo(new GameInput(Z: -1)));
    }

    [Test]
    public void SandwichedAutoplayerCommitsToLeavingTheAttackLane()
    {
        var game = new Game(m_areas[0], 2);
        game.Actors.RemoveAll(a => a != game.Hero);
        var hero = game.Hero;
        game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X - 24, hero.Z);
        game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 16, hero.X + 24, hero.Z);
        var controller = new CodedController();
        var first = controller.Read(game);
        Assert.That(Math.Abs(first.Z), Is.EqualTo(1), "Two enemies on opposing sides are enough to trigger an escape.");
        Assert.That(first.X, Is.Zero);
        var startZ = hero.Z;
        // Move past the initial threat-depth threshold: the escape must not stop
        // early and send the hero straight back into the same two attackers.
        for (var step = 1; step < 19; step++)
        {
            hero.Z = startZ + first.Z * step;
            var input = controller.Read(game);
            Assert.That(input, Is.EqualTo(first), $"Escape step {step}");
            Assert.That(Math.Abs(game.Ground(hero.X, hero.Z) - hero.Y), Is.LessThanOrEqualTo(8));
            Assert.That(game.ProjectView(hero.X, hero.Z, hero.Y).Y, Is.InRange(16, 208));
        }
    }

    [Test]
    public void NearbyEnemiesStaggerHitsWhenTheyAreReadyTogether()
    {
        var game = new Game(m_areas[0], 2);
        var hero = game.Hero;
        var first = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 24, hero.Z);
        var second = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 25, hero.Z + 1);
        first.Cooldown = second.Cooldown = 0;

        var hitTicks = new List<int>();
        for (var tick = 0; tick < 90 && hitTicks.Count < 2; tick++)
        {
            var health = hero.Health;
            game.Tick(default);
            if (hero.Health < health)
            {
                Assert.That(health - hero.Health, Is.EqualTo(2));
                hitTicks.Add(game.TickCount);
            }
        }

        Assert.That(hitTicks, Has.Count.EqualTo(2));
        Assert.That(hitTicks[1] - hitTicks[0], Is.GreaterThanOrEqualTo(8));
    }

    [Test]
    public void EagleFeathersDriftOnlyOverTheEaglesBackAndStopDuringCamp()
    {
        foreach (var area in m_areas)
            Assert.That(EagleFeathers.IsVisible(new Game(area)), Is.False, "Opening scenery should not contain drifting feathers.");
        var data = m_areas[3];
        var game = new Game(data, 2);
        AdvanceTo(game, 4850);
        Assert.That(EagleFeathers.IsVisible(game), Is.True);
        var feather = EagleFeathers.Sample(0, game.TickCount, game.CameraX);
        var later = EagleFeathers.Sample(0, game.TickCount + 10, game.CameraX);
        Assert.That(later.X, Is.LessThan(feather.X), "Wind should carry feathers to the left.");
        Assert.That(feather.Frame, Is.EqualTo(1042), "Use the feather identified in the MAME reference.");
        Assert.That(later.Frame, Is.EqualTo(1042), "Do not cycle through the unrelated 1024-1041 frames.");

        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(320, 224, data.Palette);
        renderer.Draw(screen, game);
        var first = screen.Pixels.ToArray();
        renderer.Draw(screen, game);
        Assert.That(screen.Pixels, Is.EqualTo(first), "Drawing must not advance the effect or alter simulation state.");
        void Save(string name)
        {
            using var bitmap = new SKBitmap(320, 224);
            bitmap.Pixels = screen.Pixels.Select(p => new SKColor(screen.Palette[p].R, screen.Palette[p].G, screen.Palette[p].B)).ToArray();
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, name));
            encoded.SaveTo(file);
        }
        Save("goldenaxe-eagle-feathers-color.png");
        for (var tick = 0; tick < 10; tick++)
            game.Tick(default);
        renderer.Draw(screen, game);
        Save("goldenaxe-eagle-feathers-later.png");
        screen.SetPalette(PixelScreenData.CreateGreyscalePalette(data.Palette, Rgb.Black, new Rgb(100, 255, 140)));
        renderer.Draw(screen, game);
        Save("goldenaxe-eagle-feathers-mono.png");

        AdvanceTo(game, data.Stops[^1]);
        for (var tick = 0; tick < 40; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        game.StartCamp();
        Assert.That(game.IsCamp, Is.True);
        Assert.That(EagleFeathers.IsVisible(game), Is.False);
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void GroundBreathingDragonStartsEveryFlameFacingForward(int facing)
    {
        var game = new Game(m_areas[0], random: new Random(0));
        var dragon = game.Add(ActorKind.BlueDragon, game.Character.DragonMoves, 0x43, 99, game.Hero.X + 65, game.Hero.Z);
        dragon.Facing = facing;
        dragon.Rider = game.Hero;
        game.Hero.Mount = dragon;
        var bursts = 0;
        for (var tick = 0; tick < 240 && bursts < 3; tick++)
        {
            game.Tick(new GameInput(Attack: true));
            var flame = game.Actors.SingleOrDefault(actor => actor.Kind == ActorKind.Flame && actor.Age == 0);
            if (flame == null)
                continue;

            bursts++;
            Assert.That(flame.Facing, Is.EqualTo(dragon.Facing), "The flame can render on the same tick it spawns.");
            Assert.That(flame.Y, Is.EqualTo(dragon.Y));
            Assert.That(dragon.Facing, Is.EqualTo(facing));
        }

        Assert.That(bursts, Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RedDragonFiresTravellingProjectileThatRetainsItsOwnersSide(int character)
    {
        var data = m_areas[0];
        var game = new Game(data, character);
        var dragon = new Fighter(1000, data, ActorKind.RedDragon, game.Character.RedDragonMoves, 0x44, 99)
        { X = game.Hero.X, Z = game.Hero.Z, Y = game.Hero.Y, Rider = game.Hero, Facing = 1 };
        game.Hero.Mount = dragon;
        var enemy = new Fighter(1001, data, ActorKind.Heninger, Moves.Heninger, 0x18, 16)
        { X = dragon.X + 120, Z = dragon.Z, Y = dragon.Y, Cooldown = 1000 };
        game.Actors.Add(dragon);
        game.Actors.Add(enemy);
        game.Tick(new GameInput(Attack: true));
        for (var tick = 0; tick < 60 && !game.Actors.Any(a => a.Kind == ActorKind.Fireball); tick++)
            game.Tick(default);
        var fireball = game.Actors.Single(a => a.Kind == ActorKind.Fireball);
        Assert.That(fireball.Owner, Is.SameAs(game.Hero));
        var shotX = fireball.X;
        var screen = new PixelScreenData(320, 224, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        using var bitmap = new SKBitmap(320, 224);
        bitmap.Pixels = screen.Pixels.Select(p => new SKColor(screen.Palette[p].R, screen.Palette[p].G, screen.Palette[p].B)).ToArray();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, $"goldenaxe-red-dragon-{character}.png"));
        encoded.SaveTo(file);

        // Losing the mount must not switch the allegiance of a shot already in flight.
        dragon.Rider = null;
        game.Hero.Mount = null;
        game.Hero.MountCooldown = 1000;
        game.Tick(default);
        Assert.That(fireball.X, Is.EqualTo(shotX + 4));
        for (var tick = 0; tick < 80 && !fireball.IsGone; tick++)
            game.Tick(default);
        Assert.That(enemy.Health, Is.EqualTo(8));
        Assert.That(game.Hero.Health, Is.EqualTo(48));
        Assert.That(fireball.IsGone, Is.True);
    }

    [Test]
    public void MissingBridgeJumpCostsLifeAndRespawnsOnSafeGround()
    {
        var game = new Game(m_areas[2], 2);
        AdvanceTo(game, 4625);
        game.Hero.X = 4810;
        game.Hero.Z = 4184;
        game.Hero.Y = 4896;
        game.Hero.Vx = 0;
        game.Hero.Vy = 2;
        game.Hero.SetState(FighterState.Jumping, game.Hero.Moves.Jump);
        for (var tick = 0; tick < 240 && game.Lives == 2; tick++)
            game.Tick(default);
        Assert.That(game.Lives, Is.EqualTo(1));
        Assert.That(game.Hero.Health, Is.EqualTo(48));
        Assert.That(game.Ground(game.Hero.X, game.Hero.Z), Is.LessThan(5248));
        Assert.That(game.GameOver, Is.False);
    }

    [Test]
    public void OnFootShoulderBargeCoversTheArcadeDistance([Range(0, 2)] int character)
    {
        var game = new Game(m_areas[0], character, new Random(0));
        var run = new GameInput(X: 1, Run: true);
        for (var tick = 0; tick < 12; tick++)
            game.Tick(run);
        game.Tick(run with { Attack = true });
        Assert.That(game.Hero.State, Is.EqualTo(FighterState.Dashing));

        var startX = game.Hero.X;
        for (var tick = 0; tick < 40 && game.Hero.State == FighterState.Dashing; tick++)
            game.Tick(default);

        Assert.That(game.Hero.State, Is.EqualTo(FighterState.Idle));
        Assert.That(game.Hero.X - startX, Is.EqualTo(92));
    }

    [TestCase(1, 4778, 4840)]
    [TestCase(-1, 4862, 4799)]
    public void ShoulderBargeCrossesTheStageFiveBridge(int direction, int start, int farBank)
    {
        var game = new Game(m_areas[2], 1);
        AdvanceTo(game, 4625);
        var hero = game.Hero;
        hero.X = start;
        hero.Z = 4184;
        hero.Y = game.Ground(hero.X, hero.Z);
        for (var tick = 0; tick < 6; tick++)
            game.Tick(new GameInput(X: direction, Run: true));
        game.Tick(new GameInput(X: direction, Run: true, Attack: true));
        Assert.That(hero.State, Is.EqualTo(FighterState.Dashing));
        for (var tick = 0; tick < 40; tick++)
            game.Tick(default);

        Assert.That((hero.X - farBank) * direction, Is.GreaterThanOrEqualTo(0));
        Assert.That(hero.Y, Is.EqualTo(game.Ground(hero.X, hero.Z)));
        Assert.That(game.Lives, Is.EqualTo(2));
    }

    [Test]
    public void ShoulderBargeThatFallsShortOfTheStageFiveBridgeCostsALife()
    {
        var game = new Game(m_areas[2], 1);
        AdvanceTo(game, 4610);
        var hero = game.Hero;
        hero.X = 4730;
        hero.Z = 4184;
        hero.Y = game.Ground(hero.X, hero.Z);
        for (var tick = 0; tick < 6; tick++)
            game.Tick(new GameInput(X: 1, Run: true));
        game.Tick(new GameInput(X: 1, Run: true, Attack: true));
        Assert.That(hero.State, Is.EqualTo(FighterState.Dashing));
        for (var tick = 0; tick < 240 && game.Lives == 2; tick++)
            game.Tick(default);

        Assert.That(game.Lives, Is.EqualTo(1));
        Assert.That(game.Ground(hero.X, hero.Z), Is.LessThan(5248));
    }

    [Test]
    public void TurtleVillageSurfStartsAtTheShoreAndMovesBelowThePlayfield()
    {
        var data = m_areas[1];
        var game = new Game(data);
        AdvanceTo(game, 4940);
        Assert.That(game.SurfAge, Is.Null);
        AdvanceTo(game, 4944);
        Assert.That(game.SurfAge, Is.Zero);
        game.Hero.X = game.Progress + 120;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4296);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        for (var tick = 0; tick < 384; tick++)
            game.Tick(default);
        Assert.That(game.SurfAge, Is.EqualTo(384));

        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        // The GO prompt can blink above the water independently of the surf.
        byte[] Draw(Game scene, int cameraY)
        {
            scene.ViewCamera.Reset(-855, cameraY);
            renderer.Draw(screen, scene, effects: false);
            return screen.Pixels.Skip(160 * ArcadeData.Width).ToArray();
        }

        game.Actors.Clear();
        var inactive = new Game(data);
        inactive.Actors.Clear();
        var baseline = Draw(inactive, -873);
        var crest = Draw(game, -873);
        var crestRow = (176 - 160) * ArcadeData.Width;
        Assert.That(crest.Take(crestRow), Is.EqualTo(baseline.Take(crestRow)));
        Assert.That(crest.Skip(crestRow), Is.Not.EqualTo(baseline.Skip(crestRow)));
        var highView = Draw(game, -820);
        Assert.That(highView, Is.EqualTo(Draw(inactive, -820)));
        Assert.That(Draw(game, -831), Is.EqualTo(Draw(inactive, -831)), "Hide an isolated crest at the bottom edge.");
        Assert.That(Draw(game, -837), Is.Not.EqualTo(Draw(inactive, -837)), "Start the fade as more water comes into view.");

        for (var tick = 0; tick < 8; tick++)
            game.Tick(default);
        Assert.That(Draw(game, -873), Is.Not.EqualTo(crest));
        Assert.That(new Game(data, startInCamp: true).SurfAge, Is.Null);
    }

    [Test]
    public void TurtleVillageSurfCoversHighPriorityTilesAtTheBottomEdge()
    {
        var data = new ArcadeData(ArcadeData.AssetDirectory, 1);
        var game = new Game(data);
        AdvanceTo(game, 4944);
        game.Hero.X = game.Progress + 120;
        for (var tick = 0; tick < 384; tick++)
            game.Tick(default);
        game.Actors.Clear();
        const int cameraX = -855, cameraY = -872, x = 16, y = 223;
        game.ViewCamera.Reset(cameraX, cameraY);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var renderer = new ArcadeRenderer(data);
        renderer.Draw(screen, game, effects: false);
        var offset = y * ArcadeData.Width + x;
        var surfPixel = screen.Pixels[offset];
        Assert.That(surfPixel, Is.InRange(TurtleVillageSurf.PaletteStart, 255));

        var layer = data.Foreground;
        var tileX = (x - 7 - cameraX - layer.X) / 8;
        var tileY = (y - 512 - cameraY - layer.Y) / 8;
        layer.Cells[tileY * layer.Width + tileX] = (1u << 23) | 1u; // High priority, solid pen 7.
        renderer.Draw(screen, game, effects: false);
        Assert.That(screen.Pixels[offset], Is.EqualTo(surfPixel));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WalkingOffLedgeFallsOverSeveralTicksAndLands(bool mounted)
    {
        var game = new Game(m_areas[0], 2);
        AdvanceTo(game, 4800);
        var body = game.Hero;
        if (mounted)
        {
            body = new Fighter(1000, m_areas[0], ActorKind.Chicken, game.Character.MountedMoves, 0x23, 99) { Rider = game.Hero };
            game.Hero.Mount = body;
            game.Actors.Add(body);
        }
        body.X = 4833;
        body.Z = 4168;
        body.Y = game.Ground(body.X, body.Z);
        var top = body.Y;
        game.Tick(new GameInput(-1));
        Assert.That(body.State, Is.EqualTo(FighterState.Falling));
        Assert.That(body.Y, Is.EqualTo(top), "Stepping off must not snap to the ground below.");
        for (var tick = 0; tick < 5; tick++)
            game.Tick(default);
        Assert.That(body.Y, Is.InRange(top + 1, top + 47));
        for (var tick = 0; tick < 40; tick++)
            game.Tick(default);
        Assert.That(body.Y, Is.EqualTo(top + 48));
        Assert.That(body.State, Is.EqualTo(FighterState.Idle));
        Assert.That(body.Vy, Is.Zero);
    }

    [Test]
    public void ForestFrontLaneReachesTheTerrainBoundary()
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        for (var tick = 0; tick < 100; tick++)
            game.Tick(new GameInput(Z: 1));

        Assert.That(hero.Z, Is.InRange(4302.5, 4304.0));
        Assert.That(hero.Health, Is.EqualTo(48));
        Assert.That(hero.State, Is.EqualTo(FighterState.Walking));
        Assert.That(game.Lives, Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WalkingOffTheTurtleVillageFrontEdgeCostsOneLife(bool mounted)
    {
        var game = new Game(m_areas[1]);
        AdvanceTo(game, 4185);
        var hero = game.Hero;
        hero.X = 4333;
        hero.Z = 4264;
        hero.Y = game.Ground(hero.X, hero.Z);
        var body = hero;
        if (mounted)
        {
            body = game.Add(ActorKind.Chicken, game.Character.MountedMoves, 0x23, 99, hero.X, hero.Z);
            body.Rider = hero;
            hero.Mount = body;
        }

        for (var tick = 0; tick < 70 && body.State != FighterState.Falling; tick++)
            game.Tick(new GameInput(Z: 1));
        Assert.That(body.State, Is.EqualTo(FighterState.Falling));
        Assert.That(game.Ground(body.X, body.Z), Is.GreaterThanOrEqualTo(5248));
        Assert.That(hero.Health, Is.EqualTo(48));

        for (var tick = 0; tick < 240 && game.Lives == 2; tick++)
            game.Tick(default);
        Assert.That(game.Lives, Is.EqualTo(1));
        Assert.That(hero.Health, Is.EqualTo(48));
        Assert.That(hero.Mount, Is.Null);
        Assert.That(game.Ground(hero.X, hero.Z), Is.InRange(3841, 5247));
    }

    [Test]
    public void ShoulderBargeKnocksAnEnemyOverTheTurtleVillageEdge()
    {
        var game = new Game(m_areas[1]);
        AdvanceTo(game, 4220);
        var hero = game.Hero;
        hero.X = 4415;
        hero.Z = 4312;
        hero.Y = game.Ground(hero.X, hero.Z);
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, 4438, hero.Z);
        enemy.Cooldown = 1000;

        for (var tick = 0; tick < 6; tick++)
            game.Tick(new GameInput(X: 1, Run: true));
        game.Tick(new GameInput(X: 1, Attack: true, Run: true));
        Assert.That(hero.State, Is.EqualTo(FighterState.Dashing));
        for (var tick = 0; tick < 8 && enemy.State != FighterState.Flying; tick++)
            game.Tick(default);
        Assert.That(enemy.State, Is.EqualTo(FighterState.Flying));
        Assert.That(enemy.Health, Is.EqualTo(12));

        for (var tick = 0; tick < 160 && !enemy.IsGone; tick++)
            game.Tick(default);
        Assert.That(enemy.IsGone, Is.True);
        Assert.That(game.Ground(enemy.X, enemy.Z), Is.GreaterThanOrEqualTo(5248));
        Assert.That(game.Kills, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void HoldingDownKeepsFeetOnScreenAwayFromDropOffs(int area)
    {
        var game = new Game(m_areas[area], 2);
        Assert.That(game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.InRange(48, 196), "The player must spawn on screen.");
        foreach (var progress in new[] { 4200, 4500, 4800, 5100 })
        {
            if (area == 0 && progress <= m_areas[0].Stops[0])
                continue;
            AdvanceTo(game, progress);
            // At these positions, a mapped pit can intentionally extend the lane below the HUD.
            if (Enumerable.Range(0, 20).Any(row => game.Ground(game.Hero.X, game.Hero.Z + row * 8) >= 5248))
                continue;
            for (var tick = 0; tick < 300; tick++)
            {
                game.Actors.RemoveAll(a => a != game.Hero);
                game.Tick(new GameInput(Z: 1));
                if (tick > 30)
                    Assert.That(game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.LessThanOrEqualTo(game.BottomFeetLimit + 2), $"Area {area}, progress {progress}");
            }
        }
    }

    [Test]
    public void EagleFrontLaneUsesTheVisibleFloorBelowTheHud()
    {
        var game = new Game(m_areas[3]);
        for (var tick = 0; tick < 400; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(new GameInput(Z: 1));
        }
        Assert.That(game.ProjectView(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.InRange(210, ArcadeData.Height - 1));
        Assert.That(game.Hero.Y, Is.EqualTo(game.Ground(game.Hero.X, game.Hero.Z)));
        Assert.That(game.Lives, Is.EqualTo(2));
    }

    [Test]
    public void OnFootRunningJumpUsesArcadeAirSpeed([Range(0, 2)] int character)
    {
        var game = new Game(m_areas[0], character, new Random(0));
        var hero = game.Hero;
        var run = new GameInput(X: 1, Run: true);
        for (var tick = 0; tick < 12; tick++)
            game.Tick(run);
        Assert.That(hero.Vx, Is.EqualTo(4));

        game.Tick(run with { Jump = true });
        for (var tick = 0; tick < 16 && hero.State == FighterState.Crouching; tick++)
            game.Tick(run);
        Assert.That(hero.State, Is.EqualTo(FighterState.Jumping));
        var takeoffX = hero.X;
        var takeoffY = hero.Y;
        var highestY = takeoffY;
        for (var tick = 0; tick < 80 && hero.State == FighterState.Jumping; tick++)
        {
            game.Tick(run);
            highestY = Math.Min(highestY, hero.Y);
        }

        Assert.That(hero.State, Is.EqualTo(FighterState.Idle));
        Assert.That(hero.X - takeoffX, Is.InRange(62, 65));
        Assert.That(takeoffY - highestY, Is.InRange(124, 128));
    }

    [Test]
    public void JumpingDoesNotPullTheVerticalCameraUpAndDown()
    {
        var game = new Game(m_areas[3]);
        for (var tick = 0; tick < 120; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        var cameraY = game.CameraY;
        var viewY = game.ViewCamera.Y;
        game.Tick(new GameInput(Jump: true));
        for (var tick = 0; tick < 20 && game.Hero.State == FighterState.Crouching; tick++)
            game.Tick(default);
        Assert.That(game.Hero.State, Is.EqualTo(FighterState.Jumping));
        for (var tick = 0; tick < 80; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
            Assert.That(game.CameraY, Is.EqualTo(cameraY));
            Assert.That(game.ViewCamera.Y, Is.EqualTo(viewY));
        }
    }

    [TestCase(2, -666, -749)]
    [TestCase(4, -632, -736)]
    public void HoldingDownReachesTheMameLowerCameraAtRecoveredScenery(int area, int cameraX, int expectedY)
    {
        var game = new Game(m_areas[area], random: new Random(0));
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, (double)cameraX);
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, area == 2 ? -720.0 : -711.0);
        game.Hero.X = game.Progress + 120;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        var lives = game.Lives;
        for (var tick = 0; tick < 300 && game.CameraY > expectedY; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(new GameInput(Z: 1));
        }
        Assert.That(game.CameraX, Is.EqualTo(cameraX));
        Assert.That(game.CameraY, Is.EqualTo(expectedY));
        Assert.That(game.Lives, Is.EqualTo(lives));
        Assert.That(game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.LessThanOrEqualTo(game.BottomFeetLimit + 2));
    }

    [Test]
    public void CastleApproachUsesTheArcadeLowerBoundBetweenCameraSamples()
    {
        var data = m_areas[4];
        var game = new Game(data);
        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        foreach (var cameraX in new[] { -324, -351, -379, -632 })
        {
            var lowerY = data.CameraRange(cameraX).Low;
            Assert.That(lowerY, Is.EqualTo(-736), $"Camera X {cameraX}");
            game.ViewCamera.Reset(cameraX, lowerY);
            renderer.Draw(screen, game, effects: false);
            var bottomRow = screen.Pixels.Skip((ArcadeData.Height - 1) * ArcadeData.Width);
            Assert.That(bottomRow.Count(pixel => pixel != data.Black), Is.GreaterThan(ArcadeData.Width / 2));
        }
        Assert.That(data.CameraRange(-1223).Low, Is.EqualTo(-676));
        // The Stage 3 capture ends at this row even where the arcade could scroll lower.
        Assert.That(m_areas[1].CameraRange(-487).Low, Is.EqualTo(-868));
        Assert.That(m_areas[1].CameraRange(-500).Low, Is.EqualTo(-872));
    }

    [Test]
    public void CastleApproachKeepsTheLeftEdgeCoveredUntilTheEagleSceneryStarts()
    {
        var data = m_areas[4];
        var game = new Game(data);
        var renderer = new ArcadeRenderer(data);
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        AdvanceTo(game, 4381);
        game.Hero.X = game.Progress + 132;
        // The sampled eagle camera applies early here, while the left edge is
        // still over tiles missing from the capture.
        Assert.That(data.CameraRange(-292), Is.EqualTo((-576, -576)));
        for (var tick = 0; tick < 90; tick++)
        {
            game.Tick(default);
            game.Actors.RemoveAll(actor => actor != game.Hero);
            renderer.Draw(frame, game, effects: false);
            for (var y = 140; y < ArcadeData.Height; y++)
                Assert.That(frame.Pixels[y * ArcadeData.Width], Is.Not.EqualTo(data.Black),
                    $"Left edge at camera ({game.ViewCamera.X}, {game.ViewCamera.Y}), row {y}");
        }

        Assert.That(game.ViewCamera.X, Is.InRange(-300, -285));
        Assert.That(game.ViewCamera.Y, Is.EqualTo(-576));
        Assert.That(data.CameraRange(-324).Low, Is.EqualTo(-736));
    }

    [Test]
    public void CastleOpeningPansDownWhenBothCornersHaveScenery()
    {
        var game = new Game(m_areas[4]);
        var hero = game.Hero;
        hero.Invulnerable = 10000;
        var startingView = game.ViewCamera.Y;
        for (var tick = 0; tick < 100; tick++)
        {
            game.Tick(new GameInput(Z: 1));
            game.Actors.RemoveAll(a => a != hero);
        }

        Assert.That(game.ViewCamera.Y, Is.EqualTo(startingView), "The opening left corner has no captured tiles below this view.");
        Assert.That(hero.Z, Is.InRange(4254, 4255.99));
        Assert.That(game.Lives, Is.EqualTo(2));

        AdvanceTo(game, 4120);
        hero.X = game.Progress + 110;
        for (var tick = 0; tick < 80; tick++)
        {
            game.Tick(new GameInput(Z: 1));
            game.Actors.RemoveAll(a => a != hero);
        }
        Assert.That(game.ViewCamera.Y, Is.EqualTo(-576));
        var data = m_areas[4];
        var frame = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        new ArcadeRenderer(data).Draw(frame, game, effects: false);
        var bottom = (ArcadeData.Height - 1) * ArcadeData.Width;
        Assert.That(frame.Pixels.Skip(bottom).Take(8).Any(pixel => pixel != data.Black), Is.True);
    }

    [Test]
    public void CastleOpeningShowsTheDropBeforeThePlayerStepsOff()
    {
        var game = new Game(m_areas[4]);
        AdvanceTo(game, 4265);
        var hero = game.Hero;
        hero.X = 4418;
        hero.Z = 4220;
        hero.Y = game.Ground(hero.X, hero.Z);
        for (var tick = 0; tick < 22; tick++)
            game.Tick(new GameInput(Z: 1));

        Assert.That(hero.Z, Is.EqualTo(4253));
        Assert.That(hero.State, Is.EqualTo(FighterState.Walking));
        Assert.That(game.ProjectView(hero.X, 4256, hero.Y).Y, Is.LessThanOrEqualTo(200));
        game.Tick(new GameInput(Z: 1));
        game.Tick(new GameInput(Z: 1));
        Assert.That(hero.State, Is.EqualTo(FighterState.Falling));
    }

    [Test]
    public void PlayerCanHitAThiefBelowTheOldBottomLaneLimit()
    {
        var game = new Game(m_areas[4]);
        AdvanceTo(game, 5200);
        var hero = game.Hero;
        hero.X = game.Progress + 130;
        hero.Z = 4240;
        hero.Y = game.Ground(hero.X, hero.Z);
        var thief = game.Add(ActorKind.Thief, Moves.Thief, 0x3d, 99, hero.X + 27, 4278);
        thief.Items = 1;
        thief.Facing = -1;
        // Let the player enter the lane while the thief is stationary between steps.
        for (var tick = 0; tick < 36; tick++)
        {
            thief.SetState(FighterState.Hurt, thief.Moves.Hurt);
            game.Tick(new GameInput(Z: 1));
        }

        Assert.That(Math.Abs(hero.Z - thief.Z), Is.LessThan(7));
        Assert.That(game.ProjectView(hero.X, hero.Z, hero.Y).Y, Is.InRange(215, ArcadeData.Height - 1));
        thief.SetState(FighterState.Walking, thief.Moves.Walk);
        for (var tick = 0; tick < 18 && thief.Items > 0; tick++)
            game.Tick(new GameInput(Attack: true));
        Assert.That(thief.Items, Is.Zero);
        Assert.That(game.Actors.Any(a => a.Kind == ActorKind.Potion), Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void CampSleepsWakesAllowsPotionTheftAndDropsHealingFood(int character)
    {
        var game = new Game(m_areas[1], character);
        AdvanceTo(game, m_areas[1].Stops[^1]);
        for (var tick = 0; tick < 40; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        Assert.That(game.Complete, Is.True);
        game.Hero.Health = 16;
        game.StartCamp();
        Assert.That(game.Complete, Is.False);
        Assert.That(game.Hero.State, Is.EqualTo(FighterState.Sleeping));
        Assert.That(game.Hero.Animation.Current.Frame, Is.EqualTo(830 + character));
        Assert.That(game.Hero.Health, Is.EqualTo(16));
        var jar = game.Actors.Single(a => a.Kind == ActorKind.CampPotion);
        Assert.That(game.Potions, Is.EqualTo(1), "The visible camp jar still belongs to the sleeping hero.");
        var sleepingX = game.Hero.X;
        for (var tick = 0; tick < 150; tick++)
            game.Tick(new GameInput(1, Attack: true));
        Assert.That(game.Hero.X, Is.EqualTo(sleepingX), "Input must not move the sleeping hero.");
        Assert.That(game.Hero.Animation.Index, Is.Zero, "Keep the sleeping pose until the wake-up cue.");
        Assert.That(game.Actors.Single(a => a.Kind == ActorKind.CampPotion), Is.SameAs(jar), "Waking must not create another jar.");
        var thief = game.Actors.Single(a => a.Kind == ActorKind.Thief && !a.FoodThief);
        var carried = thief.Items;
        while (game.Potions > 0 && game.Camp.Ticks < 450)
        {
            Assert.That(game.Hero.State, Is.EqualTo(FighterState.Sleeping), "Let the thief reach the potion before waking.");
            game.Tick(default);
        }
        Assert.That(game.Potions, Is.Zero);
        Assert.That(thief.Items, Is.EqualTo(carried + 1));
        Assert.That(jar.IsGone, Is.True);
        Assert.That(game.FadingPickups, Does.Contain(jar));
        Assert.That(game.PotionPickups, Is.Empty, "Theft must not send collection sparkles to the player's HUD.");
        for (var tick = 0; tick < 50 && !game.Hero.IsFree; tick++)
            game.Tick(default);
        Assert.That(game.Hero.IsFree, Is.True);
        Assert.That(game.FadingPickups, Does.Not.Contain(jar));

        var foodThief = game.Actors.Single(a => a.FoodThief);
        game.Actors.RemoveAll(a => a != game.Hero && a != foodThief && a.Kind != ActorKind.Campfire);
        foodThief.X = game.Hero.X + 30;
        foodThief.Z = game.Hero.Z;
        foodThief.Y = game.Hero.Y;
        foodThief.Facing = -1;
        foodThief.ThiefPauseTicks = 120; // Exercise the food drop during a stationary opening.
        for (var tick = 0; tick < 120 && !game.Actors.Any(a => a.Kind == ActorKind.Food); tick++)
            game.Tick(new GameInput(1, Attack: true));
        var food = game.Actors.Single(a => a.Kind == ActorKind.Food);
        food.Age = 600;
        for (var tick = 0; tick < 40; tick++)
            game.Tick(default);
        Assert.That(game.Actors, Does.Contain(food), "Camp food must not expire while waiting to be collected.");
        game.Hero.X = food.X;
        game.Hero.Z = food.Z;
        for (var tick = 0; tick < 60 && game.Hero.Health == 16; tick++)
            game.Tick(default);
        Assert.That(game.Hero.Health, Is.EqualTo(32));
        Assert.That(game.Potions, Is.Zero, "Food restores health without granting magic.");
        var cameraX = game.CameraX;
        while (!game.Complete && game.Camp.Ticks < 1400)
            game.Tick(default);
        Assert.That(game.Complete, Is.True);
        Assert.That(game.CameraX, Is.EqualTo(cameraX), "Camp must not resume the combat spawn script or scrolling.");
        var next = game.ContinueIn(m_areas[2]);
        Assert.That(next.IsCamp, Is.False);
        Assert.That(next.Hero.Health, Is.EqualTo(32));
        Assert.That(next.Potions, Is.Zero);
        Assert.That(next.Lives, Is.EqualTo(game.Lives));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void CampThievesWalkInThenRunAndPauseOnTheGround(int area)
    {
        var game = new Game(m_areas[area], random: new Random(0), startInCamp: true);
        var movedInDepth = false;
        var paused = false;
        var ran = false;
        for (var tick = 0; tick < 700; tick++)
        {
            var previous = game.Actors.Where(a => a.Kind == ActorKind.Thief)
                .ToDictionary(a => a.Id, a => (a.X, a.Z));
            game.Tick(default);
            foreach (var thief in game.Actors.Where(a => a.Kind == ActorKind.Thief))
            {
                var floorX = thief.Entered ? thief.X : Math.Clamp(thief.X, game.Progress + 24, game.Progress + 296);
                Assert.That(thief.Y, Is.EqualTo(game.Ground(floorX, thief.Z)).Within(1), "Walk in at floor height instead of falling from above.");
                if (!previous.TryGetValue(thief.Id, out var old))
                    continue;
                if (thief.ThiefPauseTicks > 0 && thief.State == FighterState.Idle && thief.ThiefRunTicks == 0)
                    paused |= thief.X == old.X && thief.Z == old.Z;
                if (thief.ThiefRunTicks > 0)
                {
                    ran |= Math.Abs(thief.X - old.X) + Math.Abs(thief.Z - old.Z) > 1;
                    movedInDepth |= Math.Abs(thief.Z - old.Z) > 0.1;
                }
            }
        }
        Assert.That(paused && ran && movedInDepth, Is.True, "Choose directions in depth as well as sideways, with stationary pauses between runs.");
    }

    [TestCase(0, -11, true)]
    [TestCase(1, 11, true)]
    [TestCase(2, 11, true)]
    [TestCase(2, 18, false)]
    public void ChickenTailSwipeCanCatchThiefInAnAdjacentLane(int character, int depthOffset, bool expectHit)
    {
        var game = new Game(m_areas[0], character);
        var hero = game.Hero;
        var chicken = game.Add(ActorKind.Chicken, game.Character.MountedMoves, 0x23, 99, hero.X, hero.Z);
        hero.Mount = chicken;
        chicken.Rider = hero;
        chicken.Facing = 1;
        var thief = game.Add(ActorKind.Thief, Moves.Thief, 0x3d, 99, chicken.X + 30, chicken.Z + depthOffset);
        thief.Items = 3;

        game.Tick(new GameInput(Attack: true));
        Assert.That(chicken.State, Is.EqualTo(FighterState.Attacking));
        for (var tick = 0; tick < 120 && chicken.State == FighterState.Attacking; tick++)
        {
            thief.X = chicken.X + 30;
            thief.Z = chicken.Z + depthOffset;
            thief.Y = game.Ground(thief.X, thief.Z);
            game.Tick(default);
        }

        Assert.That(thief.Items, Is.EqualTo(expectHit ? 2 : 3));
    }

    [TestCase(false, -1)]
    [TestCase(false, 1)]
    [TestCase(true, -1)]
    [TestCase(true, 1)]
    public void ThiefDropsOnlyItsCarriedLootThenLeavesEmpty(bool food, int direction)
    {
        var game = new Game(m_areas[0], 2);
        var thief = game.Add(ActorKind.Thief, Moves.Thief, food ? 0x3c : 0x3d, 99, game.Hero.X + 30, game.Hero.Z);
        thief.FoodThief = food;
        thief.Items = 3;
        for (var tick = 0; tick < 360 && thief.Items > 0; tick++)
        {
            thief.X = game.Hero.X + 30;
            thief.Z = game.Hero.Z;
            thief.Y = game.Hero.Y;
            game.Tick(new GameInput(Attack: true));
        }
        Assert.That(thief.Items, Is.Zero);
        Assert.That(thief.Animation.Address, Is.EqualTo(Moves.ThiefEmptyHurt));
        var loot = game.Actors.Where(a => a.Kind == (food ? ActorKind.Food : ActorKind.Potion)).ToArray();
        Assert.That(loot, Has.Length.EqualTo(3));
        thief.X = game.Progress + (direction < 0 ? 40 : 280);
        for (var tick = 0; tick < 120 && !thief.IsGone; tick++)
        {
            game.Tick(new GameInput(Attack: true));
            if (thief.State == FighterState.Fleeing)
                Assert.That(thief.Animation.Current.Frame, Is.InRange(494, 498));
        }
        Assert.That(thief.IsGone, Is.True, "An empty thief must leave instead of bouncing off the screen edge.");
        Assert.That(thief.Facing, Is.EqualTo(direction));
        Assert.That(game.Actors.Where(a => a.Kind == (food ? ActorKind.Food : ActorKind.Potion)), Is.EquivalentTo(loot));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MagicMakesVisibleThievesDropOneItemOfTheirOwnLoot(int character)
    {
        var game = new Game(m_areas[0], character, new Random(0));
        var thieves = new[] { false, true }.Select(food =>
        {
            var thief = game.Add(ActorKind.Thief, Moves.Thief, food ? 0x3c : 0x3d, 99,
                game.Hero.X + (food ? 100 : 60), game.Hero.Z);
            thief.FoodThief = food;
            thief.Items = food ? 1 : 3;
            return thief;
        }).ToArray();
        var offscreen = game.Add(ActorKind.Thief, Moves.Thief, 0x3d, 99, game.Progress + 400, game.Hero.Z);
        offscreen.Items = 3;
        var empty = game.Add(ActorKind.Thief, Moves.Thief, 0x3d, 99, game.Hero.X + 140, game.Hero.Z);
        empty.Items = 0;

        game.Tick(new GameInput(Magic: true));
        Assert.That(game.MagicTick, Is.Zero);
        for (var tick = 0; tick < 1000 && game.MagicTick >= 0; tick++)
            game.Tick(default);

        Assert.That(game.MagicTick, Is.EqualTo(-1));
        Assert.That(thieves.Select(a => a.Items), Is.EqualTo(new[] { 2, 0 }));
        Assert.That(thieves.All(a => a.State == FighterState.Hurt && a.Health == 99), Is.True);
        Assert.That(thieves[1].Animation.Address, Is.EqualTo(Moves.ThiefEmptyHurt));
        Assert.That(offscreen.Items, Is.EqualTo(3));
        Assert.That(empty.Items, Is.Zero);
        Assert.That(game.Actors.Count(a => a.Kind == ActorKind.Potion), Is.EqualTo(1));
        Assert.That(game.Actors.Count(a => a.Kind == ActorKind.Food), Is.EqualTo(1));
    }

    [TestCase(false, 47, 1)]
    [TestCase(true, 47, 1)]
    [TestCase(false, 48, -1)]
    [TestCase(true, 48, -1)]
    public void MidStageControllerFavorsFoodOverNearbyPotions(bool carriedByThief, int health, int direction)
    {
        var game = new Game(m_areas[0], 2);
        game.Actors.RemoveAll(a => a != game.Hero);
        game.Hero.Health = health;
        game.Hero.X = game.Progress + 110;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X - 35, game.Hero.Z);
        var potionThief = game.Add(ActorKind.Thief, Moves.Thief, 0x4a, 99, game.Hero.X - 60, game.Hero.Z);
        potionThief.Items = 2;
        var food = game.Add(carriedByThief ? ActorKind.Thief : ActorKind.Food, Moves.Thief,
            carriedByThief ? 0x3c : 0x7f, 99, game.Hero.X + 130, game.Hero.Z);
        food.FoodThief = carriedByThief;
        food.Items = 2;
        Assert.That(new CodedController().Read(game).X, Is.EqualTo(direction),
            "Even one missing health point should favor food; full health should favor potions.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HungryCampControllerPursuesDistantFoodWithFullMagic(bool carriedByThief)
    {
        var game = new Game(m_areas[1], 2);
        AdvanceTo(game, m_areas[1].Stops[^1]);
        for (var tick = 0; tick < 40; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        game.StartCamp();
        while (!game.Hero.IsFree && game.Camp.Ticks < 500)
            game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero && a.Kind != ActorKind.Campfire);
        game.Hero.Health = 16;
        game.Hero.X = game.Progress + 60;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        while (game.Potions < game.Character.PotionCapacity)
        {
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
            game.Tick(default);
        }
        var food = game.Add(carriedByThief ? ActorKind.Thief : ActorKind.Food, Moves.Thief,
            carriedByThief ? 0x3c : 0x7f, 99, game.Hero.X + 140, game.Hero.Z);
        food.FoodThief = carriedByThief;
        food.Items = 1;
        food.Facing = -1;
        var controller = new CodedController();
        Assert.That(controller.Read(game).X, Is.EqualTo(1), "Full magic must not stop a hungry hero seeking distant food.");
        for (var tick = 0; tick < 400 && game.Hero.Health == 16; tick++)
            game.Tick(controller.Read(game));
        Assert.That(game.Hero.Health, Is.EqualTo(32));
        Assert.That(game.Potions, Is.EqualTo(game.Character.PotionCapacity));
    }

    [TestCase(0, -1)]
    [TestCase(0, 1)]
    [TestCase(1, -1)]
    [TestCase(1, 1)]
    [TestCase(2, -1)]
    [TestCase(2, 1)]
    public void RiderlessMountWaitsThenRunsOffNearestEdge(int kind, int direction)
    {
        var game = new Game(m_areas[0], 2);
        game.Hero.MountCooldown = 10000;
        var beast = game.Add(new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[kind],
            kind == 0 ? Moves.Chicken : Moves.BlueDragon, 0x23, 99,
            game.Progress + (direction < 0 ? 100 : 230), game.Hero.Z);
        var startX = beast.X;
        var restingFrame = kind == 0 ? 203 : 637;
        Assert.That(beast.Animation.Current.Frame, Is.EqualTo(restingFrame), "Riderless beasts should use their seated sprite.");
        for (var tick = 0; tick < 299; tick++)
        {
            game.Tick(default);
            Assert.That(beast.Animation.Current.Frame, Is.EqualTo(restingFrame), "An unclaimed beast must stay seated while waiting.");
        }
        Assert.That(beast.X, Is.EqualTo(startX), "Leave time to remount before running away.");
        Assert.That(beast.IsFree, Is.True);
        game.Tick(default);
        Assert.That(beast.State, Is.EqualTo(FighterState.Fleeing));
        Assert.That(beast.Facing, Is.EqualTo(direction));
        Assert.That(beast.IsFree, Is.False, "A fleeing mount must not attract new riders.");
        for (var tick = 0; tick < 180 && !beast.IsGone; tick++)
            game.Tick(default);
        Assert.That(beast.IsGone, Is.True);
        Assert.That(game.Project(beast.X, beast.Z, beast.Y).X * direction, Is.GreaterThan(direction < 0 ? 64 : 384));
        Assert.That(game.Actors, Does.Not.Contain(beast));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void RemountingRestartsTheRiderlessTimeout(int kind)
    {
        var game = new Game(m_areas[0], 2);
        var beast = game.Add(new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[kind],
            kind == 0 ? Moves.Chicken : Moves.BlueDragon, 0x23, 99, game.Hero.X + 50, game.Hero.Z);
        for (var tick = 0; tick < 250; tick++)
            game.Tick(default);
        game.Hero.X = beast.X;
        game.Tick(default);
        Assert.That(game.Hero.Mount, Is.SameAs(beast));
        Assert.That(beast.UnmountedTicks, Is.Zero);
        for (var tick = 0; tick < 350; tick++)
            game.Tick(default);
        Assert.That(game.Hero.Mount, Is.SameAs(beast));
        game.Tick(new GameInput(Dismount: true));
        game.Hero.MountCooldown = 10000;
        Assert.That(beast.Rider, Is.Null);
        Assert.That(beast.Animation.Current.Frame, Is.EqualTo(kind == 0 ? 203 : 637), "Dismounting restores the seated pose.");
        for (var tick = 0; tick < 299; tick++)
            game.Tick(default);
        Assert.That(beast.State, Is.EqualTo(FighterState.Idle));
        game.Tick(default);
        Assert.That(beast.State, Is.EqualTo(FighterState.Fleeing));
    }

    [Test]
    public void RiddenBeastsRunAndBargeWithoutLosingTheirRider(
        [Range(0, 2)] int character, [Range(0, 2)] int mount)
    {
        var data = m_areas[0];
        var game = new Game(data, character, new Random(0));
        var hero = game.Hero;
        var kind = new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[mount];
        var beast = game.Add(kind, game.Character.RidingMoves(kind), 0x23, 99, hero.X, hero.Z);
        beast.Rider = hero;
        hero.Mount = beast;
        var startX = beast.X;
        for (var tick = 0; tick < 20; tick++)
            game.Tick(new GameInput(X: 1, Run: true));
        Assert.That(beast.X - startX, Is.GreaterThan(60));
        Assert.That(beast.Animation.Address, Is.EqualTo(beast.Moves.Run));
        Assert.That(data.Riders.ContainsKey(beast.Moves.Run), Is.True);
        Assert.That(game.LandingDust.Any(p => p.Running), Is.True);

        var enemies = new[] { 36, 56 }.Select(offset =>
            game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 99, beast.X + offset, beast.Z)).ToArray();
        foreach (var enemy in enemies)
            enemy.Cooldown = 10000;
        game.Tick(new GameInput(X: 1, Run: true, Attack: true));
        Assert.That(beast.State, Is.EqualTo(FighterState.Dashing));
        var chargeFrame = kind == ActorKind.Chicken ? 200 : 634;
        for (var tick = 0; tick < 35; tick++)
        {
            if (tick < 18)
            {
                Assert.That(beast.Animation.Address, Is.EqualTo(beast.Moves.Jump));
                Assert.That(beast.Animation.Current.Frame, Is.EqualTo(chargeFrame));
                Assert.That(beast.Animation.Index, Is.Zero, "Hold the paired bracing pose without advancing into the airborne frames.");
                Assert.That(data.Riders.ContainsKey(beast.Animation.Address), Is.True);
                Assert.That(beast.Y, Is.EqualTo(game.Ground(beast.X, beast.Z)), "A mounted barge stays grounded.");
            }
            game.Tick(default);
        }
        Assert.That(enemies.Select(a => a.Health), Is.EqualTo(new[] { 95, 95 }), "Each target takes one body impact.");
        Assert.That(beast.Rider, Is.SameAs(hero));
        Assert.That(hero.Mount, Is.SameAs(beast));
        Assert.That((hero.X, hero.Z, hero.Y), Is.EqualTo((beast.X, beast.Z, beast.Y)));
        Assert.That(beast.State, Is.EqualTo(FighterState.Idle));
        Assert.That(game.Actors.Any(a => a.Kind is ActorKind.Fireball or ActorKind.Flame), Is.False);
    }

    [Test]
    public void MountedJumpUsesBothAirbornePosesAndKeepsTheRider(
        [Range(0, 2)] int character, [Range(0, 2)] int mount)
    {
        var data = m_areas[0];
        var game = new Game(data, character);
        var hero = game.Hero;
        var kind = new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[mount];
        var beast = game.Add(kind, kind == ActorKind.Chicken ? Moves.Chicken : Moves.BlueDragon, 0x23, 99, hero.X, hero.Z);
        game.Tick(default);
        Assert.That(hero.Mount, Is.SameAs(beast));

        var poses = new HashSet<int>();
        var startX = beast.X;
        var startY = beast.Y;
        var highestY = startY;
        game.Tick(new GameInput(X: 1, Jump: true));
        for (var tick = 0; tick < 60 && beast.State == FighterState.Jumping; tick++)
        {
            Assert.That(beast.Animation.Address, Is.EqualTo(beast.Moves.Jump));
            Assert.That(data.Riders[beast.Moves.Jump].Length, Is.EqualTo(beast.Animation.Steps.Length));
            Assert.That(hero.Mount, Is.SameAs(beast));
            Assert.That((hero.X, hero.Z, hero.Y), Is.EqualTo((beast.X, beast.Z, beast.Y)));
            poses.Add(beast.Animation.Current.Frame);
            highestY = Math.Min(highestY, beast.Y);
            game.Tick(default);
        }

        Assert.That(poses, Does.Contain(kind == ActorKind.Chicken ? 0xc8 : 0x27a));
        Assert.That(poses, Does.Contain(kind == ActorKind.Chicken ? 0xc9 : 0x27b));
        Assert.That(highestY, Is.LessThan(startY - 20));
        Assert.That(beast.X, Is.GreaterThan(startX));
        Assert.That(beast.State, Is.EqualTo(FighterState.Idle));
        Assert.That(beast.Rider, Is.SameAs(hero));
        Assert.That(beast.Y, Is.EqualTo(game.Ground(beast.X, beast.Z)));
        Assert.That(game.LandingDust, Has.Count.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void MountedJumpClearsTheHighLedgeInTheWoods(int mount)
    {
        var game = new Game(m_areas[0], 2);
        AdvanceTo(game, 4800);
        var hero = game.Hero;
        hero.X = 4819;
        hero.Z = 4168;
        hero.Y = game.Ground(hero.X, hero.Z);
        var top = game.Ground(4832, hero.Z);
        Assert.That(hero.Y - top, Is.EqualTo(48));

        var kind = new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[mount];
        var beast = game.Add(kind, kind == ActorKind.Chicken ? Moves.Chicken : Moves.BlueDragon, 0x23, 99, hero.X, hero.Z);
        game.Tick(default);
        Assert.That(hero.Mount, Is.SameAs(beast));
        game.Tick(new GameInput(X: 1, Jump: true));
        for (var tick = 0; tick < 60 && beast.State == FighterState.Jumping; tick++)
            game.Tick(new GameInput(X: 1));

        Assert.That(beast.State, Is.EqualTo(FighterState.Idle));
        Assert.That(beast.X, Is.GreaterThanOrEqualTo(4832));
        Assert.That(beast.Y, Is.EqualTo(top));
        Assert.That(hero.Mount, Is.SameAs(beast));
    }

    [Test]
    public void MountedMagicSpendsPotionsAndKeepsTheRiderSeated(
        [Range(0, 2)] int character, [Range(0, 2)] int mount)
    {
        var game = new Game(m_areas[0], character);
        var hero = game.Hero;
        var kind = new[] { ActorKind.Chicken, ActorKind.BlueDragon, ActorKind.RedDragon }[mount];
        var beast = game.Add(kind, kind == ActorKind.Chicken ? Moves.Chicken : Moves.BlueDragon, 0x23, 99, hero.X, hero.Z);
        game.Tick(default);
        Assert.That(hero.Mount, Is.SameAs(beast));
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 28, hero.Z);
        enemy.Cooldown = 0;
        game.Tick(new GameInput(Magic: true));
        Assert.That((game.MagicTick, game.MagicLevel, game.Potions), Is.EqualTo((0, 1, 0)));
        Assert.That(hero.State, Is.EqualTo(FighterState.Magic));
        Assert.That(beast.Animation.Address, Is.EqualTo(beast.Moves.Idle));

        for (var tick = 0; tick < 500 && game.MagicTick >= 0; tick++)
            game.Tick(default);
        Assert.That(game.MagicTick, Is.EqualTo(-1));
        Assert.That(enemy.Health, Is.EqualTo(12));
        Assert.That(hero.Health, Is.EqualTo(48));
        Assert.That(hero.Mount, Is.SameAs(beast));
        game.Tick(new GameInput(X: 1));
        Assert.That(beast.State, Is.EqualTo(FighterState.Walking));
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public void EnemyCanDamageHeroAndInterruptGrabOrOverheadThrow(int character, bool overhead)
    {
        var data = m_areas[0];
        var game = new Game(data, character);
        var hero = game.Hero;
        var victim = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 16, hero.Z);
        victim.Cooldown = 10000;
        game.Tick(new GameInput(1, Attack: true));
        Assert.That(hero.State, Is.EqualTo(FighterState.Grabbing));
        if (overhead)
        {
            for (var tick = 0; tick < 200 && !(hero.State == FighterState.Throwing && hero.Animation.Index == 3); tick++)
                game.Tick(default);
            Assert.That(hero.State, Is.EqualTo(FighterState.Throwing));
            Assert.That(victim.Y, Is.LessThan(hero.Y));
        }
        Assert.That(victim.HeldBy, Is.SameAs(hero));
        var attacker = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X - 24, hero.Z);
        attacker.Facing = 1;
        attacker.SetState(FighterState.Attacking, attacker.Moves.Attacks[0], true);
        attacker.Animation.Seek(data.Attacks[attacker.Animation.Address][2]);
        game.Tick(default);
        Assert.That(hero.Health, Is.EqualTo(46), "An enemy striking from behind must still damage the busy hero.");
        Assert.That(hero.State, Is.EqualTo(FighterState.Hurt));
        Assert.That(hero.Victim, Is.Null);
        Assert.That(victim.HeldBy, Is.Null);
        Assert.That(victim.State, Is.Not.EqualTo(FighterState.Held));
        Assert.That(game.Throws, Is.Zero, "An interrupted throw must not award a completed throw.");
        game.Actors.Remove(attacker);
        for (var tick = 0; tick < 40; tick++)
            game.Tick(default);
        Assert.That(victim.Y, Is.EqualTo(game.Ground(victim.X, victim.Z)), "The released victim must fall back to the floor.");
    }

    [TestCase(false, 40)]
    [TestCase(false, 140)]
    [TestCase(true, 40)]
    [TestCase(true, 140)]
    public void AutoplayDodgesRecoveryLightningOnFootAndMounted(bool mounted, int distance)
    {
        var game = new Game(m_areas[0], 2);
        typeof(Game).GetField("m_spawnIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(game, m_areas[0].Spawns.Length);
        game.Hero.X = game.Progress + 170;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        Fighter beast = null;
        if (mounted)
        {
            beast = game.Add(ActorKind.BlueDragon, Moves.BlueDragon, 0x23, 99, game.Hero.X, game.Hero.Z);
            game.Tick(default);
            Assert.That(game.Hero.Mount, Is.SameAs(beast));
        }
        var boss = game.Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 64, game.Hero.X - distance, game.Hero.Z);
        boss.SetState(FighterState.GettingUp, boss.Moves.GetUp);
        var controller = new CodedController();
        Fighter bolt = null;
        for (var tick = 0; tick < 240; tick++)
        {
            game.Tick(controller.Read(game));
            bolt ??= game.Actors.FirstOrDefault(a => a.Kind == ActorKind.Lightning);
            Assert.That(game.Hero.Health, Is.EqualTo(48), $"Lightning hit at tick {tick}");
            if (mounted)
                Assert.That(game.Hero.Mount, Is.SameAs(beast), $"Lightning dismounted the hero at tick {tick}");
            if (bolt != null && (bolt.IsGone || (bolt.X - game.Hero.X) * bolt.Facing > 40))
                break;
        }
        Assert.That(bolt, Is.Not.Null, "The recovery cast must actually release a bolt.");
        Assert.That(bolt.IsGone || (bolt.X - game.Hero.X) * bolt.Facing > 40, Is.True,
            "The bolt must pass safely, rather than the test finishing before impact.");
    }

    [TestCase(-1, false)]
    [TestCase(1, false)]
    [TestCase(-1, true)]
    [TestCase(1, true)]
    public void DeathAdderRecoveryLaunchesGroundLightningThatCanBeJumped(int direction, bool jump)
    {
        var data = m_areas[0];
        var game = new Game(data, 2);
        game.Hero.X = game.Progress + 150;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        var boss = game.Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 64, game.Hero.X - direction * 140, game.Hero.Z);
        boss.SetState(FighterState.Down, boss.Moves.Fly);
        var descentFrames = new HashSet<int>();
        for (var tick = 0; tick < 140 && !game.Actors.Any(a => a.Kind == ActorKind.Lightning); tick++)
        {
            game.Tick(default);
            foreach (var charge in game.Actors.Where(a => a.Kind == ActorKind.LightningCharge))
                descentFrames.Add(charge.Animation.Current.Frame);
        }
        Assert.That(descentFrames, Is.SupersetOf(new[] { 548, 552, 555 }));
        var bolt = game.Actors.Single(a => a.Kind == ActorKind.Lightning);
        var firstX = bolt.X;
        Assert.That(boss.Animation.Address, Is.EqualTo(Moves.DeathAdderCast));
        Assert.That(game.TickCount, Is.GreaterThan(60), "Recover and show the casting wind-up before releasing lightning.");
        Assert.That(bolt.Owner, Is.SameAs(boss));
        Assert.That(bolt.Facing, Is.EqualTo(direction));
        Assert.That(bolt.Y, Is.EqualTo(game.Ground(bolt.X, bolt.Z)));
        if (direction == -1 && !jump)
        {
            var screen = new PixelScreenData(320, 224, data.Palette);
            new ArcadeRenderer(data).Draw(screen, game);
            using var bitmap = new SKBitmap(320, 224);
            bitmap.Pixels = screen.Pixels.Select(p => new SKColor(data.Palette[p].R, data.Palette[p].G, data.Palette[p].B)).ToArray();
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, "goldenaxe-adder-lightning.png"));
            encoded.SaveTo(file);
        }
        if (jump)
            game.Tick(new GameInput(Jump: true));
        for (var tick = 0; tick < 80 && !bolt.IsGone && (bolt.X - game.Hero.X) * direction < 40; tick++)
            game.Tick(default);
        Assert.That(game.Hero.Health, Is.EqualTo(jump ? 48 : 32));
        if (jump)
        {
            Assert.That((bolt.X - game.Hero.X) * direction, Is.GreaterThanOrEqualTo(40), "The bolt must pass beneath the jumping hero.");
            boss.Health = 0;
            game.Tick(default);
        }
        Assert.That(bolt.IsGone, Is.True, "A bolt ends on impact or when its owner dies.");
        Assert.That(game.LightningTrail.Count, Is.GreaterThan(1));
        Assert.That(game.LightningTrail[0].X, Is.EqualTo(firstX));
        Assert.That(game.LightningTrail[1].X - firstX, Is.EqualTo(direction * 16));
        for (var tick = 0; tick < Game.LightningTrailTicks; tick++)
            game.Tick(default);
        Assert.That(game.LightningTrail, Is.Empty);
    }

    [Test]
    public void DefeatedDeathAdderDoesNotCastRecoveryLightning()
    {
        var game = new Game(m_areas[0], 2);
        var boss = game.Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 0, game.Hero.X + 100, game.Hero.Z);
        boss.SetState(FighterState.Down, boss.Moves.Fly);
        for (var tick = 0; tick < 140; tick++)
            game.Tick(default);
        Assert.That(boss.IsGone, Is.True);
        Assert.That(game.Actors.Any(a => a.Kind == ActorKind.Lightning), Is.False);
    }

    [Test]
    public void DeathAdderAimsAtThePlayersSideAndDepthWhenTheBoltLands()
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        hero.X = game.Progress + 150;
        hero.Z = 4224;
        hero.Y = game.Ground(hero.X, hero.Z);
        var boss = game.Add(ActorKind.DeathAdder, Moves.DeathAdder, 0x50, 64, game.Progress + 118, hero.Z);
        boss.SetState(FighterState.Down, boss.Moves.Fly);
        for (var tick = 0; tick < 120 && !game.Actors.Any(a => a.Kind == ActorKind.LightningCharge); tick++)
            game.Tick(default);
        Assert.That(game.Actors.Single(a => a.Kind == ActorKind.LightningCharge).Facing, Is.EqualTo(1));

        hero.X = game.Progress + 16;
        hero.Z = 4248;
        hero.Y = game.Ground(hero.X, hero.Z);
        for (var tick = 0; tick < 45 && !game.Actors.Any(a => a.Kind == ActorKind.Lightning); tick++)
            game.Tick(default);
        var bolt = game.Actors.Single(a => a.Kind == ActorKind.Lightning);
        Assert.That(bolt.Facing, Is.EqualTo(-1));
        Assert.That(bolt.X, Is.GreaterThan(hero.X));
        for (var tick = 0; tick < 48 && !bolt.IsGone; tick++)
            game.Tick(default);
        Assert.That(hero.Health, Is.EqualTo(32));
        Assert.That(Math.Abs(hero.Z - bolt.Z), Is.LessThan(7));
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public void EnemySeeksFreeMountAndUsesItsOwnRiderScripts(int kind, bool dragon)
    {
        var data = m_areas[0];
        var game = new Game(data);
        var enemy = new Fighter(1000, data, new[] { ActorKind.Heninger, ActorKind.Longmoan, ActorKind.Amazon }[kind],
            new[] { Moves.Heninger, Moves.Longmoan, Moves.Amazon }[kind], 0x18, 16)
        { X = game.Hero.X + 110, Z = game.Hero.Z, Y = game.Hero.Y };
        var beast = new Fighter(1001, data, dragon ? ActorKind.BlueDragon : ActorKind.Chicken,
            dragon ? Moves.BlueDragon : Moves.Chicken, dragon ? 0x43 : 0x23, 99)
        { X = enemy.X + 40, Z = enemy.Z, Y = enemy.Y };
        game.Actors.Add(enemy);
        game.Actors.Add(beast);
        var waitingTicks = 0;
        for (var tick = 0; tick < 160 && enemy.Mount == null; tick++)
        {
            game.Tick(default);
            if (enemy.PendingMount == beast)
            {
                waitingTicks++;
                Assert.That(beast.Rider, Is.Null, "The beast must remain available during the mounting delay.");
            }
        }
        Assert.That(waitingTicks, Is.EqualTo(59));
        Assert.That(enemy.Mount, Is.SameAs(beast));
        Assert.That(beast.Rider, Is.SameAs(enemy));
        var expected = dragon
            ? new[] { Moves.HeningerDragon, Moves.LongmoanDragon, Moves.AmazonDragon }[kind]
            : new[] { Moves.EnemyChicken, Moves.LongmoanChicken, Moves.AmazonChicken }[kind];
        Assert.That(beast.Moves, Is.SameAs(expected));
        Assert.That(data.Riders.ContainsKey(beast.Animation.Address), Is.True);
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    public void MagicVictimsCollapseThenRecoverOrDie(int character, bool lethal)
    {
        var game = new Game(m_areas[0], character, new Random(0));
        var enemies = new[]
        {
            (ActorKind.Heninger, Moves.Heninger), (ActorKind.Heninger, Moves.StrongHeninger),
            (ActorKind.Longmoan, Moves.Longmoan), (ActorKind.Longmoan, Moves.FastLongmoan),
            (ActorKind.Longmoan, Moves.StrongLongmoan), (ActorKind.Amazon, Moves.Amazon),
            (ActorKind.BadBrother, Moves.Brother), (ActorKind.Knight, Moves.Knight),
            (ActorKind.Skeleton, Moves.Skeleton), (ActorKind.DeathAdder, Moves.DeathAdder)
        }.Select(pair => game.Add(pair.Item1, pair.Item2, 0x18, lethal ? 4 : 32, game.Hero.X + 90, game.Hero.Z)).ToArray();
        var beast = game.Add(ActorKind.Chicken, Moves.EnemyChicken, 0x23, 99, enemies[0].X, enemies[0].Z);
        beast.Rider = enemies[0];
        enemies[0].Mount = beast;
        game.Tick(new GameInput(Magic: true));
        for (var tick = 0; tick < 1000 && game.MagicTick >= 0; tick++)
            game.Tick(default);
        Assert.That(game.MagicTick, Is.EqualTo(-1));
        Assert.That(beast.Rider, Is.Null);
        foreach (var enemy in enemies)
        {
            Assert.That(enemy.Health, Is.EqualTo(lethal ? 0 : 28), "Magic should deal its damage once.");
            Assert.That(enemy.Mount, Is.Null);
            Assert.That(enemy.State, Is.EqualTo(FighterState.Collapsing));
            Assert.That(enemy.Vulnerable, Is.False);
            Assert.That(enemy.Animation.Current.Frame,
                Is.Not.EqualTo(m_areas[0].Animations[enemy.Moves.GetUp][1].Frame),
                "Magic must not show the prone hold before playing the fall.");
        }
        var landedAt = new Dictionary<Fighter, int>();
        var recovered = new HashSet<Fighter>();
        var recoveryTimes = new HashSet<int>();
        for (var tick = 1; tick <= 120 && recovered.Count < enemies.Length; tick++)
        {
            foreach (var enemy in enemies)
                enemy.Cooldown = 10000;
            game.Tick(default);
            foreach (var enemy in enemies.Where(enemy => !recovered.Contains(enemy)))
            {
                if (enemy.State == FighterState.Collapsing)
                {
                    Assert.That(landedAt.ContainsKey(enemy), Is.False, "A victim must never replay its fall.");
                    continue;
                }
                if (enemy.State == FighterState.Down)
                {
                    landedAt.TryAdd(enemy, tick);
                    Assert.That((enemy.Animation.Address, enemy.Animation.Index), Is.EqualTo((enemy.Moves.GetUp, 1)));
                    continue;
                }
                Assert.That(landedAt.ContainsKey(enemy), Is.True, "Every victim must land before recovering or petrifying.");
                Assert.That(enemy.State, Is.EqualTo(lethal ? FighterState.Petrified : FighterState.GettingUp));
                var duration = tick - landedAt[enemy];
                Assert.That(duration, Is.InRange(lethal ? 36 : 34, lethal ? 36 : 38));
                recoveryTimes.Add(duration);
                recovered.Add(enemy);
            }
        }
        Assert.That(recovered, Has.Count.EqualTo(enemies.Length));
        if (!lethal)
            Assert.That(recoveryTimes.Count, Is.GreaterThan(1));
        else
        {
            Assert.That(game.StoneBodies, Is.EquivalentTo(enemies));
            Assert.That(game.Actors.Intersect(enemies), Is.Empty, "Bodies must not block stage progression or attract attacks.");
            var frames = enemies.Select(a => a.Animation.Current.Frame).ToArray();
            for (var tick = 0; tick < 120; tick++)
                game.Tick(default);
            Assert.That(game.StoneBodies, Is.EquivalentTo(enemies));
            Assert.That(enemies.Select(a => a.Animation.Current.Frame), Is.EqualTo(frames));
        }
    }

    [Test]
    public void FullGiliusMagicKillsTheAmazonButNotTheTurtleVillageSkeleton()
    {
        var data = m_areas[1];
        var game = new Game(data, character: 2);
        var hero = game.Hero;
        for (var i = game.Potions; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, hero.X, hero.Z);
        game.Tick(default);
        Assert.That(game.Potions, Is.EqualTo(4));

        var wave = data.Spawns.Where(spawn => spawn.Trigger == data.Stops[^1] && spawn.Routine is 0x229c8 or 0x2536e);
        foreach (var spawn in wave)
            game.Spawn(spawn with { X = (int)hero.X + 70, Z = (int)hero.Z });
        var amazon = game.Actors.Single(actor => actor.Kind == ActorKind.Amazon);
        var skeleton = game.Actors.Single(actor => actor.Kind == ActorKind.Skeleton);
        Assert.That(amazon.Health, Is.EqualTo(16));
        Assert.That(skeleton.Health, Is.EqualTo(24));

        game.Tick(new GameInput(Magic: true));
        Assert.That(game.MagicShakeStart, Is.EqualTo(40), "The recorded casting potions occupy ticks 0 through 39.");
        Assert.That(MagicScreenShake.Sample(game.MagicTick - game.MagicShakeStart,
            game.MagicDuration - game.MagicShakeStart, game.MagicLevel), Is.EqualTo((0, 0)));
        for (var tick = 0; tick < 500 && game.MagicTick >= 0; tick++)
            game.Tick(default);

        Assert.That(game.MagicTick, Is.EqualTo(-1));
        Assert.That(amazon.Health, Is.Zero);
        Assert.That(skeleton.Health, Is.EqualTo(8));
        Assert.That(skeleton.State, Is.EqualTo(FighterState.Collapsing));
    }

    [TestCase(0, new int[] { 1, 1, 2, 2, 3, 4 })]
    [TestCase(1, new int[] { 1, 1, 1, 1, 2, 3, 4, 5, 6 })]
    [TestCase(2, new int[] { 1, 2, 2, 3 })]
    public void PotionSlotsUnlockTheCorrectSpellTiers(int character, int[] tiers)
    {
        for (var potions = 1; potions <= tiers.Length + 1; potions++)
        {
            var game = new Game(m_areas[0], character);
            for (var i = 1; i < potions; i++)
            {
                game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
                game.Tick(default);
            }

            var collected = Math.Min(potions, tiers.Length);
            Assert.That(game.Potions, Is.EqualTo(collected));
            game.Tick(new GameInput(Magic: true));
            Assert.That(game.MagicLevel, Is.EqualTo(tiers[collected - 1]));
            Assert.That(game.Potions, Is.Zero);
            Assert.That(game.ActiveMagicEffects, Is.SameAs(game.MagicEffects[Math.Min(collected, game.MagicEffects.Length) - 1]));

            for (var tick = 0; tick <= game.ActiveMagicEffects.Length && game.MagicTick >= 0; tick++)
                game.Tick(default);
            Assert.That(game.MagicTick, Is.EqualTo(-1));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void SpawnFacesRightAndGivesOnePotionButAreaTransitionPreservesInventory(int character)
    {
        var game = new Game(m_areas[0], character);
        Assert.That(game.Potions, Is.EqualTo(1));
        Assert.That(game.Hero.Facing, Is.EqualTo(1));
        game.Tick(new GameInput(Magic: true));
        Assert.That(game.Potions, Is.Zero);
        Assert.That(game.MagicLevel, Is.EqualTo(1));

        game = game.ContinueIn(m_areas[1]);
        Assert.That(game.Potions, Is.Zero, "Changing areas must not grant another spawn potion.");
        Assert.That(game.Hero.Facing, Is.EqualTo(1));
        game.Hero.Facing = -1;
        game.Hero.Health = 0;
        game.Hero.SetState(FighterState.Down);
        for (var tick = 0; tick < 140; tick++)
            game.Tick(default);
        Assert.That(game.Hero.Health, Is.EqualTo(48));
        Assert.That(game.Lives, Is.EqualTo(1));
        Assert.That(game.Potions, Is.EqualTo(1));
        Assert.That(game.Hero.Facing, Is.EqualTo(1));
    }

    [TestCase(2, -7, -806, 218)]
    [TestCase(2, -16, -814, 218)]
    [TestCase(1, -200, -771, 221)]
    public void ScrollingPastUncapturedEdgeTilesDoesNotLeaveBlackCorners(int area, int cameraX, int cameraY, int top)
    {
        var data = m_areas[area];
        var game = new Game(data, 2);
        typeof(Game).GetProperty(nameof(Game.CameraX)).SetValue(game, (double)cameraX);
        typeof(Game).GetProperty(nameof(Game.CameraY)).SetValue(game, (double)cameraY);
        var screen = new PixelScreenData(320, 224, data.Palette);
        new ArcadeRenderer(data).Draw(screen, game);
        var corner = Enumerable.Range(top, 224 - top).SelectMany(y => screen.Pixels.Skip(y * 320).Take(8));
        Assert.That(corner.Count(p => p != data.Black), Is.GreaterThan((224 - top) * 6), "The partially visible edge tile must not be an empty black square.");
    }

    [Test]
    public void DeathConsumesLivesAndFadeDoesNotBlackenRestartedArtwork()
    {
        var data = m_areas[1];
        var originalColors = data.Palette.ToArray();
        var screen = new PixelScreenData(320, 224, data.Palette);
        var game = new Game(data, 2);
        for (var death = 0; death < 3; death++)
        {
            game.Hero.Health = 0;
            game.Hero.SetState(FighterState.Down);
            for (var tick = 0; tick < 140; tick++)
                game.Tick(default);
            Assert.That(game.Lives, Is.EqualTo(Math.Max(0, 1 - death)));
            Assert.That(game.GameOver, Is.EqualTo(death == 2));
        }
        for (var restart = 0; restart < 3; restart++)
        {
            screen.SetPalette(Enumerable.Repeat(Rgb.Black, 256).ToArray());
            Assert.That(data.Palette, Is.EqualTo(originalColors), "The display fade must leave the source palette intact.");
            game = new Game(data, 2);
            screen.SetPalette(data.Palette);
            new ArcadeRenderer(data).Draw(screen, game);
            Assert.That(screen.Pixels.Count(p => screen.Palette[p] != Rgb.Black), Is.GreaterThan(1000));
        }
    }

    [TestCase(1, 0x2536e)]
    [TestCase(4, 0x2538a)]
    [TestCase(4, 0x25352)]
    [TestCase(3, 0x23bda)]
    public void SkeletonDoesNotEmergeAwayFromAuthoredEntrances(int area, int routine)
    {
        var game = new Game(m_areas[area]);
        game.Spawn(new SpawnDefinition((int)game.Progress, (int)game.Hero.X, (int)game.Hero.Z, 0, routine));
        var skeleton = game.Actors.Single(a => a.Kind == ActorKind.Skeleton);
        Assert.That(skeleton.State, Is.EqualTo(FighterState.Idle));
        Assert.That(skeleton.Vulnerable, Is.True);
    }

    [TestCase(4096)]
    [TestCase(4149)]
    [TestCase(4297)]
    public void CastleSkeletonRisesAndLeavesAnOpenCrack(int trigger)
    {
        var data = m_areas[4];
        var spawn = data.Spawns.Single(s => s.Routine == 0x2538a && s.Trigger == trigger);
        var game = new Game(data);
        AdvanceTo(game, trigger);
        game.Hero.X = game.Progress + 65;
        game.Hero.Invulnerable = 10000;
        Assert.That(game.SkeletonMounds, Is.Empty, "The castle cracks appear as the skeleton rises.");
        game.Tick(default);
        var skeleton = game.Actors.Single(a => a.Kind == ActorKind.Skeleton && a.X == spawn.X && a.Z == spawn.Z);
        Assert.That(skeleton.State, Is.EqualTo(FighterState.Emerging));
        Assert.That(skeleton.Animation.Address, Is.EqualTo(SkeletonEmergence.CastleAnimation));
        Assert.That(skeleton.Animation.Current.Frame, Is.EqualTo(846));
        for (var tick = 1; tick < SkeletonEmergence.Duration; tick++)
            game.Tick(default);
        Assert.That(skeleton.State, Is.EqualTo(FighterState.Idle));
        Assert.That(game.OpenedSkeletonMounds, Does.Contain(spawn));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void WeaponTrailsFollowTheStrikeAndMirrorWithTheHero(int character)
    {
        var data = m_areas[0];
        var hero = new Game(data, character).Hero;
        hero.SetState(FighterState.Attacking, hero.Moves.Attacks[0], attack: true);
        Assert.That(WeaponSwipe.Sample(hero, data), Is.Null, "No trail while the weapon is held ready.");
        var seen = 0;
        for (var tick = 0; tick < 50; tick++)
        {
            hero.Facing = 1;
            var right = WeaponSwipe.Sample(hero, data);
            hero.Facing = -1;
            var left = WeaponSwipe.Sample(hero, data);
            Assert.That(left.HasValue, Is.EqualTo(right.HasValue));
            if (right is { } r && left is { } l)
            {
                seen++;
                Assert.That(l.Frame, Is.EqualTo(r.Frame));
                Assert.That(l.Dx, Is.EqualTo(-r.Dx));
                Assert.That(l.Dy, Is.EqualTo(r.Dy));
                Assert.That(l.Flip, Is.Not.EqualTo(r.Flip));
            }
            if (hero.Animation.Tick())
                break;
        }
        Assert.That(seen, Is.InRange(6, 10), "A brief sweep, not a persistent weapon glow.");
        hero.SetState(FighterState.Hurt, hero.Moves.Hurt);
        Assert.That(WeaponSwipe.Sample(hero, data), Is.Null, "Interrupted attacks lose their trail immediately.");
        hero.SetState(FighterState.Attacking, hero.Moves.Attacks[3], attack: true);
        hero.Animation.Seek(data.Attacks[hero.Animation.Address][2]);
        Assert.That(WeaponSwipe.Sample(hero, data), Is.Null, "Kicks have no weapon sweep.");
    }

    [TestCase(0, 65)]
    [TestCase(1, 359)]
    [TestCase(2, 934)]
    public void KnockedDownHeroesLieFlatThenSitUpAndStand(int character, int flatFrame)
    {
        var game = new Game(m_areas[0], character);
        var hero = game.Hero;
        hero.Invulnerable = 10000;
        hero.SetState(FighterState.Flying, hero.Moves.Fly);
        hero.Y -= 1;
        hero.Vy = 1;
        game.Tick(default);
        Assert.That(hero.State, Is.EqualTo(FighterState.Down));
        for (var tick = 0; tick < 36; tick++)
        {
            Assert.That(hero.Animation.Current.Frame, Is.EqualTo(flatFrame), "Hold the original flat-on-back artwork on the floor.");
            game.Tick(default);
        }
        Assert.That(hero.State, Is.EqualTo(FighterState.GettingUp));
        for (var tick = 0; tick < 24; tick++)
        {
            Assert.That(hero.Animation.Current.Frame, Is.Not.EqualTo(flatFrame), "Recovery must not loop back into the lying pose.");
            game.Tick(default);
        }
        Assert.That(hero.State, Is.EqualTo(FighterState.Idle));
        Assert.That(hero.Animation.Address, Is.EqualTo(hero.Moves.Idle));
    }

    [Test]
    public void SkeletonEmergesBeforeEnteringCombat()
    {
        var data = m_areas[3];
        var game = new Game(data);
        var mound = data.Spawns.First(s => s.Routine == 0x23bda);
        AdvanceTo(game, mound.Trigger);
        Assert.That(game.SkeletonMounds, Does.Contain(mound), "Show the undisturbed mound before its spawn triggers.");
        game.Hero.X = game.Progress + 60;
        game.Tick(default);
        Assert.That(game.SkeletonMounds, Does.Not.Contain(mound), "The active entrance replaces the undisturbed mound.");
        var skeleton = game.Actors.Single(a => a.Kind == ActorKind.Skeleton);
        var origin = (skeleton.X, skeleton.Z, skeleton.Y);
        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(320, 224, data.Palette);
        using var contactSheet = new SKBitmap(1280, 448);
        var sample = 0;
        game.Hero.X -= 48;
        game.Hero.Invulnerable = 10000;
        for (var tick = 1; tick <= 48; tick++)
        {
            if (tick < 48)
            {
                Assert.That(skeleton.State, Is.EqualTo(FighterState.Emerging));
                Assert.That(skeleton.Vulnerable, Is.False);
                Assert.That((skeleton.X, skeleton.Z, skeleton.Y), Is.EqualTo(origin));
                var expectedFrames = new[] { 846, 847, 848, 849, 850, 780 };
                Assert.That(skeleton.Animation.Current.Frame, Is.EqualTo(expectedFrames[tick / 8]));
                Assert.That(SkeletonEmergence.GroundFrame(tick), Is.EqualTo(853 + Math.Min(4, tick / 8)));
            }
            if (tick is 1 or 8 or 16 or 24 or 32 or 40 or 47 or 48)
            {
                renderer.Draw(screen, game);
                var pixels = new int[320 * 224];
                screen.CopyToBgra(pixels);
                for (var y = 0; y < 224; y++)
                for (var x = 0; x < 320; x++)
                    contactSheet.SetPixel(sample % 4 * 320 + x, sample / 4 * 224 + y, new SKColor((uint)pixels[y * 320 + x]));
                sample++;
            }
            if (tick < 48)
                game.Tick(default);
        }
        Assert.That(skeleton.State, Is.EqualTo(FighterState.Idle));
        Assert.That(skeleton.Vulnerable, Is.True);
        Assert.That(skeleton.Animation.Address, Is.EqualTo(Moves.Skeleton.Idle));
        Assert.That(skeleton.Palette, Is.EqualTo(0x6f));
        Assert.That(game.OpenedSkeletonMounds, Does.Contain(mound));
        Assert.That(game.LandingDust.Count(p => p.Heavy && p.X == skeleton.X), Is.EqualTo(2));
        game.Actors.Remove(skeleton);
        Assert.That(game.OpenedSkeletonMounds, Does.Contain(mound), "The opened ground outlives the skeleton.");
        using var image = SKImage.FromBitmap(contactSheet);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, "goldenaxe-skeleton-emergence.png"));
        encoded.SaveTo(file);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ControllerWaitsForScrollingToUnlockThenGoPromptFlashes(bool mounted)
    {
        var data = m_areas[0];
        var game = new Game(data);
        for (var tick = 0; tick < 200 && game.Progress < data.Stops[0]; tick++)
        {
            game.Hero.X = game.Progress + 180;
            game.Hero.Invulnerable = 10000;
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.EqualTo(data.Stops[0]));
        Assert.That(game.CanAdvance, Is.False);
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero);
        var body = game.Hero;
        if (mounted)
        {
            body = game.Add(ActorKind.Chicken, game.Character.MountedMoves, 0x23, 99, game.Hero.X, game.Hero.Z);
            body.Rider = game.Hero;
            game.Hero.Mount = body;
        }
        body.X = game.Progress + 305;
        body.Vx = 4;
        body.SetState(FighterState.Walking, body.Moves.Run);
        var waitingX = body.X;
        var controller = new CodedController();
        for (var tick = 0; tick < 35 && !game.CanAdvance; tick++)
        {
            var input = controller.Read(game);
            Assert.That(input, Is.EqualTo(default(GameInput)), "Wait during the wave-clear delay instead of running into the edge.");
            game.Tick(input);
            Assert.That(body.X, Is.EqualTo(waitingX));
            Assert.That(body.State, Is.EqualTo(FighterState.Idle));
            Assert.That(body.Vx, Is.Zero);
        }
        Assert.That(game.CanAdvance, Is.True);
        Assert.That(controller.Read(game).X, Is.EqualTo(1), "Resume advancing when the route unlocks.");
        // Stay behind the scrolling threshold so the next wave cannot hide the prompt.
        body.X = game.Progress + 80;
        while (game.TickCount % 60 != 0)
            game.Tick(default);
        Assert.That(game.CanAdvance, Is.True, "Keep the route clear while checking the prompt.");
        var screen = new PixelScreenData(320, 224, data.Palette);
        var renderer = new ArcadeRenderer(data);
        var prompt = data.Frames[646];
        byte[] PromptPixels() => Enumerable.Range(48, prompt.Height).SelectMany(y => screen.Pixels.Skip(y * 320 + 248).Take(prompt.Width)).ToArray();
        renderer.Draw(screen, game);
        var visible = PromptPixels();
        for (var y = 0; y < prompt.Height; y++)
        for (var x = 0; x < prompt.Width; x++)
        {
            var pen = data.SpritePens[(prompt.Y + y) * data.SpriteStride + prompt.X + x];
            if (pen is > 0 and < 15)
                Assert.That(visible[y * prompt.Width + x], Is.EqualTo(data.SpritePalette[0x40][pen]),
                    "Draw the original GO! artwork at native size, including its lettering and sword.");
        }
        for (var tick = 0; tick < 30; tick++)
            game.Tick(default);
        renderer.Draw(screen, game);
        Assert.That(PromptPixels(), Is.Not.EqualTo(visible));
    }

    [Test]
    public void ForestReinforcementsHoldUntilTheFirstFightersAreCleared()
    {
        var data = m_areas[0];
        var game = new Game(data);
        AdvanceTo(game, 4173);
        game.Tick(default);
        Assert.That(game.Actors.Single(actor => actor.Kind == ActorKind.Heninger).Health, Is.EqualTo(8));

        for (var tick = 0; tick < 100 && game.Progress < data.Stops[0]; tick++)
        {
            game.Hero.X = game.Progress + 180;
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.EqualTo(data.Stops[0]));
        game.Tick(default);
        var reinforcements = game.Actors.Where(actor => actor.IsEnemy && actor.State == FighterState.Sleeping).ToArray();
        Assert.That(reinforcements, Has.Length.EqualTo(2));
        Assert.That(reinforcements.Any(actor => actor.Moves == Moves.FastLongmoan), Is.True);

        foreach (var actor in game.Actors.Where(actor => actor.IsEnemy && actor.State != FighterState.Sleeping))
            actor.SetState(FighterState.Gone);
        game.Tick(default);

        Assert.That(reinforcements.All(actor => actor.IsFree), Is.True);
    }

    [Test]
    public void StageFiveBadBrothersEnterOneAtATimeAndHoldTheScrollStop()
    {
        var game = new Game(m_areas[2]);
        AdvanceTo(game, 4910);
        game.Hero.X = game.Progress + 180;
        game.Hero.Z = 4192;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        game.Tick(default);
        var waveTick = game.TickCount;
        const int firstDelay = 64, secondDelay = 512;
        while (game.TickCount < waveTick + firstDelay)
        {
            Assert.That(game.CanAdvance, Is.False);
            Assert.That(game.Progress, Is.EqualTo(4910));
            Assert.That(game.Actors.Any(a => a.Kind == ActorKind.BadBrother), Is.False);
            game.Tick(default);
        }
        var first = game.Actors.Single(a => a.Kind == ActorKind.BadBrother);
        Assert.That(first.State, Is.EqualTo(FighterState.Entering));
        Assert.That(first.Vulnerable, Is.False);

        for (var tick = 0; tick < 65; tick++)
            game.Tick(default);
        Assert.That(first.State, Is.EqualTo(FighterState.Chuckling));
        Assert.That(first.X, Is.LessThan(5136));
        Assert.That(first.Z, Is.GreaterThan(4168));

        // Defeating the first brother early must not unlock the scroll stop.
        first.SetState(FighterState.Gone);
        game.Hero.X = game.Progress + 180;
        while (game.TickCount < waveTick + secondDelay - 1)
        {
            game.Tick(default);
            Assert.That(game.CanAdvance, Is.False);
            Assert.That(game.Progress, Is.EqualTo(4910));
        }
        Assert.That(game.Actors.Any(a => a.Kind == ActorKind.BadBrother), Is.False);
        game.Tick(default);
        var second = game.Actors.Single(a => a.Kind == ActorKind.BadBrother);
        Assert.That(second.State, Is.EqualTo(FighterState.Entering));
        Assert.That(second.X, Is.EqualTo(5136));
    }

    [Test]
    public void BadBrotherChuckleHoldsPositionAndCanRecurDuringCombat()
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        hero.Z = 4224;
        hero.Y = game.Ground(hero.X, hero.Z);
        game.Spawn(new SpawnDefinition((int)game.Progress, (int)hero.X + 80, (int)hero.Z, 0, 0x282d4));
        var brother = game.Actors.Single(a => a.Kind == ActorKind.BadBrother);
        var start = (brother.X, brother.Z);
        var frames = new HashSet<int>();
        for (var tick = 0; tick < 100; tick++)
        {
            game.Tick(default);
            Assert.That(brother.State, Is.EqualTo(FighterState.Chuckling));
            Assert.That((brother.X, brother.Z), Is.EqualTo(start));
            Assert.That(brother.Vulnerable, Is.True);
            frames.Add(brother.Animation.Current.Frame);
        }
        Assert.That(frames, Is.EquivalentTo(new[] { 270, 271 }));

        game.Tick(default);
        game.Tick(default);
        Assert.That(brother.State, Is.EqualTo(FighterState.Walking));
        Assert.That(brother.X, Is.LessThan(start.X));

        brother.Age = brother.NextChuckleAge - 1;
        game.Tick(default);
        Assert.That(brother.State, Is.EqualTo(FighterState.Chuckling));
    }

    [TestCase((int)ActorKind.BadBrother, (int)ActorKind.BadBrother)]
    [TestCase((int)ActorKind.Knight, (int)ActorKind.Knight)]
    [TestCase((int)ActorKind.Knight, (int)ActorKind.DeathAdder)]
    public void HeavyEnemiesOnTheSameSideSplitAroundThePlayer(int frontKind, int rearKind)
    {
        var game = new Game(m_areas[0]);
        var hero = game.Hero;
        hero.Z = 4224;
        hero.Y = game.Ground(hero.X, hero.Z);
        hero.Invulnerable = 1000;
        Fighter AddHeavy(ActorKind kind, int offset)
        {
            var moves = kind == ActorKind.BadBrother ? Moves.Brother : kind == ActorKind.Knight ? Moves.Knight : Moves.DeathAdder;
            return game.Add(kind, moves, 0x38, 32, hero.X + offset, hero.Z);
        }
        var front = AddHeavy((ActorKind)frontKind, 32);
        var rear = AddHeavy((ActorKind)rearKind, 62);
        for (var tick = 0; tick < 440 && rear.X > hero.X - 28; tick++)
            game.Tick(default);

        Assert.That(front.X, Is.GreaterThan(hero.X));
        Assert.That(rear.X, Is.LessThanOrEqualTo(hero.X - 28));
        Assert.That(rear.Health, Is.EqualTo(32));
        for (var tick = 0; tick < 100 && rear.State != FighterState.Attacking; tick++)
            game.Tick(default);
        Assert.That(rear.State, Is.EqualTo(FighterState.Attacking));
    }

    [Test]
    public void FirstAreaClearsAfterTheTwoBadBrothersWithoutFurtherScrolling()
    {
        var data = m_areas[0];
        var game = new Game(data);
        var bossSpawns = data.Spawns.Where(spawn => spawn.Routine == 0x282d4).ToArray();
        AdvanceTo(game, bossSpawns.Min(spawn => spawn.Trigger) - 4);
        for (var tick = 0; tick < 30 && game.Progress < bossSpawns.Max(spawn => spawn.Trigger); tick++)
        {
            game.Hero.X = game.Progress + 180;
            game.Tick(default);
        }

        game.Hero.X = game.Progress + 120;
        game.Tick(default);
        var bosses = game.Actors.Where(actor => actor.Kind == ActorKind.BadBrother).ToArray();
        Assert.That(bosses, Has.Length.EqualTo(2));
        var progress = game.Progress;
        Assert.That(progress, Is.LessThan(data.Stops[^1]));
        Assert.That(game.Complete, Is.False);

        foreach (var boss in bosses)
        {
            boss.Health = 0;
            boss.SetState(FighterState.Dying, boss.Moves.GetUp);
        }
        for (var tick = 0; tick < 100 && !game.Complete; tick++)
        {
            game.Tick(default);
            Assert.That(game.CanAdvance, Is.False);
        }
        Assert.That(game.Complete, Is.True);
        Assert.That(game.Progress, Is.EqualTo(progress));

        game.StartCamp();
        Assert.That(game.IsCamp, Is.True);
        Assert.That(game.Progress, Is.EqualTo(data.Stops[^1]));
    }

    private static void AdvanceTo(Game game, int progress, Fighter preservedActor = null)
    {
        for (var tick = 0; tick < 2000 && game.Progress < progress; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero && a != preservedActor);
            game.Hero.X = game.Progress + 180;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.Invulnerable = 10000;
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.GreaterThanOrEqualTo(progress));
        game.Actors.RemoveAll(a => a != game.Hero && a != preservedActor);
    }
}
