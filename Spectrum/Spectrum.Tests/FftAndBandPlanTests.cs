using System;
using System.Linq;
using Spectrum.Dsp;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)] // keeps timing/allocation measurements clean

namespace Spectrum.Tests;

public class RadixTwoFftTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(1024)]
    public void MatchesNaiveDft(int n)
    {
        var rng = new Random(n);
        var re = Enumerable.Range(0, n).Select(_ => rng.NextDouble() - 0.5).ToArray();
        var im = Enumerable.Range(0, n).Select(_ => rng.NextDouble() - 0.5).ToArray();
        var fre = (double[])re.Clone();
        var fim = (double[])im.Clone();

        new RadixTwoFft(n).Forward(fre, fim);

        for (var k = 0; k < n; k++)
        {
            double sr = 0, si = 0;
            for (var t = 0; t < n; t++)
            {
                var a = -2 * Math.PI * k * t / n;
                sr += (re[t] * Math.Cos(a)) - (im[t] * Math.Sin(a));
                si += (re[t] * Math.Sin(a)) + (im[t] * Math.Cos(a));
            }

            // Relative to the transform magnitude (~sqrt(n) here): both computations accumulate O(n) rounding.
            Assert.InRange(fre[k] - sr, -1e-9 * n, 1e-9 * n);
            Assert.InRange(fim[k] - si, -1e-9 * n, 1e-9 * n);
        }
    }

    [Theory]
    [InlineData(32768)]
    [InlineData(65536)] // largest production size (96 kHz: 32768-sample window zero-padded x2)
    public void SatisfiesParsevalAtProductionSizes(int n)
    {
        var rng = new Random(n);
        var re = Enumerable.Range(0, n).Select(_ => rng.NextDouble() - 0.5).ToArray();
        var im = Enumerable.Range(0, n).Select(_ => rng.NextDouble() - 0.5).ToArray();
        var timeEnergy = re.Sum(v => v * v) + im.Sum(v => v * v);

        new RadixTwoFft(n).Forward(re, im);

        var freqEnergy = 0.0;
        for (var k = 0; k < n; k++)
        {
            freqEnergy += (re[k] * re[k]) + (im[k] * im[k]);
        }

        Assert.InRange(freqEnergy / (n * timeEnergy), 1 - 1e-12, 1 + 1e-12);
    }

    [Fact]
    public void BinFrequencyMapping_IsKTimesFsOverN()
    {
        // A complex exponential at exactly bin k puts all energy in bin k: frequency = k · fs / N.
        const int n = 4096;
        const int k = 341;
        var re = new double[n];
        var im = new double[n];
        for (var t = 0; t < n; t++)
        {
            re[t] = Math.Cos(2 * Math.PI * k * t / n);
            im[t] = Math.Sin(2 * Math.PI * k * t / n);
        }

        new RadixTwoFft(n).Forward(re, im);

        Assert.Equal(n, Math.Sqrt((re[k] * re[k]) + (im[k] * im[k])), 6);
        Assert.True(Math.Sqrt((re[k + 1] * re[k + 1]) + (im[k + 1] * im[k + 1])) < 1e-6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1000)]
    public void RejectsNonPowerOfTwo(int n) => Assert.Throws<ArgumentOutOfRangeException>(() => new RadixTwoFft(n));
}

public class BandPlanTests
{
    [Theory]
    [InlineData(83, 44100)]
    [InlineData(83, 48000)]
    [InlineData(83, 96000)]
    [InlineData(83, 192000)]
    [InlineData(31, 48000)]
    [InlineData(10, 48000)]
    [InlineData(200, 48000)]
    public void Bands_AreContiguousMonotonicAndInsideNyquist(int count, int fs)
    {
        var plan = BandPlan.CreateLogarithmic(count, 20, 20000, fs);

        Assert.Equal(count, plan.Count);
        for (var i = 0; i < count; i++)
        {
            var b = plan[i];
            Assert.True(b.LowerHz > 0);
            Assert.True(b.LowerHz < b.CenterHz && b.CenterHz < b.UpperHz, $"band {i} not ordered");
            Assert.Equal(Math.Sqrt(b.LowerHz * b.UpperHz), b.CenterHz, 6); // geometric centre
            Assert.True(b.UpperHz <= fs / 2.0);
            if (i > 0)
            {
                Assert.Equal(plan[i - 1].UpperHz, b.LowerHz); // shared edge: no gap, no overlap
                Assert.Equal(plan.BandRatio, b.CenterHz / plan[i - 1].CenterHz, 9);
            }
        }

        Assert.Equal(20.0, plan[0].CenterHz, 9);
        var requestedRatio = Math.Pow(1000.0, 1.0 / (count - 1));
        if (20000 * Math.Sqrt(requestedRatio) <= fs / 2.0)
        {
            Assert.InRange(plan[count - 1].CenterHz / 20000.0, 1 - 1e-9, 1 + 1e-9);
        }
        else
        {
            // Too few bands for 20 kHz to fit below Nyquist with its upper half-band: plan regenerated to end at Nyquist.
            Assert.InRange(plan.MaxHz / (fs / 2.0), 1 - 1e-9, 1.0);
        }
    }

