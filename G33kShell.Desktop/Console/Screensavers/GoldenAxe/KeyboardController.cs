// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// Converts held keys and fresh button presses into game input.
/// </summary>
/// <remarks>
/// UI events and simulation ticks share a lock; short taps survive between ticks.
/// </remarks>
internal sealed class KeyboardController
{
    private readonly object m_lock = new();
    private readonly HashSet<Key> m_held = [];
    private bool m_attack, m_jump, m_magic, m_dismount;
    private Key m_lastDirection;
    private Key m_runDirection;
    private long m_lastDirectionTime;

    public void SetKey(Key key, bool down)
    {
        lock (m_lock)
        {
            if (!down)
            {
                m_held.Remove(key);
                if (key == m_runDirection)
                    m_runDirection = Key.None;
                return;
            }
            if (!m_held.Add(key))
                return;
            switch (key)
            {
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    m_attack = true;
                    break;
                case Key.Space:
                    if (m_held.Contains(Key.LeftShift) || m_held.Contains(Key.RightShift))
                        m_dismount = true;
                    else
                        m_jump = true;
                    break;
                case Key.Tab:
                    m_magic = true;
                    break;
                case Key.Left:
                case Key.Right:
                    var now = Environment.TickCount64;
                    m_runDirection = m_lastDirection == key && now - m_lastDirectionTime < 250 ? key : Key.None;
                    m_lastDirection = key;
                    m_lastDirectionTime = now;
                    break;
            }
        }
    }

    public GameInput Read()
    {
        lock (m_lock)
        {
            var x = (m_held.Contains(Key.Right) ? 1 : 0) - (m_held.Contains(Key.Left) ? 1 : 0);
            var z = (m_held.Contains(Key.Down) ? 1 : 0) - (m_held.Contains(Key.Up) ? 1 : 0);
            var attack = m_attack || m_held.Contains(Key.LeftCtrl) || m_held.Contains(Key.RightCtrl);
            var run = x != 0 && m_runDirection == (x < 0 ? Key.Left : Key.Right);
            var input = new GameInput(x, z, attack, m_jump, m_magic, run, m_dismount, AdvanceDialogue: m_attack);
            m_attack = m_jump = m_magic = m_dismount = false;
            return input;
        }
    }

    public void Clear()
    {
        lock (m_lock)
        {
            m_held.Clear();
            m_attack = m_jump = m_magic = m_dismount = false;
            m_lastDirection = m_runDirection = Key.None;
            m_lastDirectionTime = 0;
        }
    }
}
