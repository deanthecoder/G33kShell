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
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Navigate to a folder and push it onto the directory stack.",
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
    "See: popd")]
public class PushDCommand : LocationCommand
{
    protected override Task<bool> Run(ITerminalState state)
    {
        try
        {
            var newDir = state.CurrentDirectory.Resolve(GetTargetPath(state)).ToDir();
            if (newDir.Exists)
            {
                state.DirStack.Push(state.CurrentDirectory); // push current directory to stack
                state.CurrentDirectory = newDir;             // make new directory current
                return Task.FromResult(true);
            }
        }
        catch (Exception)
        {
            // Fall through.
        }

        WriteLine($"Directory not found: {Path}");
        return Task.FromResult(false);
    }
}