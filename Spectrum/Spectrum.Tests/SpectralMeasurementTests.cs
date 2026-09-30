using System;
using System.Linq;
using Spectrum.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace Spectrum.Tests;

/// <summary>Synthetic-signal validation of the measurement: tones, amplitude, boundaries, sweeps, multi-tone, noise.</summary>
public class SpectralMeasurementTests
{
    private readonly ITestOutputHelper _out;

    public SpectralMeasurementTests(ITestOutputHelper output) => _out = output;

    // ---------- Single tones / dead bars ----------

    [Theory]
    [InlineData(83, 44100)]
    [InlineData(83, 48000)]
    [InlineData(83, 96000)]
    [InlineData(31, 48000)]
    [InlineData(120, 48000)]
    public void EveryBar_IsTheStrongestBarForAToneAtItsCentre(int count, int fs)
    {
        var engine = new BandSpectrumAnalyzer(BandPlan.CreateLogarithmic(count, 20, 20000, fs), 2);
        var worst = 0.0;
        for (var b = 0; b < count; b++)
        {
            var db = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, fs, engine.Plan[b].CenterHz, 1, 1));

            // The bar for the tone's frequency is the strongest bar: no dead or misplaced bars.
            Assert.Equal(b, Signals.ArgMax(db));

            if (Signals.IsResolved(engine.GetDiagnostic(b)))
            {
                // Physically resolvable band (window main lobe fits inside): the whole tone is captured.
                Assert.InRange(db[b], -0.05, 0.05);
            }
            else
            {
                // Band narrower than the main lobe: the tone is shared with neighbours (documented limitation).
                Assert.InRange(db[b], -8.0, 0.05);
            }

