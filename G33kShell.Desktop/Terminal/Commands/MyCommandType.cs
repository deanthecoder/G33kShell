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
// ReSharper disable UnusedMember.Global

namespace G33kShell.Desktop.Terminal.Commands;

public enum MyCommandType
{
    // File Management
    [Command(typeof(CatCommand), LongName = "cat", ShortName="type", Description = "Write the contents of a file to the console.")]
    [CommandCategory(CommandType.File)]
    Cat,

    [Command(typeof(CopyCommand), LongName = "copy", ShortName = "cp", Description = "Copy files and directories.")]
    [CommandCategory(CommandType.File)]
    Copy,

    [Command(typeof(DirCommand), LongName = "dir", ShortName = "ls", Description = "List files and directories.")]
    [CommandCategory(CommandType.File)]
    Dir,

    [Command(typeof(FindCommand), LongName = "find", Description = "Search for files or directories by name.")]
    [CommandCategory(CommandType.File)]
    Find,
    
    [Command(typeof(GrepCommand), LongName = "grep", Description = "Search(/replace) for text within files.")]
    [CommandCategory(CommandType.File)]
    Grep,

    [Command(typeof(MkdirCommand), LongName = "mkdir", ShortName = "md", Description = "Creates a directory.")]
    [CommandCategory(CommandType.File)]
    Mkdir,

    [Command(typeof(OpenCommand), LongName = "open", Description = "Launches a file using the associated application based on the OS settings.")]
    [CommandCategory(CommandType.File)]
    Open,

    [Command(typeof(RevealCommand), LongName = "reveal", Description = "View the target in the OS file browser.")]
    [CommandCategory(CommandType.File)]
    Reveal,

    [Command(typeof(RmCommand), LongName = "rm", ShortName = "del", Description = "Recursively delete a directory or file(s).")]
    [CommandCategory(CommandType.File)]
    Rm,

    [Command(typeof(TailCommand), LongName = "tail", Description = "Output the last few lines of a file.")]
    [CommandCategory(CommandType.File)]
    Tail,

    [Command(typeof(TreeCommand), LongName = "tree", Description = "List files and directories in a tree structure.")]
    [CommandCategory(CommandType.File)]
    Tree,

    // Navigation
    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    [CommandCategory(CommandType.Navigation)]
    Cd,

    [Command(typeof(CddCommand), LongName = "cdd", Description = "Change directory using an intelligent look-up.")]
    [CommandCategory(CommandType.Navigation)]
    Cdd,

    [Command(typeof(PopDCommand), LongName = "popd", Description = "Restore to the previously 'pushed' directory.")]
    [CommandCategory(CommandType.Navigation)]
    PopD,

    [Command(typeof(PushDCommand), LongName = "pushd", Description = "Change the current directory.")]
    [CommandCategory(CommandType.Navigation)]
    PushD,

    // System & Utility
    [Command(typeof(ClearCommand), LongName = "clear", ShortName = "cls", Description = "Clear the terminal.")]
    [CommandCategory(CommandType.System)]
    Clear,

    [Command(typeof(ClipCommand), LongName = "clip", Description = "Copy command output into the clipboard.")]
    [CommandCategory(CommandType.System)]
    Clip,

    [Command(typeof(ExitCommand), LongName = "exit", Description = "Quit the terminal.")]
    [CommandCategory(CommandType.Hidden)]
    Exit,

    [Command(typeof(HelpCommand), LongName = "help", ShortName = "?", Description = "Get help information.")]
    [CommandCategory(CommandType.Hidden)]
    Help,

    [Command(typeof(HistoryCommand), LongName = "history", Description = "List command history.")]
    [CommandCategory(CommandType.System)]
    History,

    [Command(typeof(ManCommand), LongName = "man", Description = "Get usage information for a specific command.")]
    [CommandCategory(CommandType.Hidden)]
    Man,

    [Command(typeof(MaxCommand), LongName = "max", Description = "Toggle the terminal window between maximized and restored states.")]
    [CommandCategory(CommandType.System)]
    Max,

    [Command(typeof(NowCommand), LongName = "now", Description = "Report the current date and time.")]
    [CommandCategory(CommandType.System)]
    Now,

    [Command(typeof(PathsCommand), LongName = "paths", Description = "Lists all PATH entries alphabetically.")]
    [CommandCategory(CommandType.System)]
    Path,

    [Command(typeof(ScreensaverCommand), LongName = "screensaver", Description = "Select and activate the screensaver.")]
    [CommandCategory(CommandType.System)]
    Screensaver,

    [Command(typeof(SkinCommand), LongName = "skin", Description = "List or change the current Terminal skin.")]
    [CommandCategory(CommandType.System)]
    Skin,

    [Command(typeof(SpaceCommand), LongName = "space", Description = "Display disk space usage.")]
    [CommandCategory(CommandType.System)]
    Space,

    [Command(typeof(ShutdownCommand), LongName = "shutdown", Description = "Shut down the OS.")]
    [CommandCategory(CommandType.System)]
    Shutdown,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    [CommandCategory(CommandType.System)]
    WhereIs,

    [Command(typeof(WorktimeCommand), LongName = "worktime", Description = "How far are you through the working day?")]
    [CommandCategory(CommandType.System)]
    Worktime,

    [Command(typeof(VerCommand), LongName = "ver", Description = "Report the G33kShell application version.")]
    [CommandCategory(CommandType.System)]
    Ver
}