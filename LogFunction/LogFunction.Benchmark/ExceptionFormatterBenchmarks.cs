using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using LogFunction.Logger;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogFunction.Benchmark;

[MemoryDiagnoser(displayGenColumns: true)]
public class ExceptionFormatterBenchmarks
{
    private readonly Exception _ex = new InvalidOperationException("Test exception");

    [GlobalSetup]
    public void Setup()
    {
        ExLogger.ExceptionFormatter = (ex, title, detailed) =>
        {
            return JsonSerializer.Serialize(new
            {
                Time = DateTime.UtcNow,
                Title = title,
                Type = ex.GetType().Name,
                ErrorMessage = ex.Message,
                Detailed = detailed
            });
        };
    }

    [Benchmark]
    public void Format_CustomJson() =>
        ExLogger.ExLogErrorException(NullLogger.Instance, _ex, "Json formatted error", moreDetailsEnabled: true);
}