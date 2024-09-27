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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Layout;
using CSharp.Core;
using CSharp.Core.Extensions;
using CSharp.Core.ImageProcessing;
using CSharp.Core.ViewModels;
using G33kShell.Desktop.Console;
using G33kShell.Desktop.Skins;
using SkiaSharp;

namespace G33kShell.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public WindowManager WindowManager { get; }

    // Used by the Designer.
    public MainWindowViewModel() : this(new RetroPlasma())
    {
    }

    public MainWindowViewModel(SkinBase skin)
    {
        WindowManager = new WindowManager(100, 38, skin);
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
#if true
        // Start the 'sign in' face-finding background task.
        var signInTask = CaptureFaceAsync();

        await BiosCheckAsync();
        await LoadOsAsync();

        var signInResult = await signInTask;
        await LogInAsync(signInResult);
        
        _ = Task.Run(RunTerminal);
        WindowManager.Root.AddChild(new MatrixCanvas(WindowManager.Root.Width, WindowManager.Root.Height));
#else
        //WindowManager.Root.AddChild(new DonutCanvas(WindowManager.Root.Width, WindowManager.Root.Height));
        WindowManager.Root.AddChild(new MatrixCanvas(WindowManager.Root.Width, WindowManager.Root.Height));
#endif
    }

    private void RunTerminal()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        WindowManager.Root
            .AddChild(new TextBlock(
                "",
                $"Welcome to G33kShell v{Assembly.GetExecutingAssembly().GetName().Version}",
                "",
                $"System Uptime: {uptime.Hours:D2}h:{uptime.Minutes:D2}m:{uptime.Seconds:D2}s",
                "",
                "",
                "C:\\>_"
            ));
    }

    private static async Task<(SKBitmap Image, FaceFinder.FaceDetails Face)?> CaptureFaceAsync()
    {
        var webcamImage = new TempFile(".jpg");
        var webcamSnapResult = await WebCam.Snap(webcamImage);
        if (!webcamSnapResult)
            return null; // Webcam image not available.

        try
        {
            var faceFinder = CreateFaceFinder();
            if (faceFinder == null)
                return null; // Face finding not available.

            var face = await faceFinder.DetectFaceAsync(webcamImage);
            if (face == null)
                return null; // No face found.

            return (FaceFinder.CreateFaceBitmap(webcamImage, face), face);
        }
        finally
        {
            webcamImage.Dispose();
        }
    }

    private async Task BiosCheckAsync()
    {
        var biosText = new RevealingTextBlock(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            "",
            "-----------------------------------------------------------",
            $"G33kShell v{Assembly.GetExecutingAssembly().GetName().Version} - Initializing Terminal",
            "-----------------------------------------------------------",
            "",
            "Booting Secure System...",
            "",
            "Verifying System Integrity........... [OK]",
            "",
            "Checking Hardware Config...",
            "CPU: Z80 Dual-Core Processor......... [OK]",
            "RAM: 640KB Conventional Memory....... [OK]",
            "Disk Drive: 5.25\" Floppy............. [ACTIVE]",
            "CRT Display Scanlines................ [OPTIMAL]",
            "Modem: 56k Baud Rate................. [INITIALIZED]",
            "",
            "Securing Connection to Mainframe..... [ENCRYPTED]",
            "Authenticating User Credentials...... [PENDING]");
        WindowManager.Root
            .AddChild(new TextBlock(
                " ______    _________    ______ ",
                "|_   _ `. |  _   _  | .' ___  |",
                "  | | `. \\|_/ | | \\_|/ .'   \\_|",
                "  | |  | |    | |    | |",
                " _| |_.' /   _| |_   \\ `.___.'\\",
                "|______.'   |_____|   `.____ .'"
            )
            {
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom
            })
            .AddChild(biosText);

        await Task.Run(() => biosText.Waiter.Wait());
        await Task.Delay(TimeSpan.FromSeconds(1));
        await WindowManager.Root.ClearAsync(ClearTransition.Immediate);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task LoadOsAsync()
    {
        WindowManager.Root
            .AddChild(new Border(21, 3, Border.LineStyle.SingleHorizontalDoubleVertical)
                {
                    HorizontalAlignment = HorizontalAlignment.Right
                })
            .AddChild(new TextBlock(
                " ░░░░░░╗ ░░░░░░╗ ░░░░░░╗ ░░╗  ░░╗ ░░░░░░╗░░╗  ░░╗░░░░░░░╗░░╗     ░░╗",
                "▒▒╔════╝  ╚═══▒▒╗ ╚═══▒▒╗▒▒║ ▒▒╔╝▒▒╔════╝▒▒║  ▒▒║▒▒╔════╝▒▒║     ▒▒║",
                "▓▓║  ▓▓▓╗ ▓▓▓▓▓╔╝ ▓▓▓▓▓╔╝▓▓▓▓▓╔╝  ▓▓▓▓▓▓╗▓▓▓▓▓▓▓║▓▓▓▓▓╗  ▓▓║     ▓▓║",
                "▓▓║   ▓▓║  ╚══▓▓╗  ╚══▓▓╗▓▓╔═▓▓╗  ╚═══▓▓║▓▓╔══▓▓║▓▓╔══╝  ▓▓║     ▓▓║",
                " ▐█████╔╝▐█████╔╝▐█████╔╝██║  ██╗▐██████║██║  ██║███████╗███████╗███████╗",
                "  ╚════╝  ╚════╝  ╚════╝  ╚╝  ▐█║ ╚═════╝ ╚╝  ▐█║ ╚═════╝ ╚═════╝ ╚═════╝",
                "                               ╚╝             ▐█║",
                "                                               ╚╝")
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Y = -4
            })
            .AddChild(new ProgressBar(50, 1)
            {
                Name = "LogoProgress", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Y = 2
            })
            .AddChild(new TextBlock("Penetrating Advanced Stealth Systems...")
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Y = 3, IsFlashing = true
            })
            .AddChild(new Fire(WindowManager.Root.Width, 12)
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom
            });

        // Animate the 'Penetration' process.
        await new Animation(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                f =>
                {
                    WindowManager.Find<ProgressBar>("LogoProgress").Progress = (int)(f * 100);
                    return true;
                })
            .StartAsync();

        // Clear the screen with the Explode transition.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await WindowManager.Root.ClearAsync(ClearTransition.Explode);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    private async Task LogInAsync((SKBitmap Image, FaceFinder.FaceDetails Face)? faceAnalysis)
    {
        if (faceAnalysis == null)
            return;
        
        // 'Sign in' face analysis...
        using var faceBitmap = faceAnalysis.Value.Image;

        // Display the face.
        var faceAspect = (double)faceBitmap.Width / faceBitmap.Height;
        var width = WindowManager.Root.Height * faceAspect * 2.0; // Double width, as we show with a 8x16 font.
        var fadeInDuration = TimeSpan.FromSeconds(5);
        var faceImage = new Image((int)width, WindowManager.Root.Height, faceBitmap)
            .EnableFadeIn(fadeInDuration);
        WindowManager.Root.AddChild(faceImage);
            
        // Display message area.
        var messageArea = new Border(WindowManager.Root.Width - faceImage.Width, WindowManager.Root.Height, Border.LineStyle.Single)
        {
            HorizontalAlignment = HorizontalAlignment.Right
        };
        WindowManager.Root.AddChild(messageArea);

        // Loading...
        var loading = new TextBlock(
            "╦  ┌─┐┌─┐┌┬┐┬ ┌┐┌┌─┐   ",
            "║  │ │├─┤ │││ ││││ ┬   ",
            "╩═╝└─┘┴ ┴─┴┘┴ ┘└┘└─┘ooo")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsFlashing = true
        };
        messageArea.AddChild(loading);
        await Task.Delay(fadeInDuration);
        loading.RemoveFromParent();

        // Verifying...
        var verifying = new TextBlock(
            "╦  ╦┌─┐┬─┐┬┌─┐┬ ┬┬ ┌┐┌┌─┐   ",
            "╚╗╔╝├┤ ├┬┘│├┤ └┬┘│ ││││ ┬   ",
            " ╚╝ └─┘┴└─┴└   ┴ ┴ ┘└┘└─┘ooo")
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsFlashing = true
        };
        messageArea.AddChild(verifying);
            
        // Eye/mouth highlighting.
        var featureBoxes = new List<Border>();
        foreach (var (feature, boxWidth) in new[]
                 {
                     (faceAnalysis.Value.Face.RightEye, 15),
                     (faceAnalysis.Value.Face.LeftEye, 15),
                     (faceAnalysis.Value.Face.MouthCenter, 25)
                 }.Where(o => o.Item1 != null).Select(o => (o.Item1.Value, o.Item2)))
        {
            var x = (feature.X - faceAnalysis.Value.Face.FaceCenter.X + faceAnalysis.Value.Face.FaceWidth / 2.0) / faceAnalysis.Value.Face.FaceWidth * faceImage.Width;
            var y = (feature.Y - faceAnalysis.Value.Face.FaceCenter.Y + faceAnalysis.Value.Face.FaceHeight / 2.0) / faceAnalysis.Value.Face.FaceHeight * faceImage.Height;
            const int boxHeight = 5;
            var border = new Border(boxWidth, boxHeight, Border.LineStyle.Single)
            {
                X = (int)(x - boxWidth / 2.0),
                Y = (int)(y - boxHeight / 2.0)
            };

            using var _ = border.Screen.Lock(out var screen);
            screen.ClearChars('\0');
            featureBoxes.Add(border);
        }

        for (var i = 0; i < 8; i++)
        {
            featureBoxes.Shuffle();
            foreach (var featureBox in featureBoxes)
            {
                faceImage.AddChild(featureBox);
                await Task.Delay(100);
                faceImage.RemoveChild(featureBox);
                await Task.Delay(50);
            }
        }
            
        // "Access Granted"
        verifying.RemoveFromParent();
        messageArea.AddChild(new TextBlock(
            " ╔═╗┌─┐┌─┐┌─┐┌─┐┌─┐  ",
            " ╠═╣│  │  ├┤ └─┐└─┐  ",
            " ╩ ╩└─┘└─┘└─┘└─┘└─┘  ",
            "╔═╗┬─┐┌─┐┌┐┌┌┬┐┌─┐┌┬┐",
            "║ ╦├┬┘├─┤│││ │ ├┤  ││",
            "╚═╝┴└─┴ ┴┘└┘ ┴ └─┘─┴┘"
        )
        {
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });

        await Task.Delay(TimeSpan.FromSeconds(4));
        await WindowManager.Root.ClearAsync(ClearTransition.Explode);
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
    
    private static FaceFinder CreateFaceFinder() =>
        new FaceFinder("ra6c4l3o5d1n8d82d3f4pb0tat", "dkim5e1mjtdbniar2eliq5re4n");
}