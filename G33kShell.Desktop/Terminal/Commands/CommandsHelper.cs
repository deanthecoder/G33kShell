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
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

/// <summary>
/// Helper class for retrieving command attribute information.
/// </summary>
public static class CommandsHelper
{
    /// <summary>
    /// Retrieves all available command types in the application.
    /// </summary>
    public static IEnumerable<MyCommandType> GetAllCommandTypes() =>
        Enum.GetValues<MyCommandType>()
            .Cast<MyCommandType?>()
            .Where(o => o != null)
            .Cast<MyCommandType>();

    /// <summary>
    /// Retrieves all available command names (long and short).
    /// </summary>
    /// <returns>
    /// An enumerable collection of strings containing the names of all available commands.
    /// </returns>
    public static IEnumerable<string> GetAllCommandNames() =>
        GetAllCommandTypes().SelectMany(o => GetCommandNames(GetCommandAttribute(o)));

    /// <summary>
    /// Retrieves the names (long and short) for the specified command type.
    /// </summary>
    public static IEnumerable<string> GetCommandNames(MyCommandType o) =>
        GetCommandNames(GetCommandAttribute(o));

    /// <summary>
    /// Retrieves the names (long and short) for the specified command attributes.
    /// </summary>
    /// <param name="commandAttr">The command attribute to retrieve names from.</param>
    /// <returns>An IEnumerable of strings containing the names of the command attributes.</returns>
    public static IEnumerable<string> GetCommandNames(CommandAttribute commandAttr) =>
        new[]
        {
            commandAttr.LongName, commandAttr.ShortName
        }.Where(o => o != null);

    /// <summary>
    /// Retrieves the CommandAttribute associated with the specified MyCommandType.
    /// </summary>
    public static CommandAttribute GetCommandAttribute(MyCommandType command) =>
        typeof(MyCommandType).GetMember(command.ToString()).FirstOrDefault()?.GetCustomAttribute<CommandAttribute>();

    /// <summary>
    /// Retrieves the command description attribute associated with the specified command.
    /// </summary>
    public static CommandDescriptionAttribute GetCommandDescription(CommandBase command) =>
        command.GetType().GetCustomAttribute<CommandDescriptionAttribute>();
}