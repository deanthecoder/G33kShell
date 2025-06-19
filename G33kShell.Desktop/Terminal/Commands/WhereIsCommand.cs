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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DTC.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Locates an executable file in the system’s PATH that matches the specified name or pattern.", "Example: whereis git")]
public class WhereIsCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Executable name/pattern to find.")]
    public string Name { get; [UsedImplicitly] set; }

    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            var foundExecutables = false;
            foreach (var executablePath in FindExecutables(Name, state.CurrentDirectory))
            {
                WriteLine(executablePath.FullName);
                foundExecutables = true;
            }

            if (!foundExecutables)
                WriteLine($"No executable found for '{Name}' in PATH.");

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Public method that yields paths to executables matching the name.
    /// </summary>
    public static IEnumerable<FileInfo> FindExecutables(string name, DirectoryInfo cwd)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        var paths = new List<string>
        {
            cwd.FullName
        };
        paths.AddRange(pathEnv?.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':') ?? Array.Empty<string>());

        foreach (var dir in paths.Select(o => o.ToDir()).Where(o => o.Exists()))
        {
            var searchPattern = name;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.IsNullOrEmpty(Path.GetExtension(name)))
                searchPattern = name + ".*";
            
            var files = dir.GetFiles(searchPattern);
            foreach (var fileInfo in files.Where(o => o.IsExecutable()))
                yield return fileInfo;
        }
    }
}