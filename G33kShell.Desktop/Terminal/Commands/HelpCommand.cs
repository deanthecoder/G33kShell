using System;
using System.Linq;
using System.Reflection;
using CSharp.Core.Extensions;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class HelpCommand : CommandBase
{
    public override bool Run(ITerminalState state)
    {
        var commands = Enum.GetValues<MyCommandType>()
            .Select(GetCommandAttribute)
            .OrderBy(o => o.LongName)
            .Select(o => (GetCommandNames(o), o.Description))
            .ToArray();
        
        var maxLength = commands.Max(o => o.Item1.Length);
        foreach (var cmd in commands)
            WriteLine($"{cmd.Item1.PadLeft(maxLength + 4)} │ {cmd.Description}".TrimEnd(' ', ':'));
        
        return true;
    }

    private static string GetCommandNames(CommandAttribute commandAttr) =>
        new[]
        {
            commandAttr.LongName, commandAttr.ShortName
        }.Where(o => o != null).ToArray().ToCsv();

    private static CommandAttribute GetCommandAttribute(MyCommandType command) =>
        typeof(MyCommandType).GetMember(command.ToString()).FirstOrDefault()?.GetCustomAttribute<CommandAttribute>();
}