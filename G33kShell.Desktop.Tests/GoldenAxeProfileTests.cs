// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Console.Screensavers.GoldenAxe;
using NUnit.Framework;
using SkiaSharp;

namespace G33kShell.Desktop.Tests;

/// <summary>
/// Opt-in, deterministic final-battle CPU timings; excludes asset loading and desktop presentation.
/// </summary>
public sealed class GoldenAxeProfileTests
{
    [Test, Explicit("Set G33K_GOLDEN_AXE_PROFILE_DIR and select this test explicitly.")]
    public void ProfileFinalBattle()
    {
        var output = Environment.GetEnvironmentVariable("G33K_GOLDEN_AXE_PROFILE_DIR");
        if (string.IsNullOrEmpty(output))
            Assert.Ignore("Set G33K_GOLDEN_AXE_PROFILE_DIR to retain measurements.");
        Directory.CreateDirectory(output!);
        var hashes = new List<object>();
        var data = new ArcadeData(ArcadeData.AssetDirectory, 4);
        var samples = new Dictionary<(string Hero, bool Effects, string Phase, string Stage), List<double>>();
        var stageAllocation = new Dictionary<(string Hero, bool Effects, string Phase, string Stage), List<double>>();
        var populations = new Dictionary<string, (int Actors, int Smoke, int Heat)>();
        var allocation = new Dictionary<(string Hero, bool Effects, string Phase), List<double>>();

        // One untimed warm-up and two measured replays per hero/mode. Alternate mode order.
        for (var pass = 0; pass < 3; pass++)
        for (var character = 0; character < 3; character++)
        foreach (var effects in pass % 2 == 0 ? new[] { true, false } : new[] { false, true })
        {
            var game = new Game(data, character, new Random(0));
            for (var tick = 0; tick < 4000 && !game.Actors.Any(a => a.Kind == ActorKind.DeathAdder); tick++)
            {
                game.Actors.RemoveAll(a => a != game.Hero);
                game.Hero.X = game.Progress + 180;
                game.Hero.Z = game.FindFloor(game.Hero.X, 4240, keepVisible: true);
                game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
                game.Hero.Invulnerable = 10000;
                game.Tick(default);
            }
            Assert.That(game.Actors.Any(a => a.Kind == ActorKind.DeathAdder), Is.True);
            game.Hero.X = game.Progress + 100;
            game.Hero.Z = game.FindFloor(game.Hero.X, game.Hero.Z, keepVisible: true);
            game.Hero.Y = game.Ground(game.Hero.X, game.Hero.Z);
            game.Hero.SetState(FighterState.Idle, game.Hero.Moves.Idle);
            typeof(Game).GetProperty(nameof(Game.Potions))!.SetValue(game, game.Character.PotionCapacity);
            foreach (var enemy in game.Actors.Where(a => a.IsEnemy))
                enemy.Health = 1000;
            var name = game.Character.MagicKey;
            var renderer = new ArcadeRenderer(data);
            var screen = new PixelScreenData(320, 224, data.Palette);
            var pixels = new int[320 * 224];
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var phase = "before";
            long stageStart = 0;
            long stageBytes = 0;
            if (pass > 0)
                renderer.ProfileStage = stage =>
                {
                    var now = Stopwatch.GetTimestamp();
                    var bytesNow = GC.GetAllocatedBytesForCurrentThread();
                    if (stage != "begin")
                    {
                        Add(stage, (now - stageStart) * 1000.0 / Stopwatch.Frequency);
                        var key = (name, effects, phase, stage);
                        if (!stageAllocation.TryGetValue(key, out var values))
                            stageAllocation[key] = values = new List<double>(1400);
                        values.Add(bytesNow - stageBytes);
                    }
                    stageBytes = GC.GetAllocatedBytesForCurrentThread();
                    stageStart = Stopwatch.GetTimestamp();
                };

            for (var tick = 0; tick < 660; tick++)
            {
                var bytes = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                game.Tick(new GameInput(Magic: tick == 90));
                var simulated = Stopwatch.GetTimestamp();
                phase = tick < 90 ? "before" : game.MagicTick < 0 ? "after" :
                    game.MagicTick < game.MagicShakeStart ? "potion fan" : "magic";
                renderer.Draw(screen, game, effects);
                var drawn = Stopwatch.GetTimestamp();
                screen.CopyToBgra(pixels);
                var converted = Stopwatch.GetTimestamp();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                hash.AppendData(MemoryMarshal.AsBytes(pixels.AsSpan()));
                if (pass > 0)
                {
                    Add("simulation", (simulated - start) * 1000.0 / Stopwatch.Frequency);
                    Add("draw total", (drawn - simulated) * 1000.0 / Stopwatch.Frequency);
                    Add("BGRA conversion", (converted - drawn) * 1000.0 / Stopwatch.Frequency);
                    Add("frame total", (converted - start) * 1000.0 / Stopwatch.Frequency);
                    var key = (name, effects, phase);
                    if (!allocation.TryGetValue(key, out var values))
                        allocation[key] = values = new List<double>(1400);
                    values.Add(allocated);
                }
                var counts = populations.GetValueOrDefault(name);
                populations[name] = (Math.Max(counts.Actors, game.Actors.Count),
                    Math.Max(counts.Smoke, game.DragonSmoke.Count), Math.Max(counts.Heat, game.MagicGroundGlows.Count));
                if (pass == 2 && effects && tick is 70 or 220 or 500)
                {
                    using var bitmap = new SKBitmap(320, 224);
                    bitmap.Pixels = pixels.Select(p => new SKColor((uint)p)).ToArray();
                    using var image = SKImage.FromBitmap(bitmap);
                    using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var file = File.Create(Path.Combine(output!, $"{name}-{tick}.png"));
                    encoded.SaveTo(file);
                }
            }
            hashes.Add(new { Hero = name, Effects = effects, Pass = pass, Sha256 = Convert.ToHexString(hash.GetHashAndReset()) });
            Assert.That(game.GameOver, Is.False);
            TestContext.Progress.WriteLine($"Profile pass {pass}: {name}, effects={effects}, boss alive={game.Actors.Any(a => a.Kind == ActorKind.DeathAdder && a.Health > 0)}");

            void Add(string stage, double value)
            {
                var key = (name, effects, phase, stage);
                if (!samples.TryGetValue(key, out var values))
                    samples[key] = values = new List<double>(1400);
                values.Add(value);
            }
        }

        var rows = samples.Select(pair =>
        {
            var sorted = pair.Value.Order().ToArray();
            return new
            {
                pair.Key.Hero, pair.Key.Effects, pair.Key.Phase, pair.Key.Stage,
                Frames = sorted.Length, MeanMs = sorted.Average(),
                P50Ms = sorted[sorted.Length / 2], P95Ms = sorted[(int)((sorted.Length - 1) * 0.95)],
                MaxMs = sorted[^1]
            };
        }).ToArray();
        var report = new
        {
            Runtime = Environment.Version.ToString(), CpuCount = Environment.ProcessorCount,
            Resolution = "320x224", MeasuredPasses = 2, WarmupPasses = 1,
#if DEBUG
            Build = "Debug",
#else
            Build = "Release",
#endif
            Notes = "Headless timings. Real final-area spawns; earlier waves skipped, hero protected and enemy HP raised to keep the boss alive. Stage callbacks add small measurement overhead. No desktop scaling/presentation or asset loading.",
            Populations = populations.Select(p => new { Hero = p.Key, p.Value.Actors, p.Value.Smoke, p.Value.Heat }),
            Allocation = allocation.Select(p => new { p.Key.Hero, p.Key.Effects, p.Key.Phase, MeanBytes = p.Value.Average() }),
            StageAllocation = stageAllocation.Select(p => new { p.Key.Hero, p.Key.Effects, p.Key.Phase, p.Key.Stage, MeanBytes = p.Value.Average() }),
            FrameHashes = hashes,
            Timings = rows
        };
        File.WriteAllText(Path.Combine(output!, "profile.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var row in rows.Where(r => r.Stage == "frame total"))
            TestContext.Progress.WriteLine($"{row.Hero} effects={row.Effects} {row.Phase}: mean {row.MeanMs:F3} ms, p95 {row.P95Ms:F3} ms");
    }
}
