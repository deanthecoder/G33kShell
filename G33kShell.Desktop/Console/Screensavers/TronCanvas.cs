// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// A screensaver that draws split, turning ASCII trails with a tracking camera.
/// </summary>
[DebuggerDisplay("TronCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class TronCanvas : ScreensaverBase
{
    private const double HeadSpeed = 0.6;
    private const double HeadSpeedJitter = 0.2;
    private const int MaxHeads = 7;
    private const int MinSpawnFrames = 25;
    private const int MaxSpawnFrames = 70;
    private const int MinVerticalSteps = 2;
    private const int MaxVerticalSteps = 6;
    private const int MinSplitVerticalSteps = 3;
    private const int MaxSplitVerticalSteps = 9;
    private const int MinSplitCooldown = 40;
    private const int MaxSplitCooldown = 120;
    private const double TurnChance = 0.02;
    private const double SplitChance = 0.03;
    private const double MaxTrailAge = 240.0;
    private const double StripePeriod = 16.0;
    private const double StripeScrollPerFrame = 0.6;
    private const double CameraSmoothX = 0.04;
    private const double CameraSmoothY = 0.04;
    private const double CameraEase = 0.518;

    private readonly List<Head> m_heads = [];
    private readonly Dictionary<(int X, int Y), Segment> m_segments = [];
    private int m_spawnCountdown;
    private double m_cameraX;
    private double m_cameraY;
    private double m_cameraVelX;
    private double m_cameraVelY;
    private double m_stripePhase;

    public TronCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 60)
    {
        Name = "tron";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);
        m_heads.Clear();
        m_segments.Clear();
        m_cameraX = 0.0;
        m_cameraY = 0.0;
        m_cameraVelX = 0.0;
        m_cameraVelY = 0.0;
        m_stripePhase = 0.0;
        m_spawnCountdown = Random.Shared.Next(MinSpawnFrames, MaxSpawnFrames + 1);
    }

    public override void UpdateFrame(ScreenData screen)
    {
        m_stripePhase += StripeScrollPerFrame;
        UpdateSegmentsAge();
        MoveHeads();
        UpdateCamera();
        PruneHeadsOutsideView();
        MaybeSpawnHead();

        screen.Clear(Foreground, Background);
        DrawSegments(screen);
        DrawHeads(screen);
    }

    private void UpdateSegmentsAge()
    {
        if (m_segments.Count == 0)
            return;

        var toRemove = new List<(int X, int Y)>();
        foreach (var kvp in m_segments)
        {
            kvp.Value.Age += 1.0;
            if (kvp.Value.Age > MaxTrailAge)
                toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
            m_segments.Remove(key);
    }

    private void MoveHeads()
    {
        if (m_heads.Count == 0)
            return;

        var newHeads = new List<Head>();
        var removeHeads = new HashSet<Head>();

        foreach (var head in m_heads)
        {
            head.Progress += head.Speed;
            while (head.Progress >= 1.0)
            {
                head.Progress -= 1.0;
                StepHead(head, newHeads, removeHeads);
                if (removeHeads.Contains(head))
                    break;
            }
        }

        if (removeHeads.Count > 0)
            m_heads.RemoveAll(removeHeads.Contains);

        if (newHeads.Count == 0)
            return;

        foreach (var head in newHeads)
        {
            if (m_heads.Count >= MaxHeads)
                break;
            m_heads.Add(head);
        }
    }

    private void StepHead(Head head, List<Head> newHeads, HashSet<Head> removeHeads)
    {
        var (dx, dy) = head.Dir switch
        {
            Direction.Up => (0, -1),
            Direction.Right => (1, 0),
            Direction.Down => (0, 1),
            _ => (0, 0)
        };

        var from = (head.X, head.Y);
        var to = (head.X + dx, head.Y + dy);

        if (m_segments.ContainsKey(to))
        {
            removeHeads.Add(head);
            return;
        }

        head.X = to.Item1;
        head.Y = to.Item2;

        AddConnection(from, to, head.Dir);

        if (head.VerticalRemaining > 0)
        {
            head.VerticalRemaining--;
            if (head.VerticalRemaining == 0)
                head.Dir = Direction.Right;
        }
        else if (head.Dir == Direction.Right && Random.Shared.NextDouble() < TurnChance)
        {
            head.Dir = Random.Shared.NextBool() ? Direction.Up : Direction.Down;
            head.VerticalRemaining = Random.Shared.Next(MinVerticalSteps, MaxVerticalSteps + 1);
        }

        if (head.SplitCooldown > 0)
            head.SplitCooldown--;

        if (head.Dir == Direction.Right &&
            head.VerticalRemaining == 0 &&
            head.SplitCooldown == 0 &&
            Random.Shared.NextDouble() < SplitChance &&
            m_heads.Count + newHeads.Count < MaxHeads)
        {
            var upHead = head.Clone();
            upHead.Dir = Direction.Up;
            upHead.VerticalRemaining = Random.Shared.Next(MinSplitVerticalSteps, MaxSplitVerticalSteps + 1);
            upHead.Speed = HeadSpeed * Random.Shared.NextDouble().Lerp(1.0 - HeadSpeedJitter, 1.0 + HeadSpeedJitter);
            upHead.SplitCooldown = Random.Shared.Next(MinSplitCooldown, MaxSplitCooldown + 1);

            var downHead = head.Clone();
            downHead.Dir = Direction.Down;
            downHead.VerticalRemaining = Random.Shared.Next(MinSplitVerticalSteps, MaxSplitVerticalSteps + 1);
            downHead.Speed = HeadSpeed * Random.Shared.NextDouble().Lerp(1.0 - HeadSpeedJitter, 1.0 + HeadSpeedJitter);
            downHead.SplitCooldown = Random.Shared.Next(MinSplitCooldown, MaxSplitCooldown + 1);

            newHeads.Add(upHead);
            newHeads.Add(downHead);
            removeHeads.Add(head);
        }
    }

    private void AddConnection((int X, int Y) from, (int X, int Y) to, Direction dir)
    {
        UpdateSegment(from, GetMask(dir));
        UpdateSegment(to, GetMask(Opposite(dir)));
    }

    private void UpdateSegment((int X, int Y) pos, int connection)
    {
        if (!m_segments.TryGetValue(pos, out var segment))
        {
            segment = new Segment();
            m_segments[pos] = segment;
            segment.Stripe = 0.75 + 0.25 * Math.Sin(m_stripePhase / StripePeriod * Math.Tau - Math.PI / 2.0);
        }

        segment.Connections |= connection;
        segment.Age = 0.0;
    }

    private void UpdateCamera()
    {
        if (m_heads.Count == 0)
            return;

        var lead = m_heads[0];
        for (var i = 1; i < m_heads.Count; i++)
        {
            if (m_heads[i].X > lead.X)
                lead = m_heads[i];
        }

        var leadX = (double)lead.X;
        var leadY = (double)lead.Y;

        var desiredX = leadX - Width * 0.7;
        var minX = leadX - (Width - 1);
        var maxX = leadX;
        desiredX = desiredX.Clamp(minX, maxX);

        var smoothedX = CameraSmoothX.Lerp(m_cameraX, desiredX);
        if (smoothedX < m_cameraX)
            smoothedX = m_cameraX;
        m_cameraVelX = m_cameraVelX * (1.0 - CameraEase) + (smoothedX - m_cameraX) * CameraEase;
        m_cameraX += m_cameraVelX;
        if (m_cameraX < minX)
            m_cameraX = minX;
        if (m_cameraX > maxX)
            m_cameraX = maxX;

        var desiredY = leadY - Height * 0.5;
        var minY = leadY - (Height - 1);
        var maxY = leadY;
        desiredY = desiredY.Clamp(minY, maxY);
        var smoothedY = CameraSmoothY.Lerp(m_cameraY, desiredY);
        m_cameraVelY = m_cameraVelY * (1.0 - CameraEase) + (smoothedY - m_cameraY) * CameraEase;
        m_cameraY += m_cameraVelY;

        if (leadY < m_cameraY)
            m_cameraY = leadY;
        else if (leadY > m_cameraY + Height - 1)
            m_cameraY = leadY - (Height - 1);
    }

    private void PruneHeadsOutsideView()
    {
        if (m_heads.Count == 0)
            return;

        var camX = (int)Math.Floor(m_cameraX);
        var camY = (int)Math.Floor(m_cameraY);
        var left = camX;
        var right = camX + Width - 1;
        var top = camY;
        var bottom = camY + Height - 1;

        m_heads.RemoveAll(o => o.X < left || o.X > right || o.Y < top || o.Y > bottom);
    }

    private void MaybeSpawnHead()
    {
        if (m_spawnCountdown > 0)
        {
            m_spawnCountdown--;
            return;
        }

        if (m_heads.Count < MaxHeads)
            SpawnHead();

        m_spawnCountdown = Random.Shared.Next(MinSpawnFrames, MaxSpawnFrames + 1);
    }

    private void SpawnHead()
    {
        var camX = (int)Math.Floor(m_cameraX);
        var camY = (int)Math.Floor(m_cameraY);
        var y = camY + Random.Shared.Next(0, Math.Max(1, Height));

        var head = new Head(camX, y)
        {
            Dir = Direction.Right,
            Speed = HeadSpeed * Random.Shared.NextDouble().Lerp(1.0 - HeadSpeedJitter, 1.0 + HeadSpeedJitter),
            SplitCooldown = Random.Shared.Next(MinSplitCooldown, MaxSplitCooldown + 1)
        };
        m_heads.Add(head);
    }

    private void DrawSegments(ScreenData screen)
    {
        if (m_segments.Count == 0)
            return;

        var camX = (int)Math.Floor(m_cameraX);
        var camY = (int)Math.Floor(m_cameraY);

        foreach (var kvp in m_segments)
        {
            var sx = kvp.Key.X - camX;
            var sy = kvp.Key.Y - camY;
            if (sx < 0 || sy < 0 || sx >= screen.Width || sy >= screen.Height)
                continue;

            var fadeOut = (1.0 - kvp.Value.Age / MaxTrailAge).Clamp(0.0, 1.0);
            var brightness = (fadeOut * kvp.Value.Stripe).Clamp(0.0, 1.0);
            var color = brightness.Lerp(Background, Foreground);
            var ch = GetCharForMask(kvp.Value.Connections);

            screen.PrintAt(sx, sy, ch);
            screen.SetForeground(sx, sy, color);
        }
    }

    private void DrawHeads(ScreenData screen)
    {
        if (m_heads.Count == 0)
            return;

        var camX = (int)Math.Floor(m_cameraX);
        var camY = (int)Math.Floor(m_cameraY);

        foreach (var head in m_heads)
        {
            var sx = head.X - camX;
            var sy = head.Y - camY;
            if (sx < 0 || sy < 0 || sx >= screen.Width || sy >= screen.Height)
                continue;

            screen.PrintAt(sx, sy, 'O');
            screen.SetForeground(sx, sy, Foreground);
        }
    }

    private static int GetMask(Direction dir) =>
        dir switch
        {
            Direction.Up => UpMask,
            Direction.Right => RightMask,
            Direction.Down => DownMask,
            Direction.Left => LeftMask,
            _ => 0
        };

    private static Direction Opposite(Direction dir) =>
        dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Right => Direction.Left,
            Direction.Down => Direction.Up,
            _ => Direction.Right
        };

    private static char GetCharForMask(int mask)
    {
        var up = (mask & UpMask) != 0;
        var right = (mask & RightMask) != 0;
        var down = (mask & DownMask) != 0;
        var left = (mask & LeftMask) != 0;

        if (up && right && down && left) return '┼';
        if (up && down && left) return '┤';
        if (up && down && right) return '├';
        if (up && down) return '│';
        if (left && right && up) return '┴';
        if (left && right && down) return '┬';
        if (left && down) return '┐';
        if (left && up) return '┘';
        if (left && right) return '─';
        if (right && down) return '┌';
        if (right && up) return '└';
        if (left || right) return '─';
        return '│';
    }

    private sealed class Segment
    {
        public int Connections { get; set; }
        public double Age { get; set; }
        public double Stripe { get; set; } = 1.0;
    }

    private sealed class Head
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Direction Dir { get; set; } = Direction.Right;
        public int VerticalRemaining { get; set; }
        public double Speed { get; set; }
        public double Progress { get; set; }
        public int SplitCooldown { get; set; }

        public Head(int x, int y)
        {
            X = x;
            Y = y;
        }

        public Head Clone() =>
            new Head(X, Y)
            {
                Dir = Dir,
                VerticalRemaining = VerticalRemaining,
                Speed = Speed,
                Progress = Progress,
                SplitCooldown = SplitCooldown
            };
    }

    private enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    private const int UpMask = 1 << 0;
    private const int RightMask = 1 << 1;
    private const int DownMask = 1 << 2;
    private const int LeftMask = 1 << 3;
}
