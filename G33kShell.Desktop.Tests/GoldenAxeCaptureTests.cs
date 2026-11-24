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
            var area = name switch { "skeleton-dust" or "eagle-sunset" => 3, "village-routing" => 1, "turtle-eye" => 2, "eagle-eye" or "castle-camera" => 4, _ => 0 };
            var data = new ArcadeData(ArcadeData.AssetDirectory, area);
            var (game, ticks, input) = name switch
            {
                "shadows" => Shadows(data),
                "campfire" => Campfire(data),
                "dragon-fire" => DragonFire(data),
                "dragon-flame" => DragonFire(data, blue: true),
                "running-dust" => RunningDust(data),
                "potion-sparkles" => PotionSparklesScene(data),
                "camera-follow" => CameraFollow(data),
                "ledge-camera" => LedgeCamera(data),
                "dwarf-lightning" => DwarfLightning(data),
                "villagers" => Villagers(data),
                "village-routing" => VillageRouting(data),
                "stone-bodies" => StoneBodies(data),
                "eagle-sunset" => EagleSunset(data),
                "castle-camera" => CastleCamera(data),
                "magic" => Magic(data),
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

    private static (Game, int, Func<int, GameInput>) VillageRouting(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        typeof(Game).GetProperty(nameof(Game.CameraX))!.SetValue(game, 4096.0 - 7 - 4768);
        var range = data.CameraPath.OrderBy(p => Math.Abs(p.Key - (int)Math.Floor(game.CameraX / 8))).First().Value;
        typeof(Game).GetProperty(nameof(Game.CameraY))!.SetValue(game, (double)range[0]);
        game.ViewCamera.Reset(game.CameraX, game.CameraY);
        game.Hero.X = game.Progress + 70;
        game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
        game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
        game.Tick(default);
        game.Actors.RemoveAll(a => a != game.Hero);
        foreach (var spawn in data.Spawns.Where(s => s.Trigger is >= 4768 and <= 4784 && s.Routine is >= 0x99c0 and <= 0x9a9a))
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
        var routines = new[] { 0x99c8, 0x99da, 0x99ec, 0x99fe, 0x9a9a };
        return (game, 270, tick =>
        {
            game.Actors.RemoveAll(a => a != game.Hero && a.Kind != ActorKind.Villager);
            if (tick % 36 == 0 && tick / 36 < routines.Length)
            {
                var z = game.FindFloor(game.Progress + 295, game.Hero.Z - 24);
                game.Spawn(new SpawnDefinition((int)game.Progress, (int)game.Progress + 295, (int)z, 0, routines[tick / 36]));
            }
            return default;
        });
    }

    private static (Game, int, Func<int, GameInput>) DwarfLightning(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        for (var i = 1; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, game.Hero.X, game.Hero.Z);
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

    private static (Game, int, Func<int, GameInput>) Magic(ArcadeData data)
    {
        var game = new Game(data, character: 2, random: new Random(0));
        var hero = game.Hero;
        for (var i = game.Potions; i < game.Character.PotionCapacity; i++)
            game.Add(ActorKind.Potion, Moves.Thief, 0x4a, 1, hero.X, hero.Z);
        game.Tick(default);
        AddTarget(game, ActorKind.Heninger, Moves.Heninger, 0x18, 100, -10);
        AddTarget(game, ActorKind.Amazon, Moves.Amazon, 0x63, 150, 8);

        return (game, 270, tick => new GameInput(Magic: tick == 36));
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
