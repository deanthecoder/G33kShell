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
using System.Diagnostics;
using System.Text;
using Avalonia.Threading;
using DTC.Core;
using DTC.Core.Extensions;

namespace G33kShell.Desktop.Diagnostics;

/// <summary>
/// Periodically records process and managed-heap details when memory use crosses configured thresholds.
/// </summary>
/// <remarks>
/// Comparing private bytes with managed heap sizes distinguishes retained .NET objects from native rendering or
/// operating-system allocations without perturbing the process with a forced garbage collection.
/// </remarks>
public sealed class MemoryMonitor : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(15);
    private const long BytesPerMegabyte = 1024 * 1024;

    private readonly Func<string> m_appDetails;
    private readonly DispatcherTimer m_timer;
    private long m_thresholdBytes;
    private long m_nextReportAtBytes;
    private bool m_hasFailed;

    public MemoryMonitor(int thresholdMegabytes, Func<string> appDetails)
    {
        m_appDetails = appDetails;
        m_timer = new DispatcherTimer { Interval = SampleInterval };
        m_timer.Tick += OnTimerTick;
        m_thresholdBytes = Math.Max(1, thresholdMegabytes) * BytesPerMegabyte;
        m_nextReportAtBytes = m_thresholdBytes;
    }

    public void Start()
    {
        var log = Logger.Instance;
        log.Info($"Memory monitor enabled: samples every {SampleInterval.TotalSeconds:N0} seconds, " +
                 $"reports every {m_thresholdBytes / BytesPerMegabyte:N0} MB. Log: {log.File.FullName}");
        LogSnapshot("Startup memory snapshot", CaptureSnapshot(), false);
        m_timer.Start();
    }

    public void Dispose()
    {
        m_timer.Stop();
        m_timer.Tick -= OnTimerTick;
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        try
        {
            var snapshot = CaptureSnapshot();
            var measuredBytes = Math.Max(snapshot.WorkingSetBytes, snapshot.PrivateBytes);
            if (measuredBytes < m_nextReportAtBytes)
                return;

            var crossedThreshold = m_nextReportAtBytes;
            while (m_nextReportAtBytes <= measuredBytes)
                m_nextReportAtBytes += m_thresholdBytes;

            LogSnapshot($"Memory threshold crossed ({crossedThreshold.ToSize()})", snapshot, true);
        }
        catch (Exception ex)
        {
            if (m_hasFailed)
                return;

            m_hasFailed = true;
            Logger.Instance.Exception("Memory monitor failed; further samples are disabled.", ex);
            m_timer.Stop();
        }
    }

    private MemorySnapshot CaptureSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        var gcInfo = GC.GetGCMemoryInfo();
        return new MemorySnapshot(
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.VirtualMemorySize64,
            GC.GetTotalMemory(false),
            gcInfo.HeapSizeBytes,
            gcInfo.TotalCommittedBytes,
            gcInfo.FragmentedBytes,
            gcInfo.MemoryLoadBytes,
            gcInfo.HighMemoryLoadThresholdBytes,
            GC.GetTotalAllocatedBytes(false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            gcInfo.PinnedObjectsCount,
            gcInfo.FinalizationPendingCount,
            process.Threads.Count,
            GetHandleCount(process),
            FormatGenerations(gcInfo),
            GetAppDetails());
    }

    private string GetAppDetails()
    {
        try
        {
            return m_appDetails?.Invoke() ?? "Unavailable";
        }
        catch (Exception ex)
        {
            return $"Unavailable ({ex.GetType().Name}: {ex.Message})";
        }
    }

    private static int? GetHandleCount(Process process)
    {
        try
        {
            return process.HandleCount;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatGenerations(GCMemoryInfo gcInfo)
    {
        string[] names = ["Gen0", "Gen1", "Gen2", "LOH", "POH"];
        var details = new List<string>();
        for (var i = 0; i < Math.Min(names.Length, gcInfo.GenerationInfo.Length); i++)
        {
            var generation = gcInfo.GenerationInfo[i];
            details.Add($"{names[i]}={generation.SizeAfterBytes.ToSize()} " +
                        $"(fragmented {generation.FragmentationAfterBytes.ToSize()})");
        }

        return string.Join(", ", details);
    }

    private static void LogSnapshot(string reason, MemorySnapshot snapshot, bool isWarning)
    {
        var report = new StringBuilder(reason)
            .AppendLine()
            .Append("Process: working set ").Append(snapshot.WorkingSetBytes.ToSize())
            .Append(", private ").Append(snapshot.PrivateBytes.ToSize())
            .Append(", virtual ").Append(snapshot.VirtualBytes.ToSize())
            .Append(", threads ").Append(snapshot.ThreadCount)
            .Append(", handles ").Append(snapshot.HandleCount?.ToString("N0") ?? "unavailable")
            .AppendLine()
            .Append("Managed: live ").Append(snapshot.ManagedLiveBytes.ToSize())
            .Append(", heap ").Append(snapshot.ManagedHeapBytes.ToSize())
            .Append(", committed ").Append(snapshot.ManagedCommittedBytes.ToSize())
            .Append(", fragmented ").Append(snapshot.ManagedFragmentedBytes.ToSize())
            .Append(", allocated since startup ").Append(snapshot.TotalAllocatedBytes.ToSize())
            .AppendLine()
            .Append("GC: collections ").Append(snapshot.Gen0Collections).Append('/')
            .Append(snapshot.Gen1Collections).Append('/').Append(snapshot.Gen2Collections)
            .Append(", pinned ").Append(snapshot.PinnedObjectCount)
            .Append(", finalization pending ").Append(snapshot.FinalizationPendingCount)
            .AppendLine()
            .Append("GC generations: ").Append(snapshot.GenerationDetails)
            .AppendLine()
            .Append("System memory load: ").Append(snapshot.MemoryLoadBytes.ToSize())
            .Append(" of ").Append(snapshot.HighMemoryLoadThresholdBytes.ToSize())
            .AppendLine()
            .Append("G33kShell: ").Append(snapshot.AppDetails);

        if (isWarning)
            Logger.Instance.Warn(report.ToString());
        else
            Logger.Instance.Info(report.ToString());
    }

    private sealed record MemorySnapshot(
        long WorkingSetBytes,
        long PrivateBytes,
        long VirtualBytes,
        long ManagedLiveBytes,
        long ManagedHeapBytes,
        long ManagedCommittedBytes,
        long ManagedFragmentedBytes,
        long MemoryLoadBytes,
        long HighMemoryLoadThresholdBytes,
        long TotalAllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections,
        long PinnedObjectCount,
        long FinalizationPendingCount,
        int ThreadCount,
        int? HandleCount,
        string GenerationDetails,
        string AppDetails);
}
