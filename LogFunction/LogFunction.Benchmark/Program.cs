using BenchmarkDotNet.Running;
using LogFunction.Benchmark;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
