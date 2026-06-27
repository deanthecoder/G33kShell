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
using DTC.Core;
using DTC.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription(
    "Searches(/replaces) text in files the specified name or pattern.",
    "Example:",
    "  grep *.txt \"some text\"",
    "  grep -f *.txt \"some text\"",
    "  grep *.txt;*.ext \"some text\" replacement")]
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
    
    [NamedArgument(ArgumentFlags.Optional, ShortName = "f", Description = "Display only filenames.")]
    [UsedImplicitly]
    public bool FilenamesOnly { get; set; }

    [NamedArgument(ArgumentFlags.Optional, ShortName = "c", LongName = "clean", Description = "Clear/delete the bigram index.")]
    [UsedImplicitly]
    public bool Clean { get; set; }

    [NamedArgument(ArgumentFlags.Optional, ShortName = "s", LongName = "case-sensitive", Description = "Enable case-sensitive search.")]
    [UsedImplicitly]
    public bool CaseSensitive { get; set; }

    [NamedArgument(ArgumentFlags.Optional, ShortName = "i", LongName = "index-info", Description = "Display bigram index information.")]
    [UsedImplicitly]
    public bool IndexInfo { get; set; }
    
    protected override async Task<bool> Run(ITerminalState state)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Handle -clean switch
            if (Clean)
            {
                BigramIndex.Instance.Clear();
                WriteLine("Bigram index cleared.");
                return true;
            }

            // Start loading index in parallel.
            var indexLoadTask = Task.Run(() =>
            {
                var idx = BigramIndex.Instance;
                idx.ResetStats(); // Reset per-operation stats
                return idx;
            });

            if (string.IsNullOrEmpty(Text))
            {
                WriteLine("No text to search for.");
                return false;
            }

            // Wait for index to finish loading
            var index = await indexLoadTask.ConfigureAwait(false);

            int matchCount = 0;
            var foundMatchingFiles = false;
            var totalFilesBeforeFilter = 0;
            var scannedFileCount = 0;

            if (Replace == null)
            {
                // Text search only.
                if (FilenamesOnly)
                {
                    // Only report filenames - stop at first match per file.
                    await foreach (var file in SearchDirectory(state.CurrentDirectory, FileMask).ConfigureAwait(false))
                    {
                        if (!ShouldScanFile(file, index, ref totalFilesBeforeFilter, ref scannedFileCount))
                            continue;

                        try
                        {
                            if (!File.ReadLines(file.FullName).Any(line => line.Contains(Text, CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)))
                                continue;

                            foundMatchingFiles = true;
                            WriteLine(file.FullName);
                            matchCount++;
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    // Report all matching lines.
                    var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    await foreach (var file in SearchDirectory(state.CurrentDirectory, FileMask).ConfigureAwait(false))
                    {
                        if (!ShouldScanFile(file, index, ref totalFilesBeforeFilter, ref scannedFileCount))
                            continue;

                        try
                        {
                            var lineIndex = 0;
                            var hasMatches = false;
                            foreach (var line in File.ReadLines(file.FullName))
                            {
                                if (IsCancelRequested)
                                    break;

                                if (line.Contains(Text, comparison))
                                {
                                    if (!hasMatches)
                                        WriteLine(file.FullName);
                                    hasMatches = true;
                                    WriteLine(GetFormattedFindResult(state, (file, lineIndex, line)));
                                }

                                lineIndex++;
                            }

                            if (hasMatches)
                            {
                                foundMatchingFiles = true;
                                matchCount++;
                                WriteLine(string.Empty);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            else
            {
                // Search and replace.
                await foreach (var file in SearchDirectory(state.CurrentDirectory, FileMask).ConfigureAwait(false))
                {
                    if (!ShouldScanFile(file, index, ref totalFilesBeforeFilter, ref scannedFileCount))
                        continue;

                    try
                    {
                        var text = file.ReadAllText();
                        var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        if (!text.Contains(Text, comparison))
                            continue;
                        file.WriteAllText(text.Replace(Text, Replace, comparison));
                        foundMatchingFiles = true;
                        WriteLine(file.FullName);
                        matchCount++;
                    }
                    catch
                    {
                    }
                }
            }

            // Save index if modified (before displaying stats so file size is accurate)
            index?.Save();

            var elapsed = DateTime.UtcNow - startTime;
            if (totalFilesBeforeFilter == 0 && !foundMatchingFiles)
            {
                WriteLine("No matching files found.");
                return true;
            }

            // Better message when prefilter eliminated everything
            if (scannedFileCount == 0 && index != null && index.FilesSkippedByPrefilter > 0)
                WriteLine($"{totalFilesBeforeFilter} file(s) checked via index, 0 actually scanned, {matchCount} match(es) found. ({elapsed.TotalSeconds:F2}s)");
            else
                WriteLine($"{scannedFileCount} file(s) scanned, {matchCount} match(es) found. ({elapsed.TotalSeconds:F2}s)");

            // Display index stats if requested
            if (IndexInfo && index != null)
            {
                WriteLine(string.Empty);
                WriteLine("═══ Bigram Index Statistics ═══");
                WriteLine($"  Index file size      : {index.IndexFileSize.ToSize()} on disk, {index.UncompressedSize.ToSize()} uncompressed");
                WriteLine($"  Total entries        : {index.EntryCount:N0}");
                WriteLine($"  Text files on disk   : {totalFilesBeforeFilter:N0}");
                WriteLine($"  Newly indexed        : {index.NewFilesIndexed:N0}");
                WriteLine($"  Skipped by prefilter : {index.FilesSkippedByPrefilter:N0} (avoided disk read)");
                WriteLine($"  Scanned from disk    : {scannedFileCount:N0}");

                var filterEfficiency = totalFilesBeforeFilter > 0 ? index.FilesSkippedByPrefilter * 100.0 / totalFilesBeforeFilter : 0;
                WriteLine($"  Filter efficiency    : {filterEfficiency:F1}%");
            }

            return true;
        }
        catch (Exception ex)
        {
            WriteLine($"An error occurred: {ex.Message}");
            Logger.Instance.Exception("grep failed.", ex);
        }

        return false;
    }

    private bool ShouldScanFile(FileInfo file, BigramIndex index, ref int totalTextFiles, ref int scannedFiles)
    {
        if (IsCancelRequested)
            return false;

        var isTextFile = index?.IsIndexed(file) == true || IsTextFile(file);
        if (!isTextFile)
            return false;

        totalTextFiles++;

        if (index?.CanContain(file, Text, CaseSensitive) == false)
            return false;

        scannedFiles++;
        return true;
    }

    private static bool IsTextFile(FileInfo o)
    {
        try
        {
            return o.IsTextFile();
        }
        catch (Exception)
        {
            return false;
        }
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

    private async IAsyncEnumerable<FileInfo> SearchDirectory(DirectoryInfo directory, string fileMask)
    {
        var components = fileMask.Split(';').Distinct();
        foreach (var mask in components)
        {
            directory.Resolve(mask, out var dir, out var fileName, out var newMask);
            newMask = fileName ?? newMask;

            if (fileMask == null)
                throw new ArgumentException("Invalid/missing file mask.");

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            IEnumerable<FileInfo> files;
            try
            {
                files = dir.EnumerateFiles(newMask, options);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (IsCancelRequested)
                    yield break;

                yield return file;
                await Task.Yield();
            }
        }
    }
}
