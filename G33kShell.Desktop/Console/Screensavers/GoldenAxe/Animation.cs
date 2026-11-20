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

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// A deterministic animation cursor. Script durations are simulation ticks (60 Hz).
/// </summary>
/// <remarks>
/// The simulation advances this cursor independently of rendering.
/// </remarks>
internal sealed class Animation
{
    private readonly ArcadeData m_data;
    private int m_remaining;
    public int Address { get; private set; }
    public int Index { get; private set; }
    public AnimationStep[] Steps { get; private set; }
    public AnimationStep Current => Steps[Index];
    public Animation(ArcadeData data) => m_data = data;

    public void Play(int address, bool restart = false, bool attack = false)
    {
        if (Address == address && !restart)
            return;
        Address = address;
        Steps = m_data.Animations[address];
        Seek(attack && Steps.Length > 1 && Steps[0].Duration >= 30 ? 1 : 0);
    }

    public void Seek(int index)
    {
        Index = Math.Clamp(index, 0, Steps.Length - 1);
        m_remaining = Current.Duration;
    }

    public bool Tick()
    {
        if (--m_remaining > 0)
            return false;
        var wrapped = Index + 1 == Steps.Length;
        Seek(wrapped ? 0 : Index + 1);
        return wrapped;
    }
}
