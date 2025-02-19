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
using System.Reflection;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Provides help information for available commands.")]
public class HelpCommand : CommandBase
{
    protected override Task<bool> Run(ITerminalState state)
    {
        var commands = Enum.GetValues<MyCommandType>()
            .Select(o => (GetCategory(o), CommandsHelper.GetCommandNames(o).ToArray().ToCsv(), CommandsHelper.GetCommandAttribute(o).Description))
            /*
            .Select(CommandsHelper.GetCommandAttribute)
            .OrderBy(o => o.LongName)
            .Select(o => (CommandsHelper.GetCommandNames(o).ToArray().ToCsv(), o.Description))
            .ToArray();
            */
            .ToArray();

        var maxLength = commands.Select(o => o.Item2.Length).Max();
        var isFirstLine = true;
        foreach (var category in commands.Select(o => o.Item1).Distinct())
        {
            if (!isFirstLine)
                WriteLine();
            isFirstLine = false;
            WriteLine($"{category}:");
            foreach (var cmd in commands.Where(o => o.Item1 == category))
                WriteLine($"{cmd.Item2.PadLeft(maxLength + 4)} │ {cmd.Description}".TrimEnd(' ', ':'));
        }
        
        return Task.FromResult(true);
    }

    private static CommandType GetCategory(MyCommandType command)
    {
        // Get the field info for the given command
        var field = typeof(MyCommandType).GetField(command.ToString());

        if (field != null)
        {
            // Get the CommandCategoryAttribute applied to this field
            var attribute = field.GetCustomAttribute<CommandCategoryAttribute>();
            if (attribute != null)
                return attribute.Category;
        }

        return CommandType.Misc; // No category specified.
    }
}