// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose. If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>Deterministic habitat, steering and scene transitions, independent of the UI.</summary>
internal sealed class AquariumScene
{
    internal enum ScenePhase { Arriving, Swimming, Leaving, Fading }
    internal sealed class Fish
    {
        public Vector2 Position, Velocity, NextVelocity, Target;
        public float Depth, Size, Phase, DecisionTime, FeedingTime;
        public int Species, School, ExitDirection;
        public bool FacingRight = true;
        public float VisualFacing = 1;
        public float RenderWidth => Math.Max(2, Size * Math.Abs(VisualFacing));

        public void UpdateFacing(float dt)
        {
            if (Math.Abs(Velocity.X) > 2)
                FacingRight = Velocity.X > 0;
            var target = FacingRight ? 1f : -1f;
            VisualFacing += Math.Clamp(target - VisualFacing, -4 * dt, 4 * dt);
        }
    }
    internal sealed record Plant(float X, float Height, float Depth, float Phase, int Sprite);
    internal sealed record Rock(float X, float Y, float Width, float Height, int Sprite);
    internal sealed class Food { public Vector2 Position; public float Age; }

    private readonly Random m_random;
    private readonly Vector2[] m_schoolTargets = new Vector2[2];
    private readonly float[] m_schoolTimers = new float[2];
    private float m_phaseTime, m_feedTime;
    private float m_groundPhase;
    public int Width { get; }
    public int Height { get; }
    public List<Fish> Fishes { get; } = [];
    public List<Plant> Plants { get; } = [];
    public List<Rock> Rocks { get; } = [];
    public List<Food> FoodParticles { get; } = [];
    public ScenePhase Phase { get; private set; }
    public float Time { get; private set; }
    public float Opacity { get; private set; }
    public int Generation { get; private set; }

    public AquariumScene(int width, int height, int seed)
    {
        Width = width;
        Height = height;
        m_random = new Random(seed);
        Populate();
    }

    private float Range(float min, float max) => min + (max - min) * m_random.NextSingle();
    public float Ground(float x) => Height - 15 - 5 * MathF.Sin(x * 0.019f + m_groundPhase) - 3 * MathF.Sin(x * 0.047f);

    private void Populate()
    {
        Generation++;
        Fishes.Clear(); Plants.Clear(); Rocks.Clear(); FoodParticles.Clear();
        m_groundPhase = Range(0, 6.28f);
        for (var i = 0; i < 6; i++)
        {
            var x = Range(25, Width - 25);
            Rocks.Add(new Rock(x, Ground(x) + 3, Range(18, 40), Range(10, 22), i % 2));
        }
        // Clusters leave open water between rooted beds.
        for (var bed = 0; bed < 5; bed++)
        {
            var center = Range(0, Width);
            for (var i = 0; i < 5; i++)
                Plants.Add(new Plant(Math.Clamp(center + Range(-22, 22), 0, Width - 1),
                    Range(22, Height * 0.48f), Range(0, 1), Range(0, 6.28f), m_random.Next(4)));
        }
        // Keep only a couple of foreground plants so they do not hide the fish.
        var foregroundCount = 0;
        Plants.RemoveAll(plant => plant.Depth >= 0.8f && ++foregroundCount > 2);
        Plants.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        for (var i = 0; i < 26; i++)
        {
            var school = i < 20 ? i / 10 : -1;
            var right = school == 0 || (school < 0 && i % 2 == 0);
            var species = school >= 0 ? school : 2 + i % 4;
            var size = school >= 0 ? Range(9, 13) : species == 5 ? Range(38, 48) : Range(20, 29);
            var fish = new Fish
            {
                Position = new Vector2(right ? Range(-100, -20) : Range(Width + 20, Width + 100),
                    school >= 0 ? Height * (0.3f + school * 0.25f) + Range(-14, 14) : Range(25, Height - 45)),
                Velocity = new Vector2(right ? 15 : -15, 0),
                School = school, Species = species,
                Size = size, Depth = school >= 0 ? 0.35f + school * 0.3f : Range(0.2f, 0.9f),
                Phase = Range(0, 6.28f), FacingRight = right, VisualFacing = right ? 1 : -1,
                DecisionTime = Range(2, 8)
            };
            fish.Target = new Vector2(Range(40, Width - 40), fish.Position.Y);
            Fishes.Add(fish);
        }
        for (var school = 0; school < 2; school++)
        {
            m_schoolTargets[school] = new Vector2(Width * (school == 0 ? 0.8f : 0.2f), Height * (0.3f + school * 0.25f));
            m_schoolTimers[school] = 25;
        }
        Phase = ScenePhase.Arriving;
        m_phaseTime = 0;
        m_feedTime = Range(12, 20);
        Opacity = 0;
    }

