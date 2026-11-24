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
        var crowd = game.Actors.Where(a => a.Kind == ActorKind.Villager).ToArray();
        var initialDepths = crowd.Select(a => a.Z).ToArray();
        Assert.That(crowd, Has.Length.GreaterThan(5));
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
        game.Spawn(new SpawnDefinition((int)game.Progress, (int)game.Progress + 160, (int)game.Hero.Z, 0, 0x9a9a));
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
        var overlap = 9 * screen.Width + 108;
        renderer.Draw(screen, game);
        var withPotion = screen.Pixels[overlap];

        game.Tick(new GameInput(Magic: true));
        Assert.That(game.Potions, Is.Zero);
        renderer.Draw(screen, game);
        Assert.That(withPotion, Is.EqualTo(screen.Pixels[overlap]));
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
    public void RegularPotionStaysCollectibleAfterTheOldTimeout()
    {
        var game = new Game(m_areas[0]);
        var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X + 30, game.Hero.Z);
        var food = game.Add(ActorKind.Food, Moves.Thief, 0x7f, 1, potion.X, potion.Z);
        potion.Age = food.Age = 600;
        game.Tick(default);
        Assert.That(game.Actors, Does.Contain(potion));
        Assert.That(game.Actors, Does.Not.Contain(food));

        game.Hero.X = potion.X;
        game.Hero.Y = potion.Y;
        game.Tick(default);
        Assert.That(game.Potions, Is.EqualTo(2));
        Assert.That(game.Actors, Does.Not.Contain(potion));
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
    public void HoldingDownKeepsFeetAboveHudAwayFromDropOffs(int area)
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
        }
        for (var tick = 0; tick < 60; tick++)
            game.Tick(default);
        Assert.That(enemies.All(enemy => enemy.State == FighterState.Down), Is.True, "Every enemy must visibly land and lie down.");
        Assert.That(enemies.All(enemy => enemy.Animation.Address == enemy.Moves.GetUp && enemy.Animation.Index == 1), Is.True,
            "Every enemy must hold its grounded pose while down.");
        var waiting = enemies.ToDictionary(enemy => enemy, enemy => enemy.StateTicks);
        var recoveryTimes = new HashSet<int>();
        for (var tick = 1; tick <= 38; tick++)
        {
            game.Tick(default);
            foreach (var enemy in waiting.Keys.Where(enemy => enemy.State != FighterState.Down).ToArray())
            {
                Assert.That(enemy.State, Is.EqualTo(lethal ? FighterState.Dying : FighterState.GettingUp));
                var duration = waiting[enemy] + tick;
                Assert.That(duration, Is.InRange(lethal ? 36 : 34, lethal ? 36 : 38));
                recoveryTimes.Add(duration);
                waiting.Remove(enemy);
            }
        }
        Assert.That(waiting, Is.Empty);
        if (!lethal)
            Assert.That(recoveryTimes.Count, Is.GreaterThan(1));
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
