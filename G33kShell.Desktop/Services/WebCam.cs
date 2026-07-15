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
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;

namespace G33kShell.Desktop.Services;

/// <summary>
/// Captures still images from the default webcam.
/// </summary>
/// <remarks>
/// This helper keeps G33kShell's OpenCV dependency local to the application that uses it.
/// </remarks>
internal static class WebCam
{
    /// <summary>
    /// Attempts to capture a non-black frame and save it to the target file.
    /// </summary>
    /// <param name="targetFile">The destination image file.</param>
    /// <returns><see langword="true"/> when an image was captured; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> Snap(FileInfo targetFile)
    {
        using var videoCapture = new VideoCapture(0);
        videoCapture.Set(VideoCaptureProperties.FrameWidth, 640);
        videoCapture.Set(VideoCaptureProperties.FrameHeight, 480);

        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromSeconds(2);

        using var webcamFrame = new Mat();
        while (DateTime.Now - startTime < timeout)
        {
            if (videoCapture.Read(webcamFrame) && !webcamFrame.Empty())
            {
                var channels = webcamFrame.Split();
                try
                {
                    if (Cv2.CountNonZero(channels[0]) > 0)
                    {
                        Cv2.ImWrite(targetFile.FullName, webcamFrame);
                        return true;
                    }
                }
                finally
                {
                    foreach (var channel in channels)
                        channel.Dispose();
                }
            }

            await Task.Delay(100);
        }

        return false;
    }
}
