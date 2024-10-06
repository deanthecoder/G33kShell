using System;
using System.IO;

namespace G33kShell.Desktop.Terminal.Commands;

public class CdCommand : LocationCommand
{
    public override bool Run(ITerminalState state)
    {
        var newDir = state.CurrentDirectory;
        try
        {
            newDir = new DirectoryInfo(GetTargetPath(state));
            if (newDir.Exists)
            {
                state.CurrentDirectory = newDir;
                return true;
            }
        }
        catch (Exception)
        {
            // Fall through.
        }
        
        WriteLine($"CWD not found: {newDir}");
        return false;
    }
}