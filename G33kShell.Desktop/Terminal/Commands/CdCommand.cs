using System;
using System.IO;
using System.Threading.Tasks;

namespace G33kShell.Desktop.Terminal.Commands;

public class CdCommand : LocationCommand
{
    public override Task<bool> Run(ITerminalState state)
    {
        var newDir = state.CurrentDirectory;
        try
        {
            newDir = new DirectoryInfo(GetTargetPath(state));
            if (newDir.Exists)
            {
                state.CurrentDirectory = newDir;
                return Task.FromResult(true);
            }
        }
        catch (Exception)
        {
            // Fall through.
        }
        
        WriteLine($"CWD not found: {newDir}");
        return Task.FromResult(false);
    }
}