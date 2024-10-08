// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Generic;
using System.Linq;

namespace G33kShell.Desktop.Terminal;

/// <summary>
/// Represents a command history that stores a list of executed commands and their results.
/// </summary>
public class CommandHistory
{
    private readonly List<CommandResult> m_commands = new List<CommandResult>();

    public void AddCommand(CommandResult command)
    {
        m_commands.Add(command);
    }

    public CommandResult LastGood => m_commands.LastOrDefault(o => o.IsSuccess);
}