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

using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public enum MyCommandType
{
    [Command(typeof(CatCommand), LongName = "cat", ShortName="type", Description = "Write the contents of a file to the console.")]
    Cat,

    [Command(typeof(CdCommand), LongName = "cd", Description = "Change the current directory.")]
    Cd,

    [Command(typeof(CddCommand), LongName = "cdd", Description = "Change directory using an intelligent look-up.")]
    Cdd,

    [Command(typeof(ClearCommand), LongName = "clear", ShortName = "cls", Description = "Clear the terminal.")]
    Clear,

    [Command(typeof(ClipCommand), LongName = "clip", Description = "Copy command output into the clipboard.")]
    Clip,

    [Command(typeof(CopyCommand), LongName = "copy", ShortName = "cp", Description = "Copy files and directories.")]
    Copy,

    [Command(typeof(DirCommand), LongName = "dir", ShortName = "ls", Description = "List files and directories.")]
    Dir,

    [Command(typeof(ExitCommand), LongName = "exit", Description = "Quit the terminal.")]
    Exit,

    [Command(typeof(FindCommand), LongName = "find", Description = "Search for files or directories by name.")]
    Find,

    [Command(typeof(HelpCommand), LongName = "help", ShortName = "?", Description = "Get help information.")]
    Help,

    [Command(typeof(HistoryCommand), LongName = "history", Description = "List command history.")]
    History,

    [Command(typeof(ManCommand), LongName = "man", Description = "Get usage information for a specific command.")]
    Man,

    [Command(typeof(MaxCommand), LongName = "max", Description = "Toggle the terminal window between maximized and restored states.")]
    Max,

    [Command(typeof(MkdirCommand), LongName = "mkdir", ShortName = "md", Description = "Creates a directory.")]
    Mkdir,

    [Command(typeof(NowCommand), LongName = "now", Description = "Report the current date and time.")]
    Now,

    [Command(typeof(OpenCommand), LongName = "open", Description = "Launches a file using the associated application based on the OS settings.")]
    Open,
    
    [Command(typeof(PopDCommand), LongName = "popd", Description = "Restore to the previously 'pushed' directory.")]
    PopD,

    [Command(typeof(PushDCommand), LongName = "pushd", Description = "Change the current directory.")]
    PushD,

    [Command(typeof(RevealCommand), LongName = "reveal", Description = "View the target in the OS file browser.")]
    Reveal,
    
    [Command(typeof(RmCommand), LongName = "rm", ShortName = "del", Description = "Recursively delete a directory or file(s).")]
    Rm,
    
    [Command(typeof(ScreensaverCommand), LongName = "screensaver", Description = "Select and activate the screensaver.")]
    Screensaver,

    [Command(typeof(SkinCommand), LongName = "skin", Description = "List or change the current Terminal skin.")]
    Skin,

    [Command(typeof(SpaceCommand), LongName = "space", Description = "Display disk space usage.")]
    Space,

    [Command(typeof(ShutdownCommand), LongName = "shutdown", Description = "Shut down the OS.")]
    Shutdown,

    [Command(typeof(TailCommand), LongName = "tail", Description = "Output the last few lines of a file.")]
    Tail,

    [Command(typeof(TreeCommand), LongName = "tree", Description = "List files and directories in a tree structure.")]
    Tree,

    [Command(typeof(WhereIsCommand), LongName = "whereis", Description = "Finds an executable located in the system’s PATH.")]
    WhereIs,

    [Command(typeof(WorktimeCommand), LongName = "worktime", Description = "How far are you through the working day?")]
    Worktime,

    [Command(typeof(VerCommand), LongName = "ver", Description = "Report the G33kShell application version.")]
    Ver
}