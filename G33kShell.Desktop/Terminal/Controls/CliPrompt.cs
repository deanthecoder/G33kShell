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
using CSharp.Core.Extensions;
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
    private string[] m_commandHistory;
    private int m_historyOffset;

    public event EventHandler<CompletionRequestEventArgs> CompletionRequest; 

    public CliPrompt(int width) : base(width)
    {
    }

    protected override bool OnKeyEvent(KeyConsoleEvent keyEvent)
    {
        if (base.OnKeyEvent(keyEvent))
            return true;

        return keyEvent.Key switch
        {
            Key.Up => OnUpArrow(),
            Key.Down => OnDownArrow(),
            Key.Tab => OnTab(),
            _ => false
        };
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

        Paste(args.CompletionResult);
        return true;
    }

    private bool OnUpArrow()
    {
        if (m_historyOffset > -m_commandHistory.Length)
        {
            Clear();
            Paste(m_commandHistory[m_commandHistory.Length + --m_historyOffset]);
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
                Paste(m_commandHistory[m_commandHistory.Length + m_historyOffset]);
        }
        
        return true;
    }

    public void Init(DirectoryInfo cwd, CommandHistory commandHistory)
    {
        Prefix = $"[{cwd.FullName.TrimEnd('\\', '/').StringOrDefault("/")}]";

        m_commandHistory =
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