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

namespace G33kShell.Desktop.Console;

public class ConsoleCursor
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public bool IsBusy { get; set; }
    public bool IsVisible { get; set; }

    /// <summary>
    /// The time at which the cursor position was last updated.
    /// </summary>
    /// <remarks>
    /// Used to ensure a regular flash event, reset when the cursor moves.
    /// </remarks>
    public long MoveTime { get; private set; }

    public void SetPos(int x, int y)
    {
        if (X == x && Y == y)
            return; // Nothing to do.
        
        X = x;
        Y = y;
        MoveTime = Environment.TickCount64;
    }

    public override string ToString() =>
        $"({X},{Y})";
}