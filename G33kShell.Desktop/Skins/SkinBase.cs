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

namespace G33kShell.Desktop.Skins;

public abstract class SkinBase
{
    // Font properties.
    public abstract IBrush ForegroundColor { get; } // e.g., "#FFFFFF" for white text
    public abstract IBrush BackgroundColor { get; } // e.g., "#000000" for black background

    // Shader uniform properties
    public abstract double BrightnessBoost { get; }
    public abstract bool EnableScanlines { get; }        // true to enable scanlines
    public abstract bool EnableSurround { get; }         // true to enable CRT surround effect
    public abstract bool EnableSignalDistortion { get; } // true to enable signal distortion
    public abstract bool EnableShadows { get; }          // true to enable shadows
}