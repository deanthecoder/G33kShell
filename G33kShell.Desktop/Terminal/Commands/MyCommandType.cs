using G33kShell.Desktop.Terminal.Attributes;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public enum MyCommandType
{
    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    [CommandDescription("Changes the current directory to the specified path.", "Example: cd /home/user/documents")]
    Cd,

    [Command(typeof(ClipCommand), LongName = "clip", Description = "Copy the output from the previous command into the clipboard.")]
    [CommandDescription("Copies the output from the previous command into the system clipboard.")]
    Clip,

    [Command(typeof(DirCommand), LongName = "dir", ShortName = "ls", Description = "List files and directories.")]
    [CommandDescription("Lists files and directories in the current directory.", "Example: dir ..")]
    Dir,

    [Command(typeof(FindCommand), LongName = "find", Description = "Search for files or directories by name.")]
    [CommandDescription("Searches for files or directories matching the specified name or pattern.", "Example: find *.txt")]
    Find,

    [Command(typeof(HelpCommand), LongName = "help", ShortName = "?", Description = "Get help information.")]
    [CommandDescription("Provides help information for available commands.")]
    Help,

    [Command(typeof(ManCommand), LongName = "man", Description = "Get usage information for a specific command.")]
    [CommandDescription("Displays manual pages for commands, detailing their usage and options.", "Example: man open")]
    Man,

    [Command(typeof(OpenCommand), LongName = "open", Description = "View the target in the OS file browser.")]
    [CommandDescription("Opens the specified file or directory in the OS file browser or associated application.", "Example: open myfile.txt")]
    Open,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    [CommandDescription("Locates an executable file in the system’s PATH that matches the specified name or pattern.", "Example: whereis git")]
    WhereIs,
}