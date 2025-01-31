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
using CSharp.Core;

namespace G33kShell.Desktop.Console.Events;

/// <summary>
/// Represents a key console event.
/// </summary>
public class KeyConsoleEvent : ConsoleEvent
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public KeyDirection Direction { get; }

    public enum KeyDirection
    {
        Up,
        Down
    }

    public KeyConsoleEvent(Key key, KeyModifiers modifiers, KeyDirection direction)
    {
        Key = key;
        Modifiers = modifiers;
        Direction = direction;
    }

    public override string ToString() =>
        $"Key: {Key}, Modifiers: {Modifiers}, Direction: {Direction}";

    public char GetChar()
    {
        var s = Key.ToString();
        var shiftPressed = Modifiers.HasFlag(KeyModifiers.Shift);

        // Support keypad numbers.
        s = s.Replace("NumPad", "D");

        // Support digits.
        var isUkKeyboard = KeyboardLayoutChecker.IsUk();
        if (s.Length == 2 && s[0] == 'D')
        {
            // Digit.
            s = s[1].ToString();
            
            // Symbol?
            if (shiftPressed)
            {
                var symbols = isUkKeyboard ? ")!\"£$%^&*(" : ")!@#$%^&*(";
                s = symbols[int.Parse(s)].ToString();
            }
        }

        // Single character.
        if (s.Length == 1)
            return s[0];

        return Key switch
        {
            Key.Space => ' ',
            Key.OemComma => shiftPressed ? '<' : ',',
            Key.OemPeriod => shiftPressed ? '>' : '.',
            Key.OemQuestion => shiftPressed ? '?' : '/',
            Key.OemOpenBrackets => shiftPressed ? '{' : '[',
            Key.OemCloseBrackets => shiftPressed ? '}' : ']',
            Key.OemSemicolon => shiftPressed ? ':' : ';',
            Key.OemQuotes => shiftPressed ? isUkKeyboard ? '~' : '"' : isUkKeyboard ? '#' : '\'',
            Key.OemBackslash => shiftPressed ? '|' : '\\',
            Key.OemPipe => shiftPressed ? '|' : '\\',
            Key.OemTilde => shiftPressed ? isUkKeyboard ? '@' : '~' : isUkKeyboard ? '\'' : '`',
            Key.OemMinus => shiftPressed ? '_' : '-',
            Key.OemPlus => shiftPressed ? '+' : '=',
            Key.Oem8 =>  shiftPressed ? '¬' : '`',
            _ => '\0'
        };
    }
}