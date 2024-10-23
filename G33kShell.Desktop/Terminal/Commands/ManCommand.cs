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
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class ManCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "The name of the command.")]
    public string CommandName { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        var commandEnum =
            CommandsHelper.GetAllCommandTypes()
                .FirstOrDefault(o => CommandsHelper.GetCommandNames(o)
                    .Any(name => name.Equals(CommandName, StringComparison.OrdinalIgnoreCase)));

        // Get the CommandAttribute associated with the enum value
        var commandAttribute = CommandsHelper.GetCommandAttribute(commandEnum);

        // Get the command type from the CommandAttribute
        var commandType = commandAttribute?.GetImplementingType(null);
        if (commandType != null && Activator.CreateInstance(commandType) is CommandBase commandInstance)
        {
            commandInstance.SetState(state);
            commandInstance.WriteManPage(commandAttribute, CommandsHelper.GetCommandDescription(commandEnum));
            return Task.FromResult(true);
        }

        WriteLine("Error: Command not found.");
        return Task.FromResult(false);
    }
}