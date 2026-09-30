using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Spectrum.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace Spectrum.Tests;

/// <summary>
/// Dead-bar diagnostics: for every band, prints edges, FFT length, contributing bins, weights, and the
/// response to a full-scale tone at the band centre, then asserts that every band has a valid measurement path.
/// </summary>
public class DiagnosticsReportTests
{
    private readonly ITestOutputHelper _out;

    public DiagnosticsReportTests(ITestOutputHelper output) => _out = output;

    [Theory]
    [InlineData(44100)]
    [InlineData(48000)]
    [InlineData(96000)]
    public void EveryBand_HasContributingBins_AndRespondsToItsCentreTone(int fs)
    {
        var engine = Signals.DefaultEngine(fs);
        var n = engine.RequiredFrames;
        var sb = new StringBuilder();
        sb.AppendLine(FormattableString.Invariant(
            $"fs={fs}  bands={engine.Plan.Count}  range={engine.Plan.MinHz:F2}..{engine.Plan.MaxHz:F1} Hz"));
        sb.AppendLine(" bar    lower   centre    upper  window   fft  res.Hz    bins        Σw   tone@centre dB   argmax  neighbours(dB)");

        for (var b = 0; b < engine.Plan.Count; b++)
        {
            var band = engine.Plan[b];
            var db = Signals.AnalyzeDb(engine, Signals.StereoSine(n, fs, band.CenterHz, 1, 1));
            var d = engine.GetDiagnostic(b);
            var argmax = Signals.ArgMax(db);
            var lo = b > 0 ? db[b - 1] : double.NaN;
            var hi = b < db.Length - 1 ? db[b + 1] : double.NaN;

            sb.AppendLine(FormattableString.Invariant(
                $"{b,4} {d.LowerHz,8:F1} {d.CenterHz,8:F1} {d.UpperHz,8:F1} {d.WindowLength,6} {d.FftLength,6} {d.ResolutionHz,6:F2} {d.FirstBin,5}-{d.LastBin,-5} {d.Weights.Sum(),6:F2} {db[b],12:F2} {argmax,8}   {lo,6:F1} {hi,6:F1}"));

            Assert.True(d.Weights.Sum() > 0, $"band {b} has no contributing bins");
            Assert.All(d.Weights, w => Assert.InRange(w, 0.0, 1.0));
            Assert.True(d.UpperHz <= fs / 2.0, $"band {b} exceeds Nyquist");
            Assert.True(db[b] > LevelScale.FloorDb, $"band {b} does not respond to its own centre tone ({db[b]:F1} dB)");
        }

        _out.WriteLine(sb.ToString());
    }
}
