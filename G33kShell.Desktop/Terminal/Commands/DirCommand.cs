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

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Lists files and directories in the current directory.", "Example: dir ..")]
public class DirCommand : CommandBase
{
    [NamedArgument(Description = "Display in bare format.", ShortName = "b")]
    [UsedImplicitly]
    public bool BareFormat { get; set; }

    [NamedArgument(Description = "Recursively list files.", ShortName = "s")]
    [UsedImplicitly]
    public bool Recursive { get; set; }

    [NamedArgument(Description = "List directories only.", ShortName = "d")]
    [UsedImplicitly]
    public bool DirsOnly { get; set; }

    [NamedArgument(Description = "Include file sizes.", ShortName = "l")]
    [UsedImplicitly]
    public bool ShowSizes { get; set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "File mask (e.g. *.exe)")]
    [UsedImplicitly]
    public string FileMask { get; set; } = "*.*";

    protected override async Task<bool> Run(ITerminalState state)
    {
        BareFormat |= Recursive;

        // Display the target path if target is not the cwd.
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
                if (BareFormat && !ShowSizes)
                    DisplayBareFormat(items);
                else
                    DisplayExtendedFormat(items, state.CliPrompt.Width, ShowSizes);
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
        cwd.Resolve(FileMask, out actualCwd, out var fileName, out var mask);
        mask = fileName ?? mask ?? "*.*";
        
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

            if (sb.Length > 0 && sb[^1] == '\n')
                sb.Length--; // Remove the trailing newline
            
            WriteLine(sb.ToString());
        }
    }

    private void DisplayExtendedFormat(FileSystemInfo[] items, int availableWidth, bool showSizes)
    {
        var maxWidth = items.Max(item => item.Name.Length) + 3; // Adjust for padding/spacing
        var maxColumns = showSizes ? 1 : availableWidth / maxWidth;

        if (maxColumns < 2 || items.Length <= 15)
        {
            foreach (var item in items)
            {
                var s = GetItemName(item);
                if (showSizes && item is FileInfo file)
                    s = s.PadRight(availableWidth / 2) + $"  {file.Length:N0} bytes";
                WriteLine(s);
            }
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