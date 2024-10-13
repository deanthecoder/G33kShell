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

using System.IO;
using System.Linq;
using G33kShell.Desktop.Console.Controls;

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

    public string Command => TextWithoutPrefix.Split('\n').FirstOrDefault() ?? string.Empty;
    
    public CliPrompt(int width) : base(width)
    {
    }

    protected override bool OnUpArrow()
    {
        if (m_historyOffset > -m_commandHistory.Length)
        {
            Clear();
            Paste(m_commandHistory[m_commandHistory.Length + --m_historyOffset]);
        }
        
        return true;
    }

    protected override bool OnDownArrow()
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
        Prefix = $"[{cwd.FullName.TrimEnd('\\', '/')}]";

        m_commandHistory =
            commandHistory.Commands
                .Select(o => o.Command)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Reverse()
                .Distinct()
                .Reverse()
                .ToArray();
    }
}