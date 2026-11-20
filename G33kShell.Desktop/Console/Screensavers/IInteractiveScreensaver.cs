// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// If you modify the code, please retain this copyright header.
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using Avalonia.Input;

namespace G33kShell.Desktop.Console.Screensavers;

/// <summary>
/// Receives keyboard input while a screensaver is being played interactively.
/// </summary>
/// <remarks>
/// The host consumes input before the console and reserves Escape for dismissal.
/// </remarks>
public interface IInteractiveScreensaver
{
    bool IsInteractive { get; }
    void SetKey(Key key, bool down);
    void ClearInput();
}
