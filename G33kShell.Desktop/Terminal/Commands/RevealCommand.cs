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
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Opens the specified file or directory in the system’s file explorer.",
    "",
    "Special path options are available:",
    "  'applications' - Opens the Program Files folder.",
    "  'desktop'      - Opens the Desktop.",
    "  'documents'    - Opens the Documents folder.",
    "  'downloads'    - Opens the Downloads folder.",
    "  'home'         - Opens the user's 'home' folder.",
    "  'programs'     - Opens the Program Files folder.",
    "  'temp'         - Opens the system's temporary files directory.",
    "",
    "Example:",
    "  reveal myfile.txt",
    "  reveal desktop",
    "  reveal downloads"
)]
public class RevealCommand : LocationCommand
{
    protected override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            var targetPath = GetTargetPath(state);
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