using System;
using System.Linq;
using Spectrum.Dsp;
using Xunit;

namespace Spectrum.Tests;

/// <summary>
/// Declared stereo semantics: band power = mean of the L and R channel powers.
/// L = R reads like mono; one channel alone reads −3.01 dB; phase between channels never matters.
/// </summary>
public class StereoTests
{
    private const int Fs = 48000;
    private static readonly BandPlan Plan = Signals.DefaultEngine(Fs).Plan;

    // Band-centre tones (bars 50 and 66), so the tone lies well inside its band.
    private static readonly double Hz = Plan[50].CenterHz;
    private static readonly double Hz2 = Plan[66].CenterHz;
    private static readonly double OneChannelDb = 10 * Math.Log10(0.5);

    private static double BandDb(float[] stereo, double hz)
    {
        var engine = Signals.DefaultEngine(Fs, 2);
        var db = Signals.AnalyzeDb(engine, stereo);
        var band = Enumerable.Range(0, engine.Plan.Count).First(i => engine.Plan[i].UpperHz > hz);
        return db[band];
    }

    private static double BandDb(float[] stereo) => BandDb(stereo, Hz);

    private static float[] Stereo(Func<long, double> left, Func<long, double> right)
        => Signals.Interleaved(Signals.DefaultEngine().RequiredFrames, 2, (c, n) => c == 0 ? left(n) : right(n));

    private static double S(double hz, double amp, long n, double phase = 0) => Signals.Sine(hz, amp, n, Fs, phase);

    [Fact] public void Identical_Channels_ReadAsMono() => Assert.InRange(BandDb(Stereo(n => S(Hz, 1, n), n => S(Hz, 1, n))), -0.05, 0.05);

    [Fact] public void LeftOnly_ReadsMinus3dB() => Assert.InRange(BandDb(Stereo(n => S(Hz, 1, n), _ => 0)) - OneChannelDb, -0.05, 0.05);

    [Fact] public void RightOnly_ReadsMinus3dB() => Assert.InRange(BandDb(Stereo(_ => 0, n => S(Hz, 1, n))) - OneChannelDb, -0.05, 0.05);

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(1.0, 0.5)]
    [InlineData(1.0, 0.1)]
    public void UnequalChannels_ReadTheMeanPower(double left, double right)
    {
        var expected = 10 * Math.Log10(((left * left) + (right * right)) / 2);
        Assert.InRange(BandDb(Stereo(n => S(Hz, left, n), n => S(Hz, right, n))) - expected, -0.05, 0.05);
    }

    [Fact]
    public void AntiPhase_DoesNotCancel()
    {
        // The original (L+R)/2 mono FFT measured −∞ dB here (verified against BASS); total energy must not vanish.
        Assert.InRange(BandDb(Stereo(n => S(Hz, 1, n), n => -S(Hz, 1, n))), -0.05, 0.05);
    }

    [Theory]
    [InlineData(Math.PI / 2)]
    [InlineData(Math.PI / 3)]
    [InlineData(2.5)]
    public void PhaseOffset_DoesNotChangeTheLevel(double phase)
        => Assert.InRange(BandDb(Stereo(n => S(Hz, 1, n), n => S(Hz, 1, n, phase))), -0.05, 0.05);

    [Fact]
    public void DifferentFrequenciesPerChannel_AreBothVisible()
    {
        var stereo = Stereo(n => S(Hz, 1, n), n => S(Hz2, 1, n));
        Assert.InRange(BandDb(stereo, Hz) - OneChannelDb, -0.05, 0.05);
        Assert.InRange(BandDb(stereo, Hz2) - OneChannelDb, -0.05, 0.05);
    }

    [Fact]
    public void MonoDevice_ReadsFullLevel()
    {
        var engine = Signals.DefaultEngine(Fs, 1);
        var db = Signals.AnalyzeDb(engine, Signals.MonoSine(engine.RequiredFrames, Fs, Hz, 1));
        Assert.InRange(db.Max(), -0.05, 0.05);
    }

    [Fact]
    public void MultichannelDevice_AnalysesTheFrontPair()
    {
        // Documented behaviour: channels beyond front L/R (centre, LFE, surrounds) are not part of the measurement.
        var engine = Signals.DefaultEngine(Fs, 6);
        Assert.Equal(2, engine.AnalysedChannels);
        var front = Signals.Interleaved(engine.RequiredFrames, 6, (c, n) => c < 2 ? S(Hz, 1, n) : 0);
        var centreOnly = Signals.Interleaved(engine.RequiredFrames, 6, (c, n) => c == 2 ? S(Hz, 1, n) : 0);
        Assert.InRange(Signals.AnalyzeDb(engine, front).Max(), -0.05, 0.05);
        Assert.All(Signals.AnalyzeDb(engine, centreOnly), v => Assert.Equal(LevelScale.SilenceDb, v));
    }
}

