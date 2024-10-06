using System.IO;
using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public abstract class LocationCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Location to operate on.")]
    public string Location { get; [UsedImplicitly] set; }

    protected string GetTargetPath(ITerminalState state) =>
        Path.GetFullPath(Path.Combine(state.CurrentDirectory.FullName, Location));
}