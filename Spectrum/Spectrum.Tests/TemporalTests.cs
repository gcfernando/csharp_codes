using System;
using System.Collections.Generic;
using System.Linq;
using Spectrum.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace Spectrum.Tests;

/// <summary>Ballistics are driven by elapsed time only; results must not depend on the display refresh rate.</summary>
public class BarBallisticsTests
{
    private static readonly int[] Rates = { 30, 60, 120, 144 };

    [Fact]
    public void Release_FollowsTheExponentialTimeConstantAtAnyRefreshRate()
    {
        foreach (var fps in Rates)
        {
            var v = 255f;
            var t = 0.0;
            while (t < 0.28 - 1e-9)
            {
                v = BarBallistics.StepLevel(v, 0, 1f / fps, 255, 110, 280, false);
                t += 1.0 / fps;
            }

            // After t ≈ τ the bar is at 255·e^(−t/τ), whatever the frame rate.
            Assert.InRange(v, (255 * Math.Exp(-t / 0.28)) - 0.01, (255 * Math.Exp(-t / 0.28)) + 0.01);
        }
    }

    [Fact]
    public void Attack_ReachesFullScaleInTheAttackTimeAtAnyRefreshRate()
    {
        foreach (var fps in Rates)
        {
            var v = 0f;
            var frames = 0;
            while (v < 255)
            {
                v = BarBallistics.StepLevel(v, 255, 1f / fps, 255, 110, 280, false);
                frames++;
            }

            var seconds = frames / (double)fps;
            Assert.InRange(seconds, 0.110 - 1e-6, 0.110 + (1.0 / fps) + 1e-6); // exact up to one frame of quantization
        }
    }

    [Fact]
    public void PeakMarker_HoldsThenFallsIndependentlyOfTheBar()
    {
        foreach (var fps in Rates)
        {
            float peak = 0, hold = 0;
            var dtMs = 1000f / fps;
            BarBallistics.StepPeak(ref peak, ref hold, 200, dtMs, 300, 69, 0); // capture
            var held = 0.0;
            while (peak >= 200 && held < 2000)
            {
                BarBallistics.StepPeak(ref peak, ref hold, 0, dtMs, 300, 69, 0); // bar dropped to zero
                held += dtMs;
            }

            Assert.InRange(held, 300, 300 + (2 * dtMs)); // hold time, quantized to the frame

            var fallMs = 0.0;
            while (peak > 0)
            {
                BarBallistics.StepPeak(ref peak, ref hold, 0, dtMs, 300, 69, 0);
                fallMs += dtMs;
            }

            Assert.InRange(fallMs, (200.0 / 69 * 1000) - dtMs, (200.0 / 69 * 1000) + (2 * dtMs));
        }
    }

    // 37 bricks of 6 px + 2 px gap on a 300 px bar, bottom brick first (centre rows decrease upward).
    private static readonly int[] Centres = System.Linq.Enumerable.Range(0, 37).Select(i => 352 - 6 - (i * 8) + 3).ToArray();

    [Fact]
    public void LitBricks_WithoutHysteresis_IsTheCentreRule()
    {
        for (var top = 0; top <= 360; top++)
        {
            var expected = Centres.Count(c => c >= top);
            Assert.Equal(expected, BarBallistics.StepLitBricks(0, Centres, top, 0));
            Assert.Equal(expected, BarBallistics.StepLitBricks(37, Centres, top, 0));
        }
    }

    [Fact]
    public void LitBricks_Hysteresis_SuppressesBoundaryFlickerButFollowsRealMoves()
    {
        const int h = 2;
        var boundary = Centres[20]; // level hovering around the centre of brick 20

        // Start with 20 lit, then wobble ±1 px around the centre: never toggles.
        var lit = BarBallistics.StepLitBricks(0, Centres, boundary + 1, 0);
        Assert.Equal(20, lit);
        foreach (var wobble in new[] { 0, 1, -1, 1, 0, -1 })
        {
            lit = BarBallistics.StepLitBricks(lit, Centres, boundary + wobble, h);
            Assert.Equal(20, lit);
        }

        // A clear move up (≥ h above the centre) lights it; a clear move down (> h below) turns it off again.
        lit = BarBallistics.StepLitBricks(lit, Centres, boundary - h, h);
        Assert.Equal(21, lit);
        lit = BarBallistics.StepLitBricks(lit, Centres, boundary + h + 1, h);
        Assert.Equal(20, lit);

        // Large jumps settle in one step and never differ from the centre rule by more than one brick.
        for (var top = 0; top <= 360; top += 7)
        {
            lit = BarBallistics.StepLitBricks(lit, Centres, top, h);
            Assert.InRange(lit - Centres.Count(c => c >= top), -1, 1);
            Assert.Equal(lit, BarBallistics.StepLitBricks(lit, Centres, top, h)); // idempotent
        }
    }

    [Fact]
    public void Level_NeverLeavesTheValidRange()
    {
        var rng = new Random(7);
        var v = 0f;
        for (var i = 0; i < 20000; i++)
        {
            var target = (float)(rng.NextDouble() * 255);
            v = BarBallistics.StepLevel(v, target, (float)(rng.NextDouble() * 0.2), 255, 110, 280, i % 3 == 0);
            Assert.InRange(v, 0f, 255f);
            Assert.False(float.IsNaN(v));
        }
    }
}

/// <summary>
/// End-to-end timing from a sudden tone onset/offset through the STFT (31.7 ms hop, as measured in the application)
/// and the presentation ballistics of the default "Spectrum" mode, simulated at several refresh rates.
/// </summary>
public class EndToEndTimingTests
{
    private const int Fs = 48000;
    private const int Hop = 1520; // 31.7 ms: measured cadence of the application's 25 ms one-shot timer
    private const double Amp = 0.1; // −20 dBFS tone
    private const double OffAt = 1.5;

