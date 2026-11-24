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
    public AnimationStep PreviousPose { get; private set; }
    public int PoseTicks { get; private set; }
    public Animation(ArcadeData data) => m_data = data;

    public void Play(int address, bool restart = false, bool attack = false)
    {
        if (Address == address && !restart)
            return;
        var previous = Steps == null ? null : Current;
        Address = address;
        Steps = m_data.Animations[address];
        SelectFrame(attack ? FirstAttackStep(Steps) : 0, previous);
    }

    public static int FirstAttackStep(AnimationStep[] steps) => steps.Length > 1 && steps[0].Duration >= 30 ? 1 : 0;

    public int TicksUntilStep(int target)
    {
        if (target < Index || target >= Steps.Length)
            return -1;
        if (target == Index)
            return 0;

        var ticks = m_remaining;
        for (var step = Index + 1; step < target; step++)
            ticks += Steps[step].Duration;
        return ticks;
    }

    public void Seek(int index) => SelectFrame(index, Current);

    public void HoldFrame(int frame)
    {
        var previous = Current;
        Address = 0;
        Steps = [new AnimationStep(frame, 255, 1, 0, 0, [0, 0, 0, 0])];
        SelectFrame(0, previous);
    }

    private void SelectFrame(int index, AnimationStep previous)
    {
        Index = Math.Clamp(index, 0, Steps.Length - 1);
        var current = Current;
        m_remaining = current.Duration;
        if (previous == null || current.Frame != previous.Frame || current.Flags != previous.Flags ||
            current.Dx != previous.Dx || current.Dy != previous.Dy)
        {
            PreviousPose = previous;
            PoseTicks = 0;
        }
    }

    public bool Tick()
    {
        PoseTicks++;
        if (--m_remaining > 0)
            return false;
        var wrapped = Index + 1 == Steps.Length;
        Seek(wrapped ? 0 : Index + 1);
        return wrapped;
    }
}
