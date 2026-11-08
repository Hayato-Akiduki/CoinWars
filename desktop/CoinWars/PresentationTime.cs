using System.Diagnostics;
namespace CoinWars;

/// <summary>Monotonic presentation time shared by action waits and playback controls.</summary>
public sealed class PresentationTime
{
    readonly Func<double> now;
    double last, elapsed;
    int speed = 1;
    bool suspended;
    public PresentationTime(Func<double>? milliseconds = null)
    {
        var stopwatch = Stopwatch.StartNew();
        now = milliseconds ?? (() => stopwatch.Elapsed.TotalMilliseconds);
        last = now();
    }
    void Update()
    {
        double current = now();
        if (!suspended) elapsed += Math.Max(0, current - last) * speed;
        last = current;
    }
    public double Milliseconds { get { Update(); return elapsed; } }
    public int Speed
    {
        get => speed;
        set { if (value is not (1 or 2 or 4)) throw new ArgumentOutOfRangeException(nameof(value)); Update(); speed = value; }
    }
    public bool Suspended { get => suspended; set { Update(); suspended = value; } }
    public static bool ShouldSuspend(bool manualPause, bool dialogOpen) => manualPause || dialogOpen;
}