    /// <returns>True once per completed fade, for the random screensaver playlist.</returns>
    public bool Step(float dt)
    {
        Time += dt;
        m_phaseTime += dt;
        if (Phase == ScenePhase.Arriving)
        {
            Opacity = Math.Min(1, m_phaseTime / 3);
            if (m_phaseTime >= 10) { Phase = ScenePhase.Swimming; m_phaseTime = 0; }
        }
        else if (Phase == ScenePhase.Swimming && m_phaseTime >= 110)
        {
            Phase = ScenePhase.Leaving;
            m_phaseTime = 0;
            FoodParticles.Clear();
            foreach (var fish in Fishes)
                fish.ExitDirection = fish.Position.X < Width / 2f ? -1 : 1;
        }
        else if (Phase == ScenePhase.Leaving && Fishes.All(f => f.Position.X < -f.Size || f.Position.X > Width + f.Size))
        {
            Phase = ScenePhase.Fading;
            m_phaseTime = 0;
        }
        else if (Phase == ScenePhase.Fading)
        {
            Opacity = Math.Max(0, 1 - m_phaseTime / 3);
            if (m_phaseTime >= 3) { Populate(); return true; }
            return false;
        }

        UpdateFood(dt);
        UpdateSchoolTargets(dt);
        // Calculate all steering from the same frame, so school order cannot bias motion.
        foreach (var fish in Fishes)
            fish.NextVelocity = Steer(fish, dt);
        foreach (var fish in Fishes)
        {
            fish.Velocity = fish.NextVelocity;
            fish.Position += fish.Velocity * dt;
            fish.UpdateFacing(dt);
            Constrain(fish);
        }
        return false;
    }

    private void UpdateSchoolTargets(float dt)
    {
        if (Phase == ScenePhase.Leaving) return;
        for (var school = 0; school < 2; school++)
        {
            var center = Vector2.Zero;
            var count = 0;
            foreach (var fish in Fishes)
                if (fish.School == school) { center += fish.Position; count++; }
            if (count == 0) continue;
            center /= count;
            m_schoolTimers[school] -= dt;
            if (m_schoolTimers[school] > 0 && Vector2.Distance(center, m_schoolTargets[school]) > 30) continue;
            // A shared destination avoids individual wander targets cancelling one another.
            // Choose across the tank, at a different depth, for sustained exploratory swims.
            m_schoolTargets[school] = new Vector2(
                Width * (center.X < Width / 2f ? Range(0.75f, 0.88f) : Range(0.12f, 0.25f)),
                Height * (center.Y < Height * 0.45f ? Range(0.55f, 0.72f) : Range(0.18f, 0.35f)));
            m_schoolTimers[school] = Range(22, 30);
        }
    }

    private void UpdateFood(float dt)
    {
        m_feedTime -= dt;
        if (Phase == ScenePhase.Swimming && m_feedTime <= 0)
        {
            var x = Range(35, Width - 35);
            for (var i = 0; i < 5; i++)
                FoodParticles.Add(new Food { Position = new Vector2(x + Range(-10, 10), Range(5, 10)) });
            m_feedTime = Range(20, 32);
        }
        for (var i = FoodParticles.Count - 1; i >= 0; i--)
        {
            var food = FoodParticles[i];
            food.Age += dt;
            food.Position += new Vector2(MathF.Sin(Time + food.Age) * 0.7f, 2.2f) * dt;
            if (food.Age > 45 || food.Position.Y >= Ground(food.Position.X) - 2 ||
                Fishes.Any(f => Vector2.Distance(f.Position + new Vector2(f.FacingRight ? f.Size / 2 : -f.Size / 2, 0), food.Position) < 4))
                FoodParticles.RemoveAt(i);
        }
    }

