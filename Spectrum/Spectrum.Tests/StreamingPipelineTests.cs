using System;
using Spectrum.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace Spectrum.Tests;

/// <summary>
/// The capture → history → snapshot → analysis chain as the application runs it: a continuous signal delivered in
/// irregular callback-sized chunks, analysed at the measured ~31.7 ms cadence. A steady tone must stay a steady,
/// narrow reading; any broadband "skirt" would mean the chain introduced discontinuities.
/// </summary>
public class StreamingPipelineTests
{
    private readonly ITestOutputHelper _out;

    public StreamingPipelineTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void SteadyTone_FedInIrregularChunks_ProducesNoSkirt()
    {
        const int fs = 48000;
        const int channels = 2;
        const double amp = 0.1; // −20 dBFS
        var engine = Signals.DefaultEngine(fs, channels);
        const int band = 46;
        var hz = engine.Plan[band].CenterHz;
        var history = new SampleHistory(channels, engine.RequiredFrames * 4);
        var snapshot = new float[engine.RequiredFrames * channels];
        var power = new double[engine.Plan.Count];
        var rng = new Random(42);

        long written = 0;
        long nextAnalysis = engine.RequiredFrames * 2L;
        var chunk = new float[800 * channels];
        double worstNeighbour = double.NegativeInfinity, worstFar = double.NegativeInfinity;
        double minTone = double.PositiveInfinity, maxTone = double.NegativeInfinity;
        var analyses = 0;

        while (written < fs * 10L)
        {
            var frames = rng.Next(400, 701); // callback sizes seen with a 10 ms WASAPI period
            for (var i = 0; i < frames; i++)
            {
                var s = (float)(amp * Math.Sin(2 * Math.PI * hz * (written + i) / fs));
                chunk[i * channels] = s;
                chunk[(i * channels) + 1] = s;
            }

            history.Write(chunk, frames);
            written += frames;

            if (written < nextAnalysis)
            {
                continue;
            }

            nextAnalysis += 1520 + rng.Next(-60, 61); // ~31.7 ms tick with jitter
            Assert.True(history.TryCopyLatest(snapshot, engine.RequiredFrames, out _));
            engine.Analyze(snapshot, engine.RequiredFrames, power);
            analyses++;

            var tone = LevelScale.PowerToDb(power[band]);
            minTone = Math.Min(minTone, tone);
            maxTone = Math.Max(maxTone, tone);
            for (var b = 0; b < power.Length; b++)
            {
                var db = LevelScale.PowerToDb(power[b]);
                if (Math.Abs(b - band) == 1)
                {
                    worstNeighbour = Math.Max(worstNeighbour, db);
                }
                else if (Math.Abs(b - band) > 1)
                {
                    worstFar = Math.Max(worstFar, db);
                }
            }
        }

        _out.WriteLine($"{analyses} analyses: tone {minTone:F3}..{maxTone:F3} dB, worst neighbour {worstNeighbour:F1} dB, worst other bar {worstFar:F1} dB");
        Assert.InRange(minTone, -20.05, -19.95);
        Assert.InRange(maxTone, -20.05, -19.95);
        Assert.True(worstNeighbour < -60, "adjacent bars must show only window leakage");
        Assert.True(worstFar < LevelScale.FloorDb, "no other bar may rise above the display floor");
    }

    [Fact]
    public void SingleDroppedPacket_ProducesTheObservedSkirt()
    {
        // Reference for diagnosing live reports: what one missing 480-frame packet inside the window looks like.
        const int fs = 48000;
        var engine = Signals.DefaultEngine(fs, 2);
        const int band = 46;
        var hz = engine.Plan[band].CenterHz;
        var n = engine.RequiredFrames;
        const int dropAt = 16384 - 2000; // inside the shortest (4096) window
        var buf = Signals.Interleaved(n, 2, (_, i) => 0.1 * Math.Sin(2 * Math.PI * hz * (i < dropAt ? i : i + 480) / fs));
        var db = Signals.AnalyzeDb(engine, buf);
        var far = 0;
        for (var b = 0; b < db.Length; b++)
        {
            if (Math.Abs(b - band) > 1 && db[b] > LevelScale.FloorDb)
            {
                far++;
            }
        }

        _out.WriteLine($"dropped packet: band {band} {db[band]:F1} dB, neighbours {db[band - 1]:F1}/{db[band + 1]:F1} dB, {far} other bars above the floor");
        Assert.True(far > 10, "a discontinuity must visibly splatter; this documents the failure signature");
    }
}
