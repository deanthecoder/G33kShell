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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DTC.Core;
using DTC.Core.UI;
using G33kShell.Desktop.Console.Views;
using G33kShell.Desktop.Skins;
using G33kShell.Desktop.ViewModels;

namespace G33kShell.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OnConsoleViewLoaded(object sender, RoutedEventArgs _)
    {
        var source = (ConsoleView)sender;
        source.Focus();
        
        // Configure the default CRT shader.
        ApplyRetroSkin(source, source.WindowManager.Skin);
    }

    private void ApplyRetroSkin(ConsoleView source, SkinBase skin)
    {
        // Apply skin.
        source.WindowManager.Skin = skin;

        // Configure the CRT shader using the provided skin.
        Shader.AddUniform("brightnessBoost", (float)skin.BrightnessBoost);
        Shader.AddUniform("enableScanlines", skin.EnableScanlines);
        Shader.AddUniform("enableSurround", skin.EnableSurround);
        Shader.AddUniform("enableSignalDistortion", skin.EnableSignalDistortion);
        Shader.AddUniform("enableShadows", skin.EnableShadows);

        // Alignment.
        MarginPanel.Margin = new Thickness(skin.EnableSurround ? 20 : 0);
    }
    
    private void OnShaderControlLoaded(object sender, RoutedEventArgs _)
    {
        // Set ShaderControl's source control.
        ((ShaderControl)sender).ControlSource = Source;
    }

    private void OnDoubleTapped(object sender, TappedEventArgs e) =>
        ((ShellViewModel)DataContext)?.RaiseRevealCwdRequested();

    private Point GetConsolePosition(PointerEventArgs e) =>
        ConsoleView.MapDisplayPosition(e.GetPosition(Shader), Shader.Bounds.Size, Source.Bounds.Size,
            Source.WindowManager.Skin.EnableSurround);

    private async void OnConsolePointerPressed(object sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint((Control)sender).Properties;
        if (properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            Source.Focus();
            if (Source.BeginSelection(GetConsolePosition(e)))
                e.Pointer.Capture((Control)sender);
            e.Handled = true;
        }
        else if (properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            e.Handled = true;
            var text = Source.GetSelectionText();
            if (text == null || Clipboard == null)
                return;
            try
            {
                await Clipboard.SetTextAsync(text);
                Source.ClearSelection();
            }
            catch (System.Exception exception)
            {
                Logger.Instance.Exception("Failed to copy console selection.", exception);
            }
        }
    }

    private void OnConsolePointerMoved(object sender, PointerEventArgs e) =>
        Source.ExtendSelection(GetConsolePosition(e));

    private void OnConsolePointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        Source.ExtendSelection(GetConsolePosition(e));
        Source.EndSelection();
        e.Pointer.Capture(null);
    }

    private void OnConsolePointerCaptureLost(object sender, PointerCaptureLostEventArgs e) =>
        Source.EndSelection();

    internal string GetMemoryDetails()
    {
        var shellDetails = (DataContext as ShellViewModel)?.GetMemoryDetails() ?? "shell unavailable";
        return $"{shellDetails}; {Source.GetMemoryDetails()}";
    }
}