    private Vector2 Steer(Fish fish, float dt)
    {
        if (Phase == ScenePhase.Leaving)
        {
            var vertical = MathF.Sin(Time + fish.Phase) * 2;
            foreach (var rock in Rocks)
                if (Math.Abs(fish.Position.X + fish.ExitDirection * 20 - rock.X) < rock.Width / 2 + fish.Size &&
                    fish.Position.Y > rock.Y - rock.Height - fish.Size)
                    vertical = -18;
            return Vector2.Lerp(fish.Velocity, new Vector2(fish.ExitDirection * 38, vertical), dt * 1.5f);
        }

        fish.DecisionTime -= dt;
        fish.FeedingTime = Math.Max(0, fish.FeedingTime - dt);
        if (fish.DecisionTime <= 0)
        {
            fish.Target = new Vector2(Range(25, Width - 25), Range(18, Height - 40));
            fish.DecisionTime = Range(5, 12);
            if (fish.School < 0 && m_random.Next(3) == 0)
            {
                var rock = Rocks[m_random.Next(Rocks.Count)];
                fish.Target = new Vector2(rock.X, rock.Y - rock.Height - fish.Size * 0.35f - 2);
                fish.FeedingTime = 7;
            }
        }
        var target = fish.School >= 0 ? m_schoolTargets[fish.School] : fish.Target;
        var feeding = fish.FeedingTime > 0;
        var nearest = 100f;
        foreach (var food in FoodParticles)
        {
            var distance = Vector2.Distance(fish.Position, food.Position);
            if (distance < nearest)
            {
                target = food.Position - new Vector2(fish.FacingRight ? fish.Size / 2 : -fish.Size / 2, 0);
                nearest = distance;
                feeding = true;
            }
        }
        var desired = Direction(target - fish.Position) * (fish.School >= 0 ? 15 : 10);
        if (feeding && Vector2.Distance(target, fish.Position) < 12)
            desired = (target - fish.Position) * 0.8f;
        var alignment = Vector2.Zero;
        var center = Vector2.Zero;
        var separation = Vector2.Zero;
        var count = 0;
        foreach (var other in Fishes)
        {
            if (ReferenceEquals(fish, other)) continue;
            var offset = fish.Position - other.Position;
            var distance = offset.Length();
            if (distance < 0.01f) continue;
            var clearance = (fish.Size + other.Size) * 0.6f;
            if (Math.Abs(fish.Depth - other.Depth) < 0.25f && distance < clearance)
                separation += offset / distance * (clearance - distance) * 2;
            if (fish.School >= 0 && fish.School == other.School && distance < 65)
            { alignment += other.Velocity; center += other.Position; count++; }
        }
        if (count > 0 && !feeding)
            desired = desired * 0.65f + alignment / count * 0.35f + (center / count - fish.Position) * 0.18f;
        desired += separation;
        desired.Y += MathF.Sin(Time * 0.6f + fish.Phase) * 1.5f;
        // Soft boundaries permit arrivals, while encouraging broad turns before the glass.
        if (fish.Position.X < 25) desired.X += (25 - fish.Position.X) * 1.2f;
        if (fish.Position.X > Width - 25) desired.X -= (fish.Position.X - Width + 25) * 1.2f;
        if (fish.Position.Y < 18) desired.Y += (18 - fish.Position.Y) * 2;
        var bottom = Ground(fish.Position.X) - fish.Size * 0.4f - 7;
        if (fish.Position.Y > bottom - 10) desired.Y -= (fish.Position.Y - bottom + 10) * 2;
        foreach (var rock in Rocks)
        {
            var ahead = fish.Position + fish.Velocity * 0.6f;
            if (Math.Abs(ahead.X - rock.X) < rock.Width / 2 + fish.Size / 2 && ahead.Y > rock.Y - rock.Height - fish.Size * 0.5f)
                desired.Y -= 25;
        }
        var acceleration = Limit(desired - fish.Velocity, 16);
        return Limit(fish.Velocity + acceleration * dt, fish.School >= 0 ? 24 : 17);
    }

    private void Constrain(Fish fish)
    {
        fish.Position.Y = Math.Clamp(fish.Position.Y, 9, Ground(fish.Position.X) - fish.Size * 0.35f);
        foreach (var rock in Rocks)
        {
            var radius = rock.Width / 2 + fish.Size * 0.4f;
            var dx = (fish.Position.X - rock.X) / radius;
            if (Math.Abs(dx) >= 1) continue;
            var top = rock.Y - rock.Height * MathF.Sqrt(1 - dx * dx) - fish.Size * 0.3f;
            if (fish.Position.Y > top)
            {
                fish.Position.Y = top;
                fish.Velocity.Y = Math.Min(0, fish.Velocity.Y);
            }
        }
    }

    private static Vector2 Direction(Vector2 v) => v.LengthSquared() < 0.001f ? Vector2.Zero : Vector2.Normalize(v);
    private static Vector2 Limit(Vector2 v, float max) => v.LengthSquared() > max * max ? Direction(v) * max : v;
}
