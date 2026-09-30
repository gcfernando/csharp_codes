using System;
using System.Collections.Generic;

namespace Spectrum.Dsp;

/// <summary>One display band: a contiguous frequency interval [LowerHz, UpperHz) with a geometric centre.</summary>
internal readonly struct FrequencyBand
{
    public FrequencyBand(double lowerHz, double centerHz, double upperHz)
    {
        LowerHz = lowerHz;
        CenterHz = centerHz;
        UpperHz = upperHz;
    }

    public double LowerHz { get; }
    public double CenterHz { get; }
    public double UpperHz { get; }
    public double WidthHz => UpperHz - LowerHz;
}

/// <summary>
/// Logarithmic (equal frequency-ratio) partition of the audio band into display bands.
/// Band centres are spaced by a constant ratio r; edges lie at the geometric midpoints
/// (centre / √r, centre · √r), so adjacent bands share an edge exactly: no gaps, no overlap.
/// The plan never extends above Nyquist: if the requested top band would, the whole plan is
/// regenerated with a lower top centre so the last upper edge equals Nyquist.
/// </summary>
internal sealed class BandPlan
{
    private readonly FrequencyBand[] _bands;

    private BandPlan(FrequencyBand[] bands, double ratio, int sampleRate)
    {
        _bands = bands;
        BandRatio = ratio;
        SampleRate = sampleRate;
    }

    public IReadOnlyList<FrequencyBand> Bands => _bands;
    public int Count => _bands.Length;
    public double BandRatio { get; }
    public int SampleRate { get; }
    public double MinHz => _bands[0].LowerHz;
    public double MaxHz => _bands[_bands.Length - 1].UpperHz;

    public FrequencyBand this[int index] => _bands[index];

    public static BandPlan CreateLogarithmic(int count, double firstCenterHz, double lastCenterHz, int sampleRate)
    {
        if (count < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "At least two bands are required.");
        }

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        if (!(firstCenterHz > 0) || !(lastCenterHz > firstCenterHz))
        {
            throw new ArgumentOutOfRangeException(nameof(lastCenterHz), "Require 0 < firstCenterHz < lastCenterHz.");
        }

        var nyquist = sampleRate / 2.0;
        var ratio = Math.Pow(lastCenterHz / firstCenterHz, 1.0 / (count - 1));

        if (lastCenterHz * Math.Sqrt(ratio) > nyquist)
        {
            // Lower the top centre so the top edge lands exactly on Nyquist. With r = (top/first)^(1/(count−1)),
            // top·√r = nyquist gives ln top = (2(count−1)·ln nyquist + ln first) / (2·count − 1).
            var top = Math.Exp(((2.0 * (count - 1) * Math.Log(nyquist)) + Math.Log(firstCenterHz)) / ((2.0 * count) - 1));
            if (!(top > firstCenterHz))
            {
                throw new ArgumentException($"Sample rate {sampleRate} Hz cannot support bands starting at {firstCenterHz} Hz.");
            }

            ratio = Math.Pow(top / firstCenterHz, 1.0 / (count - 1));
        }

        var halfStep = Math.Sqrt(ratio);
        var bands = new FrequencyBand[count];
        var lower = firstCenterHz / halfStep;

        for (var i = 0; i < count; i++)
        {
            var center = firstCenterHz * Math.Pow(ratio, i);
            var upper = i == count - 1 ? Math.Min(center * halfStep, nyquist) : center * halfStep;
            bands[i] = new FrequencyBand(lower, center, upper);
            lower = upper; // shared edge: exact partition
        }

        return new BandPlan(bands, ratio, sampleRate);
    }

    /// <summary>
    /// Continuous position (in band-index units, 0 = left edge of band 0, Count = right edge of last band)
    /// of an arbitrary frequency on this plan's logarithmic axis. Used by the UI so that frequency labels
    /// and bars share one mapping model.
    /// </summary>
    public double FrequencyToPosition(double hz)
    {
        if (!(hz > 0))
        {
            return 0;
        }

        return Math.Log(hz / MinHz) / Math.Log(BandRatio);
    }
}
