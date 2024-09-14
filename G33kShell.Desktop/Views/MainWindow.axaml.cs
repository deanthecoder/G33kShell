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
using Avalonia.Interactivity;
using CSharp.Core.UI;
using G33kShell.Desktop.Skins;

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
        ApplyRetroSkin(source, new RetroPlasma());
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
}