using System.Runtime.CompilerServices;

namespace LogFunction.Logger;
/// <summary>
/// Exposes metrics for <see cref="BatchLogger"/>.
/// Designed for integration with Prometheus or OpenTelemetry.
/// </summary>
public sealed class BatchLoggerMetrics
{
    private long _dropped;
    private long _batches;
    private long _flushed;

    public long DroppedCount => Interlocked.Read(ref _dropped);
    public long BatchCount => Interlocked.Read(ref _batches);
    public long TotalFlushed => Interlocked.Read(ref _flushed);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void IncrementDropped() => Interlocked.Increment(ref _dropped);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddFlushed(int count)
    {
        Interlocked.Add(ref _flushed, count);
        Interlocked.Increment(ref _batches);
    }
}
