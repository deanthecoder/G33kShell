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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
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
/// <remarks>
/// This control supports the following features:
/// - Prefix: Allows setting a non-editable prefix string that appears at the beginning of the text.
/// - Control-Backspace: Clears the entire text field.
/// - Cursor Navigation: Supports moving the cursor with arrow keys, Home, and End keys.
/// - Clipboard Paste: Allows pasting text from the clipboard using Control-V.
/// </remarks>
[DebuggerDisplay("TextBox:{X},{Y} {Width}x{Height} T:{Text}")]
public class TextBox : TextBlock
{
    private readonly StringBuilder m_s = new StringBuilder();
    private bool m_waitForKeyUp;
    private Key m_previousKey;
    private int m_cursorIndex;
    private string m_prefix;

    public event EventHandler<string> ReturnPressed;

    public override string[] Text => WrapText($"{Prefix}{m_s}", Width).ToArray();
    public string TextWithoutPrefix => m_s.ToString();
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Use 'yield return' to wrap the text string into an array with items up to 'width' characters, splitting at characters (not words).
    /// </summary>
    private static IEnumerable<string> WrapText(string s, int width)
    {
        var lines = s.Split('\n');

        foreach (var line in lines)
        {
            if (line.Length <= width)
            {
                // String will fit within the available space - All good.
                yield return line;
            }
            else
            {
                // Line is too long - Chunk it into 'width' chunks.
                var temp = new StringBuilder(line);
                while (temp.Length > width)
                {
                    var wrapText = temp.ToString(0, width).TrimEnd();
                    yield return wrapText;
                    temp = temp.Remove(0, width);
                }

                // Any bits left over?
                if (temp.Length > 0)
                    yield return temp.ToString();
            }
        }
    }

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
        if (consoleEvent is not KeyConsoleEvent keyEvent || IsReadOnly)
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
        var actionKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;
        var controlPressed = keyEvent.Modifiers.HasFlag(actionKey);
        var actionPerformed = keyEvent.Key switch
        {
            Key.Left => MoveCursor(controlPressed ? GetDistanceToWordStart() : -1),
            Key.Right => MoveCursor(controlPressed ? GetDistanceToWordEnd() : 1),
            Key.Up => OnUpArrow(),
            Key.Down => OnDownArrow(),
            Key.Home => SetCursor(0),
            Key.End => MoveCursorToEnd(),
            Key.Back when controlPressed => Clear(),
            Key.Back when m_cursorIndex > 0 => Backspace(),
            Key.Delete when m_cursorIndex < m_s.Length => Delete(),
            Key.V when controlPressed => ClipboardPaste(),
            Key.Return => Return(),
            _ => false
        };

        // Handle printable characters.
        if (!actionPerformed && !controlPressed)
        {
            var ch = keyEvent.GetChar();
            if (IsPrintableChar(ch))
            {
                if (!keyEvent.Modifiers.HasFlag(KeyModifiers.Shift))
                    ch = char.ToLower(ch);
                m_s.Insert(m_cursorIndex++, ch);
                actionPerformed = true;
            }
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

    /// <summary>
    /// Perform the logic when the up arrow key is pressed in the TextBox control.
    /// </summary>
    /// <returns>
    /// True if the arrow key functionality was handled.
    /// </returns>
    protected virtual bool OnUpArrow()
    {
        // Do nothing.
        return false;
    }

    /// <summary>
    /// Perform the logic when the down arrow key is pressed in the TextBox control.
    /// </summary>
    /// <returns>
    /// True if the arrow key functionality was handled.
    /// </returns>
    protected virtual bool OnDownArrow()
    {
        // Do nothing.
        return false;
    }

    public bool MoveCursorToEnd() =>
        SetCursor(m_s.Length);

    public void AppendLine(string s) =>
        Append($"{s}\n");
    
    public void Append(string s)
    {
        m_s.Append(s);
        SetCursor(m_s.Length);
    }

    private bool Return()
    {
        ReturnPressed?.Invoke(this, TextWithoutPrefix);
        return true;
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

        Paste(data);

        return true;
    }

    /// <summary>
    /// Inserts the provided string at the current cursor position in the text box.
    /// </summary>
    protected void Paste(string s)
    {
        m_s.Insert(m_cursorIndex, s);
        MoveCursor(s.Length);
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
        var x = m_cursorIndex + Prefix?.Length ?? 0;
        var y = x / Width;
        SetCursorPos(x % Width, y);

        if (Text.Length > Height)
        {
            SetHeight(Text.Length);
            ScrollIntoView();
        }
        
        return true;
    }

    /// <summary>
    /// Clear the content of the TextBox and set the cursor position to the beginning.
    /// </summary>
    protected bool Clear()
    {
        m_s.Clear();
        SetCursor(0);
        return true;
    }
    
    public bool Backspace()
    {
        m_s.Remove(--m_cursorIndex, 1);
        MoveCursor(0);
        return true;
    }
    
    private bool Delete()
    {
        m_s.Remove(m_cursorIndex, 1);
        MoveCursor(0);
        return true;
    }

    private static bool IsPrintableChar(char ch) =>
        char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsWhiteSpace(ch);
}