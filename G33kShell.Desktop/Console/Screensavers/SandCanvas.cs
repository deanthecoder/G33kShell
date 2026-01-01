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

[DebuggerDisplay("SandCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class SandCanvas : ScreensaverBase
{
    private const int ResetThresholdY = 3;
    private const double NoiseSpeed = 0.45;
    private const double NoiseFrequency = 1.6;
    private const double NoiseFollow = 0.25;
    private const int InitialSpawnInterval = 6;
    private const int SandFallInterval = 2;

    private ScreenData m_originalScreen;
    private ScreenData m_state;
    private double m_noiseTime;
    private double m_spawnX;
    private double m_nextSpawnResetTime;
    private Phase m_phase;
    private bool[,] m_initialMask;
    private bool[,] m_activeInitialMask;
    private readonly List<ActiveInitial> m_activeInitials = new();

    public SandCanvas(int width, int height) : base(width, height)
    {
        Name = "sand";
    }

    public override void StartScreensaver(ScreenData shellScreen)
    {
        base.StartScreensaver(shellScreen);

        // Clone the content of the current shell screen.
        using (Screen.Lock(out var screen))
            shellScreen.CopyTo(screen, 0, 0);

        m_originalScreen = shellScreen.Clone();
        m_state = new ScreenData(m_originalScreen.Width, m_originalScreen.Height);
        m_state.ClearChars(' ');
        m_originalScreen.CopyTo(m_state, 0, 0);
        BuildInitialMask();
        m_phase = Phase.SettlingInitial;
        m_noiseTime = Random.Shared.NextDouble() * 1000.0;
        m_spawnX = Width > 0 ? Width / 2.0 : 0.0;
        m_nextSpawnResetTime = Time + 4.0 + Random.Shared.NextDouble() * 4.0;
        m_activeInitials.Clear();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        if (m_state == null || m_originalScreen == null)
            return;

        if (m_phase == Phase.SettlingInitial)
            UpdateInitialSettling();
        else
            UpdateSandRain();

        m_state.CopyTo(screen, 0, 0);
    }

    private void ResetState()
    {
        m_state = new ScreenData(m_originalScreen.Width, m_originalScreen.Height);
        m_state.ClearChars(' ');
        m_originalScreen.CopyTo(m_state, 0, 0);
        BuildInitialMask();
        m_phase = Phase.SettlingInitial;
        m_noiseTime = Random.Shared.NextDouble() * 1000.0;
        m_spawnX = Width > 0 ? Width / 2.0 : 0.0;
        m_nextSpawnResetTime = Time + 4.0 + Random.Shared.NextDouble() * 4.0;
        m_activeInitials.Clear();
    }

    private void UpdateInitialSettling()
    {
        if (FrameNumber % InitialSpawnInterval == 0)
            TryAddActiveInitial();

        if (m_activeInitials.Count == 0)
            TryAddActiveInitial();

        if (m_activeInitials.Count == 0)
        {
            m_phase = Phase.Raining;
            return;
        }

        m_activeInitials.Sort((a, b) => b.Y.CompareTo(a.Y));
        for (var i = 0; i < m_activeInitials.Count; i++)
        {
            var active = m_activeInitials[i];
            if (TryMoveInitialStep(ref active))
            {
                m_activeInitials[i] = active;
                continue;
            }

            m_activeInitialMask[active.X, active.Y] = false;
            m_activeInitials.RemoveAt(i);
            i--;
        }

        if (m_activeInitials.Count == 0 && !HasMovableInitial())
            m_phase = Phase.Raining;
    }

    private void TryAddActiveInitial()
    {
        var width = m_state.Width;
        var height = m_state.Height;
        var attempts = Math.Max(1, Math.Min(width, 8));

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var x = Random.Shared.Next(width);
            for (var y = height - 2; y >= 0; y--)
            {
                if (!IsInitial(x, y) || IsActiveInitial(x, y) || !CanMoveInitial(x, y))
                    continue;

                m_activeInitials.Add(new ActiveInitial(x, y));
                m_activeInitialMask[x, y] = true;
                return;
            }
        }

        for (var y = height - 2; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsInitial(x, y) || IsActiveInitial(x, y) || !CanMoveInitial(x, y))
                    continue;

                m_activeInitials.Add(new ActiveInitial(x, y));
                m_activeInitialMask[x, y] = true;
                return;
            }
        }
    }

    private bool HasMovableInitial()
    {
        if (m_initialMask == null)
            return false;

        var width = m_state.Width;
        var height = m_state.Height;
        for (var y = height - 2; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                if (!IsInitial(x, y))
                    continue;
                if (IsActiveInitial(x, y))
                    continue;
                if (CanMoveInitial(x, y))
                    return true;
            }
        }

        return false;
    }

    private bool CanMoveInitial(int x, int y)
    {
        if (y >= m_state.Height - 1)
            return false;

        if (IsEmpty(x, y + 1))
            return true;

        var canLeft = x > 0 && IsEmpty(x - 1, y + 1);
        var canRight = x < m_state.Width - 1 && IsEmpty(x + 1, y + 1);
        return canLeft || canRight;
    }

    private bool TryMoveInitialStep(ref ActiveInitial active)
    {
        var x = active.X;
        var y = active.Y;

        if (y >= m_state.Height - 1)
            return false;

        if (IsEmpty(x, y + 1))
        {
            MoveInitialActive(x, y, x, y + 1);
            active.Y += 1;
            return true;
        }

        var canLeft = x > 0 && IsEmpty(x - 1, y + 1);
        var canRight = x < m_state.Width - 1 && IsEmpty(x + 1, y + 1);
        if (canLeft && canRight)
        {
            var nextX = Random.Shared.Next(2) == 0 ? x - 1 : x + 1;
            MoveInitialActive(x, y, nextX, y + 1);
            active.X = nextX;
            active.Y += 1;
            return true;
        }

        if (canLeft)
        {
            MoveInitialActive(x, y, x - 1, y + 1);
            active.X -= 1;
            active.Y += 1;
            return true;
        }

        if (canRight)
        {
            MoveInitialActive(x, y, x + 1, y + 1);
            active.X += 1;
            active.Y += 1;
            return true;
        }

        return false;
    }

    private void UpdateSandRain()
    {
        if (FrameNumber % SandFallInterval != 0)
            return;

        if (Time >= m_nextSpawnResetTime)
            ResetSpawnX();

        SpawnSand();
        StepSandRain();
        UpdateSandAppearance();
        UpdateFallingSandBrightness();

        if (ShouldReset())
            ResetState();
    }

    private void StepSandRain()
    {
        var width = m_state.Width;
        var height = m_state.Height;
        var occupiedAtStart = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                occupiedAtStart[x, y] = !IsEmpty(x, y);
        }

        for (var y = height - 2; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var ch = m_state.Chars[y][x].Ch;
                if (IsInitial(x, y))
                    continue;
                if (!IsSand(ch))
                    continue;

                TryMoveSand(x, y, occupiedAtStart);
            }
        }
    }

    private void TryMoveSand(int x, int y, bool[,] occupiedAtStart)
    {
        if (y >= m_state.Height - 1)
            return;

        if (IsEmptyFromStart(x, y + 1, occupiedAtStart))
        {
            MoveCell(x, y, x, y + 1);
            return;
        }

        var canLeft = x > 0 && IsEmptyFromStart(x - 1, y + 1, occupiedAtStart);
        var canRight = x < m_state.Width - 1 && IsEmptyFromStart(x + 1, y + 1, occupiedAtStart);
        if (canLeft && canRight)
        {
            if (Random.Shared.Next(2) == 0)
                MoveCell(x, y, x - 1, y + 1);
            else
                MoveCell(x, y, x + 1, y + 1);
            return;
        }

        if (canLeft)
        {
            MoveCell(x, y, x - 1, y + 1);
            return;
        }

        if (canRight)
            MoveCell(x, y, x + 1, y + 1);
    }

    private void UpdateSandAppearance()
    {
        for (var y = 0; y < m_state.Height; y++)
        {
            for (var x = 0; x < m_state.Width; x++)
            {
                if (!IsSand(m_state.Chars[y][x].Ch))
                    continue;
                if (!IsSettled(x, y))
                    continue;

                var neighbors = CountNonEmptyNeighbors(x, y);
                var next = neighbors <= 4 ? '.' : neighbors == 5 ? 'o' : 'O';
                SetSandCharOnly(x, y, next);
            }
        }
    }

    private void UpdateFallingSandBrightness()
    {
        for (var y = 0; y < m_state.Height; y++)
        {
            for (var x = 0; x < m_state.Width; x++)
            {
                if (m_state.Chars[y][x].Ch != '.')
                    continue;
                if (IsSettled(x, y))
                    continue;
                ApplySandBrightness(x, y);
            }
        }
    }

    private int CountNonEmptyNeighbors(int x, int y)
    {
        var count = 0;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || nx >= m_state.Width || ny < 0 || ny >= m_state.Height)
                    continue;
                if (!IsEmpty(nx, ny))
                    count++;
            }
        }

        return count;
    }

    private void SpawnSand()
    {
        m_noiseTime += NoiseSpeed;
        var noise = (m_noiseTime * NoiseFrequency, 17.23).SmoothNoise();
        var targetX = noise * (m_state.Width - 1);
        m_spawnX = NoiseFollow.Lerp(m_spawnX, targetX);
        var x = (int)Math.Round(m_spawnX);
        x = Math.Clamp(x, 0, m_state.Width - 1);

        if (!IsEmpty(x, 0))
            return;
        SetSandCharOnly(x, 0, '.');
        ApplySandBrightness(x, 0);
    }

    private void ResetSpawnX()
    {
        m_spawnX = Random.Shared.NextDouble() * Math.Max(1, m_state.Width - 1);
        m_noiseTime = Random.Shared.NextDouble() * 1000.0;
        m_nextSpawnResetTime = Time + 4.0 + Random.Shared.NextDouble() * 4.0;
    }

    private bool ShouldReset()
    {
        var threshold = Math.Min(ResetThresholdY, m_state.Height - 2);
        if (threshold < 0)
            threshold = 0;
        for (var y = 0; y < m_state.Height; y++)
        {
            for (var x = 0; x < m_state.Width; x++)
            {
                if (!IsSettled(x, y))
                    continue;
                return y <= threshold;
            }
        }

        return false;
    }

    private bool IsSettled(int x, int y)
    {
        if (IsEmpty(x, y))
            return false;

        if (y >= m_state.Height - 1)
            return true;

        if (IsEmpty(x, y + 1))
            return false;

        var canLeft = x > 0 && IsEmpty(x - 1, y + 1);
        var canRight = x < m_state.Width - 1 && IsEmpty(x + 1, y + 1);
        return !canLeft && !canRight;
    }

    private bool IsEmpty(int x, int y)
    {
        var ch = m_state.Chars[y][x].Ch;
        return ch is ' ' or '\0';
    }

    private bool IsEmptyFromStart(int x, int y, bool[,] occupiedAtStart) =>
        !occupiedAtStart[x, y] && IsEmpty(x, y);

    private static bool IsSand(char ch) => ch is '.' or 'o' or 'O';

    private bool IsInitial(int x, int y) =>
        m_initialMask != null && m_initialMask[x, y];

    private bool IsActiveInitial(int x, int y) =>
        m_activeInitialMask != null && m_activeInitialMask[x, y];

    private void MoveCell(int fromX, int fromY, int toX, int toY)
    {
        var source = m_state.Chars[fromY][fromX];
        var target = m_state.Chars[toY][toX];
        target.Set(source);
        ClearCell(fromX, fromY);
    }

    private void MoveInitialActive(int fromX, int fromY, int toX, int toY)
    {
        MoveCell(fromX, fromY, toX, toY);
        m_initialMask[toX, toY] = true;
        m_initialMask[fromX, fromY] = false;
        m_activeInitialMask[toX, toY] = true;
        m_activeInitialMask[fromX, fromY] = false;
    }

    private void ClearCell(int x, int y)
    {
        var cell = m_state.Chars[y][x];
        cell.Set(' ');
        cell.Foreground = Foreground;
        cell.Background = Background;
    }

    private void SetSandCharOnly(int x, int y, char ch)
    {
        var cell = m_state.Chars[y][x];
        cell.Set(ch);
        cell.Background ??= Background;
    }

    private void ApplySandBrightness(int x, int y)
    {
        var brightness = 0.3 + 0.7 * ((Math.Sin(Time * 0.8) + 1.0) * 0.5);
        m_state.Chars[y][x].Foreground = brightness.Lerp(Background, Foreground);
    }

    private void BuildInitialMask()
    {
        m_initialMask = new bool[m_state.Width, m_state.Height];
        m_activeInitialMask = new bool[m_state.Width, m_state.Height];
        for (var y = 0; y < m_state.Height; y++)
        {
            for (var x = 0; x < m_state.Width; x++)
                m_initialMask[x, y] = !IsEmpty(x, y);
        }
    }

    private enum Phase
    {
        SettlingInitial,
        Raining
    }

    private struct ActiveInitial
    {
        public ActiveInitial(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }
}
