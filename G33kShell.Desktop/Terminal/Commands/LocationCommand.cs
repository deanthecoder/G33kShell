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
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Retrieves an array of FileSystemInfos based on the provided current working directory and path/mask.
    /// </summary>
    /// <param name="cwd">The current working directory.</param>
    /// <param name="pathAndMask">The path or file mask to search for. This can refer to a specific file, a directory, or contain a wildcard.</param>
    /// <returns>An array of FileSystemInfo objects representing files and directories that match the criteria.</returns>
    /// <remarks>
    /// If <paramref name="pathAndMask"/> points to a directory, the method will retrieve all files within that directory (default mask is "*.*").
    /// If it points to a file or contains a wildcard, the method will retrieve items based on the file name or mask pattern.
    /// If the mask is empty (e.g., the provided path ends with a directory), it defaults to retrieving all files ("*.*").
    /// </remarks>
    protected static FileSystemInfo[] GetItems(DirectoryInfo cwd, string pathAndMask)
    {
        cwd.Resolve(pathAndMask, out var directory, out var fileName, out var mask);
        mask = fileName ?? mask ?? "*.*";

        return
            directory
                .EnumerateFileSystemInfos(mask)
                .OrderBy(o => o.FullName)
                .ToArray();
    }
}