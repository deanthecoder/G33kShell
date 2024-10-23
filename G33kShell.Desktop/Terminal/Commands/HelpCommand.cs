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

using System;
using System.Linq;
using System.Threading.Tasks;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class HelpCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var commands = Enum.GetValues<MyCommandType>()
            .Select(CommandsHelper.GetCommandAttribute)
            .OrderBy(o => o.LongName)
            .Select(o => (CommandsHelper.GetCommandNames(o).ToArray().ToCsv(), o.Description))
            .ToArray();
        
        var maxLength = commands.Max(o => o.Item1.Length);
        foreach (var cmd in commands)
            WriteLine($"{cmd.Item1.PadLeft(maxLength + 4)} │ {cmd.Description}".TrimEnd(' ', ':'));
        
        return Task.FromResult(true);
    }
}