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
        Assert.That(later.Frame, Is.Not.EqualTo(feather.Frame), "The original feather poses should tumble as they drift.");

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

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void HoldingDownKeepsFeetAboveHudAcrossArea(int area)
    {
        var game = new Game(m_areas[area], 2);
        Assert.That(game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.InRange(48, 196), "The player must spawn on screen.");
        foreach (var progress in new[] { 4200, 4500, 4800, 5100 })
        {
            AdvanceTo(game, progress);
            for (var tick = 0; tick < 300; tick++)
            {
                game.Actors.RemoveAll(a => a != game.Hero);
                game.Tick(new GameInput(Z: 1));
                if (tick > 30)
                    Assert.That(game.Project(game.Hero.X, game.Hero.Z, game.Hero.Y).Y, Is.LessThanOrEqualTo(198), $"Area {area}, progress {progress}");
            }
        }
    }

    [Test]
    public void CampSleepsWakesAllowsPotionTheftAndDropsHealingFood()
    {
        var game = new Game(m_areas[1], 2);
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
        for (var tick = 0; tick < 50 && !game.Hero.IsFree; tick++)
            game.Tick(default);
        Assert.That(game.Hero.IsFree, Is.True);

        var foodThief = game.Actors.Single(a => a.FoodThief);
        game.Actors.RemoveAll(a => a != game.Hero && a != foodThief && a.Kind != ActorKind.Campfire);
        foodThief.X = game.Hero.X + 30;
        foodThief.Z = game.Hero.Z;
        foodThief.Y = game.Hero.Y;
        foodThief.Facing = -1;
        for (var tick = 0; tick < 120 && !game.Actors.Any(a => a.Kind == ActorKind.Food); tick++)
            game.Tick(new GameInput(1, Attack: true));
        var food = game.Actors.Single(a => a.Kind == ActorKind.Food);
        for (var tick = 0; tick < 40; tick++)
            game.Tick(default);
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
        var loot = game.Actors.Where(a => a.Kind == (food ? ActorKind.Food : ActorKind.Potion)).ToArray();
        Assert.That(loot, Has.Length.EqualTo(3));
        thief.X = game.Progress + (direction < 0 ? 40 : 280);
        for (var tick = 0; tick < 120 && !thief.IsGone; tick++)
            game.Tick(new GameInput(Attack: true));
        Assert.That(thief.IsGone, Is.True, "An empty thief must leave instead of bouncing off the screen edge.");
        Assert.That(thief.Facing, Is.EqualTo(direction));
        Assert.That(game.Actors.Where(a => a.Kind == (food ? ActorKind.Food : ActorKind.Potion)), Is.EquivalentTo(loot));
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
        while (game.Potions < game.Character.MagicLevels)
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
        Assert.That(game.Potions, Is.EqualTo(game.Character.MagicLevels));
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
        game.Tick(new GameInput(Jump: true));
        game.Hero.MountCooldown = 10000;
        Assert.That(beast.Rider, Is.Null);
        Assert.That(beast.Animation.Current.Frame, Is.EqualTo(kind == 0 ? 203 : 637), "Dismounting restores the seated pose.");
        for (var tick = 0; tick < 299; tick++)
            game.Tick(default);
        Assert.That(beast.State, Is.EqualTo(FighterState.Idle));
        game.Tick(default);
        Assert.That(beast.State, Is.EqualTo(FighterState.Fleeing));
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
        for (var tick = 0; tick < 140 && !game.Actors.Any(a => a.Kind == ActorKind.Lightning); tick++)
            game.Tick(default);
        var bolt = game.Actors.Single(a => a.Kind == ActorKind.Lightning);
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
        var game = new Game(m_areas[0], character);
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
        }
        for (var tick = 0; tick < 60; tick++)
            game.Tick(default);
        Assert.That(enemies.All(enemy => enemy.State == FighterState.Down), Is.True, "Every enemy must visibly land and lie down.");
        var waiting = enemies.ToList();
        for (var tick = 0; tick < 36; tick++)
        {
            game.Tick(default);
            foreach (var enemy in waiting.Where(enemy => enemy.State != FighterState.Down).ToArray())
            {
                Assert.That(enemy.State, Is.EqualTo(lethal ? FighterState.Dying : FighterState.GettingUp));
                waiting.Remove(enemy);
            }
        }
        Assert.That(waiting, Is.Empty);
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
        while (game.TickCount % 60 != 0)
            game.Tick(default);
        var screen = new PixelScreenData(320, 224, data.Palette);
        var renderer = new ArcadeRenderer(data);
        byte[] PromptPixels() => Enumerable.Range(48, 12).SelectMany(y => screen.Pixels.Skip(y * 320 + 264).Take(44)).ToArray();
        renderer.Draw(screen, game);
        var visible = PromptPixels();
        for (var tick = 0; tick < 30; tick++)
            game.Tick(default);
        renderer.Draw(screen, game);
        Assert.That(PromptPixels(), Is.Not.EqualTo(visible));
    }

    private static void AdvanceTo(Game game, int progress)
    {
        for (var tick = 0; tick < 2000 && game.Progress < progress; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.X = game.Progress + 180;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.Invulnerable = 10000;
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.GreaterThanOrEqualTo(progress));
        game.Actors.RemoveAll(a => a != game.Hero);
    }
}
