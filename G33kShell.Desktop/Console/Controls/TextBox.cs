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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Events;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// Represents an editable text box control.
/// </summary>
[DebuggerDisplay("TextBox:{X},{Y} {Width}x{Height} T:{Text}")]
public class TextBox : TextBlock
{
    private readonly StringBuilder m_s = new StringBuilder();
    private bool m_waitForKeyUp;
    private Key m_previousKey;
    private int m_cursorIndex;
    private string m_prefix;

    public override string[] Text => new[]
    {
        $"{Prefix}{m_s} "
    };

    public string Prefix
    {
        get => m_prefix;
        set
        {
            if (m_prefix == value)
                return;
            m_prefix = value;
            SetCursor(0);
            InvalidateVisual();
        }
    }

    public TextBox(int width) : base(width, 1)
    {
    }

    public override void OnLoaded()
    {
        base.OnLoaded();
        SetCursor(0);
    }

    public override void OnEvent(ConsoleEvent consoleEvent, ref bool handled)
    {
        if (consoleEvent is not KeyConsoleEvent keyEvent)
        {
            base.OnEvent(consoleEvent, ref handled);
            return;
        }

        // Handle key change and key up logic.
        if (m_waitForKeyUp && m_previousKey != keyEvent.Key)
            m_waitForKeyUp = false;
        m_previousKey = keyEvent.Key;
        
        if (m_waitForKeyUp)
        {
            m_waitForKeyUp = keyEvent.Direction == KeyConsoleEvent.KeyDirection.Down;
            base.OnEvent(consoleEvent, ref handled);
            return;
        }

        // Only process KeyDown events.
        if (keyEvent.Direction != KeyConsoleEvent.KeyDirection.Down)
        {
            base.OnEvent(consoleEvent, ref handled);
            return;
        }

        // Handle non-printable control keys.
        var controlPressed = keyEvent.Modifiers.HasFlag(KeyModifiers.Meta);
        var actionPerformed = keyEvent.Key switch
        {
            Key.Left => MoveCursor(controlPressed ? GetDistanceToWordStart() : -1),
            Key.Right => MoveCursor(controlPressed ? GetDistanceToWordEnd() : 1),
            Key.Home => SetCursor(0),
            Key.End => SetCursor(m_s.Length),
            Key.Back when m_cursorIndex > 0 => Backspace(),
            Key.V when controlPressed => ClipboardPaste(),
            _ => false
        };

        // Handle printable characters.
        var ch = keyEvent.GetChar();
        if (!actionPerformed && !controlPressed && IsPrintableChar(ch))
        {
            if (!keyEvent.Modifiers.HasFlag(KeyModifiers.Shift))
                ch = char.ToLower(ch);
            m_s.Insert(m_cursorIndex++, ch);
            actionPerformed = true;
        }

        if (actionPerformed)
        {
            m_waitForKeyUp = true;
            InvalidateVisual();
            MoveCursor(0);
            handled = true;
            
            // Auto-reset the wait for 'key up' after a short delay, to allow press-and-hold key entry.
            Task.Delay(50).ContinueWith(_ => m_waitForKeyUp = false);
        }

        base.OnEvent(consoleEvent, ref handled);
    }

    private bool ClipboardPaste()
    {
        var clipboard = Application.Current.GetMainWindow().Clipboard;
        if (clipboard == null)
            return false;

        string data = null;
        var formatsAsync = clipboard.GetFormatsAsync();
        formatsAsync.Wait();
        var formats = formatsAsync.Result;
        if (formats.Contains("FileNames"))
        {
            var dataAsync = clipboard.GetDataAsync("FileNames");
            dataAsync.Wait();
            data = ((string[])dataAsync.Result)?.Aggregate(string.Empty, (current, s) => $"{current}{s.AsFileName()} ").Trim();
        }
        
        if (data == null && formats.Contains("Text"))
        {
            var dataAsync = clipboard.GetDataAsync("Text");
            dataAsync.Wait();
            data = (string)dataAsync.Result;
        }

        if (string.IsNullOrEmpty(data))
            return false;

        m_s.Insert(m_cursorIndex, data);
        m_cursorIndex += data.Length;
        
        return true;
    }

    private int GetDistanceToWordStart()
    {
        if (m_cursorIndex <= 0)
            return 0;

        var distance = m_cursorIndex;
        if (char.IsWhiteSpace(m_s[distance - 1]))
        {
            while (distance > 0 && char.IsWhiteSpace(m_s[distance - 1]))
                distance--;
        }
        else
        {
            while (distance > 0 && !char.IsWhiteSpace(m_s[distance - 1]))
                distance--;
        }
            
        return distance - m_cursorIndex;
    }

    private int GetDistanceToWordEnd()
    {
        if (m_cursorIndex >= m_s.Length)
            return 0;

        var distance = m_cursorIndex;
        if (char.IsWhiteSpace(m_s[distance]))
        {
            while (distance < m_s.Length && char.IsWhiteSpace(m_s[distance]))
                distance++;
        }
        else
        {
            while (distance < m_s.Length && !char.IsWhiteSpace(m_s[distance]))
                distance++;
        }

        return distance - m_cursorIndex;
    }

    private bool MoveCursor(int offset) =>
        SetCursor(m_cursorIndex + offset);

    private bool SetCursor(int position)
    {
        m_cursorIndex = position.Clamp(0, m_s.Length);
        SetCursorPos(m_cursorIndex + Prefix?.Length ?? 0, 0);
        return true;
    }

    private bool Backspace()
    {
        m_s.Remove(--m_cursorIndex, 1);
        MoveCursor(0);
        return true;
    }

    private static bool IsPrintableChar(char ch) =>
        char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsWhiteSpace(ch);
}