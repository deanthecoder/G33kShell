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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Console.Controls;

/// <summary>
/// A TextBlock that reveals the content of [Foo] tokens, one by one.
/// </summary>
[DebuggerDisplay("RevealingTextBlock:{X},{Y} {Width}x{Height} T:{Text}")]
public class RevealingTextBlock : TextBlock
{
    private List<(int Line, int Offset, string Content)> m_toReveal;
    private readonly TimeSpan m_delay;
    private readonly TimeSpan m_period;
    private readonly CancellationTokenSource m_cancellationTokenSource;
    private Task m_revealTask;

    /// <summary>
    /// Allows the caller to wait for the 'reveal' to complete.
    /// </summary>
    public ManualResetEventSlim Waiter { get; } = new ManualResetEventSlim(false);

    /// <summary>
    /// Initializes the RevealingTextBlock with the specified delay, period, and lines of text to reveal.
    /// </summary>
    /// <param name="delay">The delay before starting to reveal the text.</param>
    /// <param name="period">The time interval between revealing each token.</param>
    /// <param name="lines">The lines of text to reveal.</param>
    /// <returns>The initialized RevealingTextBlock instance.</returns>
    public RevealingTextBlock(TimeSpan delay, TimeSpan period, params string[] lines) : base(lines)
    {
        m_delay = delay;
        m_period = period;
        m_cancellationTokenSource = new CancellationTokenSource();
        
        BuildRevealList(lines);
    }

    private void BuildRevealList(string[] lines)
    {
        // Find the column/line position of all any text of the form '[Foo]', making a note
        // of location, and replacing the matching text in 'lines' with whitespace.
        m_toReveal = new List<(int Line, int Offset, string Content)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var tokens = Regex.Matches(line, @"\[[a-zA-Z0-9]+\]");
            foreach (Match token in tokens)
            {
                var offset = token.Index;
                var len = token.Length;
                m_toReveal.Add((i, offset, line.Substring(offset, len)));
                line = line.Replace(token.Value, new string(' ', token.Length));
            }

            lines[i] = line;
        }
        
        if (!m_toReveal.Any())
            Waiter.Set(); // Nothing to wait for.
    }

    private Task StartTextReveal() =>
        Task.Run(async () =>
        {
            await Task.Delay(m_delay);
            foreach (var (line, offset, content) in m_toReveal)
            {
                if (m_cancellationTokenSource.Token.IsCancellationRequested)
                    return;
                    
                var stringBuilder = new StringBuilder(Text[line]);
                for (var i = 0; i < content.Length; i++)
                    stringBuilder[offset + i] = content[i];
                Text[line] = stringBuilder.ToString();
                    
                InvalidateVisual();
                    
                await Task.Delay(m_period);
            }

            Waiter.Set();
        }, m_cancellationTokenSource.Token);

    public override void OnLoaded()
    {
        base.OnLoaded();
        if (m_toReveal.Any())
            m_revealTask = StartTextReveal();
    }

    protected override void OnUnloaded()
    {
        m_cancellationTokenSource.Cancel();
        m_revealTask?.Wait();
        m_revealTask = null;
        base.OnUnloaded();
    }
}
