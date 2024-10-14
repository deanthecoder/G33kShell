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

using G33kShell.Desktop.Terminal.Attributes;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public enum MyCommandType
{
    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    [CommandDescription("Changes the current directory to the specified path.", "Example: cd /home/user/documents")]
    Cd,

    [Command(typeof(ClearCommand), LongName = "clear", ShortName = "cls", Description = "Clear the terminal.")]
    [CommandDescription("Clears the terminal screen.")]
    Clear,

    [Command(typeof(ClipCommand), LongName = "clip", Description = "Copy command output into the clipboard.")]
    [CommandDescription("Copies the output from the previous command into the system clipboard.")]
    Clip,

    [Command(typeof(DirCommand), LongName = "dir", ShortName = "ls", Description = "List files and directories.")]
    [CommandDescription("Lists files and directories in the current directory.", "Example: dir ..")]
    Dir,

    [Command(typeof(ExitCommand), LongName = "exit", Description = "Quit the terminal.")]
    Exit,

    [Command(typeof(FindCommand), LongName = "find", Description = "Search for files or directories by name.")]
    [CommandDescription("Searches for files or directories matching the specified name or pattern.", "Example: find *.txt")]
    Find,

    [Command(typeof(HelpCommand), LongName = "help", ShortName = "?", Description = "Get help information.")]
    [CommandDescription("Provides help information for available commands.")]
    Help,

    [Command(typeof(HistoryCommand), LongName = "history", Description = "List command history.")]
    [CommandDescription("List all of the previously-used Terminal commands.")]
    History,

    [Command(typeof(ManCommand), LongName = "man", Description = "Get usage information for a specific command.")]
    [CommandDescription("Displays manual pages for commands, detailing their usage and options.", "Example: man open")]
    Man,

    [Command(typeof(NowCommand), LongName = "now", Description = "Report the current date and time.")]
    [CommandDescription("Report the current date and time.")]
    Now,

    [Command(typeof(OpenCommand), LongName = "open", Description = "View the target in the OS file browser.")]
    [CommandDescription("Opens the specified file or directory in the OS file browser or associated application.", "Example: open myfile.txt")]
    Open,

    [Command(typeof(PopDCommand), LongName = "popd", Description = "Restore to the previously 'pushed' directory.")]
    [CommandDescription("Navigate to the previously 'pushed' folder.", "See: pushd")]
    PopD,

    [Command(typeof(PushDCommand), LongName = "pushd", Description = "Change the current directory.")]
    [CommandDescription("Navigate to a folder and push it onto the directory stack.", "See: popd")]
    PushD,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    [CommandDescription("Locates an executable file in the system’s PATH that matches the specified name or pattern.", "Example: whereis git")]
    WhereIs,

    [Command(typeof(VerCommand), LongName = "ver", Description = "Report the G33kShell application version.")]
    [CommandDescription("Report the G33kShell application version.")]
    Ver,
}