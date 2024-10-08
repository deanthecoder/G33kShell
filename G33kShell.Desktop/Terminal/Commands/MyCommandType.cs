using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public enum MyCommandType
{
    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    Cd,

    [Command(typeof(DirCommand), LongName = "dir", ShortName = "ls", Description = "List files and directories.")]
    Dir,

    [Command(typeof(FindCommand), LongName = "find", Description = "Search for files or directories by name.")]
    Find,

    [Command(typeof(HelpCommand), LongName = "help", ShortName = "?", Description = "Get help information.")]
    Help,

    [Command(typeof(ManCommand), LongName = "man", Description = "Get usage information for a specific command.")]
    Man,

    [Command(typeof(OpenCommand), LongName = "open", Description = "View the target in the OS file browser.")]
    Open,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    WhereIs,
}