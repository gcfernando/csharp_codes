using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;

namespace LogFunction.Benchmark;

[MemoryDiagnoser(displayGenColumns: true)]
public class AsyncBenchmarks
{
    [Benchmark]
    public Task FlushAsync_NoToken() => ExLogger.FlushAsync();

    [Benchmark]
    public async Task FlushAsync_WithCancellation()
    {
        using var cts = new CancellationTokenSource();
        await ExLogger.FlushAsync(cts.Token);
    }
}