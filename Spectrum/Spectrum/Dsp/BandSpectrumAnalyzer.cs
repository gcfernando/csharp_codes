using System;
using System.Collections.Generic;

namespace Spectrum.Dsp;

/// <summary>Inspectable description of how one display band is measured (dead-bar diagnostics).</summary>
internal sealed class BandDiagnostic
{
    public int Index { get; set; }
    public double LowerHz { get; set; }
    public double CenterHz { get; set; }
    public double UpperHz { get; set; }
    public int WindowLength { get; set; }
    public int FftLength { get; set; }
    public double ResolutionHz { get; set; }
    public double BinWidthHz { get; set; }
    public int FirstBin { get; set; }
    public int LastBin { get; set; }
    public double[] Weights { get; set; }
    public double RelativePower { get; set; }
    public double LevelDb { get; set; }
    public double Normalized { get; set; }
}

/// <summary>
/// Measures integrated band power for a <see cref="BandPlan"/> from raw interleaved PCM samples.
///
/// Per analysis frame, for each FFT length in use:
/// <list type="number">
/// <item>take the most recent N frames of each analysed channel;</item>
/// <item>remove the window-weighted mean (DC only — no other spectral shaping);</item>
/// <item>apply a periodic Hann window and FFT;</item>
/// <item>one-sided bin power P[k] = 2·|X[k]|² / (N·Σw²) (factor 1 at Nyquist). By Parseval, Σ P[k] equals the
///   window-weighted mean square of the signal, so summing bins gives signal power for tones and noise alike
///   and is free of scalloping loss once a band spans the window main lobe;</item>
/// <item>channel strategy: mean of the per-channel powers of the front pair (L, R). This is total stereo energy
///   normalized so L = R reads the same as mono; anti-phase content cannot cancel;</item>
/// <item>band power = Σ weight[k]·P[k] where weight is the fraction of bin k's interval [(k−½)Δf, (k+½)Δf]
///   lying inside the band, so every bin's power is assigned exactly once across adjacent bands;</item>
/// <item>divide by 0.5 (power of a full-scale sine) → relative power; 0 dBFS = full-scale sine.</item>
/// </list>
///
/// Multi-resolution: each band uses the shortest window whose resolution fs/N puts at least
/// <see cref="MinBinsPerBand"/> bins in the band (the null-to-null width of the Hann main lobe), else the longest.
/// Short windows cut latency for mid/high bands; the longest window keeps the finest bass resolution.
/// Band power has the same meaning at every length, so levels are consistent across the transition.
/// Window lengths are defined in time (about 85/171/341 ms), so behaviour is the same at 44.1, 48 or 96 kHz.
/// Known limit: for a tone within about two coarse bins of a band edge where the window length changes, the coarser
/// window's wider main lobe crosses the edge into a band measured with the finer window, so up to ~13 % of the tone's
/// power is counted twice or not at all: the tone reads within about ±0.6 dB there at 44.1/48 kHz and ±1 dB at
/// 22.05/32 kHz (bounded by the sweep tests; measured independently).
/// Everywhere else the bands partition the power exactly (±0.1 dB in the tests).
///
/// Zero padding: where bands are narrower than one bin (deep bass, below the physical resolution), the window is
/// zero-padded (at most 4x) so every band holds at least one interpolated bin. This samples the true DTFT of the
/// windowed frame more densely, so the lobe peak of a tone lands in the band that contains the tone. It does
/// not improve resolution: an unresolved tone still lights a cluster of neighbouring bars.
///
/// Not thread-safe: one instance per analysis thread. <see cref="Analyze"/> does not allocate.
/// </summary>
internal sealed class BandSpectrumAnalyzer
{
    /// <summary>Hann main-lobe width (null to null) in bins.</summary>
    public const double MinBinsPerBand = 4.0;

    /// <summary>Power of a full-scale sine (A²/2 with A = 1).</summary>
    public const double FullScaleSinePower = 0.5;

    /// <summary>Window lengths at 48 kHz: about 85 ms, 171 ms and 341 ms.</summary>
    public static readonly IReadOnlyList<int> ReferenceWindowLengths = new[] { 4096, 8192, 16384 };

    public const int ReferenceSampleRate = 48000;

    /// <summary>Largest zero-padding factor applied to a window.</summary>
    public const int MaxZeroPadding = 4;

    private readonly Resolution[] _resolutions;
    private readonly int[] _bandResolution;
    private readonly int[] _bandFirstBin;
    private readonly double[][] _bandWeights;
    private readonly double[] _lastRelativePower;

