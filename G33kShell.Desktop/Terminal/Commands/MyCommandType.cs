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
    [CommandDescription("Copies the output from the previous command into the system clipboard.", "All output is copied, unless a specific line number is requested.")]
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

    [Command(typeof(MaxCommand), LongName = "max", Description = "Toggle the terminal window between maximized and restored states.")]
    [CommandDescription("Maximizes the window if it’s not already, or restores it to its previous size. Useful for adjusting the view of the terminal.")]
    Max,

    [Command(typeof(NowCommand), LongName = "now", Description = "Report the current date and time.")]
    [CommandDescription("Report the current date and time.")]
    Now,

    [Command(typeof(OpenCommand), LongName = "open", Description = "Launches a file using the associated application based on the OS settings.")]
    [CommandDescription("Opens the specified file or folder using the default application on the system.")]
    Open,
    
    [Command(typeof(PopDCommand), LongName = "popd", Description = "Restore to the previously 'pushed' directory.")]
    [CommandDescription("Navigate to the previously 'pushed' folder.", "See: pushd")]
    PopD,

    [Command(typeof(PushDCommand), LongName = "pushd", Description = "Change the current directory.")]
    [CommandDescription("Navigate to a folder and push it onto the directory stack.", "See: popd")]
    PushD,

    [Command(typeof(RevealCommand), LongName = "reveal", Description = "View the target in the OS file browser.")]
    [CommandDescription("Opens the specified file or directory in the system’s file explorer.", "Example: reveal myfile.txt")]
    Reveal,

    [Command(typeof(ScreensaverCommand), LongName = "screensaver", Description = "Select and activate the screensaver.")]
    [CommandDescription("Select and activate the screensaver.")]
    Screensaver,

    [Command(typeof(SkinCommand), LongName = "skin", Description = "List or change the current Terminal skin.")]
    [CommandDescription("List or change the current Terminal skin.")]
    Skin,

    [Command(typeof(ShutdownCommand), LongName = "shutdown", Description = "Shut down the OS.")]
    [CommandDescription("Shut down the OS, taking no prisoners.")]
    Shutdown,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    [CommandDescription("Locates an executable file in the system’s PATH that matches the specified name or pattern.", "Example: whereis git")]
    WhereIs,

    [Command(typeof(VerCommand), LongName = "ver", Description = "Report the G33kShell application version.")]
    [CommandDescription("Report the G33kShell application version.")]
    Ver,
}