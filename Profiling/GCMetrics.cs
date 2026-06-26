using System;
using System.Diagnostics;

namespace TerraMap.Profiling;

public class GcMetrics
{
    private long _lastAllocatedBytes = 0;
    private double _allocatedPerSecond = 0;
    private Stopwatch _timer = Stopwatch.StartNew();

    // Public properties your UI can read every frame
    public double TotalMemoryMb { get; private set; }
    public double AllocRateMbPerSec => _allocatedPerSecond;
    public int Gen0Collections { get; private set; }
    public int Gen1Collections { get; private set; }
    public int Gen2Collections { get; private set; }
    public double GcPausePercentage { get; private set; }

    public void Update()
    {
        long totalBytes = GC.GetTotalMemory(false);
        TotalMemoryMb = totalBytes / (1024.0 * 1024.0);

        Gen0Collections = GC.CollectionCount(0);
        Gen1Collections = GC.CollectionCount(1);
        Gen2Collections = GC.CollectionCount(2);

        double elapsedSeconds = _timer.Elapsed.TotalSeconds;
        if (elapsedSeconds >= 0.5) // Update rate every half second to smooth out spikes
        {
            long currentTotalAllocated = GC.GetTotalAllocatedBytes(false);
            long diff = currentTotalAllocated - _lastAllocatedBytes;
            
            // If a GC happened, diff might be negative or weird depending on timing, 
            // but GetTotalAllocatedBytes is monotonically increasing for the thread/process context.
            _allocatedPerSecond = (diff / (1024.0 * 1024.0)) / elapsedSeconds;

            _lastAllocatedBytes = currentTotalAllocated;
            _timer.Restart();
        }

        GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
        if (gcInfo.PauseTimePercentage > 0)
        {
            GcPausePercentage = gcInfo.PauseTimePercentage;
        }
    }
}