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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;

namespace G33kShell.Desktop.Tests;

/// <summary>
/// Renders short Golden Axe effect demonstrations at the native resolution.
/// </summary>
/// <remarks>
/// The capture tool runs these scripted scenes headlessly against the normal game renderer.
/// </remarks>
public sealed class GoldenAxeCaptureTests
{
    [Test, Explicit("Run through tools/capture-golden-axe.py.")]
    public void RenderClips()
    {
        var output = Environment.GetEnvironmentVariable("G33K_GOLDEN_AXE_CAPTURE_DIR");
        var sceneList = Environment.GetEnvironmentVariable("G33K_GOLDEN_AXE_CAPTURE_SCENES");
        if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(sceneList))
            Assert.Ignore("Run through tools/capture-golden-axe.py.");
        Assert.That(ArcadeData.IsAvailable, Is.True, "The bundled Golden Axe assets are required.");

        foreach (var name in sceneList.Split(','))
        {
            var area = name switch { "story-map2" => 1, "story-map3" => 2, "story-map4" => 3, "skeleton-dust" or "eagle-sunset" or "eagle-clouds" => 3, "village-stamp" or "village-routing" or "village-opening" or "camp-thieves" or "turtle-surf" => 1, "turtle-eye" => 2, "bridge-barge" or "knight-door" or "brothers-door" => 2, "eagle-eye" or "castle-camera" or "castle-ending" or "victory-map" or "adder-entrance" or "adder-defeat" => 4, _ => 0 };
            var data = new ArcadeData(ArcadeData.AssetDirectory, area);
            if (name == "title")
            {
                CaptureTitle(data, Path.Combine(output, name + ".bgra"));
                continue;
            }
            if (name == "castle-ending")
            {
                CaptureCastleEnding(data, Path.Combine(output, name + ".bgra"));
                continue;
            }
            if (name == "victory-map" || name.StartsWith("story-map", StringComparison.Ordinal))
            {
                CaptureMap(data, Path.Combine(output, name + ".bgra"), FileMode.Create);
                continue;
            }
            var (game, ticks, input) = name switch
            {
                "enemy-pair" => EnemyPair(data),
                "bridge-barge" => BridgeBarge(data),
                "knight-door" => KnightDoor(data),
                "adder-entrance" => AdderEntrance(data),
                "adder-defeat" => AdderDefeat(data),
                "brothers-door" => BrothersDoor(data),
                "forest-stamp" => VillagerStamp(data, 5136),
                "village-stamp" => VillagerStamp(data, 4784),
                "forest-opening" => (new Game(data, playOpening: true), 800, _ => new GameInput(X: 1)),
                "shadows" => Shadows(data),
                "campfire" => Campfire(data),
                "camp-thieves" => (new Game(data, character: 2, random: new Random(0), startInCamp: true), 840, _ => default(GameInput)),
                "dragon-fire" => DragonFire(data),
                "dragon-flame" => DragonFire(data, blue: true),
                "running-dust" => RunningDust(data),
                "potion-sparkles" => PotionSparklesScene(data),
                "camera-follow" => CameraFollow(data),
                "ledge-camera" => LedgeCamera(data),
                "dwarf-lightning" => DwarfLightning(data),
                "villagers" => Villagers(data),
                "turtle-surf" => TurtleSurf(data),
                "village-routing" => VillageRouting(data),
                "village-opening" => VillageRouting(data, opening: true),
                "stone-bodies" => StoneBodies(data),
                "eagle-sunset" => EagleSunset(data),
                "eagle-clouds" => EagleClouds(data),
                "castle-camera" => CastleCamera(data),
                "mounted-barge-chicken" => MountedBarge(data, ActorKind.Chicken),
                "mounted-barge-blue" => MountedBarge(data, ActorKind.BlueDragon),
                "mounted-barge-red" => MountedBarge(data, ActorKind.RedDragon),
                "autoplay-edge" => AutoplayEdge(data),
                "hero-respawn" => HeroRespawn(data),
                "magic" => Magic(data),
                "gilius-one" => HeroSpell(data, 2, 1),
                "gilius-two" => HeroSpell(data, 2, 2),
                "gilius-three" => HeroSpell(data, 2, 3),
                "ax-one" => HeroSpell(data, 0, 1),
                "ax-two" => HeroSpell(data, 0, 2),
                "ax-three" => HeroSpell(data, 0, 3),
                "ax-four" => HeroSpell(data, 0, 4),
                "ax-five" => HeroSpell(data, 0, 5),
                "ax-six" => HeroSpell(data, 0, 6),
                "gilius-four" => HeroSpell(data, 2, 4),
                "tyris-six" => HeroSpell(data, 1, 6),
                "tyris-seven" => HeroSpell(data, 1, 7),
                "tyris-eight" => HeroSpell(data, 1, 8),
                "tyris-dragon" => HeroSpell(data, 1, 9),
                "landing-dust" => LandingDust(data),
                "skeleton-dust" => SkeletonDust(data),
                "turtle-eye" or "eagle-eye" => SceneryEye(data),
                "weapon-swipes-ax" => WeaponSwipes(data, 0),
                "weapon-swipes-tyris" => WeaponSwipes(data, 1),
                "weapon-swipes-gilius" => WeaponSwipes(data, 2),
                "weapon-swipes-enemies" => EnemyWeaponSwipes(data),
                _ => throw new ArgumentException($"Unknown capture scene '{name}'.")
            };
            Capture(data, game, ticks, input, Path.Combine(output, name + ".bgra"));
        }
    }

    private static (Game, int, Func<int, GameInput>) EnemyPair(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        PrepareAt(game, (int)game.Progress, (int)game.Progress + 145, 4224);
        var hero = game.Hero;
        hero.Invulnerable = 1000;
        var first = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 16, hero.X + 90, hero.Z);
        var second = game.Add(ActorKind.Heninger, Moves.Heninger, 0x1b, 16, first.X, first.Z);
        return (game, 420, _ =>
        {
            game.Actors.RemoveAll(a => a != hero && a != first && a != second);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) BridgeBarge(ArcadeData data)
    {
        var game = new Game(data, 1, new Random(0));
        PrepareAt(game, 4625, 4750, 4184);
        var controller = new CodedController();
        return (game, 145, _ => controller.Read(game));
    }

    private static (Game, int, Func<int, GameInput>) VillagerStamp(ArcadeData data, int trigger)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var pair = data.Spawns.Where(s => s.Trigger == trigger).ToArray();
        PrepareAt(game, pair[0].X - 238, pair[0].X - 158, pair[0].Z);
        foreach (var spawn in pair)
            game.Spawn(spawn);
        return (game, 600, tick => new GameInput(X: tick is >= 128 and < 235 ? 1 : 0));
    }

    private static (Game, int, Func<int, GameInput>) KnightDoor(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var spawn = data.Spawns.Single(s => s.Routine == 0x2a358);
        PrepareAt(game, spawn.Trigger, spawn.X - 75, spawn.Z + 70);
        return (game, 180, tick =>
        {
            if (tick == 30)
                game.Spawn(spawn);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) BrothersDoor(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        PrepareAt(game, 4908, 5100, 4196);
        return (game, 650, _ => default);
    }

    private static (Game, int, Func<int, GameInput>) AdderEntrance(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        PrepareAt(game, data.Stops[^1], data.Stops[^1] + 82, 4192, keepAdder: true);
        return (game, DeathAdderEntrance.WalkTick + 144, _ => default);
    }

    private static (Game, int, Func<int, GameInput>) AdderDefeat(ArcadeData data)
    {
        var (game, _, _) = AdderEntrance(data);
        var boss = game.Actors.Single(a => a.Kind == ActorKind.DeathAdder);
        while (boss.State == FighterState.Entering)
            game.Tick(default);
        // Stage the final blow in the middle of the room so the whole axe arc is visible.
        game.Hero.X = boss.X - 32;
        game.Hero.Z = boss.Z;
        game.Hero.Y = boss.Y;
        game.Hero.Facing = 1;
        boss.Health = 1;
        boss.Cooldown = 120;
        var duration = DeathAdderDefeat.Duration + 208;
        return (game, duration, tick =>
        {
            if (tick == duration - 10)
                Assert.That(game.RescuingCaptives, Is.True);
            return new GameInput(Attack: boss.Health > 0);
        });
    }

    private static void CaptureTitle(ArcadeData data, string path)
    {
        var title = AnimatedTitle.Load(data);
        Assert.That(title, Is.Not.Null);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, title.Palette);
        var pixels = new int[screen.Pixels.Length];
        var game = new Game(data, playOpening: true);
        var renderer = new ArcadeRenderer(data);
        using var output = File.Create(path);
        for (var tick = 0; tick < AnimatedTitle.DurationTicks + 72; tick++)
        {
            if (tick >= AnimatedTitle.DurationTicks)
                game.Tick(default);
            if (tick % 2 != 0)
                continue;
            if (tick < AnimatedTitle.DurationTicks)
            {
                title.Draw(screen, title.Palette, tick);
                ArcadeTitleCard.ApplyBrightness(screen, Math.Min(1, tick / 20.0));
            }
            else
            {
                screen.SetPalette(data.Palette);
                var fade = Math.Min(1, game.TickCount / 30.0);
                ArcadeTitleCard.ApplyBrightness(screen, fade);
                renderer.Draw(screen, game, effects: true, effectOpacity: (byte)(255 * fade));
            }
            screen.CopyToBgra(pixels);
            output.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
        }
    }

    private static void CaptureCastleEnding(ArcadeData data, string path)
    {
        var game = new Game(data, 1, new Random(0));
        var controller = new CodedController();
        for (var tick = 0; tick < 18000 && !game.Complete && !game.GameOver; tick++)
            game.Tick(controller.Read(game));
        Assert.That(game.Complete, Is.True, "The capture starts with the actual cleared arena.");
        Capture(data, game, CastleRescue.Duration, _ => default, path);
        CaptureMap(data, path, FileMode.Append);
    }

    private static void CaptureMap(ArcadeData data, string path, FileMode mode)
    {
        using var map = IArcadeMap.Load(data);
        Assert.That(map, Is.Not.Null);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var pixels = new int[screen.Pixels.Length];
        using var output = new FileStream(path, mode);
        for (var frame = 0; map.AdvanceTo(frame / 30.0); frame++)
        {
            map.Draw(screen, null, frame / 30.0);
            screen.CopyToBgra(pixels);
            output.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
        }
    }

    private static void PrepareAt(Game game, int progress, int x, int z, bool keepAdder = false)
    {
        for (var tick = 0; tick < 4000 && game.Progress < progress; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.X = game.Progress + 180;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.GreaterThanOrEqualTo(progress));
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero && !(keepAdder && a.Kind is (ActorKind.DeathAdder or ActorKind.BodyPile)));
        game.Hero.X = x;
        game.Hero.Z = z;
        game.Hero.Y = game.Ground(x, z);
        game.ViewCamera.Reset(game.CameraX, game.CameraY);
    }

    private static (Game, int, Func<int, GameInput>) TurtleSurf(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        PrepareAt(game, TurtleVillageSurf.StartProgress, TurtleVillageSurf.StartProgress + 120, 4296);
        for (var tick = 0; tick < 384; tick++)
            game.Tick(default);
        return (game, 384, _ =>
        {
            game.ViewCamera.Reset(-855, -873);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) VillageRouting(ArcadeData data, bool opening = false)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var progress = opening ? 4120 : 4768;
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, 4096.0 - 7 - progress);
        var range = data.CameraPath.OrderBy(p => Math.Abs(p.Key - (int)Math.Floor(game.CameraX / 8))).First().Value;
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, (double)range[0]);
        game.ViewCamera.Reset(game.CameraX, game.CameraY);
        game.Hero.X = game.Progress + 70;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero);
        foreach (var spawn in data.Spawns.Where(s => s.Trigger >= progress && s.Trigger <= progress + 16 && s.Routine is >= 0x99c0 and <= 0x9a9a))
            game.Spawn(spawn);
        return (game, 600, _ =>
        {
            game.Actors.RemoveAll(a => a != game.Hero && a.Kind != ActorKind.Villager);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) Villagers(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var routines = new[] { 0x99c8, 0x99da, 0x99ec, 0x99fe, 0x9a5a };
        return (game, 270, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero && a.Kind != ActorKind.Villager);
            if (tick % 36 == 0 && tick / 36 < routines.Length)
            {
                var z = game.FindFloor(game.Progress + 295, game.Hero.Z - 24);
                game.Spawn(new SpawnDefinition((int)game.Progress, (int)game.Progress + 295, (int)z, 0x20, routines[tick / 36]));
            }
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) DwarfLightning(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        for (var i = 1; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z).Animation.Play(0x1870a);
        game.Tick(default);
        // Start the review clip after setup pickups have finished, rather than showing artificial setup glitter.
        for (var tick = 0; tick < PotionPickup.Lifetime; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        game.Actors.RemoveAll(a => a != game.Hero);
        AddTarget(game, ActorKind.Heninger, Moves.Heninger, 0x18, 105, -15);
        AddTarget(game, ActorKind.Longmoan, Moves.Longmoan, 0x78, 185, 8);
        foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
            enemy.Health = 48;
        return (game, 330, tick => new GameInput(Magic: tick == 45));
    }

    private static (Game, int, Func<int, GameInput>) LedgeCamera(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, 4096.0 - 7 - 4700);
        game.Hero.X = 4833;
        game.Hero.Z = 4168;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        for (var tick = 0; tick < 180; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        var previousY = game.ViewCamera.Y;
        return (game, 300, tick =>
        {
            Assert.That(Math.Abs(game.ViewCamera.Y - previousY), Is.LessThanOrEqualTo(2), "The view must not snap across the ledge.");
            previousY = game.ViewCamera.Y;
            game.Actors.RemoveAll(a => a != game.Hero);
            return new GameInput(X: tick is >= 90 and < 110 ? -1 : 0);
        });
    }

    private static (Game, int, Func<int, GameInput>) CameraFollow(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        return (game, 900, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.Invulnerable = 10000;
            return tick switch
            {
                >= 60 and < 190 => new GameInput(X: 1),
                >= 250 and < 300 => new GameInput(X: -1),
                >= 340 and < 440 => new GameInput(X: 1, Run: true),
                >= 500 and < 545 => new GameInput(Z: -1),
                >= 600 and < 645 => new GameInput(Z: 1),
                >= 700 and < 770 => new GameInput(X: 1),
                _ => default
            };
        });
    }

    private static (Game, int, Func<int, GameInput>) PotionSparklesScene(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        for (var i = 0; i < 2; i++)
        {
            var potion = game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X + 65 + i * 42, game.Hero.Z);
            potion.Animation.Play(0x1870a);
        }
        var food = game.Add(ActorKind.Food, Moves.Thief, 0x7f, 1, game.Hero.X + 149, game.Hero.Z);
        food.Animation.Play(0x18722);
        return (game, 360, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero && a.Kind is not (ActorKind.Potion or ActorKind.Food));
            return new GameInput(X: tick is >= 150 and < 245 ? 1 : 0);
        });
    }

    private static (Game, int, Func<int, GameInput>) SceneryEye(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        var target = data.AreaIndex == 2 ? 4608 : 4520;
        for (var tick = 0; tick < 2000 && game.Progress < target; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.X = game.Progress + 180;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.Invulnerable = 10000;
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.GreaterThanOrEqualTo(target));
        game.Hero.X = game.Progress + 110;
        return (game, 360, _ =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) Shadows(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        AddTarget(game, ActorKind.Heninger, Moves.Heninger, 0x18, 135, -16);
        AddTarget(game, ActorKind.Longmoan, Moves.Longmoan, 0x78, 170, 12);

        // Cross the lit ground, jump, then swing so the projected silhouette changes with the pose.
        return (game, 204, tick => new GameInput(X: tick is >= 18 and < 102 ? 1 : 0,
            Attack: tick is >= 108 and < 180, Jump: tick == 34));
    }

    private static (Game, int, Func<int, GameInput>) Campfire(ArcadeData data)
    {
        var game = GoldenAxeTests.ReachFirstCamp(data);
        for (var tick = 0; tick < 110; tick++)
            game.Tick(default);

        var fire = game.Actors.Single(actor => actor.Kind == ActorKind.Campfire);
        var hero = game.Hero;
        hero.X = fire.X - 46;
        hero.Z = fire.Z + 12;
        hero.Y = game.Ground(hero.X, hero.Z);
        hero.SetState(FighterState.Idle, hero.Moves.Idle);

        // Walk in front of the fire and back through the light.
        return (game, 288, tick => new GameInput(X: tick is >= 28 and < 105 ? 1 : tick is >= 147 and < 214 ? -1 : 0));
    }

    private static (Game, int, Func<int, GameInput>) EagleClouds(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, 4096.0 - 7 - 5245);
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, -560.0);
        game.Hero.X = game.Progress + 110;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        for (var tick = 0; tick < 120; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Tick(default);
        }
        return (game, 600, _ =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) EagleSunset(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        return (game, 240, tick =>
        {
            var progress = 5040 + Math.Clamp(tick - 30, 0, 150) * 0.5;
            var cameraX = 4096 - 7 - progress;
            var range = data.CameraPath.OrderBy(p => Math.Abs(p.Key - (int)Math.Floor(cameraX / 8))).First().Value;
            // Stay within the captured route: forcing the later -560 camera here exposes uncaptured sky.
            typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, (double)range[1]);
            typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, cameraX);
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.X = progress + 120;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            game.Hero.Invulnerable = 10000;
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) CastleCamera(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, -632.0);
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, -711.0);
        game.Hero.X = game.Progress + 155;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        return (game, 240, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            return new GameInput(Z: tick >= 30 && game.CameraY > -736 ? 1 : 0);
        });
    }

    private static (Game, int, Func<int, GameInput>) StoneBodies(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var enemies = new[]
        {
            game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 4, game.Hero.X + 65, game.Hero.Z),
            game.Add(ActorKind.Amazon, Moves.Amazon, 0x63, 4, game.Hero.X + 125, game.Hero.Z + 15),
            game.Add(ActorKind.Skeleton, Moves.Skeleton, 0x6f, 4, game.Hero.X + 190, game.Hero.Z)
        };
        return (game, 420, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero && !enemies.Contains(a));
            foreach (var enemy in enemies)
                enemy.Cooldown = 10000;
            return new GameInput(Magic: tick == 20);
        });
    }

    private static (Game, int, Func<int, GameInput>) RunningDust(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        return (game, 210, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            return new GameInput(X: tick % 100 < 50 ? 1 : -1, Run: true);
        });
    }

    private static (Game, int, Func<int, GameInput>) DragonFire(ArcadeData data, bool blue = false)
    {
        var game = new Game(data, character: 1, random: new Random(0));
        var hero = game.Hero;
        var dragon = game.Add(blue ? ActorKind.BlueDragon : ActorKind.RedDragon,
            blue ? game.Character.DragonMoves : game.Character.RedDragonMoves, blue ? 0x43 : 0x44, 99, hero.X, hero.Z);
        dragon.Facing = 1;
        dragon.Rider = hero;
        hero.Mount = dragon;
        AddTarget(game, ActorKind.Longmoan, Moves.Longmoan, 0x78, 157, 0);

        return (game, 216, tick => new GameInput(Attack: tick is 22 or 126));
    }

    private static (Game, int, Func<int, GameInput>) MountedBarge(ArcadeData data, ActorKind kind)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var hero = game.Hero;
        hero.X = game.Progress + 70;
        var beast = game.Add(kind, game.Character.RidingMoves(kind), kind == ActorKind.RedDragon ? 0x25 : 0x23,
            99, hero.X, hero.Z);
        beast.Rider = hero;
        hero.Mount = beast;
        game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 99, hero.X + 125, hero.Z);
        return (game, 210, tick =>
        {
            foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
                enemy.Cooldown = 10000;
            if (tick is >= 15 and <= 33)
                return new GameInput(X: 1, Run: true, Attack: tick == 33);
            if (tick is >= 110 and <= 128)
                return new GameInput(X: -1, Run: true, Attack: tick == 128);
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) AutoplayEdge(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero);
        typeof(Game).GetField("m_spawnIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(game, data.Spawns.Length);
        var hero = game.Hero;
        hero.X += 300 - game.ProjectView(hero.X, hero.Z, hero.Y).X;
        hero.Y = game.Ground(hero.X, hero.Z);
        var enemy = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 48, hero.X + 36, hero.Z);
        enemy.Cooldown = 100;
        var arrival = game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 32, hero.X + 90, hero.Z + 10);
        arrival.Cooldown = 100;
        var controller = new CodedController();
        return (game, 600, _ => controller.Read(game));
    }

    private static (Game, int, Func<int, GameInput>) HeroRespawn(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        game.Hero.X = game.Progress + 150;
        return (game, 240, tick =>
        {
            foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
                enemy.Cooldown = 10000;
            if (tick == 36)
            {
                game.Hero.Health = 0;
                game.Hero.SetState(FighterState.Respawning, game.Hero.Moves.GetUp);
                game.Hero.Animation.Seek(1);
            }
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) Magic(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var hero = game.Hero;
        for (var i = game.Potions; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, hero.X, hero.Z).Animation.Play(0x1870a);
        game.Tick(default);
        AddTarget(game, ActorKind.Heninger, Moves.Heninger, 0x18, 100, -10);
        AddTarget(game, ActorKind.Amazon, Moves.Amazon, 0x63, 150, 8);

        return (game, 270, tick => new GameInput(Magic: tick == 36));
    }

    private static (Game, int, Func<int, GameInput>) HeroSpell(ArcadeData data, int character, int potions)
    {
        var game = new Game(data, character: character, random: new Random(0));
        game.Hero.X = game.Progress + 150;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        for (var i = game.Potions; i < potions; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z).Animation.Play(0x1870a);
        game.Tick(default);
        game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 64, game.Hero.X + 65, game.Hero.Z - 10);
        game.Add(ActorKind.Amazon, Moves.Amazon, 0x63, 64, game.Hero.X + 120, game.Hero.Z + 12);
        return (game, 540, tick =>
        {
            foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
                enemy.Cooldown = 10000;
            return new GameInput(Magic: tick == 36);
        });
    }

    private static (Game, int, Func<int, GameInput>) LandingDust(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        var hero = game.Hero;
        var enemy = game.Add(ActorKind.Longmoan, Moves.Longmoan, 0x78, 24, hero.X + 95, hero.Z);
        return (game, 210, tick =>
        {
            hero.Invulnerable = enemy.Cooldown = 10000;
            if (tick == 20)
            {
                enemy.Y -= 18;
                enemy.Vy = 1;
                enemy.SetState(FighterState.Flying, enemy.Moves.Fly);
            }
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) SkeletonDust(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        var spawn = data.Spawns.First(s => s.Routine == 0x23bda);
        for (var tick = 0; tick < 2000 && game.Progress < spawn.Trigger; tick++)
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            game.Hero.X = game.Progress + 180;
            game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.Invulnerable = 10000;
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            game.Tick(default);
        }
        Assert.That(game.Progress, Is.GreaterThanOrEqualTo(spawn.Trigger));
        game.Actors.RemoveAll(a => a != game.Hero);
        game.Hero.X = spawn.X - 48;
        game.Hero.Z = spawn.Z;
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        return (game, 210, _ =>
        {
            foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
                enemy.Cooldown = 10000;
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) WeaponSwipes(ArcadeData data, int character)
    {
        var game = new Game(data, character, new Random(0));
        game.Hero.X = game.Progress + 140;
        return (game, 288, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero);
            if (tick % 48 == 0)
            {
                game.Hero.Facing = tick < 144 ? 1 : -1;
                game.Hero.SetState(FighterState.Attacking, game.Hero.Moves.Attacks[tick / 48 % 3], attack: true);
            }
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) EnemyWeaponSwipes(ArcadeData data)
    {
        var game = new Game(data, random: new Random(0));
        var hero = game.Hero;
        hero.SetState(FighterState.Gone);
        var mace = game.Add(ActorKind.Heninger, Moves.Heninger, 0x18, 24, game.Progress + 100, hero.Z);
        var sword = game.Add(ActorKind.Skeleton, Moves.Skeleton, 0x6f, 24, game.Progress + 230, hero.Z);
        return (game, 360, tick =>
        {
            game.Actors.RemoveAll(a => a != hero && a != mace && a != sword);
            foreach (var actor in new[] { mace, sword })
            {
                actor.Cooldown = 10000;
                if (tick % 60 == 0)
                {
                    actor.Facing = tick < 180 ? 1 : -1;
                    actor.SetState(FighterState.Attacking, actor.Moves.Attacks[tick / 60 % 3], attack: true);
                }
            }
            return default;
        });
    }

    private static void AddTarget(Game game, ActorKind kind, Moves moves, int palette, int offsetX, int offsetZ)
    {
        var target = game.Add(kind, moves, palette, 24, game.Hero.X + offsetX, game.Hero.Z + offsetZ);
        target.Cooldown = 600;
    }

    private static void Capture(ArcadeData data, Game game, int ticks, Func<int, GameInput> input, string path)
    {
        var renderer = new ArcadeRenderer(data);
        var screen = new PixelScreenData(ArcadeData.Width, ArcadeData.Height, data.Palette);
        var pixels = new int[ArcadeData.Width * ArcadeData.Height];
        using var output = File.Create(path);
        for (var tick = 0; tick < ticks; tick++)
        {
            game.Tick(input(tick));
            if (tick % 2 != 0)
                continue;
            renderer.Draw(screen, game, effects: true);
            screen.CopyToBgra(pixels);
            output.Write(MemoryMarshal.AsBytes(pixels.AsSpan()));
        }
    }
}
