// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any non-commercial
//  purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CSharp.Core.Extensions;
using G33kShell.Desktop.Console.Controls;
using G33kShell.Desktop.Console.Extensions;
using JetBrains.Annotations;

namespace G33kShell.Desktop.Console.Screensavers;

[DebuggerDisplay("DefragCanvas:{X},{Y} {Width}x{Height}")]
[UsedImplicitly]
public class DefragCanvas : ScreensaverBase
{
    private enum Action
    {
        Reading,
        Writing,
        // ReSharper disable once InconsistentNaming
        Updating_FAT,
        Complete
    }

    private readonly List<int> m_readingBlocks = [];
    private readonly List<int> m_writingBlocks = [];
    private readonly Stopwatch m_stopwatch = Stopwatch.StartNew();
    private Action m_action;
    private int m_chunkDisplayWidth;
    private int m_chunkDisplayHeight;
    private int[] m_chunks;
    private Stopwatch m_restartStopwatch;

    public DefragCanvas(int screenWidth, int screenHeight) : base(screenWidth, screenHeight, 3)
    {
        Name = "defrag";
    }

    public override void BuildScreen(ScreenData screen)
    {
        base.BuildScreen(screen);

        InitDrive(screen);
    }

    private void InitDrive(ScreenData screen)
    {
        m_action = Action.Reading;
        m_stopwatch.Restart();
        m_restartStopwatch = null;

        m_chunkDisplayWidth = (screen.Width - 4) / 2;
        m_chunkDisplayHeight = (screen.Height - 8) / 2;
        m_chunks = new int[m_chunkDisplayWidth * m_chunkDisplayHeight - (int)(m_chunkDisplayWidth * 0.75)];

        var rand = Random.Shared;
        var unmovable = rand.Next(5, 10);
        var filled = (int)(m_chunks.Length * rand.Next(15, 25) / 100.0);
        for (var i = 0; i < m_chunks.Length; i++)
        {
            if (i < unmovable)
                m_chunks[i] = -1;
            else
                m_chunks[i] = i < filled ? 255 : rand.Next(-300, 256).Clamp(0, 255);
        }

        m_readingBlocks.Clear();
        m_writingBlocks.Clear();
    }