            worst = Math.Min(worst, db[b]);
        }

        _out.WriteLine($"count={count} fs={fs}: lowest own-band reading {worst:F2} dB");
    }

    [Theory]
    [InlineData(1000.0)]
    [InlineData(3150.0)]
    [InlineData(10000.0)]
    public void Amplitude_IsAccurateOnAndOffBinCentre(double nominalHz)
    {
        var engine = Signals.DefaultEngine();
        var band = Enumerable.Range(0, engine.Plan.Count).First(i => engine.Plan[i].UpperHz > nominalHz);
        var d = engine.GetDiagnostic(band);
        Assert.True(d.WidthHz() / d.ResolutionHz >= 6, "test needs a band well wider than the main lobe");
        var centreBin = Math.Round(engine.Plan[band].CenterHz / d.BinWidthHz);

        foreach (var offset in new[] { 0.0, 0.25, 0.5 })
        {
            foreach (var amp in new[] { 1.0, 0.1, 0.001 })
            {
                var hz = (centreBin + offset) * d.BinWidthHz;
                var db = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 48000, hz, amp, amp));
                var expected = 20 * Math.Log10(amp);

                // Integrated band power is scallop-free: on-bin and half-bin tones read the same.
                Assert.InRange(db[band] - expected, -0.05, 0.05);
            }
        }
    }

    [Fact]
    public void ToneOnBandBoundary_SplitsEnergyBetweenTheTwoBandsWithoutLoss()
    {
        var engine = Signals.DefaultEngine();
        const int b = 55; // ~2 kHz, resolved region
        var edge = engine.Plan[b].UpperHz;

        var onEdge = Signals.AnalyzePower(engine, Signals.StereoSine(engine.RequiredFrames, 48000, edge, 1, 1));
        Assert.InRange(LevelScale.PowerToDb(onEdge[b]), -3.5, -2.5);
        Assert.InRange(LevelScale.PowerToDb(onEdge[b + 1]), -3.5, -2.5);
        Assert.InRange(Signals.SumDb(onEdge, b, b + 1), -0.05, 0.05);

        var below = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 48000, edge * 0.98, 1, 1));
        var above = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 48000, edge * 1.02, 1, 1));
        Assert.Equal(b, Signals.ArgMax(below));
        Assert.Equal(b + 1, Signals.ArgMax(above));

        // No unexplained distant activity: bands further than one away stay far below the tone.
        for (var i = 0; i < engine.Plan.Count; i++)
        {
            if (Math.Abs(i - b) > 2)
            {
                Assert.True(LevelScale.PowerToDb(onEdge[i]) < -40, $"band {i} = {LevelScale.PowerToDb(onEdge[i]):F1} dB");
            }
        }
    }

    [Fact]
    public void TonesAtTheEdgesOfTheRange_LandInTheOuterBars()
    {
        var engine = Signals.DefaultEngine(44100);
        var low = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 44100, 19.5, 1, 1));
        var high = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 44100, 20000, 1, 1));
        Assert.Equal(0, Signals.ArgMax(low));
        Assert.Equal(82, Signals.ArgMax(high));
        Assert.InRange(high[82], -0.05, 0.05);
    }

    [Fact]
    public void ContentAboveTheTopBand_CreatesNoFakeActivity()
    {
        // 21.8 kHz at 44.1 kHz: above the plan (top edge 20.86 kHz) but below Nyquist.
        var engine = Signals.DefaultEngine(44100);
        var db = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 44100, 21800, 1, 1));
        Assert.All(db, v => Assert.True(v < LevelScale.FloorDb, $"{v:F1} dB"));
    }

    // ---------- Sweeps ----------

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    public void SteppedLogSweep_MovesMonotonicallyAndNeverLosesEnergy(int fs)
    {
        var engine = Signals.DefaultEngine(fs);
        var previous = 0;
        var maxLoss = 0.0;
        var worstOffset = 0.0;
        var maxTransition = 0.0;
        const int steps = 700;
        for (var s = 0; s < steps; s++)
        {
            var hz = 22 * Math.Pow(19500.0 / 22, s / (steps - 1.0));
            var p = Signals.AnalyzePower(engine, Signals.StereoSine(engine.RequiredFrames, fs, hz, 1, 1));
            var argmax = Signals.ArgMax(p);

            Assert.True(argmax >= previous, $"{hz:F1} Hz: bar went back from {previous} to {argmax}");
            Assert.True(argmax - previous <= 1, $"{hz:F1} Hz: bar skipped from {previous} to {argmax}");
            previous = argmax;

            // The strongest bar contains the tone, or (below the resolution limit) is within half a resolution
            // bin of it: the lobe peak is located to the interpolated-bin grid.
            var band = engine.Plan[argmax];
            var d = engine.GetDiagnostic(argmax);
            var offset = hz < band.LowerHz ? band.LowerHz - hz : (hz > band.UpperHz ? hz - band.UpperHz : 0.0);
            worstOffset = Math.Max(worstOffset, offset / d.ResolutionHz);
            Assert.True(offset <= 0.5 * d.ResolutionHz, $"{hz:F2} Hz shown in bar {argmax} [{band.LowerHz:F2}, {band.UpperHz:F2}]");

            // Energy conservation across the whole partition: the tone's power is found somewhere, never lost.
            // Checked where the main lobe (±2 resolution bins) lies inside the plan; energy below the first
            // band's lower edge (19.2 Hz) is intentionally not displayed.
            if (hz - (2 * engine.GetDiagnostic(0).ResolutionHz) >= engine.Plan.MinHz)
            {
                var total = Signals.SumDb(p, 0, p.Length - 1);
                if (NearResolutionTransition(engine, hz))
                {
                    // Documented multi-resolution limit: within two coarse bins of a window-length switch the coarser
                    // window's wider lobe crosses the edge into a band measured with the finer window, so the total is off by up to ~0.5 dB.
                    maxTransition = Math.Max(maxTransition, Math.Abs(total));
                    Assert.True(Math.Abs(total) <= 0.6, $"{hz:F2} Hz (transition): total band power {total:F3} dB");
                }
                else
                {
                    maxLoss = Math.Max(maxLoss, Math.Abs(total));
                    Assert.True(Math.Abs(total) <= 0.1, $"{hz:F2} Hz: total band power {total:F3} dB");
                }
            }
        }

        Assert.Equal(engine.Plan.Count - 1, previous);
        _out.WriteLine($"fs={fs}: max |total band power| deviation {maxLoss:F4} dB over {steps} steps " +
                       $"(max |{maxTransition:F3}| dB next to a window-length switch); worst peak-bar offset {worstOffset:F2} resolution bins");
    }

    private static bool NearResolutionTransition(BandSpectrumAnalyzer engine, double hz)
    {
        for (var b = 0; b + 1 < engine.Plan.Count; b++)
        {
            if (engine.GetWindowLength(b) != engine.GetWindowLength(b + 1))
            {
                var coarse = Math.Max(engine.GetDiagnostic(b).ResolutionHz, engine.GetDiagnostic(b + 1).ResolutionHz);
                if (Math.Abs(hz - engine.Plan[b].UpperHz) < 2 * coarse)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public void ContinuousLogChirp_TravelsThroughAdjacentBarsWithoutSkippingOrSticking()
    {
        const int fs = 48000;
        const double f0 = 20, f1 = 20000, duration = 12.0;
        const int hop = 1520; // 31.7 ms: measured analysis cadence of the application
        var engine = Signals.DefaultEngine(fs);
        var k = Math.Log(f1 / f0);

        double Phase(long n)
        {
            var t = Math.Max(0, n / (double)fs);
            return 2 * Math.PI * f0 * duration / k * (Math.Exp(k * t / duration) - 1);
        }

        var visited = new bool[engine.Plan.Count];
        var previous = -1;
        var frames = (int)(duration * fs / hop);
        for (var f = 0; f <= frames; f++)
        {
            var end = (long)f * hop;
            var start = end - engine.RequiredFrames;
            var buf = Signals.Interleaved(engine.RequiredFrames, 2, (_, i) => start + i < 0 ? 0 : Math.Sin(Phase(start + i)));
            var p = Signals.AnalyzePower(engine, buf);
            if (end < engine.RequiredFrames)
            {
                continue; // window not yet full of chirp
            }

            var argmax = Signals.ArgMax(p);
            visited[argmax] = true;
            if (previous >= 0)
            {
                Assert.True(argmax >= previous, $"t={end / (double)fs:F3}s: bar went back {previous}→{argmax}");
                Assert.True(argmax - previous <= 1, $"t={end / (double)fs:F3}s: skipped {previous}→{argmax}");
            }

            previous = argmax;
        }

        // Every bar above the first window-length of the sweep is visited (no dead zones).
        var firstReachable = visited.ToList().IndexOf(true);
        for (var b = firstReachable; b < engine.Plan.Count; b++)
        {
            Assert.True(visited[b], $"bar {b} never became the strongest bar during the sweep");
        }

        _out.WriteLine($"chirp visited bars {firstReachable}..{engine.Plan.Count - 1} contiguously");
    }

    // ---------- Multi-tone ----------

    [Fact]
    public void MultipleTones_AreMeasuredIndependently()
    {
        var engine = Signals.DefaultEngine();
        var tones = new[] { (hz: 50.5, db: -6.0), (hz: 451.5, db: -20.0), (hz: 3134.4, db: -30.0), (hz: 12064.7, db: -45.0) };
        var n = engine.RequiredFrames;

        var mix = Signals.Interleaved(n, 2, (_, i) => tones.Sum(t => Signals.Sine(t.hz, Math.Pow(10, t.db / 20), i, 48000)));
        var mixDb = Signals.AnalyzeDb(engine, mix);

        foreach (var (hz, db) in tones)
        {
            var single = Signals.AnalyzeDb(engine, Signals.StereoSine(n, 48000, hz, Math.Pow(10, db / 20), Math.Pow(10, db / 20)));
            var band = Signals.ArgMax(single);

            // A quiet tone is not suppressed by a loud one and no global normalization alters relative levels.
            Assert.InRange(mixDb[band] - single[band], -0.1, 0.1);
            _out.WriteLine($"{hz,7} Hz @ {db,5} dBFS -> bar {band,2}: single {single[band],7:F2} dB, in mix {mixDb[band],7:F2} dB");
        }
    }

    // ---------- Noise ----------

    [Fact]
    public void WhiteNoise_BandPowerMatchesTheIntegratedFlatDensity()
    {
        const int fs = 48000;
        const double sigma = 0.1;
        var engine = Signals.DefaultEngine(fs, 1);
        var n = engine.RequiredFrames;
        const int frames = 200;
        var noise = Signals.WhiteNoise(n * frames, sigma, 1234);

        var mean = new double[engine.Plan.Count];
        for (var f = 0; f < frames; f++)
        {
            var p = Signals.AnalyzePower(engine, Signals.ToInterleaved(noise, f * n, n, 1));
            for (var b = 0; b < mean.Length; b++)
            {
                mean[b] += p[b] / frames;
            }
        }

        // Expected: σ² spread uniformly over 0..fs/2, integrated over the band width, relative to 0.5.
        var worst = 0.0;
        for (var b = 0; b < mean.Length; b++)
        {
            var expected = sigma * sigma * engine.Plan[b].WidthHz / (fs / 2.0) / BandSpectrumAnalyzer.FullScaleSinePower;
            var err = LevelScale.PowerToDb(mean[b]) - LevelScale.PowerToDb(expected);
            worst = Math.Max(worst, Math.Abs(err));

            // 4σ of the averaged power estimate (σ ≈ 4.34 dB / sqrt(dof · frames)) plus 0.1 dB model allowance.
            var tolerance = (4 * 4.34 / Math.Sqrt(Signals.DegreesOfFreedom(engine.GetDiagnostic(b)) * frames)) + 0.1;
            Assert.True(Math.Abs(err) < tolerance, $"band {b}: {err:F2} dB (tolerance {tolerance:F2})");
        }

        // Integrated white noise rises 10·log10(2) ≈ 3.01 dB per octave across constant-ratio bands.
        var perOctave = (LevelScale.PowerToDb(mean[80]) - LevelScale.PowerToDb(mean[40])) / Math.Log(engine.Plan[80].CenterHz / engine.Plan[40].CenterHz, 2);
        Assert.InRange(perOctave, 2.8, 3.2);
        _out.WriteLine($"white noise: worst band error {worst:F2} dB, slope {perOctave:F2} dB/octave");
    }

    [Fact]
    public void PinkNoise_ReadsFlatAcrossConstantRatioBands()
    {
        const int fs = 44100; // the filter's documented accuracy is specified at 44.1 kHz
        var engine = Signals.DefaultEngine(fs, 1);
        var n = engine.RequiredFrames;
        const int frames = 200;
        var noise = Signals.PinkNoise((n * frames) + fs, 99);

        var mean = new double[engine.Plan.Count];
        for (var f = 0; f < frames; f++)
        {
            var p = Signals.AnalyzePower(engine, Signals.ToInterleaved(noise, fs + (f * n), n, 1)); // skip filter warm-up
            for (var b = 0; b < mean.Length; b++)
            {
                mean[b] += p[b] / frames;
            }
        }

        var db = mean.Select(LevelScale.PowerToDb).ToArray();
        var reference = db.Skip(40).Take(20).Average();
        var worst = 0.0;
        for (var b = 0; b < db.Length; b++)
        {
            // 4σ statistical allowance (see white-noise test) plus the filter's documented ±0.05 dB and 0.1 dB model allowance.
            var tolerance = (4 * 4.34 / Math.Sqrt(Signals.DegreesOfFreedom(engine.GetDiagnostic(b)) * frames)) + 0.15;
            worst = Math.Max(worst, Math.Abs(db[b] - reference));
            Assert.True(Math.Abs(db[b] - reference) < tolerance, $"band {b} ({engine.Plan[b].CenterHz:F0} Hz): {db[b] - reference:F2} dB");
        }

        _out.WriteLine($"pink noise: max deviation from flat {worst:F2} dB (band level ≈ {reference:F1} dBFS)");
    }
}
