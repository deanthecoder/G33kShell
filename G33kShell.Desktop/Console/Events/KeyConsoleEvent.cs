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
using Avalonia.Input;

namespace G33kShell.Desktop.Console.Events;

/// <summary>
/// Represents a key console event.
/// </summary>
public class KeyConsoleEvent : ConsoleEvent
{
    private readonly Key m_key;
    private readonly KeyModifiers m_modifiers;
    private readonly Direction m_direction;

    public enum Direction
    {
        Up,
        Down
    }

    public KeyConsoleEvent(Key key, KeyModifiers modifiers, Direction direction)
    {
        m_key = key;
        m_modifiers = modifiers;
        m_direction = direction;
    }

    public override string ToString() =>
        $"Key: {m_key}, Modifiers: {m_modifiers}, Direction: {m_direction}";
}