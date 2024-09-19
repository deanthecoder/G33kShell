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

    public MainWindowViewModel(SkinBase skin)
    {
        WindowManager = new WindowManager(100, 38, skin);
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        // Start the 'sign in' face-finding background task.
        var signInTask = CaptureFace();

        await BiosCheck();
        await LoadOs();
        await LogIn(signInTask);
    }
    
    private static async Task<(SKBitmap Image, FaceFinder.FaceDetails Face)?> CaptureFace()
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

    private async Task BiosCheck()
    {
        WindowManager.Root
            .AddChild(
                new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom
                    }
                    .Init(32, 10, Border.LineStyle.Block)
                    .AddChild(new TextBlock()
                        .Init(
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░",
                            "░───────░░░────────░░░░─────░░",
                            "░──░░░░──░░░░░──░░░░░░──░░░──░",
                            "░──░░░░──░░░░░──░░░░░──░░░░░░░",
                            "░──░░░░──░░░░░──░░░░░──░░░░░░░",
                            "░──░░░░──░░░░░──░░░░░░──░░░──░",
                            "░───────░░░░░░──░░░░░░░─────░░",
                            "░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░")))
            .AddChild(
                new TextBlock()
                    .Init(
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
                        "Authenticating User Credentials...... [PENDING]")
                    );
        await Task.Delay(TimeSpan.FromSeconds(4));
        await WindowManager.Root.ClearAsync(ClearTransition.Immediate);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task LoadOs()
    {
        WindowManager.Root
            .AddChild(new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right // todo - Support shadow (slim/░/█)
                }.Init(21, 3, Border.LineStyle.SingleHorizontalDoubleVertical)
                .AddChild(new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }.Init("By DeanTheCoder")))
            .AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Y = -4
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
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Y = 2
            }.Init(50, 1))
            .AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Y = 3, IsFlashing = true
            }.Init("Penetrating Advanced Stealth Systems..."))
            .AddChild(new Fire
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            }.Init(WindowManager.Root.Width, 12));

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
        
    private async Task LogIn(Task<(SKBitmap Image, FaceFinder.FaceDetails Face)?> signInTask)
    {
        // 'Sign in' face analysis...
        var faceAnalysis = await signInTask;
        if (faceAnalysis != null)
        {
            using var faceBitmap = faceAnalysis.Value.Image;

            // Display the face.
            var faceAspect = (double)faceBitmap.Width / faceBitmap.Height;
            var width = WindowManager.Root.Height * faceAspect * 2.0; // Double width, as we show with a 8x16 font.
            var fadeInDuration = TimeSpan.FromSeconds(5);
            var faceImage = new Image()
                .Init((int)width, WindowManager.Root.Height, faceBitmap)
                .EnableFadeIn(fadeInDuration);
            WindowManager.Root.AddChild(faceImage);
            
            // Display message area.
            var messageArea = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right
            }.Init(WindowManager.Root.Width - faceImage.Width, WindowManager.Root.Height, Border.LineStyle.Single);
            WindowManager.Root.AddChild(messageArea);

            // Loading...
            var loading = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsFlashing = true
                }
                .Init(
                    "╦  ┌─┐┌─┐┌┬┐┬ ┌┐┌┌─┐   ",
                    "║  │ │├─┤ │││ ││││ ┬   ",
                    "╩═╝└─┘┴ ┴─┴┘┴ ┘└┘└─┘ooo");
            messageArea.AddChild(loading);
            await Task.Delay(fadeInDuration);
            loading.RemoveFromParent();

            // Verifying...
            var verifying = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsFlashing = true
                }
                .Init(
                    "╦  ╦┌─┐┬─┐┬┌─┐┬ ┬┬ ┌┐┌┌─┐   ",
                    "╚╗╔╝├┤ ├┬┘│├┤ └┬┘│ ││││ ┬   ",
                    " ╚╝ └─┘┴└─┴└   ┴ ┴ ┘└┘└─┘ooo");
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
                var boxHeight = 5;
                var border = new Border
                {
                    X = (int)(x - boxWidth / 2.0),
                    Y = (int)(y - boxHeight / 2.0)
                };

                var featureBox = border.Init(boxWidth, boxHeight, Border.LineStyle.Single);
                featureBox.Screen.ClearChars('\0');
                featureBoxes.Add(featureBox);
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
            messageArea.AddChild(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }.Init(
                " ╔═╗┌─┐┌─┐┌─┐┌─┐┌─┐  ",
                " ╠═╣│  │  ├┤ └─┐└─┐  ",
                " ╩ ╩└─┘└─┘└─┘└─┘└─┘  ",
                "╔═╗┬─┐┌─┐┌┐┌┌┬┐┌─┐┌┬┐",
                "║ ╦├┬┘├─┤│││ │ ├┤  ││",
                "╚═╝┴└─┴ ┴┘└┘ ┴ └─┘─┴┘"));

            await Task.Delay(TimeSpan.FromSeconds(4));
            await WindowManager.Root.ClearAsync(ClearTransition.Explode);
        }
    }
    
    private static FaceFinder CreateFaceFinder() =>
}