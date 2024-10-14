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
using System.Threading.Tasks;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class RevealCommand : LocationCommand
{
    public override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            var targetPath = state.CurrentDirectory.Resolve(Path);
            
            var fileInfo = new FileInfo(targetPath);
            if (fileInfo.Exists)
            {
                await Task.Run(() => fileInfo.Explore());
                return true;
            }
            
            var directoryInfo = new DirectoryInfo(targetPath);
            if (directoryInfo.Exists)
            {
                await Task.Run(() => directoryInfo.Explore());
                return true;
            }
            
            WriteLine($"Location not found: {Path}");
            return false;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
}