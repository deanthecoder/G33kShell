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
[DebuggerDisplay("TextBox:{X},{Y} {Width}x{Height} T:{ToString(),nq}")]
public class TextBox : TextBlock, ICursorHost
{
    private readonly StringBuilder m_s = new StringBuilder();
    private bool m_waitForKeyUp;
    private Key m_previousKey;
    private string m_prefix;

    public event EventHandler<string> ReturnPressed;

    public override string[] Text => WrapText(Prefix, m_s, Width).ToArray();
    public string TextWithoutPrefix => m_s.ToString();
    public int CursorIndex { get; private set; }
    public ConsoleCursor Cursor { get; private set; }
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Use 'yield return' to wrap the text string into an array with items up to 'maxLength' characters, splitting at characters (not words).
    /// </summary>
    private static IEnumerable<string> WrapText(string prefix, StringBuilder content, int maxLength)
    {
        // No content? Then we just need the prefix.
        if (content.Length == 0)
        {
            yield return prefix;
            yield break;
        }

        lock (content)
        {
            try
            {
                content.Insert(0, prefix);
            
                var lines = SplitIntoLines(content, maxLength);

                foreach (var (lineStart, lineLength) in lines)
                {
                    var wrapText = content.ToString(lineStart, lineLength);
                    yield return wrapText;
                }
            }
            finally
            {
                content.Remove(0, prefix.Length);
            }
        }
    }
    private static IEnumerable<(int lineStart, int lineLength)> SplitIntoLines(StringBuilder content, int maxLength)
    {
        var lineStart = 0;
        var i = 0;
        while (lineStart < content.Length)
        {
            if (content[i] == '\n')
            {
                // Empty newline.
                yield return (lineStart++, 0);
                i = lineStart;
                continue;
            }
            // Find the next newline or end of content
            while (i < content.Length && content[i] != '\n')
                i++;

            var lineLength = i - lineStart; // Calculate the length of the current line

            // Split the line into sections if it exceeds maxLength
            for (var splitStart = lineStart; splitStart < lineStart + lineLength; splitStart += maxLength)
            {
                var splitLength = Math.Min(maxLength, lineStart + lineLength - splitStart);
                yield return (splitStart, splitLength); // Yield the start and length of the line/sub-section
            }

            if (i == content.Length)
            {
                // We're at the end of the last line.
                yield break;
            }

            // Move to the next line (skip the newline character)
            lineStart = i + 1;
            i = lineStart;
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

    public override void OnLoaded(WindowManager windowManager)
    {
        base.OnLoaded(windowManager);
        Cursor = windowManager.Cursor;
        SetCursor(0);
    }

    public override void OnEvent(ConsoleEvent consoleEvent, ref bool handled)
    {
        if (IsReadOnly)
        {
            base.OnEvent(consoleEvent, ref handled);
            return;
        }

        if (consoleEvent is PasteEvent pasteEvent)
        {
            Paste(string.Join(" ", pasteEvent.Items));
            return;
        }
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

        var actionPerformed = OnKeyEvent(keyEvent);
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
    /// Override this method to handle key events for the TextBox control.
    /// </summary>
    /// <param name="keyEvent">The KeyConsoleEvent representing the key event.</param>
    /// <returns>True if an action was performed, false otherwise.</returns>
    protected virtual bool OnKeyEvent(KeyConsoleEvent keyEvent)
    {
        // Handle non-printable control keys.
        var actionKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;
        var controlPressed = keyEvent.Modifiers.HasFlag(actionKey);
        var actionPerformed = keyEvent.Key switch
        {
            Key.Left => MoveCursor(controlPressed ? GetDistanceToWordStart() : -1),
            Key.Right => MoveCursor(controlPressed ? GetDistanceToWordEnd() : 1),
            Key.Home => SetCursor(0),
            Key.End => MoveCursorToEnd(),
            Key.Back when controlPressed => Clear(),
            Key.Back when CursorIndex > 0 => Backspace(),
            Key.Delete when CursorIndex < m_s.Length => Delete(),
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
                m_s.Insert(CursorIndex++, ch);
                actionPerformed = true;
            }
        }
        
        return actionPerformed;
    }

    public bool MoveCursorToEnd() =>
        SetCursor(m_s.Length);

    public void AppendLine(string s) =>
        Append($"{s}\n");
    
    public void Append(string s)
    {
        lock (m_s)
            m_s.Append(s);
        SetCursor(m_s.Length);
        InvalidateVisual();
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

        var trimEnd = string.Join(' ', data.Split('\n').Select(o => o.Trim()));
        Paste(trimEnd);

        return true;
    }

    /// <summary>
    /// Inserts the provided string at the current cursor position in the text box.
    /// </summary>
    protected void Paste(string s)
    {
        if (string.IsNullOrEmpty(s))
            return;

        lock (m_s)
        {
            m_s.Insert(CursorIndex, s);
            MoveCursor(s.Length);
        }
        InvalidateVisual();
    }

    private int GetDistanceToWordStart()
    {
        if (CursorIndex <= 0)
            return 0;

        var distance = CursorIndex;
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
            
        return distance - CursorIndex;
    }

    private int GetDistanceToWordEnd()
    {
        if (CursorIndex >= m_s.Length)
            return 0;

        var distance = CursorIndex;
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

        return distance - CursorIndex;
    }

    private bool MoveCursor(int offset) =>
        SetCursor(CursorIndex + offset);

    protected override void SetActualY(int value)
    {
        base.SetActualY(value);
        SetCursor(CursorIndex);
    }

    private bool SetCursor(int charOffset)
    {
        lock (m_s)
        {
            var cursorIndex = charOffset.Clamp(0, m_s.Length);
            CursorIndex = cursorIndex;
        
            // Position the cursor at the beginning of the string.
            var x = Prefix?.Length ?? 0;
            var y = ActualY;
        
            // Advance the cursor along the string.
            for (var i = 0; i < cursorIndex; i++)
            {
                // Check for explicit line break.
                if (m_s[i] == '\n')
                {
                    y++;
                    x = 0;
                    continue;
                }
            
                // ...or add newline if cursor reaches far screen right.
                if (x == Width)
                {
                    y++;
                    x = 0;
                }

                // Just jog the cursor along a character.
                x++;
            }
        
            Cursor?.SetPos(x, y);
        }

        var lineCount = WrapText(Prefix, m_s, Width).Count();
        if (lineCount > Height)
        {
            SetHeight(lineCount);
            ScrollIntoView();
        }

        return true;
    }

    /// <summary>
    /// Clear the content of the TextBox and set the cursor position to the beginning.
    /// </summary>
    protected bool Clear()
    {
        lock (m_s)
            m_s.Clear();
        SetCursor(0);
        InvalidateVisual();
        return true;
    }
    
    public bool Backspace(int count = 1)
    {
        lock (m_s)
        {
            while (count-- > 0 && CursorIndex > 0)
                m_s.Remove(--CursorIndex, 1);
        }
        
        MoveCursor(0);
        InvalidateVisual();
        return true;
    }

    public void ClearText()
    {
        Backspace(m_s.Length);
    }
    
    private bool Delete()
    {
        m_s.Remove(CursorIndex, 1);
        MoveCursor(0);
        InvalidateVisual();
        return true;
    }

    private static bool IsPrintableChar(char ch) =>
        char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsWhiteSpace(ch);

    public override string ToString()
    {
        var result = string.Join(Environment.NewLine, m_s);
        if (string.IsNullOrEmpty(result))
            return "<Empty>";

        return result.Length > 40 ? result[..40] + "..." : result;
    }
}