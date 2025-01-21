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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using G33kShell.Desktop.Terminal.Controls;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public abstract class CommandBase : Command
{
    private ITerminalState m_state;

    private CliPrompt CliPrompt => m_state.CliPrompt;

    /// <summary>
    /// Gets a value indicating whether the command is supported on the current platform.
    /// </summary>
    protected virtual bool IsSupported => true;
    
    public CommandBase SetState(ITerminalState state)
    {
        m_state = state;
        return this;
    }

    protected abstract Task<bool> Run(ITerminalState state);
    
    public override async Task<NClap.Metadata.CommandResult> ExecuteAsync(CancellationToken cancel)
    {
        if (!IsSupported)
        {
            WriteLine("Command is not supported.");
            return NClap.Metadata.CommandResult.Success;
        }
        
        try
        {
            var commandSuccess = await Run(m_state);
            var lines = CliPrompt.TextWithoutPrefix.Split('\n');
            var result = new CommandResult(lines.First(), m_state.CurrentDirectory.Clone(), string.Join('\n', lines.Skip(1)).Trim(), commandSuccess);
            m_state.CommandHistory.AddCommand(result);
        }
        catch
        {
            // Continue.
        }
        
        return NClap.Metadata.CommandResult.Success;
    }

    protected void WriteLine(string s = null)
    {
        // Add a line to the prompt output control.
        CliPrompt.AppendLine(s ?? string.Empty);

        // ...and expand the control height.
        var lineCount = CliPrompt.Text.Length;
        CliPrompt.SetHeight(lineCount);
        
        // If the control now pokes off the bottom of the screen, scroll the controls up.
        CliPrompt.ScrollIntoView();
    }

    public void WriteManPage([NotNull] CommandAttribute commandAttribute, CommandDescriptionAttribute description)
    {
        if (commandAttribute == null)
            throw new ArgumentNullException(nameof(commandAttribute));
        
        // Get the CommandAttribute to fetch the command name and description
        var commandName = commandAttribute.LongName ?? commandAttribute.ShortName;
        var commandDescription = commandAttribute.Description ?? "No description available.";

        // Retrieve all properties marked with PositionalArgument or NamedArgument attributes
        var commandType = GetType();
        var positionalArguments = commandType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(PositionalArgumentAttribute), false).Length > 0)
            .Select(p => new
            {
                Property = p,
                Attribute = p.GetCustomAttribute<PositionalArgumentAttribute>()
            }).ToList();

        var namedArguments = commandType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(NamedArgumentAttribute), false).Any())
            .Select(p => new
            {
                Property = p,
                Attribute = p.GetCustomAttribute<NamedArgumentAttribute>()
            }).ToList();

        WriteLine("NAME");
        WriteLine($"    {commandName} - {commandDescription}");
        WriteLine();

        var alias = commandAttribute.LongName != null ? commandAttribute.ShortName : null;
        if (alias != null)
        {
            WriteLine("ALIAS");
            WriteLine($"    {alias}");
            WriteLine();
        }
        
        WriteLine("SYNOPSIS");
        var synopsis = new StringBuilder($"    {commandName}");
        foreach (var arg in namedArguments)
            synopsis.Append($" [-{(string.IsNullOrEmpty(arg.Attribute.ShortName) ? arg.Property.Name.ToLower() : arg.Attribute.ShortName)}]");
        foreach (var arg in positionalArguments)
            synopsis.Append(arg.Attribute.Flags.HasFlag(ArgumentFlags.Optional) ? $" [<{arg.Property.Name.ToLower()}>]" : $" <{arg.Property.Name.ToLower()}>");
        WriteLine(synopsis.ToString());
        WriteLine();

        if (positionalArguments.Any() || namedArguments.Any())
        {
            WriteLine("OPTIONS");

            var pairs = new List<(string Command, string Description)>();
            foreach (var arg in namedArguments)
            {
                var shortName = string.IsNullOrEmpty(arg.Attribute.ShortName) ? "" : $"-{arg.Attribute.ShortName}";
                pairs.Add((shortName.Trim(' ', ','), arg.Attribute.Description));
            }
            pairs.AddRange(positionalArguments.Select(arg => (arg.Property.Name.ToLower(), arg.Attribute.Description)));

            var maxLength = pairs.Max(pair => pair.Command.Length);
            foreach (var (command, shortDescription) in pairs)
                WriteLine($"    {command.PadRight(maxLength)} : {shortDescription}");
            WriteLine();
        }

        if (description != null)
        {
            WriteLine("DESCRIPTION");
            foreach (var s in description.Lines)
                WriteLine($"    {s}");
            WriteLine();
        }
    }
}