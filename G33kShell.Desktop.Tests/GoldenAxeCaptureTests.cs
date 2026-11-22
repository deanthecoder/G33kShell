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

        var data = new ArcadeData(ArcadeData.AssetDirectory);
        foreach (var name in sceneList.Split(','))
        {
            var (game, ticks, input) = name switch
            {
                "shadows" => Shadows(data),
                "campfire" => Campfire(data),
                "dragon-fire" => DragonFire(data),
                "magic" => Magic(data),
                _ => throw new ArgumentException($"Unknown capture scene '{name}'.")
            };
            Capture(data, game, ticks, input, Path.Combine(output, name + ".bgra"));
        }
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

    private static (Game, int, Func<int, GameInput>) DragonFire(ArcadeData data)
    {
        var game = new Game(data, character: 1, random: new Random(0));
        var hero = game.Hero;
        var dragon = game.Add(ActorKind.RedDragon, game.Character.RedDragonMoves, 0x44, 99, hero.X, hero.Z);
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
