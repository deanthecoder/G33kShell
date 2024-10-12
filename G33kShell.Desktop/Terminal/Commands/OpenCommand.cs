using System;
using System.IO;
using System.Threading.Tasks;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class OpenCommand : LocationCommand
{
    public override async Task<bool> Run(ITerminalState state)
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