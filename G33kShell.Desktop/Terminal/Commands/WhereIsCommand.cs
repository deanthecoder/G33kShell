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
using System.Linq;
using System.Runtime.InteropServices;
using CSharp.Core.Extensions;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class WhereIsCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Executable name/pattern to find.")]
    public string ExecutableName { get; [UsedImplicitly] set; }

    public override bool Run(ITerminalState state)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            var paths = pathEnv?.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':') ?? Array.Empty<string>();

            var foundExecutables = false;
            foreach (var dir in paths.Select(o => o.ToDir()).Where(o => o.Exists()))
            {
                // Search for files matching the executable name
                var files = dir.GetFiles(ExecutableName);
                foundExecutables |= files.Length > 0;

                foreach (var fileInfo in files.Where(o => o.IsExecutable()))
                    WriteLine(fileInfo.FullName);
            }

            if (!foundExecutables)
                WriteLine($"No executable found for '{ExecutableName}' in PATH.");

            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
}