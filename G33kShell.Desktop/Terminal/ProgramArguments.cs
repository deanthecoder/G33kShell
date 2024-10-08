using G33kShell.Desktop.Terminal.Commands;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal;

public class ProgramArguments
{
    [PositionalArgument(ArgumentFlags.Required, Position = 0)]
    public CommandGroup<MyCommandType> PrimaryCommand { get; set; }
}