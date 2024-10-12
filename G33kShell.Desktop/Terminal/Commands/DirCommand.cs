using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public class DirCommand : CommandBase
{
    // Option for /b switch
    [NamedArgument(Description = "Display in bare format", ShortName = "b")]
    public bool BareFormat { get; [UsedImplicitly] set; }

    // Option for /s switch
    [NamedArgument(Description = "Recursively list files", ShortName = "s")]
    public bool Recursive { get; [UsedImplicitly] set; }

    // Positional argument for file mask (e.g. *.exe)
    [PositionalArgument(ArgumentFlags.Optional, Description = "File mask (e.g. *.exe)")]
    public string FileMask { get; [UsedImplicitly] set; } = "*.*";

    public override Task<bool> Run(ITerminalState state)
    {
        IEnumerable<FileSystemInfo> files = state.CurrentDirectory.EnumerateFiles();
        IEnumerable<FileSystemInfo> dirs = state.CurrentDirectory.EnumerateDirectories();
        foreach (var file in files.Union(dirs).OrderBy(o => o.Name))
            WriteLine(file.ToString());

        return Task.FromResult(true);
    }
}