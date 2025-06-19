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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DTC.Core.Extensions;
using G33kShell.Desktop.Terminal.Attributes;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

[CommandDescription("Displays an ASCII tree representation of files and directories.", "Example: tree -d")]
public class TreeCommand : CommandBase
{
    [NamedArgument(Description = "Display directories only.", ShortName = "d")]
    [UsedImplicitly]
    public bool FoldersOnly { get; set; }

    [NamedArgument(ArgumentFlags.Optional, Description = "Use standard ASCII characters.", ShortName = "a")]
    [UsedImplicitly]
    public bool UseAscii { get; set; }

    [PositionalArgument(ArgumentFlags.Optional, Description = "Starting directory.")]
    [UsedImplicitly]
    public string StartingDirectory { get; set; } = ".";

    protected override async Task<bool> Run(ITerminalState state)
    {
        state.CurrentDirectory.Resolve(StartingDirectory, out var actualCwd, out _, out _);
        if (!actualCwd.Exists())
        {
            WriteLine("Directory not found.");
            return true;
        }

        try
        {
            var items = await GetItems(actualCwd);
            DisplayTree(items, actualCwd);
        }
        catch (DirectoryNotFoundException)
        {
            WriteLine("Directory not found.");
        }

        return true;
    }

    private async Task<IList<FileSystemInfo>> GetItems(DirectoryInfo root)
    {
        var items = await root
            .TryGetContentAsync(searchOption: SearchOption.AllDirectories, isCancelRequested: () => IsCancelRequested)
            .ToListAsync();
        return FoldersOnly ? items.Where(o => o is DirectoryInfo).ToList() : items;
    }

    private void DisplayTree(IEnumerable<FileSystemInfo> items, DirectoryInfo root)
    {
        // Group by parent directory
        var itemLookup = items
            .GroupBy(item => Path.GetDirectoryName(item.FullName))
            .ToDictionary(group => group.Key, group => group.ToList());

        WriteLine(root.FullName);
        var sb = new StringBuilder();
        PrintTree(root, string.Empty);
        WriteLine(sb.ToString());
        return;

        void PrintTree(DirectoryInfo path, string prefix)
        {
            if (!itemLookup.TryGetValue(path.FullName, out var children))
                return;

            var endChild = UseAscii ? "`-- " : "└── ";
            var midChild = UseAscii ? "+-- " : "├── ";
            var noSibling = UseAscii ? "|   " : "│   ";

            for (var i = 0; i < children.Count && !IsCancelRequested; i++)
            {
                var child = children[i];
                var isLast = i == children.Count - 1;
                var connector = isLast ? endChild : midChild;
                sb.AppendLine(prefix + connector + child.Name);

                if (child is DirectoryInfo dir)
                {
                    var newPrefix = prefix + (isLast ? "    " : noSibling);
                    PrintTree(dir, newPrefix);
                }

                if (IsCancelRequested)
                    return;
            }
        }
    }
}