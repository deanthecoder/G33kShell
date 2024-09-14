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
using Avalonia.Layout;
using CSharp.Core;
using CSharp.Core.ViewModels;
using G33kShell.Desktop.Console;

namespace G33kShell.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public WindowManager WindowManager { get; } = new WindowManager(100, 38);

    public MainWindowViewModel()
    {
        WindowManager.Root
            .AddChild(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right // todo - Support shadow (slim/░/█)
                }.Init(21, 3, Border.LineStyle.SingleHorizontalDoubleVertical)
                .AddChild(new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }.Init("By DeanTheCoder")))
            .AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Y = -4
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
            .AddChild(new ProgressBar
            {
                Name = "LogoProgress",
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Y = 2
            }.Init(50, 1))
            .AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Y = 3,
                IsFlashing = true
            }.Init("Penetrating Advanced Stealth Systems..."))
            .AddChild(new Fire
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom
            }.Init(100, 12))
            //.AddChild(new Image().Init(40, 40, new FileInfo("/Users/dean/Desktop/me.png")))
            ;

        _ = new Animation(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                f =>
                {
                    WindowManager.Find<ProgressBar>("LogoProgress").Progress = (int)(f * 100);
                    return true;
                })
            .StartAsync();
    }
}