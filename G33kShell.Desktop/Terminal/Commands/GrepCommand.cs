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
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Searches(/replaces) text in files the specified name or pattern.",
    "Example:",
    "  grep *.txt \"some text\"",
    "  grep *.txt \"some text\" replacement")]
public class GrepCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "File mask to search for (e.g. *.txt)")]
    [UsedImplicitly]
    public string FileMask { get; [UsedImplicitly] set; } = "*.*";
    
    [PositionalArgument(ArgumentFlags.Required, Position = 1, Description = "Text to search for.")]
    [UsedImplicitly]
    public string Text { get; set; }
    
    [PositionalArgument(ArgumentFlags.Optional, Position = 2, Description = "Text to replace with.")]
    [UsedImplicitly]
    public string Replace { get; set; }
    
    protected override async Task<bool> Run(ITerminalState state)
    {
        try
        {
            // Find all the files/.
            var results = new List<FileSystemInfo>();
            await foreach (var fileSystemInfo in SearchDirectory(state.CurrentDirectory, FileMask))
                results.Add(fileSystemInfo);
            
            if (results.Count == 0)
            {
                WriteLine("No matching files found.");
                return true;
            }

            if (string.IsNullOrEmpty(Text))
            {
                WriteLine("No text to search for.");
                return false;
            }
            
            // Filter to get the text-only files.
            int? modifiedCount = null;
            var textFiles = await Task.Run(() => results.OfType<FileInfo>().Where(o => o.IsTextFile()).ToArray());

            List<string> output;
            if (Replace == null)
            {
                // Text search only.
                var matches =
                    textFiles
                        .AsParallel()
                        .SelectMany(file =>
                            file
                                .ReadAllLines()
                                .Select((s, lineIndex) => (file, lineIndex, s))
                                .Where(o => o.s.Contains(Text) && !IsCancelRequested));
                var allMatches = await Task.Run(() => matches.GroupBy(o => o.file).ToList());
                output = new List<string>();
                foreach (var match in allMatches)
                {
                    output.Add(match.Key.FullName);
                    output.AddRange(match.Select(o => GetFormattedFindResult(state, o)));
                    output.Add(string.Empty);
                }
            }
            else
            {
                // Search and replace.
                var modified = textFiles
                    .AsParallel()
                    .Select(FileSystemInfo (o) =>
                    {
                        var text = o.ReadAllText();
                        if (!text.Contains(Text))
                            return null;
                        o.WriteAllText(text.Replace(Text, Replace));
                        return o;
                    })
                    .Where(o => o != null && !IsCancelRequested)
                    .Select(o => o.FullName);
                output = await Task.Run(() => modified.ToList());
                modifiedCount = results.Count;
            }

            // Write out the results.
            await Task.Run(() =>
            {
                const int chunkSize = 5;
                var sb = new StringBuilder();
                for (var i = 0; i < output.Count && !IsCancelRequested; i += chunkSize)
                {
                    sb.Clear();
                    foreach (var match in output.Skip(i).Take(chunkSize))
                        sb.AppendLine(match);
                    WriteLine(sb.ToString().TrimEnd());
                    
                    if (IsCancelRequested)
                        break;
                }
            });

            if (modifiedCount.HasValue)
                WriteLine($"{textFiles.Length} file(s) found, {modifiedCount} modified.");
            else
                WriteLine($"{textFiles.Length} file(s) found, {results.Count} contain search text.");
            
            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
        }

        return false;
    }

    private static string GetFormattedFindResult(ITerminalState state, (FileInfo file, int lineIndex, string s) o)
    {
        var sb = new StringBuilder($"Line {o.lineIndex + 1}: {o.s.Trim()}");
        if (sb.Length >= state.CliPrompt.Width)
        {
            var maxLength = state.CliPrompt.Width - 4;
            sb.Remove(maxLength, sb.Length - maxLength);
            sb.Append("...");
        }
        
        return sb.ToString().Trim();
    }

    private async IAsyncEnumerable<FileSystemInfo> SearchDirectory(DirectoryInfo directory, string fileMask)
    {
        directory.Resolve(fileMask, out var dir, out var fileName, out fileMask);
        fileMask = fileName ?? fileMask;
        
        if (fileMask == null)
            throw new ArgumentException("Invalid/missing file mask.");

        await foreach (var fileSystemInfo in dir.TryGetContentAsync(fileMask, SearchOption.AllDirectories, () => IsCancelRequested))
            yield return fileSystemInfo;
    }
}