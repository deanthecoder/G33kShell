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
using System.Threading.Tasks;
using Avalonia;
using DTC.Core;
using G33kShell.Desktop.Terminal;
using G33kShell.Desktop.Views;

namespace G33kShell.Desktop;

static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Logger.Instance.SysInfo();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            Logger.Instance.Info("Application ended cleanly.");
        }
        catch (Exception ex)
        {
            HandleFatalException(ex);
        }
        finally
        {
            Settings.Instance.Save();
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            HandleFatalException(ex);
    }

    private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Logger.Instance.Exception("Unobserved task exception.", e.Exception);
    }

    private static void HandleFatalException(Exception ex) =>
        Logger.Instance.Exception("A fatal error occurred.", ex);

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}