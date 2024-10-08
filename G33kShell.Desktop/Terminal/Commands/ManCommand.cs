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
using System.Linq;
using System.Reflection;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class ManCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "The name of the command.")]
    public string CommandName { get; [UsedImplicitly] set; }

    public override bool Run(ITerminalState state)
    {
        var commandEnum =
            Enum.GetValues<MyCommandType>()
                .Cast<MyCommandType?>()
                .FirstOrDefault(o => o != null && GetCommandNames(GetCommandAttribute((MyCommandType)o)).Any(name => name.Equals(CommandName, StringComparison.OrdinalIgnoreCase)));
        if (commandEnum != null)
        {
            // Get the CommandAttribute associated with the enum value
            var commandAttribute = GetCommandAttribute((MyCommandType)commandEnum);

            // Get the command type from the CommandAttribute
            var commandType = commandAttribute?.GetImplementingType(null);
            if (commandType != null && Activator.CreateInstance(commandType) is CommandBase commandInstance)
            {
                commandInstance.SetState(state);
                commandInstance.WriteManPage(commandAttribute, GetCommandDescription((MyCommandType)commandEnum));
                return true;
            }
        }

        WriteLine("Error: Command not found.");
        return false;
    }

    private static IEnumerable<string> GetCommandNames(CommandAttribute commandAttr) =>
        new[]
        {
            commandAttr.LongName, commandAttr.ShortName
        }.Where(o => o != null);

    private static CommandAttribute GetCommandAttribute(MyCommandType command) =>
        typeof(MyCommandType).GetMember(command.ToString()).FirstOrDefault()?.GetCustomAttribute<CommandAttribute>();

    private static CommandDescriptionAttribute GetCommandDescription(MyCommandType command) =>
        typeof(MyCommandType).GetMember(command.ToString()).FirstOrDefault()?.GetCustomAttribute<CommandDescriptionAttribute>();
}