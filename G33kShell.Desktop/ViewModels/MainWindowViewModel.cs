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
    public WindowManager WindowManager { get; } = new WindowManager(80, 30);

    public MainWindowViewModel()
    {
        WindowManager.Root
            .AddChild(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }.Init(0, 0, 20, 6, Border.LineStyle.Double)
                .AddChild(new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom
                }.Init(0, 0, "Hello world!\nGreetings!")));
    }
}