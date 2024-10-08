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
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Terminal.Commands;
using G33kShell.Desktop.Terminal.Controls;
using JetBrains.Annotations;
using NClap;

namespace G33kShell.Desktop.Terminal;

/// <summary>
/// <inheritdoc cref="ITerminalState"/>
/// </summary>
public class TerminalState : ITerminalState
{
    public DirectoryInfo CurrentDirectory { get; set; }
    public CommandHistory CommandHistory { get; } = new CommandHistory();
    public CliPrompt CliPrompt { get; private set; }

    public TerminalState(DirectoryInfo cwd, [NotNull] CliPrompt cliPrompt)
    {
        CliPrompt = cliPrompt ?? throw new ArgumentNullException(nameof(cliPrompt));
        CurrentDirectory = cwd ?? throw new ArgumentNullException(nameof(cwd));

        CliPrompt.Cwd = CurrentDirectory;
        CliPrompt.ReturnPressed += OnCliPromptReturnPressed;

        CommandLineParserOptions.Quiet();
    }

    private void OnCliPromptReturnPressed(object _, string cmd) =>
        Execute(cmd);

    private void Execute(string cmdString)
    {
        if (string.IsNullOrWhiteSpace(cmdString))
            return; // Nothing to do.

        // Prepare the prompt to receive command output.
        BeginCommandExecution();

        // Execute the command.
        var args = cmdString.ToArgumentArray();
        if (!CommandLineParser.TryParse(args, out ProgramArguments parsedCommand))
        {
            CliPrompt.IsReadOnly = false;
            CliPrompt.MoveCursorToEnd();
            CliPrompt.Backspace();

            // Invalid command - Completely unknown, or just bad arguments?
            var isKnownCommand = Enum.GetNames(typeof(MyCommandType)).Any(o => o.Equals(args[0], StringComparison.OrdinalIgnoreCase));
            CliPrompt.Append(isKnownCommand ? "...?" : "?");
            return;
        }

        var command = (CommandBase)parsedCommand.PrimaryCommand.InstantiatedCommand;
        command.SetState(this).Execute();
        
        // Prepare the next prompt for input.
        PrepareNextInputPrompt();
    }

    private void BeginCommandExecution()
    {
        CliPrompt.IsReadOnly = true;
        CliPrompt.Append("\n");
        CliPrompt.ScrollIntoView();
    }

    private void PrepareNextInputPrompt()
    {
        CliPrompt.Parent.AddChild(new Separator(CliPrompt.Parent.Width)
        {
            Y = CliPrompt.Y + CliPrompt.Height
        });
        var newCliPrompt = new CliPrompt(CliPrompt.Parent.Width)
        {
            Y = CliPrompt.Y + CliPrompt.Height + 1, Cwd = CurrentDirectory
        };
        CliPrompt.Parent.AddChild(newCliPrompt);
        CliPrompt.ReturnPressed -= OnCliPromptReturnPressed;
        CliPrompt = newCliPrompt;
        CliPrompt.ReturnPressed += OnCliPromptReturnPressed;

        CliPrompt.ScrollIntoView();
    }
}