    public BandSpectrumAnalyzer(BandPlan plan, int channels, IReadOnlyList<int> windowLengths = null)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (channels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(channels), channels, "At least one channel is required.");
        }

        Channels = channels;
        AnalysedChannels = Math.Min(channels, 2);

        var lengths = new List<int>(windowLengths ?? DefaultWindowLengths(plan.SampleRate));
        if (lengths.Count == 0)
        {
            throw new ArgumentException("At least one window length is required.", nameof(windowLengths));
        }

        lengths.Sort();

        var fs = plan.SampleRate;
        _bandResolution = new int[plan.Count];
        _bandFirstBin = new int[plan.Count];
        _bandWeights = new double[plan.Count][];
        _lastRelativePower = new double[plan.Count];

        var used = new bool[lengths.Count];
        for (var b = 0; b < plan.Count; b++)
        {
            var r = lengths.Count - 1;
            for (var i = 0; i < lengths.Count; i++)
            {
                if (plan[b].WidthHz >= MinBinsPerBand * fs / lengths[i])
                {
                    r = i;
                    break;
                }
            }

            _bandResolution[b] = r;
            used[r] = true;
        }

        _resolutions = new Resolution[lengths.Count];
        for (var i = 0; i < lengths.Count; i++)
        {
            // The narrowest band served by this window decides the zero padding (1 when every band spans >= 1 bin).
            var narrowest = double.MaxValue;
            for (var b = 0; b < plan.Count; b++)
            {
                if (_bandResolution[b] == i)
                {
                    narrowest = Math.Min(narrowest, plan[b].WidthHz);
                }
            }

            var padding = 1;
            while (padding < MaxZeroPadding && (double)fs / (lengths[i] * padding) > narrowest)
            {
                padding *= 2;
            }

            _resolutions[i] = new Resolution(lengths[i], lengths[i] * padding, used[i]);
        }

        for (var b = 0; b < plan.Count; b++)
        {
            BuildWeights(b, plan[b], _resolutions[_bandResolution[b]].FftLength, fs);
        }

        RequiredFrames = lengths[lengths.Count - 1];
    }

    public BandPlan Plan { get; }
    public int Channels { get; }
    public int AnalysedChannels { get; }

    /// <summary>Number of most-recent frames <see cref="Analyze"/> needs (the longest window).</summary>
    public int RequiredFrames { get; }

    /// <summary>Analysis window length (time resolution) used for a band.</summary>
    public int GetWindowLength(int band) => _resolutions[_bandResolution[band]].WindowLength;

    /// <summary>
    /// Window lengths for a sample rate: the 48 kHz reference lengths scaled by the nearest power of two of
    /// fs / 48 kHz, so window durations (and therefore latency and resolution in Hz) stay roughly constant.
    /// </summary>
    public static int[] DefaultWindowLengths(int sampleRate)
    {
        var scale = Math.Pow(2, Math.Round(Math.Log((double)sampleRate / ReferenceSampleRate, 2)));
        var lengths = new int[ReferenceWindowLengths.Count];
        for (var i = 0; i < lengths.Length; i++)
        {
            lengths[i] = Math.Max(256, (int)(ReferenceWindowLengths[i] * scale));
        }

        return lengths;
    }

    /// <summary>
    /// Analyse the most recent <see cref="RequiredFrames"/> frames ending at <paramref name="frameCount"/>
    /// in <paramref name="interleaved"/> (frame-interleaved, <see cref="Channels"/> samples per frame).
    /// Writes band power relative to a full-scale sine into <paramref name="relativeBandPower"/>.
    /// Non-finite samples are treated as 0.
    /// </summary>
    public void Analyze(float[] interleaved, int frameCount, double[] relativeBandPower)
    {
        if (interleaved == null)
        {
            throw new ArgumentNullException(nameof(interleaved));
        }

        if (relativeBandPower == null || relativeBandPower.Length < Plan.Count)
        {
            throw new ArgumentException("Output must hold one value per band.", nameof(relativeBandPower));
        }

        if (frameCount < RequiredFrames || (long)frameCount * Channels > interleaved.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), frameCount, "Not enough frames for the longest FFT.");
        }

        for (var r = 0; r < _resolutions.Length; r++)
        {
            if (_resolutions[r].InUse)
            {
                ComputeBinPower(_resolutions[r], interleaved, frameCount);
            }
        }

        for (var b = 0; b < Plan.Count; b++)
        {
            var power = _resolutions[_bandResolution[b]].Power;
            var weights = _bandWeights[b];
            var first = _bandFirstBin[b];

            var sum = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                sum += weights[i] * power[first + i];
            }

            var relative = sum / FullScaleSinePower;
            _lastRelativePower[b] = relative;
            relativeBandPower[b] = relative;
        }
    }

    public BandDiagnostic GetDiagnostic(int band)
    {
        var res = _resolutions[_bandResolution[band]];
        var weights = (double[])_bandWeights[band].Clone();
        var db = LevelScale.PowerToDb(_lastRelativePower[band]);
        return new BandDiagnostic
        {
            Index = band,
            LowerHz = Plan[band].LowerHz,
            CenterHz = Plan[band].CenterHz,
            UpperHz = Plan[band].UpperHz,
            WindowLength = res.WindowLength,
            FftLength = res.FftLength,
            ResolutionHz = (double)Plan.SampleRate / res.WindowLength,
            BinWidthHz = (double)Plan.SampleRate / res.FftLength,
            FirstBin = _bandFirstBin[band],
            LastBin = _bandFirstBin[band] + weights.Length - 1,
            Weights = weights,
            RelativePower = _lastRelativePower[band],
            LevelDb = db,
            Normalized = LevelScale.Normalize(db),
        };
    }

    private void BuildWeights(int band, FrequencyBand fb, int n, int fs)
    {
        var df = (double)fs / n;
        var half = n / 2;

        // Bin k represents [(k − ½)Δf, (k + ½)Δf]; the Nyquist bin only [(N/2 − ½)Δf, N/2·Δf].
        var first = Math.Max(1, (int)Math.Floor((fb.LowerHz / df) + 0.5));
        var last = Math.Min(half, (int)Math.Ceiling((fb.UpperHz / df) - 0.5));
        if (last < first)
        {
            last = first;
        }

        var weights = new double[last - first + 1];
        for (var k = first; k <= last; k++)
        {
            var binLo = (k - 0.5) * df;
            var binHi = k == half ? k * df : (k + 0.5) * df;
            var overlap = Math.Min(fb.UpperHz, binHi) - Math.Max(fb.LowerHz, binLo);
            weights[k - first] = overlap > 0 ? overlap / (binHi - binLo) : 0.0;
        }

        _bandFirstBin[band] = first;
        _bandWeights[band] = weights;
    }

    private void ComputeBinPower(Resolution res, float[] interleaved, int frameCount)
    {
        var n = res.FftLength;
        var ch = Channels;
        var start = frameCount - res.WindowLength;
        var window = res.Window;
        var re = res.Re;
        var im = res.Im;

        LoadWindowed(interleaved, start, ch, 0, window, res.WindowSum, re);
        if (AnalysedChannels == 2)
        {
            LoadWindowed(interleaved, start, ch, 1, window, res.WindowSum, im);
        }
        else
        {
            Array.Clear(im, 0, n);
        }

        // Zero padding (if any): everything after the window is zero.
        if (n > res.WindowLength)
        {
            Array.Clear(re, res.WindowLength, n - res.WindowLength);
            Array.Clear(im, res.WindowLength, n - res.WindowLength);
        }

        res.Fft.Forward(re, im);

        var power = res.Power;
        var scale = 1.0 / (n * res.WindowSumOfSquares);
        var half = n / 2;
        power[0] = 0.0;

        if (AnalysedChannels == 2)
        {
            // Two real signals packed as re + j·im: X_L[k] = (Z[k] + Z*[N−k]) / 2, X_R[k] = (Z[k] − Z*[N−k]) / 2j.
            for (var k = 1; k <= half; k++)
            {
                var a = re[k];
                var bIm = im[k];
                var c = re[n - k];
                var d = im[n - k];

                var left = (((a + c) * (a + c)) + ((bIm - d) * (bIm - d))) * 0.25;
                var right = (((bIm + d) * (bIm + d)) + ((a - c) * (a - c))) * 0.25;

                // Mean of channel powers, one-sided (×2) except at Nyquist.
                var oneSided = k == half ? 1.0 : 2.0;
                power[k] = oneSided * 0.5 * (left + right) * scale;
            }
        }
        else
        {
            for (var k = 1; k <= half; k++)
            {
                var oneSided = k == half ? 1.0 : 2.0;
                power[k] = oneSided * ((re[k] * re[k]) + (im[k] * im[k])) * scale;
            }
        }
    }

    private static void LoadWindowed(float[] interleaved, int startFrame, int channels, int channel, double[] window, double windowSum, double[] dest)
    {
        var n = window.Length;
        var idx = (startFrame * channels) + channel;

        var weightedSum = 0.0;
        for (var i = 0; i < n; i++, idx += channels)
        {
            var s = interleaved[idx];
            var x = float.IsNaN(s) || float.IsInfinity(s) ? 0.0 : s;
            dest[i] = x;
            weightedSum += window[i] * x;
        }

        // Subtract the window-weighted mean so the windowed frame has exactly zero DC (no DC leakage into low bands).
        var mean = weightedSum / windowSum;
        for (var i = 0; i < n; i++)
        {
            dest[i] = (dest[i] - mean) * window[i];
        }
    }

    private sealed class Resolution
    {
        public Resolution(int length, int fftLength, bool inUse)
        {
            WindowLength = length;
            FftLength = fftLength;
            InUse = inUse;
            Fft = new RadixTwoFft(fftLength);
            Window = new double[length];
            Re = new double[fftLength];
            Im = new double[fftLength];
            Power = new double[(fftLength / 2) + 1];

            // Periodic Hann: w[n] = 0.5 − 0.5·cos(2πn/N). Coherent gain 0.5, ENBW 1.5 bins, main lobe ±2 bins,
            // first sidelobe −31.5 dB falling 18 dB/octave: low leakage for music with a large dynamic range.
            for (var i = 0; i < length; i++)
            {
                var w = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * i / length));
                Window[i] = w;
                WindowSum += w;
                WindowSumOfSquares += w * w;
            }
        }

        public int WindowLength { get; }
        public int FftLength { get; }
        public bool InUse { get; }
        public RadixTwoFft Fft { get; }
        public double[] Window { get; }
        public double WindowSum { get; }
        public double WindowSumOfSquares { get; }
        public double[] Re { get; }
        public double[] Im { get; }
        public double[] Power { get; }
    }
}
