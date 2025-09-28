using BenchmarkDotNet.Running;
using LogFunction.Benchmark;

var summary = BenchmarkRunner.Run<LoggerBenchmarks>();
