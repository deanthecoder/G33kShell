using G33kShell.Desktop.Terminal.Controls;
using NClap.Metadata;

namespace G33kShell.Desktop.Terminal.Commands;

public abstract class CommandBase : SynchronousCommand
{
    private ITerminalState m_state;

    private CliPrompt CliPrompt => m_state.CliPrompt;
    
    public CommandBase SetState(ITerminalState state)
    {
        m_state = state;
        return this;
    }

    public abstract bool Run(ITerminalState state);
    
    public override NClap.Metadata.CommandResult Execute()
    {
        Run(m_state);
        return NClap.Metadata.CommandResult.Success;
    }

    protected void WriteLine(string s)
    {
        // Add a line to the prompt output control.
        CliPrompt.AppendLine(s);

        // ...and expand the control height.
        var lineCount = CliPrompt.Text.Length;
        CliPrompt.SetHeight(lineCount);
        
        // If the control now pokes off the bottom of the screen, scroll the controls up.
        CliPrompt.ScrollIntoView();
    }
}