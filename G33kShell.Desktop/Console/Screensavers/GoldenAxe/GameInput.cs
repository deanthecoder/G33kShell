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
/// Inputs for one 60 Hz simulation tick.
/// </summary>
/// <remarks>
/// Controllers request actions; they never change fighter state directly.
/// </remarks>
internal readonly record struct GameInput(int X = 0, int Z = 0, bool Attack = false, bool Jump = false, bool Magic = false, bool Run = false);
