using System;
using System.IO;
using CSharp.Core.Extensions;

namespace G33kShell.Desktop.Terminal.Commands;

public class OpenCommand : LocationCommand
{
    public override bool Run(ITerminalState state)
    {
        try
        {
            var targetPath = GetTargetPath(state);
            
            var fileInfo = new FileInfo(targetPath);
            if (fileInfo.Exists)
            {
                fileInfo.Explore();
                return true;
            }
            
            var directoryInfo = new DirectoryInfo(targetPath);
            if (directoryInfo.Exists)
            {
                directoryInfo.Explore();
                return true;
            }
            
            WriteLine($"Location not found: {Location}");
            return false;
        }
        catch (Exception e)
        {
            WriteLine($"An error occurred: {e.Message}");
            return false;
        }
    }
}