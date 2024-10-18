// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class DirCommand : CommandBase
{
    [NamedArgument(Description = "Display in bare format.", ShortName = "b")]
    public bool BareFormat { get; [UsedImplicitly] set; }

    [NamedArgument(Description = "Recursively list files.", ShortName = "s")]
    public bool Recursive { get; [UsedImplicitly] set; }

    [NamedArgument(Description = "List directories only.", ShortName = "d")]
    public bool DirsOnly { get; [UsedImplicitly] set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "File mask (e.g. *.exe)")]
    public string FileMask { get; [UsedImplicitly] set; } = "*.*";

    public override async Task<bool> Run(ITerminalState state)
    {
        BareFormat |= Recursive;

        var displayFolderPath = FileMask.Contains("..") || FileMask.Contains("/") || FileMask.Contains("\\");
        
        try
        {
            var items = GetItems(state.CurrentDirectory, out var actualCwd);
            if (displayFolderPath)
            {
                WriteLine($"Path: {actualCwd.FullName}");
                WriteLine();
            }

            if (items.Length == 0)
            {
                WriteLine("No files or directories found.");
                return true;
            }

            await Task.Run(() =>
            {
                if (BareFormat)
                    DisplayBareFormat(items);
                else
                    DisplayExtendedFormat(items, state.CliPrompt.Width);
            });
        }
        catch (DirectoryNotFoundException)
        {
            WriteLine("Directory not found.");
        }

        return true;
    }

    private FileSystemInfo[] GetItems(DirectoryInfo cwd, out DirectoryInfo actualCwd)
    {
        var fullPath = cwd.Resolve(FileMask);
        string directory;
        string mask;

        // Check if the full path is an actual directory
        if (Directory.Exists(fullPath))
        {
            directory = fullPath;
            mask = "*.*"; // Default to all files
        }
        else
        {
            // Otherwise, split into directory and mask
            directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Directory name could not be evaluated.");
            mask = Path.GetFileName(fullPath);

            // If mask is empty, default to all files
            if (string.IsNullOrEmpty(mask))
            {
                directory = fullPath;
                mask = "*.*";
            }
        }

        actualCwd = new DirectoryInfo(directory ?? ".");
        var recurse = Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var infos = DirsOnly ? actualCwd.EnumerateDirectories(mask, recurse) : actualCwd.EnumerateFileSystemInfos(mask, recurse);
        return infos
            .OrderBy(o => o.FullName)
            .ToArray();
    }

    private void DisplayBareFormat(FileSystemInfo[] items)
    {
        const int chunkSize = 5;
        var sb = new StringBuilder();
        for (var i = 0; i < items.Length; i += chunkSize)
        {
            sb.Clear();
            foreach (var item in items.Skip(i).Take(chunkSize))
                sb.AppendLine(item.FullName);
            WriteLine(sb.ToString());
        }
    }

    private void DisplayExtendedFormat(FileSystemInfo[] items, int availableWidth)
    {
        var maxWidth = items.Max(item => item.Name.Length) + 3; // Adjust for padding/spacing
        var maxColumns = availableWidth / maxWidth;

        if (maxColumns < 2 || items.Length <= 15)
        {
            foreach (var item in items)
                WriteLine(GetItemName(item));
            return;
        }

        var toDisplay = items.Select(o => GetItemName(o).PadRight(maxWidth)).ToArray();
        var i = 0;
        while (i < toDisplay.Length)
        {
            var column = 0;
            var line = new StringBuilder(toDisplay[i++]);
            while (i < toDisplay.Length && ++column < maxColumns)
                line.Append(toDisplay[i++]);
            WriteLine(line.ToString());
        }
    }

    private static string GetItemName(FileSystemInfo item) =>
        (item is DirectoryInfo ? "►" : " ") + item.Name;
}