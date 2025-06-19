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

using Avalonia.Media;
using DTC.Core;

namespace G33kShell.Desktop.Skins;

public class RetroPlasma : SkinBase
{
    public override string Name => "RetroPlasma";

    // Font properties
    public override Rgb ForegroundColor { get; } = Color.Parse("#ffa000"); // Orange foreground
    public override Rgb BackgroundColor { get; } = Color.Parse("#301800"); // Brownish-orange background
    public override double BrightnessBoost => 1.2;

    // Shader uniform properties
    public override bool EnableScanlines => true;        // Enable scanlines
    public override bool EnableSurround => true;         // Enable CRT surround effect
    public override bool EnableSignalDistortion => true; // Enable signal distortion
    public override bool EnableShadows => true;          // Enable shadows
}