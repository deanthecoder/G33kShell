using JetBrains.Annotations;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public abstract class LocationCommand : CommandBase
{
    [PositionalArgument(ArgumentFlags.Required, Description = "Location to operate on.")]
    public string Path { get; [UsedImplicitly] set; }
}