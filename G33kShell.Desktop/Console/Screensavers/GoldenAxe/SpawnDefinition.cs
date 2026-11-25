// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kShell.Desktop.Console.Screensavers.GoldenAxe;

/// <summary>
/// An enemy or prop entry from an arcade area spawn script.
/// </summary>
/// <remarks>
/// Values retain the original extracted arcade data format.
/// </remarks>
internal record SpawnDefinition(int Trigger, int X, int Z, int Parameter, int Routine)
{
    public bool IsEnemy => Routine is
        0x1b454 or 0x20414 or 0x23bda or 0x2536e or 0x2538a or 0x25352 or
        0x2a338 or 0x29364 or 0x1a0a8 or 0x1f016 or 0x21854 or 0x21876 or
        0x229c8 or 0x2a358 or 0x18b50 or 0x18b28 or 0x317b8 or 0x18b3c or
        0x18ba6 or 0x1c846 or 0x1c868 or 0x1dc3e or 0x282ec or 0x282d4;
}
