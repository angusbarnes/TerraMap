using System;

public class FrameMetrics
{
    private readonly double[] _frameTimeBuffer;
    private int _bufferIndex = 0;
    private int _totalRecordedFrames = 0;
    
    public double AverageFrameTimeMs { get; private set; }
    public double WorstFrameTimeMs { get; private set; }
    public double AverageFps { get; private set; }

    /// <param name="windowSize">The number of frames (N) to track over a rolling window.</param>
    public FrameMetrics(int windowSize = 60)
    {
        if (windowSize <= 0) windowSize = 60;
        _frameTimeBuffer = new double[windowSize];
    }

    /// <summary>
    /// Update metrics. Call this at the very end of your Game.Draw() loop.
    /// </summary>
    public void Update(TimeSpan elapsedGameTime)
    {
        // Convert elapsed time directly to total milliseconds
        double currentFrameTime = elapsedGameTime.TotalMilliseconds;

        // Push into the ring buffer
        _frameTimeBuffer[_bufferIndex] = currentFrameTime;
        _bufferIndex = (_bufferIndex + 1) % _frameTimeBuffer.Length;
        
        if (_totalRecordedFrames < _frameTimeBuffer.Length)
        {
            _totalRecordedFrames++;
        }

        // Calculate Average and Worst metrics from the active window
        double totalSum = 0;
        double highestTime = 0;

        for (int i = 0; i < _totalRecordedFrames; i++)
        {
            double t = _frameTimeBuffer[i];
            totalSum += t;
            if (t > highestTime) highestTime = t;
        }

        AverageFrameTimeMs = totalSum / _totalRecordedFrames;
        WorstFrameTimeMs = highestTime;

        // Calculate FPS safely to prevent Division By Zero errors
        AverageFps = AverageFrameTimeMs > 0 ? 1000.0 / AverageFrameTimeMs : 0;
    }
}