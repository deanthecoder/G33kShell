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
using System.IO;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal.Controls;

namespace G33kShell.Desktop.Terminal;

/// <summary>
/// Represents the state of the terminal including the current directory, command history, and CLI prompt control.
/// </summary>
public interface ITerminalState
{
    event EventHandler<SkinBase> SkinLoadRequest;
    
    DirectoryInfo CurrentDirectory { get; set; }
    CommandHistory CommandHistory { get; }
    CliPrompt CliPrompt { get; }
    
    /// <summary>
    /// Used by the pushd and popd commands.
    /// </summary>
    Stack<DirectoryInfo> DirStack { get; }

    void LoadSkin(SkinBase skin);
}