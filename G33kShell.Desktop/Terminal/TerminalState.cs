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

using NClap.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CSharp.Core.Extensions;
using G33kShell.Desktop.TerminalControls;
using JetBrains.Annotations;
using NClap;

namespace G33kShell.Desktop.Terminal;

public record CommandResult(
    DirectoryInfo Directory,
    string Command,
    string Output,
    bool IsSuccess
);

public class CommandHistory
{
    private readonly List<CommandResult> m_commands = new List<CommandResult>();

    public void AddCommand(CommandResult command)
    {
        m_commands.Add(command);
    }
    
}

public enum MyCommandType
{
    [Command(typeof(HelpCommand), LongName = "help", Description = "Get help information.")]
    Help,

    [Command(typeof(DirCommand), LongName = "dir", Description = "List files and directories.")]
    Dir,

    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    Cd
}

public abstract class CommandBase : SynchronousCommand
{
    private ITerminalState m_state;

    protected CliPrompt CliPrompt => m_state.CliPrompt;
    
    public CommandBase SetState(ITerminalState state)
    {
        m_state = state;
        return this;
    }

    public abstract bool Run(ITerminalState state);
    
    public override NClap.Metadata.CommandResult Execute()
    {
        Run(m_state);
        return NClap.Metadata.CommandResult.Success;
    }

    protected void WriteLine(string s)
    {
        // Add a line to the prompt output control.
        CliPrompt.AppendLine(s);

        // ...and expand the control height.
        var lineCount = CliPrompt.Text.Length;
        CliPrompt.SetHeight(lineCount);
        
        // If the control now pokes off the bottom of the screen, scroll the controls up.
        CliPrompt.ScrollIntoView();
    }
}

public class HelpCommand : CommandBase
{
    public override bool Run(ITerminalState state)
    {
        var commands = Enum.GetValues<MyCommandType>()
            .Select(GetCommandDescription)
            .OrderBy(o => o.ShortName)
            .Select(o => (o.ShortName ?? o.LongName, o.Description))
            .ToArray();
        
        var maxLength = commands.Max(o => o.Item1.Length);
        foreach (var cmd in commands)
            WriteLine($"{cmd.Item1.PadLeft(maxLength + 4)} │ {cmd.Description}".TrimEnd(' ', ':'));

        return true;
    }

    private static CommandAttribute GetCommandDescription(MyCommandType command)
    {
        var memberInfo = typeof(MyCommandType).GetMember(command.ToString()).FirstOrDefault();
        return memberInfo?.GetCustomAttribute<CommandAttribute>();
    }
}

public class DirCommand : CommandBase
{
    // Option for /b switch
    [NamedArgument(Description = "Display in bare format", ShortName = "b")]
    public bool BareFormat { get; [UsedImplicitly] set; }

    // Option for /s switch
    [NamedArgument(Description = "Recursively list files", ShortName = "s")]
    public bool Recursive { get; [UsedImplicitly] set; }

    // Positional argument for file mask (e.g. *.exe)
    [PositionalArgument(ArgumentFlags.Optional, Description = "File mask (e.g. *.exe)")]
    public string FileMask { get; [UsedImplicitly] set; } = "*.*";

    public override bool Run(ITerminalState state)
    {
        IEnumerable<FileSystemInfo> files = state.CurrentDirectory.EnumerateFiles();
        IEnumerable<FileSystemInfo> dirs = state.CurrentDirectory.EnumerateDirectories();
        foreach (var file in files.Union(dirs).OrderBy(o => o.Name))
            WriteLine(file.ToString());

        return true;
    }
}

public class CdCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "New directory location.")]
    public string Location { get; [UsedImplicitly] set; }

    public override bool Run(ITerminalState state)
    {
        var newDir = state.CurrentDirectory;
        try
        {
            newDir = state.CurrentDirectory.GetDir(Location);
            if (newDir.Exists)
            {
                state.CurrentDirectory = newDir;
                System.Console.WriteLine($"CWD: {newDir}");
                return true;
            }
        }
        catch (Exception)
        {
            // Fall through.
        }

        WriteLine($"CWD not found: {newDir}");

        return false;
    }}

public class ProgramArguments
{
    [PositionalArgument(ArgumentFlags.Required, Position = 0)]
    public CommandGroup<MyCommandType> PrimaryCommand { get; set; }
}

public interface ITerminalState
{
    DirectoryInfo CurrentDirectory { get; set; }
    CommandHistory CommandHistory { get; }
    CliPrompt CliPrompt { get; }
}

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
            Debug.WriteLine($"Unknown command: {cmdString}");
            CliPrompt.IsReadOnly = false;
            CliPrompt.MoveCursorToEnd();
            CliPrompt.Backspace();
            CliPrompt.Append("?");
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
        var newCliPrompt = new CliPrompt(CliPrompt.Parent.Width)
        {
            Y = CliPrompt.Y + CliPrompt.Height,
            Cwd = CurrentDirectory
        };
        CliPrompt.Parent.AddChild(newCliPrompt);
        CliPrompt.ReturnPressed -= OnCliPromptReturnPressed;
        CliPrompt = newCliPrompt;
        CliPrompt.ReturnPressed += OnCliPromptReturnPressed;
        
        CliPrompt.ScrollIntoView();
    }
}
