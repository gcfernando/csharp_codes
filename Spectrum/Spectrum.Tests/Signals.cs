using System;
using Spectrum.Dsp;

namespace Spectrum.Tests;

/// <summary>Deterministic test-signal generation and analysis helpers.</summary>
internal static class Signals
{
    public const int Fs48k = 48000;

    /// <summary>Interleaved buffer of <paramref name="frames"/> frames; <paramref name="gen"/>(channel, sampleIndex).</summary>
    public static float[] Interleaved(int frames, int channels, Func<int, long, double> gen)
    {
        var buffer = new float[frames * channels];
        for (var i = 0; i < frames; i++)
        {
            for (var c = 0; c < channels; c++)
            {
                buffer[(i * channels) + c] = (float)gen(c, i);
            }
        }

        return buffer;
    }

    public static double Sine(double hz, double amplitude, long n, int fs, double phase = 0)
        => amplitude * Math.Sin((2 * Math.PI * hz * n / fs) + phase);

    public static float[] StereoSine(int frames, int fs, double hz, double ampL, double ampR, double phaseR = 0)
        => Interleaved(frames, 2, (c, n) => c == 0 ? Sine(hz, ampL, n, fs) : Sine(hz, ampR, n, fs, phaseR));

    public static float[] MonoSine(int frames, int fs, double hz, double amp)
        => Interleaved(frames, 1, (_, n) => Sine(hz, amp, n, fs));

    /// <summary>Gaussian white noise with standard deviation <paramref name="sigma"/> (same seed → same samples).</summary>
    public static double[] WhiteNoise(int length, double sigma, int seed)
    {
        var rng = new Random(seed);
        var x = new double[length];
        for (var i = 0; i < length; i += 2)
        {
            // Box–Muller.
            var u1 = 1.0 - rng.NextDouble();
            var u2 = rng.NextDouble();
            var r = Math.Sqrt(-2.0 * Math.Log(u1)) * sigma;
            x[i] = r * Math.Cos(2 * Math.PI * u2);
            if (i + 1 < length)
            {
                x[i + 1] = r * Math.Sin(2 * Math.PI * u2);
            }
        }

        return x;
    }

    /// <summary>
    /// Pink noise via Paul Kellet's refined filter (documented accuracy ±0.05 dB above 9.2 Hz at 44.1 kHz).
    /// </summary>
    public static double[] PinkNoise(int length, int seed)
    {
        var white = WhiteNoise(length, 1.0, seed);
        var y = new double[length];
        double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (var i = 0; i < length; i++)
        {
            var w = white[i];
            b0 = (0.99886 * b0) + (w * 0.0555179);
            b1 = (0.99332 * b1) + (w * 0.0750759);
            b2 = (0.96900 * b2) + (w * 0.1538520);
            b3 = (0.86650 * b3) + (w * 0.3104856);
            b4 = (0.55000 * b4) + (w * 0.5329522);
            b5 = (-0.7616 * b5) - (w * 0.0168980);
            y[i] = (b0 + b1 + b2 + b3 + b4 + b5 + b6 + (w * 0.5362)) * 0.05;
            b6 = w * 0.115926;
        }

        return y;
    }

    public static float[] ToInterleaved(double[] mono, int start, int frames, int channels)
        => Interleaved(frames, channels, (_, n) => mono[start + n]);

    /// <summary>Analyse a buffer that holds exactly <see cref="BandSpectrumAnalyzer.RequiredFrames"/> frames; returns band levels in dB.</summary>
    public static double[] AnalyzeDb(BandSpectrumAnalyzer engine, float[] interleaved)
    {
        var power = new double[engine.Plan.Count];
        engine.Analyze(interleaved, interleaved.Length / engine.Channels, power);
        var db = new double[power.Length];
        for (var i = 0; i < power.Length; i++)
        {
            db[i] = LevelScale.PowerToDb(power[i]);
        }

        return db;
    }

    public static double[] AnalyzePower(BandSpectrumAnalyzer engine, float[] interleaved)
    {
        var power = new double[engine.Plan.Count];
        engine.Analyze(interleaved, interleaved.Length / engine.Channels, power);
        return power;
    }

    public static int ArgMax(double[] values)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }

        return best;
    }

    public static double SumDb(double[] power, int from, int to)
    {
        var sum = 0.0;
        for (var i = from; i <= to; i++)
        {
            sum += power[i];
        }

        return LevelScale.PowerToDb(sum);
    }

    /// <summary>Resolution-limited band: the analysis window cannot place <see cref="BandSpectrumAnalyzer.MinBinsPerBand"/> bins in it.</summary>
    public static bool IsResolved(BandDiagnostic d) => d.WidthHz() / d.ResolutionHz >= BandSpectrumAnalyzer.MinBinsPerBand;

    public static double WidthHz(this BandDiagnostic d) => d.UpperHz - d.LowerHz;

    /// <summary>
    /// Approximate independent estimates per frame for a noise-like input: resolution bins in the band divided by
    /// the Hann equivalent noise bandwidth (1.5 bins), at least one.
    /// </summary>
    public static double DegreesOfFreedom(BandDiagnostic d) => Math.Max(1.0, d.WidthHz() / d.ResolutionHz / 1.5);

    public static BandSpectrumAnalyzer DefaultEngine(int fs = Fs48k, int channels = 2)
        => new(BandPlan.CreateLogarithmic(83, 20, 20000, fs), channels);
}