    public override void UpdateFrame(ScreenData screen)
    {
        switch (m_action)
        {
            case Action.Reading:
                if (m_readingBlocks.Count == 0)
                {
                    // Find some blocks to read.
                    var firstCandidate = m_chunks.TakeWhile(o => o < 0 || o == 255).Count();
                    var readableBlockIndices = m_chunks.Select((o, i) => (o, i)).Where(o => o.i >= firstCandidate && o.o > 0).Select(o => o.i);
                    var indicesToRead = readableBlockIndices.OrderBy(_ => Random.Shared.Next()).ToArray();
                    if (indicesToRead.Length == 0 || (indicesToRead.Length == 1 && indicesToRead[0] == firstCandidate))
                    {
                        m_action = Action.Complete;
                        return;
                    }
                    
                    var bytesToRead = 256;
                    foreach (var i in indicesToRead)
                    {
                        m_readingBlocks.Add(i);
                        var readCount = Math.Min(m_chunks[i], 32);
                        m_chunks[i] -= readCount;
                        bytesToRead -= readCount;
                        if (bytesToRead <= 0)
                            break;
                    }
                }
                break;
            case Action.Writing:
                if (m_writingBlocks.Count == 0)
                {
                    // Find blocks to write to.
                    var firstCandidate = m_chunks.TakeWhile(o => o < 0 || o == 255).Count();
                    var writableBlockIndices = m_chunks.Select((o, i) => (o, i)).Where(o => o.i >= firstCandidate && o.o >= 0 && o.o < 255).Select(o => o.i);
                    var bytesToWrite = m_readingBlocks.Count * 32;
                    foreach (var i in writableBlockIndices)
                    {
                        var bytesToFill = 255 - m_chunks[i];
                        if (bytesToFill > bytesToWrite)
                            bytesToFill = bytesToWrite;
                        m_chunks[i] += bytesToFill;
                        bytesToWrite -= bytesToFill;
                        m_writingBlocks.Add(i);
                        if (bytesToWrite <= 0)
                            break;
                    }
                }
                break;
            case Action.Updating_FAT:
                break;
            case Action.Complete:
                m_stopwatch.Stop();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        DrawScreen(screen);

        switch (m_action)
        {
            // Queue next action.
            case Action.Reading:
                m_action = Action.Writing;
                break;
            case Action.Writing:
                m_action = Action.Updating_FAT;
                break;
            case Action.Updating_FAT:
                m_action = Action.Reading;
                m_readingBlocks.Clear();
                m_writingBlocks.Clear();
                break;
            case Action.Complete:
                m_restartStopwatch ??= Stopwatch.StartNew();
                if (m_restartStopwatch != null && m_restartStopwatch.ElapsedMilliseconds >= 5000)
                    InitDrive(screen);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DrawScreen(ScreenData screen)
    {
        screen.Clear(Foreground, Background);
        
        // Header.
        screen.Clear(0, 0, screen.Width - 21, 1, 0.2.Lerp(Background, Foreground), Foreground);
        screen.PrintAt(2, 0, "Optimizing...");
        screen.PrintAt(screen.Width - 19, 0, "ESC=Stop Defrag");
        
        // Drive.
        screen.Clear(0, 1, screen.Width, screen.Height - 2, Foreground, 0.2.Lerp(Background, Foreground));
        for (var y = 0; y < m_chunkDisplayHeight; y++)
        {
            for (var x = 0; x < m_chunkDisplayWidth; x++)
            {
                var i = (y * m_chunkDisplayWidth) + x;
                if (i >= m_chunks.Length)
                    break;

                var xx = x * 2 + 2;
                var yy = y * 2 + 2;
                
                if (m_chunks[i] < 0)
                    screen.PrintAt(xx, yy, 'X');
                else if (m_action == Action.Reading && m_readingBlocks.Contains(i))
                    screen.PrintAt(xx, yy, 'r');
                else if (m_action == Action.Writing && m_writingBlocks.Contains(i))
                    screen.PrintAt(xx, yy, 'W');
                else if (m_chunks[i] == 0)
                    screen.PrintAt(xx, yy, '░', 0.5.Lerp(Background, Foreground));
                else if (m_chunks[i] < 200)
                    screen.PrintAt(xx, yy, '▒', 0.8.Lerp(Background, Foreground));
                else
                    screen.PrintAt(xx, yy, '◘');
            }
        }
        
        // Status.
        screen.DrawBox(4, screen.Height - 7, screen.Width / 2 - 1, screen.Height - 2, "┌┐└┘──││");
        screen.PrintAt(screen.Width / 4 - 4, screen.Height - 7, " Status ");
        var progressBar = new ProgressBar(screen.Width / 2 - 8, 1) { Progress = GetProgress() }.AsString();
        screen.PrintAt(6, screen.Height - 5, progressBar);
        screen.PrintAt(screen.Width / 2 - 6, screen.Height - 6, $"{GetProgress()}%".PadLeft(4));
        screen.PrintAt(6, screen.Height - 6, $"Cluster {GetProgress() * 692.32:N0}");
        screen.PrintAt(15, screen.Height - 4, $@"Elapsed Time: {m_stopwatch.Elapsed:hh\:mm\:ss}");
        screen.PrintAt(17, screen.Height - 3, "Full Optimization");
        
        // Legend.
        screen.DrawBox(screen.Width / 2 + 1, screen.Height - 7, screen.Width - 5, screen.Height - 2, "┌┐└┘──││");
        screen.PrintAt(screen.Width / 2 + 18, screen.Height - 7, " Legend ");
        screen.PrintAt(screen.Width / 2 + 7, screen.Height - 6, "◘ - Used          ░ - Unused/Partial");
        screen.PrintAt(screen.Width / 2 + 7, screen.Height - 5, "r - Reading       W - Writing");
        screen.PrintAt(screen.Width / 2 + 7, screen.Height - 4, "B - Bad           X - Unmovable");
        screen.PrintAt(screen.Width / 2 + 4, screen.Height - 3, "Drive C:          1 block = 69 clusters");
        
        // Footer.
        screen.Clear(0, screen.Height - 1, screen.Width, 1, 0.2.Lerp(Background, Foreground), Foreground);
        screen.PrintAt(2, screen.Height - 1, m_action.ToString().Replace('_', ' ') + "...");
        screen.PrintAt(screen.Width - 29, screen.Height - 1, "│  G33kShell Defrag Utility");
        
        // Complete?
        if (m_action == Action.Complete)
            ShowMessageBox(screen, "Finished Condensing");
    }

    private void ShowMessageBox(ScreenData screen, string s)
    {
        var w = s.Length + 6;
        const int h = 3;
        var l = (screen.Width - w) / 2;
        var t = (screen.Height - h) / 2;
        screen.Clear(l, t, w, h, Foreground, 0.75.Lerp(Background, Foreground));
        screen.PrintAt((screen.Width - s.Length) / 2, (screen.Height - 1) / 2, s, Background);

        for (var i = 0; i < w; i++)
            screen.SetColors(l + i + 2, t + h, Background, 0.5.Lerp(screen.GetBackground(l + i + 2, t + h), Background));
        for (var i = 0; i < h; i++)
        {
            screen.SetColors(l + w, t + i + 1, Background, 0.5.Lerp(screen.GetBackground(l + w, t + i + 1), Background));
            screen.SetColors(l + w + 1, t + i + 1, Background, 0.5.Lerp(screen.GetBackground(l + w + 1, t + i + 1), Background));
        }
    }

    private int GetProgress()
    {
        var done = m_chunks.Count(o => o < 0 || o == 255);
        var remaining = m_chunks.Count(o => o > 0);
        return (int)(100.0 * done / remaining).Clamp(0, 100);
    }
}