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
using CSharp.Core.Extensions;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public abstract class LocationCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Location to operate on.")]
    public string Path { get; [UsedImplicitly] set; }
    
    /// <summary>
    /// Return the <see cref="Path"/>, but additionally allow shortcuts (desktop, home, etc).
    /// </summary>
    protected string GetTargetPath(ITerminalState state)
    {
        var targetPath = Path switch
        {
            "applications" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "downloads" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads",
            "home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "programs" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "temp" => System.IO.Path.GetTempPath(),
            _ => state.CurrentDirectory.Resolve(Path)
        };
        return targetPath;
    }
}