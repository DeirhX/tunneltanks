namespace Tunnerer.Desktop;

using System.Globalization;

internal sealed class PerfCaptureSession
{
    private readonly PerfCaptureOptions? _options;
    private readonly List<double> _frameMs;
    private int _framesSeen;

    private PerfCaptureSession(PerfCaptureOptions? options)
    {
        _options = options;
        _frameMs = options is PerfCaptureOptions o ? new List<double>(o.MeasureFrames) : [];
    }

    public static PerfCaptureSession Create(PerfCaptureOptions? options)
    {
        if (options is not PerfCaptureOptions perf)
            return new PerfCaptureSession(null);

        var normalized = new PerfCaptureOptions(
            WarmupFrames: Math.Max(0, perf.WarmupFrames),
            MeasureFrames: Math.Max(1, perf.MeasureFrames),
            CsvPath: string.IsNullOrWhiteSpace(perf.CsvPath) ? null : perf.CsvPath);
        Console.WriteLine($"[Perf] enabled warmup={normalized.WarmupFrames} measure={normalized.MeasureFrames}");
        return new PerfCaptureSession(normalized);
    }

    public bool Capture(TimeSpan totalFrame)
    {
        if (_options is not PerfCaptureOptions perf)
            return false;

        _framesSeen++;
        if (_framesSeen > perf.WarmupFrames)
            _frameMs.Add(totalFrame.TotalMilliseconds);

        return _frameMs.Count >= perf.MeasureFrames;
    }

    public void ReportIfEnabled()
    {
        if (_options is not PerfCaptureOptions perf || _frameMs.Count == 0)
            return;

        double sum = 0;
        for (int i = 0; i < _frameMs.Count; i++)
            sum += _frameMs[i];

        double[] sorted = _frameMs.ToArray();
        Array.Sort(sorted);
        double avg = sum / _frameMs.Count;
        double min = sorted[0];
        double max = sorted[^1];
        double p50 = Percentile(sorted, 0.50);
        double p95 = Percentile(sorted, 0.95);
        double p99 = Percentile(sorted, 0.99);

        Console.WriteLine(
            $"[Perf] frames={_frameMs.Count} warmup={perf.WarmupFrames} avg={avg:F3}ms " +
            $"p50={p50:F3}ms p95={p95:F3}ms p99={p99:F3}ms min={min:F3}ms max={max:F3}ms");

        if (perf.CsvPath is null)
            return;

        string fullPath = Path.GetFullPath(perf.CsvPath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var writer = new StreamWriter(fullPath, false);
        writer.WriteLine("frame,ms");
        for (int i = 0; i < _frameMs.Count; i++)
            writer.WriteLine($"{i},{_frameMs[i].ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"[Perf] wrote csv: {fullPath}");
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0)
            return 0;
        int idx = (int)Math.Ceiling(sorted.Length * p) - 1;
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }
}

public readonly record struct PerfCaptureOptions(
    int WarmupFrames,
    int MeasureFrames,
    string? CsvPath);
