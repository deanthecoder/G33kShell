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
using Avalonia.Layout;
using CSharp.Core.ViewModels;
using G33kShell.Desktop.Console;

namespace G33kShell.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public WindowManager WindowManager { get; } = new WindowManager(100, 38);

    public MainWindowViewModel()
    {
        WindowManager.Root
            .AddChild(new Fire
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom
            }.Init(100, 12))
            .AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }.Init(
                " ░░░░░░╗ ░░░░░░╗ ░░░░░░╗ ░░╗  ░░╗ ░░░░░░╗░░╗  ░░╗░░░░░░░╗░░╗     ░░╗",
                "▒▒╔════╝  ╚═══▒▒╗ ╚═══▒▒╗▒▒║ ▒▒╔╝▒▒╔════╝▒▒║  ▒▒║▒▒╔════╝▒▒║     ▒▒║",
                "▓▓║  ▓▓▓╗ ▓▓▓▓▓╔╝ ▓▓▓▓▓╔╝▓▓▓▓▓╔╝  ▓▓▓▓▓▓╗▓▓▓▓▓▓▓║▓▓▓▓▓╗  ▓▓║     ▓▓║",
                "▓▓║   ▓▓║  ╚══▓▓╗  ╚══▓▓╗▓▓╔═▓▓╗  ╚═══▓▓║▓▓╔══▓▓║▓▓╔══╝  ▓▓║     ▓▓║",
                " ▐█████╔╝▐█████╔╝▐█████╔╝██║  ██╗▐██████║██║  ██║███████╗███████╗███████╗",
                "  ╚════╝  ╚════╝  ╚════╝  ╚╝  ▐█║ ╚═════╝ ╚╝  ▐█║ ╚═════╝ ╚═════╝ ╚═════╝",
                "                               ╚╝             ▐█║",
                "                                               ╚╝"
            ))
            .AddChild(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right // todo - Support shadow (slim/░/█)
                }.Init(21, 3, Border.LineStyle.SingleHorizontalDoubleVertical)
                .AddChild(new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }.Init("By DeanTheCoder")));
    }
}