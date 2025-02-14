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
using System.Text;
using System.Threading.Tasks;
using G33kShell.Desktop.Terminal.Attributes;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Write the contents of a file to the console.", "Example: cat readme.md")]
public class CatCommand : LocationCommand
{
    protected override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            var targetPath = GetTargetPath(state);
            var fileInfo = new FileInfo(targetPath);
            if (fileInfo.Exists)
            {
                await Task.Run(async () =>
                {
                    await foreach (var s in ReadLines(fileInfo))
                    {
                        WriteLine(s);
                        if (IsCancelRequested)
                            break;
                    }       
                });
                
                return true;
            }

            WriteLine($"File not found: {Path}");
            return false;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    private static async IAsyncEnumerable<string> ReadLines(FileInfo fileInfo)
    {
        const int bufferSize = 4096;
        const int maxPrintableChars = 256;

        await using var stream = fileInfo.OpenRead();
        using var reader = new StreamReader(stream, leaveOpen: true);

        var buffer = new char[bufferSize];
        int charsRead;
        var printableCharCount = 0;

        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var content = new string(buffer, 0, charsRead).Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                var formattedLine = new StringBuilder();

                foreach (var ch in line)
                {
                    if (ch == '\t')
                    {
                        formattedLine.Append("  "); // Replace tab with two spaces
                        printableCharCount += 2;
                    }
                    else if (IsPrintable(ch))
                    {
                        formattedLine.Append(ch);
                        printableCharCount++;
                    }
                    else
                    {
                        formattedLine.Append('.');
                    }

                    // Check if we've exceeded the limit.
                    if (printableCharCount > maxPrintableChars)
                    {
                        var remainingBytes = fileInfo.Length - stream.Position;
                        if (remainingBytes > 0)
                            yield return $"[Stopped: {remainingBytes:N0} bytes remaining]";
                        yield break;
                    }
                }

                yield return formattedLine.ToString();
            }
        }
    }

    private static bool IsPrintable(char ch) =>
        !char.IsControl(ch) && char.IsAscii(ch);
}