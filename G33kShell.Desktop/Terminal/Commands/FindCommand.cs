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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class FindCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "File mask to search for (e.g. *.txt)")]
    public string FileMask { get; [UsedImplicitly] set; } = "*.*";

    public override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            var results = SearchDirectory(state.CurrentDirectory, FileMask).ToArray();
            if (results.Length == 0)
            {
                WriteLine("No files or directories found.");
                return true;
            }

            await Task.Run(() =>
            {
                const int chunkSize = 5;
                var sb = new StringBuilder();
                for (var i = 0; i < results.Length; i += chunkSize)
                {
                    sb.Clear();
                    foreach (var item in results.Skip(i).Take(chunkSize))
                        sb.AppendLine(item.FullName);
                    WriteLine(sb.ToString());
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
        }

        return false;
    }

    private static IEnumerable<FileSystemInfo> SearchDirectory(DirectoryInfo directory, string fileMask)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos(fileMask))
            yield return entry;

        // Recursively search subdirectories
        foreach (var subDirectory in directory.EnumerateDirectories())
        {
            foreach (var subEntry in SearchDirectory(subDirectory, fileMask))
                yield return subEntry;
        }
    }
}