public class NumericalSafetyTests
{
    [Fact]
    public void DigitalSilence_ReadsSilenceEverywhere()
    {
        var engine = Signals.DefaultEngine();
        var db = Signals.AnalyzeDb(engine, new float[engine.RequiredFrames * 2]);
        Assert.All(db, v => Assert.Equal(LevelScale.SilenceDb, v));
        Assert.All(db, v => Assert.Equal(0, LevelScale.ToDisplayByte(v)));
    }

    [Theory]
    [InlineData(1e-6)]   // −120 dBFS
    [InlineData(1e-20)]  // far below float resolution of normal music
    [InlineData(1e-40)]  // subnormal float
    public void ExtremelyLowLevels_StayFiniteAndBelowTheFloor(double amp)
    {
        var engine = Signals.DefaultEngine();
        var db = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 48000, 1000, amp, amp));
        Assert.All(db, v => Assert.False(double.IsNaN(v) || double.IsInfinity(v)));
        Assert.All(db, v => Assert.Equal(0, LevelScale.ToDisplayByte(v)));
    }

    [Fact]
    public void NonFiniteSamples_AreTreatedAsZero()
    {
        var engine = Signals.DefaultEngine();
        var clean = Signals.StereoSine(engine.RequiredFrames, 48000, 1000, 0.5, 0.5);
        var dirty = (float[])clean.Clone();
        var zeroed = (float[])clean.Clone();
        foreach (var i in new[] { 10, 5000, 20001, 32000 })
        {
            dirty[i] = i % 2 == 0 ? float.NaN : float.PositiveInfinity;
            zeroed[i] = 0;
        }

        dirty[777] = float.NegativeInfinity;
        zeroed[777] = 0;

        var a = Signals.AnalyzePower(engine, dirty);
        var b = Signals.AnalyzePower(engine, zeroed);
        Assert.All(a, v => Assert.False(double.IsNaN(v) || double.IsInfinity(v)));
        Assert.Equal(b, a);
    }

    [Fact]
    public void OverFullScaleFloatInput_ClampsTheDisplayButKeepsTheMeasurement()
    {
        var engine = Signals.DefaultEngine();
        var db = Signals.AnalyzeDb(engine, Signals.StereoSine(engine.RequiredFrames, 48000, engine.Plan[50].CenterHz, 1e6, 1e6));
        Assert.InRange(db.Max(), 119.9, 120.1); // measurement reports +120 dBFS honestly
        Assert.Equal(255, LevelScale.ToDisplayByte(db.Max()));
    }

    [Fact]
    public void DcOffset_DoesNotReachTheBars()
    {
        var engine = Signals.DefaultEngine();
        var db = Signals.AnalyzeDb(engine, Signals.Interleaved(engine.RequiredFrames, 2, (_, _) => 0.5));
        Assert.All(db, v => Assert.True(v < LevelScale.FloorDb - 20, $"{v:F1} dB"));
    }
}