    [Theory]
    [InlineData(32000)]
    [InlineData(22050)]
    [InlineData(16000)]
    public void LowSampleRates_RegenerateThePlanBelowNyquist(int fs)
    {
        var plan = BandPlan.CreateLogarithmic(83, 20, 20000, fs);

        Assert.Equal(83, plan.Count);
        Assert.True(plan.MaxHz <= fs / 2.0, $"top edge {plan.MaxHz} above Nyquist {fs / 2}");
        Assert.InRange(plan.MaxHz / (fs / 2.0), 1 - 1e-9, 1.0); // uses the available bandwidth exactly
        Assert.Equal(20.0, plan[0].CenterHz, 9);
    }

    [Fact]
    public void FrequencyToPosition_PutsBandCentresAtHalfIndex()
    {
        var plan = BandPlan.CreateLogarithmic(83, 20, 20000, 48000);
        for (var i = 0; i < plan.Count; i++)
        {
            Assert.Equal(i + 0.5, plan.FrequencyToPosition(plan[i].CenterHz), 9);
        }

        Assert.Equal(0.5, plan.FrequencyToPosition(20), 9);
        Assert.InRange(plan.FrequencyToPosition(20000), 82.5 - 1e-9, 82.5 + 1e-9);
    }

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void BinWeights_CoverEachBandExactlyOnce(int fs)
    {
        var engine = Signals.DefaultEngine(fs);
        for (var b = 0; b < engine.Plan.Count; b++)
        {
            var d = engine.GetDiagnostic(b);

            // Each weight is the fraction of a bin's interval inside the band, so Σw·Δf must equal the band width:
            // nothing inside the band is missed and nothing outside it is counted.
            Assert.Equal(d.UpperHz - d.LowerHz, d.Weights.Sum() * d.BinWidthHz, 6);
            Assert.All(d.Weights, w => Assert.InRange(w, 0.0, 1.0));
            Assert.True(d.FirstBin >= 1, "DC bin must never contribute");
            Assert.True(d.LastBin <= d.FftLength / 2);
        }
    }

    [Theory]
    [InlineData(44100, 4096)]
    [InlineData(48000, 4096)]
    [InlineData(88200, 8192)]
    [InlineData(96000, 8192)]
    [InlineData(192000, 16384)]
    public void WindowLengths_ScaleWithSampleRateToKeepTheirDuration(int fs, int shortest)
    {
        var lengths = BandSpectrumAnalyzer.DefaultWindowLengths(fs);
        Assert.Equal(new[] { shortest, shortest * 2, shortest * 4 }, lengths);
        Assert.InRange(lengths[0] / (double)fs, 0.080, 0.095); // ~85 ms shortest window at every rate
    }

    [Fact]
    public void ResolutionSelection_GivesEveryResolvedBandAtLeastTheHannMainLobe()
    {
        var engine = Signals.DefaultEngine();
        for (var b = 0; b < engine.Plan.Count; b++)
        {
            var d = engine.GetDiagnostic(b);
            if (d.WindowLength < engine.RequiredFrames)
            {
                Assert.True(Signals.IsResolved(d), $"band {b}: {d.WidthHz() / d.ResolutionHz:F2} bins at N={d.WindowLength}");
            }

            // Zero padding only where a band would otherwise hold less than one bin.
            Assert.True(d.BinWidthHz <= d.WidthHz() || d.FftLength == d.WindowLength * BandSpectrumAnalyzer.MaxZeroPadding, $"band {b}");

            if (b > 0)
            {
                // Resolution only ever gets coarser (shorter window) going up in frequency.
                Assert.True(d.WindowLength <= engine.GetWindowLength(b - 1));
            }
        }
    }
}

public class LevelScaleTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-1e-12)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e-40)]
    public void InvalidOrSilentPower_MapsToSilence(double p)
    {
        Assert.Equal(LevelScale.SilenceDb, LevelScale.PowerToDb(p));
        Assert.Equal(0, LevelScale.ToDisplayByte(LevelScale.PowerToDb(p)));
    }

    [Fact]
    public void PowerToDb_UsesTenLog10()
    {
        Assert.Equal(0.0, LevelScale.PowerToDb(1.0), 12);
        Assert.Equal(-20.0, LevelScale.PowerToDb(0.01), 12);
        Assert.Equal(-3.0103, LevelScale.PowerToDb(0.5), 4);
        Assert.Equal(LevelScale.CeilingDb, LevelScale.PowerToDb(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(double.NaN, 0.0)]
    [InlineData(double.NegativeInfinity, 0.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(-300.0, 0.0)]
    [InlineData(-72.0, 0.0)]
    [InlineData(-36.0, 0.5)]
    [InlineData(0.0, 1.0)]
    [InlineData(12.0, 1.0)]
    public void Normalize_IsClampedLinearInDb(double db, double expected)
        => Assert.Equal(expected, LevelScale.Normalize(db), 12);

    [Fact]
    public void DisplayByte_IsMonotonicAcrossTheRange()
    {
        var previous = -1;
        for (var db = -80.0; db <= 5.0; db += 0.05)
        {
            int v = LevelScale.ToDisplayByte(db);
            Assert.True(v >= previous);
            previous = v;
        }

        Assert.Equal(255, previous);
    }
}
