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
using System.IO;
using System.Linq;
using Avalonia.Input;
using DTC.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Events;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Terminal.Controls;

/// <summary>
/// Represents a command-line interface (CLI) control.
/// </summary>
/// <remarks>
/// The <see cref="CliPrompt"/> class extends the <see cref="G33kShell.Desktop.Console.Controls.TextBox"/> class to provide
/// a specialized text box for terminal input. It has a prefix based on the current directory of the terminal state.
/// </remarks>
public class CliPrompt : TextBox
{
    private string[] m_fullCommandHistory;
    private string[] m_filteredCommandHistory;
    private int m_historyOffset;

    /// <summary>
    /// Raised when a tab completion is requested.
    /// </summary>
    public event EventHandler<CompletionRequestEventArgs> CompletionRequest;
    
    /// <summary>
    /// Raised when ESCape is pressed, intended for an executing command to abort.
    /// </summary>
    public event EventHandler CancelActionRequested;

    public CliPrompt(int width) : base(width)
    {
    }

    /// <summary>
    /// The first line of the CLI prompt text.
    /// </summary>
    public string CommandLine => TextWithoutPrefix[..TextWithoutPrefix.IndexOfAny(['\r', '\n'])];

    public override void OnEvent(ConsoleEvent consoleEvent, ref bool handled)
    {
        if (consoleEvent is KeyConsoleEvent { Key: Key.Escape, Direction: KeyConsoleEvent.KeyDirection.Down } && IsReadOnly)
        {
            CancelActionRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        
        base.OnEvent(consoleEvent, ref handled);
    }

    protected override bool OnKeyEvent(KeyConsoleEvent keyEvent)
    {
        if (keyEvent.Key == Key.Up)
            return OnUpArrow();
        if (keyEvent.Key == Key.Down)
            return OnDownArrow();

        // Any other key resets any up/down command history navigation.
        ResetHistoryNavigation();

        if (base.OnKeyEvent(keyEvent))
            return true;
        
        return keyEvent.Key == Key.Tab && OnTab();
    }

    private void ResetHistoryNavigation()
    {
        m_filteredCommandHistory = null;
        m_historyOffset = 0;
    }

    private bool OnTab()
    {
        var isCursorAtEndOfLine = CursorIndex == TextWithoutPrefix.Length;
        if (!isCursorAtEndOfLine)
            return false;

        if (string.IsNullOrEmpty(TextWithoutPrefix) || char.IsWhiteSpace(TextWithoutPrefix.Last()))
            return false;

        var quoteCount = TextWithoutPrefix.Count(ch => ch == '"');
        var completionStartChar = (quoteCount & 1) == 1 ? '"' : ' ';
        var startCharIndex = Math.Max(0, TextWithoutPrefix.LastIndexOf(completionStartChar));
        var completionPrefix = TextWithoutPrefix.Substring(startCharIndex).Trim(' ', '"');
        
        var isCommand = !TextWithoutPrefix.Contains(' ');
        var args = new CompletionRequestEventArgs(completionPrefix, isCommand);
        CompletionRequest?.Invoke(this, args);

        if (string.IsNullOrEmpty(args.CompletionResult))
            return false;

        if (TextWithoutPrefix.EndsWith('*'))
            Backspace();
        Paste(args.CompletionResult);
        return true;
    }

    private bool OnUpArrow()
    {
        if (m_filteredCommandHistory == null)
            m_filteredCommandHistory = m_fullCommandHistory?.Where(o => o.StartsWith(TextWithoutPrefix.Substring(0, CursorIndex), StringComparison.OrdinalIgnoreCase)).ToArray();
        
        if (m_filteredCommandHistory == null)
            return true;
        
        if (m_historyOffset > -m_filteredCommandHistory.Length)
        {
            Clear();
            Paste(m_filteredCommandHistory[m_filteredCommandHistory.Length + --m_historyOffset]);
        }
        
        return true;
    }

    private bool OnDownArrow()
    {
        Clear();

        if (m_historyOffset < 0)
        {
            m_historyOffset++;
            if (m_historyOffset < 0)
                Paste(m_filteredCommandHistory[m_filteredCommandHistory.Length + m_historyOffset]);
        }
        else
        {
            ResetHistoryNavigation();
        }
        
        return true;
    }

    public void Init(DirectoryInfo cwd, CommandHistory commandHistory)
    {
        Prefix = $"[{cwd.FullName.TrimEnd('\\', '/').StringOrDefault("/")}]";

        m_fullCommandHistory =
            commandHistory.Commands
                .Select(o => o.Command)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Reverse()
                .Distinct()
                .Reverse()
                .ToArray();
    }

    public class CompletionRequestEventArgs : EventArgs
    {
        /// <summary>
        /// The first part of the string to complete.
        /// </summary>
        public string CompletionPrefix { get; }

        /// <summary>
        /// Gets a value indicating whether the completion request is for a command (if false, it's a filename).
        /// </summary>
        public bool IsCommand { get; }

        /// <summary>
        /// Represents the result of a completion request in the command line interface.
        /// </summary>
        public string CompletionResult { get; set; }

        public CompletionRequestEventArgs([NotNull] string completionPrefix, bool isCommand)
        {
            CompletionPrefix = completionPrefix ?? throw new ArgumentNullException(nameof(completionPrefix));
            IsCommand = isCommand;
        }
    }
}