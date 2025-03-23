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

namespace G33kShell.Desktop.Console.Screensavers.Snake;

public class GameState
{
    private readonly Flags m_flags;

    [Flags]
    public enum Flags : ushort
    {
        None = 0,
        DangerStraight = 1 << 0,
        DangerLeft = 1 << 1,
        DangerRight = 1 << 2,
        MovingLeft = 1 << 3,
        MovingRight = 1 << 4,
        MovingUp = 1 << 5,
        MovingDown = 1 << 6,
        FoodStraight = 1 << 7,
        FoodLeftRelative = 1 << 8,
        FoodRightRelative = 1 << 9
    }

    public GameState(Flags flags)
    {
        m_flags = flags;
    }

    public override bool Equals(object obj) =>
        obj is GameState other && m_flags == other.m_flags;

    public override int GetHashCode() => (int)m_flags;
}