    // Default ("Spectrum") preset in FormAudioSpectrum.ApplyMeterPresetOptimized.
    private const int AttackMs = FormAudioSpectrum.SPECTRUM_ATTACK_MS;
    private const int ReleaseMs = FormAudioSpectrum.SPECTRUM_RELEASE_MS;

    private readonly ITestOutputHelper _out;

    public EndToEndTimingTests(ITestOutputHelper output) => _out = output;

    private static List<(double t, byte v)> AnalysisFrames(BandSpectrumAnalyzer engine, int band, double hz)
    {
        var frames = new List<(double, byte)>();
        var power = new double[engine.Plan.Count];
        for (long end = -Fs / 2; end <= (long)(3.0 * Fs); end += Hop)
        {
            var start = end - engine.RequiredFrames;
            var buf = Signals.Interleaved(engine.RequiredFrames, 2, (_, i) =>
            {
                var n = start + i;
                return n >= 0 && n < OffAt * Fs ? Signals.Sine(hz, Amp, n, Fs) : 0.0;
            });
            engine.Analyze(buf, engine.RequiredFrames, power);
            frames.Add((end / (double)Fs, LevelScale.ToDisplayByte(LevelScale.PowerToDb(power[band]))));
        }

        return frames;
    }

    private static List<(double t, float v)> Display(List<(double t, byte v)> analysis, int fps)
    {
        var trace = new List<(double, float)>();
        var v = 0f;
        var idx = 0;
        byte target = 0;
        for (var j = (int)(-0.5 * fps); j <= 3 * fps; j++)
        {
            var t = j / (double)fps;
            while (idx < analysis.Count && analysis[idx].t <= t + 1e-12)
            {
                target = analysis[idx++].v;
            }

            v = BarBallistics.StepLevel(v, target, 1f / fps, 255, AttackMs, ReleaseMs, false);
            trace.Add((t, v));
        }

        return trace;
    }

    private static double Crossing<T>(List<(double t, T v)> trace, Func<T, bool> predicate, double after)
        => trace.First(p => p.t >= after && predicate(p.v)).t;

    private static (double t50, double t90, double release10) Measure(List<(double t, float v)> trace, float steady) => (
        Crossing(trace, v => v >= 0.5f * steady, 0),
        Crossing(trace, v => v >= 0.9f * steady, 0),
        Crossing(trace, v => v <= 0.1f * steady, OffAt) - OffAt);

    [Theory]
    [InlineData(13)] // ~60 Hz (bass, 341 ms window)
    [InlineData(47)] // ~1 kHz (85 ms window)
    [InlineData(68)] // ~6 kHz (85 ms window)
    public void OnsetAndRelease_AreConsistentAcrossRefreshRates(int band)
    {
        var engine = Signals.DefaultEngine();
        var hz = engine.Plan[band].CenterHz;
        var analysis = AnalysisFrames(engine, band, hz);
        var steady = (float)analysis.First(f => f.t >= 1.0).v;
        Assert.True(steady > 150, $"steady display value {steady}");

        var reference = Measure(Display(analysis, 60), steady);
        foreach (var fps in new[] { 30, 60, 120, 144 })
        {
            var m = Measure(Display(analysis, fps), steady);
            _out.WriteLine($"{hz,6} Hz (N={engine.GetWindowLength(band)}) @ {fps,3} fps: 50% {m.t50 * 1000,4:F0} ms, 90% {m.t90 * 1000,4:F0} ms, release to 10% {m.release10 * 1000,4:F0} ms");

            // Time-consistent within one 30 Hz display period plus one analysis hop of sampling phase.
            const double tolerance = (1.0 / 30) + (Hop / (double)Fs);
            Assert.InRange(m.t50 - reference.t50, -tolerance, tolerance);
            Assert.InRange(m.t90 - reference.t90, -tolerance, tolerance);
            Assert.InRange(m.release10 - reference.release10, -tolerance, tolerance);
        }
    }

    [Fact]
    public void MultiResolution_CutsMidAndHighFrequencyOnsetLatency()
    {
        // Evidence for the multi-resolution design: identical measurement semantics, shorter windows above ~0.5 kHz.
        var multi = Signals.DefaultEngine();
        var single = new BandSpectrumAnalyzer(multi.Plan, 2, new[] { 16384 });

        foreach (var band in new[] { 47, 68 })
        {
            var hz = multi.Plan[band].CenterHz;
            var a = AnalysisFrames(multi, band, hz);
            var b = AnalysisFrames(single, band, hz);
            var steadyA = a.First(f => f.t >= 1.0).v;
            var steadyB = b.First(f => f.t >= 1.0).v;

            var t90Multi = Crossing(a, v => v >= 0.9 * steadyA, 0);
            var t90Single = Crossing(b, v => v >= 0.9 * steadyB, 0);
            var offMulti = Crossing(a, v => v <= 0.1 * steadyA, OffAt) - OffAt;
            var offSingle = Crossing(b, v => v <= 0.1 * steadyB, OffAt) - OffAt;

            _out.WriteLine($"{hz:F0} Hz analysis only: 90% rise {t90Multi * 1000:F0} ms (N={multi.GetWindowLength(band)}) vs {t90Single * 1000:F0} ms (N=16384); " +
                           $"fall to 10% {offMulti * 1000:F0} ms vs {offSingle * 1000:F0} ms");

            Assert.Equal(steadyB, steadyA); // same steady-state level: same measurement
            // At least 75 ms (more than two analysis hops) faster in both directions.
            Assert.True(t90Single - t90Multi >= 0.075, "expected a clearly faster rise");
            Assert.True(offSingle - offMulti >= 0.075, "expected a clearly faster release");
        }
    }
}
