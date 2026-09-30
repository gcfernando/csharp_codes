using System;
using System.Diagnostics;
using Spectrum.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace Spectrum.Tests;

/// <summary>
/// Per-frame cost and allocation of the analysis path. Timing is reported, not asserted tightly
/// (machine dependent); allocation-freedom of the steady-state hot path is asserted.
/// </summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _out;

    public PerformanceTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AnalysisFrame_IsAllocationFreeAndCheapRelativeToTheHop()
    {
        AppDomain.MonitoringIsEnabled = true;

        var engine = Signals.DefaultEngine();
        var history = new SampleHistory(2, engine.RequiredFrames * 4);
        var chunk = Signals.StereoSine(480, 48000, 1000, 0.5, 0.5);
        var snapshot = new float[engine.RequiredFrames * 2];
        var power = new double[engine.Plan.Count];
        var bytes = new byte[engine.Plan.Count];

        void Frame()
        {
            history.Write(chunk, 480);
            history.Write(chunk, 480);
            Assert.True(history.TryCopyLatest(snapshot, engine.RequiredFrames, out _));
            engine.Analyze(snapshot, engine.RequiredFrames, power);
            for (var i = 0; i < power.Length; i++)
            {
                bytes[i] = LevelScale.ToDisplayByte(LevelScale.PowerToDb(power[i]));
            }
        }

        for (var i = 0; i < 50; i++)
        {
            Frame(); // warm-up / JIT
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var gen0 = GC.CollectionCount(0);
        var allocBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;

        const int frames = 400;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < frames; i++)
        {
            Frame();
        }

        sw.Stop();
        var allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocBefore;
        var msPerFrame = sw.Elapsed.TotalMilliseconds / frames;

        _out.WriteLine($"stereo 83-band frame (windows 4096+8192+16384, longest zero-padded to 32768): {msPerFrame:F3} ms/frame = {msPerFrame / 31.7 * 100:F1} % of one core at the measured 31.7 ms hop");
        _out.WriteLine($"allocated {allocated} bytes over {frames} frames (AppDomain-wide, includes test-runner noise); gen0 collections {GC.CollectionCount(0) - gen0}");

        // AppDomain-wide counter: allow a little background noise from the test host, far below one array allocation per frame.
        Assert.True(allocated / (double)frames < 64, $"{allocated / (double)frames:F1} bytes/frame allocated");
        Assert.True(msPerFrame < 12.5, "analysis must use well under the analysis hop");
    }
}
