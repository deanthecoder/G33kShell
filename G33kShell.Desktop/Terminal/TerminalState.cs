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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Commands;
using G33kShell.Desktop.Terminal.Controls;
using JetBrains.Annotations;
using NClap;

namespace G33kShell.Desktop.Terminal;

/// <summary>
/// <inheritdoc cref="ITerminalState"/>
/// </summary>
public class TerminalState : ITerminalState, IDisposable
{
    public event EventHandler<SkinBase> SkinLoadRequest;
    public event EventHandler<(string Name, string ExtendedName)> ScreensaverLoadRequest;
    
    public DirectoryInfo CurrentDirectory
    {
        get => Settings.Instance.Cwd;
        set => Settings.Instance.Cwd = value;
    }
    
    public CommandHistory CommandHistory { get; } = new CommandHistory();
    public CliPrompt CliPrompt { get; private set; }
    public Stack<DirectoryInfo> DirStack { get; } = new Stack<DirectoryInfo>();

    public TerminalState(DirectoryInfo cwd, [NotNull] CliPrompt cliPrompt)
    {
        CliPrompt = cliPrompt ?? throw new ArgumentNullException(nameof(cliPrompt));
        CurrentDirectory = cwd ?? throw new ArgumentNullException(nameof(cwd));

        // Restore commands from the previous session.
        RestoreSessionState();

        CliPrompt.Init(CurrentDirectory, CommandHistory);
        CliPrompt.ReturnPressed += OnCliPromptReturnPressed;
        CliPrompt.CompletionRequest += OnCliPromptCompletionRequest;

        CommandLineParserOptions.Quiet();
    }

    private void RestoreSessionState()
    {
        foreach (var command in Settings.Instance.UsedCommands)
            CommandHistory.AddCommand(new CommandResult(command));
    }

    private void OnCliPromptReturnPressed(object sender, string cmd) =>
        _ = ExecuteAsync(cmd);

    private async Task ExecuteAsync(string cmdString)
    {
        if (string.IsNullOrWhiteSpace(cmdString))
            return; // Nothing to do.

        // Prepare the prompt to receive command output.
        BeginCommandExecution();

        // Execute the command.
        var args = cmdString.ToArgumentArray();
        CommandBase command;
        if (!CommandLineParser.TryParse(args, out ProgramArguments parsedCommand))
        {
            // Unknown command. Could be an executable though?
            var executableCommand = new ExecutableCommand(args);
            if (!executableCommand.CanExecute(CurrentDirectory))
            {
                // Unknown command - Completely unknown, or just bad arguments?
                CliPrompt.IsReadOnly = false;
                CliPrompt.MoveCursorToEnd();
                CliPrompt.Backspace();

                var isKnownCommand = Enum.GetNames(typeof(MyCommandType)).Any(o => o.Equals(args[0], StringComparison.OrdinalIgnoreCase));
                CliPrompt.Append(isKnownCommand ? "...?" : "?");
                return;
            }
            
            // It's an executable...
            command = executableCommand;
        }
        else
        {
            command = (CommandBase)parsedCommand.PrimaryCommand.InstantiatedCommand;
        }

        await command.SetState(this).ExecuteAsync(new CancellationToken());
        
        // Prepare the next prompt for input.
        PrepareNextInputPrompt();
    }

    /// <summary>
    /// Makes the CLI prompt read-only and prepares it to receive command output.
    /// </summary>
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
            Y = CliPrompt.Y + CliPrompt.Height + 1
        };
        newCliPrompt.Init(CurrentDirectory, CommandHistory);
        CliPrompt.Parent.AddChild(newCliPrompt);
        CliPrompt.ReturnPressed -= OnCliPromptReturnPressed;
        CliPrompt.CompletionRequest -= OnCliPromptCompletionRequest;
        CliPrompt = newCliPrompt;
        CliPrompt.ReturnPressed += OnCliPromptReturnPressed;
        CliPrompt.CompletionRequest += OnCliPromptCompletionRequest;

        CliPrompt.ScrollIntoView();
    }

    public void LoadSkin(SkinBase skin) =>
        SkinLoadRequest?.Invoke(this, skin);
    
    public void LoadScreensaver(string screensaverName, string extendedName) =>
        ScreensaverLoadRequest?.Invoke(this, (screensaverName, extendedName));

    /// <summary>
    /// Processes the completion request by providing completion suggestions based on the input.
    /// </summary>
    private void OnCliPromptCompletionRequest(object sender, CliPrompt.CompletionRequestEventArgs args)
    {
        if (args.IsCommand)
        {
            args.CompletionResult = CommandsHelper.GetAllCommandNames().FirstOrDefault(o => o.StartsWith(args.CompletionPrefix))?.Substring(args.CompletionPrefix.Length);
            return;
        }
        
        var targetPath = CurrentDirectory.Resolve(args.CompletionPrefix);
        
        try
        {
            if (File.Exists(targetPath) || Directory.Exists(targetPath))
            {
                // Already complete - We're done.
                return;
            }
        }
        catch
        {
            // Bad path.
        }

        // Might have a path with a bit extra on the end.
        try
        {
            var file = targetPath.ToFile();
            var wholeDir = file.Directory;
            var suffix = file.Name.TrimEnd('*');
            var candidates = wholeDir.TryGetContent(suffix + "*").OrderBy(o => o.Name);
            args.CompletionResult = candidates.FirstOrDefault()?.Name.Substring(suffix.Length);
        }
        catch
        {
            // Bad path.
        }
    }

    /// <summary>
    /// Reveals the current working directory by opening it in the OS's file explorer.
    /// </summary>
    public void RevealCwd() =>
        Task.Run(() => CurrentDirectory?.Explore());

    public void Dispose()
    {
        // Save state for the next session.
        Settings.Instance.UsedCommands = CommandHistory.Commands.Select(o => o.Command).Reverse().Distinct().Reverse().ToList();
    }
}
