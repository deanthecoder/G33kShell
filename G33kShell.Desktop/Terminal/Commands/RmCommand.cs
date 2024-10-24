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
using System.IO;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Removes files and directories, including recursively deleting folders.", "Example: rm file.txt")]
public class RmCommand : LocationCommand
{
    protected override async Task<bool> Run(ITerminalState state)
    {
        var fileSystemInfos = GetItems(state.CurrentDirectory, Path);

        var success = true;
        foreach (var info in fileSystemInfos)
        {
            try
            {
                if (info is FileInfo fileInfo && fileInfo.Exists)
                {
                    if (await Task.Run(() => fileInfo.TryDelete()))
                        continue;
                }
                else
                {
                    if (info is DirectoryInfo directoryInfo && directoryInfo.Exists)
                    {
                        if (await Task.Run(() => directoryInfo.TryDelete()))
                            continue;
                    }
                }

                WriteLine($"Error: Unable to delete {info}");
                success = false;
            }
            catch (IOException ex)
            {
                WriteLine($"Error: Unable to delete {info} due to I/O error ({ex.Message})");
                success = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLine($"Error: Unauthorized to delete {info} ({ex.Message})");
                success = false;
            }
            catch (Exception ex)
            {
                WriteLine($"Error: Unable to delete {info} ({ex.Message})");
                success = false;
            }
        }

        return success;
    }
}