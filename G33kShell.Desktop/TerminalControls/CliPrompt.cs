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
using G33kShell.Desktop.Console.Controls;

namespace G33kShell.Desktop.TerminalControls;

/// <summary>
/// Represents a command-line interface (CLI) control.
/// </summary>
/// <remarks>
/// The <see cref="CliPrompt"/> class extends the <see cref="G33kShell.Desktop.Console.Controls.TextBox"/> class to provide
/// a specialized text box for terminal input. It has a prefix based on the current directory of the terminal state.
/// </remarks>
public class CliPrompt : TextBox
{
    private DirectoryInfo m_cwd;
    
    public DirectoryInfo Cwd
    {
        get => m_cwd;
        set
        {
            if (m_cwd == value)
                return;
            m_cwd = value;
            Prefix = $"[{m_cwd.FullName}]";
        }
    }
    
    public CliPrompt(int width) : base(width)
    {
    }
}