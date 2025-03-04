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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CSharp.Core;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Opens the specified file or folder using the default application on the system.")]
public class OpenCommand : LocationCommand
{
    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            state.CurrentDirectory.Resolve(Path, out var dir, out var fileName, out _);

            var toOpen = dir.FullName;
            if (fileName != null)
                toOpen = System.IO.Path.Combine(toOpen, fileName);
            if (File.Exists(toOpen) || Directory.Exists(toOpen))
            { 
                Task.Run(() => Process.Start(new ProcessStartInfo(toOpen)
                {
                    UseShellExecute = true
                }));
                return Task.FromResult(true);
            }

            WriteLine($"Location not found: {Path}");
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            Logger.Instance.Exception($"Failed to open '{Path}'", ex);
        }

        return Task.FromResult(false);
    }
}