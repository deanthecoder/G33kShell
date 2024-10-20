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
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.Terminal;
using G33kShell.Desktop.Terminal.Commands;
using G33kShell.Desktop.ViewModels;

namespace G33kShell.Desktop.Views;

public class App : Application
{
    public App()
    {
        DataContext = new AppViewModel();
    }
    
    public override void Initialize() =>
        AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var skin = SkinCommand.CreateSkins().FirstOrDefault(o => o?.Name.Equals(Settings.Instance.SkinName, StringComparison.OrdinalIgnoreCase) == true) ?? new RetroPlasma();
            var viewModel = new ShellViewModel(skin);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            desktop.Exit += (_, _) => viewModel.Dispose();

            if (!Design.IsDesignMode)
            {
                desktop.MainWindow.Closing += (_, _) => Settings.Instance.WindowState = desktop.MainWindow.GetPositionString();
                desktop.MainWindow.Loaded += (_, _) => desktop.MainWindow.SetPositionFromString(Settings.Instance.WindowState);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}