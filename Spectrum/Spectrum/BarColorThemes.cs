using System.Drawing;

namespace Spectrum;

/// <summary>
/// Named color palettes for the bar heat-map (low -&gt; mid -&gt; high -&gt; peak), selected via the
/// <c>Theme</c> key in App.config. Independent of <c>Mode</c>, which controls animation/shape: a palette
/// can be combined with any visualization mode.
/// </summary>
internal readonly struct BarColorTheme
{
    public Color Low { get; }
    public Color Mid { get; }
    public Color High { get; }
    public Color Peak { get; }
    public float IntensityCurve { get; }

    public BarColorTheme(Color low, Color mid, Color high, Color peak, float intensityCurve)
    {
        Low = low;
        Mid = mid;
        High = high;
        Peak = peak;
        IntensityCurve = intensityCurve;
    }
}

internal static class BarColorThemes
{
    // Classic VU, with a gentler curve than the original 1.85 so the green -> yellow -> orange handoff
    // reads as a gradient rather than a visible seam.
    public static readonly BarColorTheme ClassicSmooth = new(
        low: Color.FromArgb(0, 230, 90),
        mid: Color.FromArgb(255, 220, 0),
        high: Color.FromArgb(255, 110, 0),
        peak: Color.Red,
        intensityCurve: 1.5f);

    // Cool monitoring look: deep blue through cyan to near-white.
    public static readonly BarColorTheme Ice = new(
        low: Color.FromArgb(20, 70, 200),
        mid: Color.FromArgb(0, 200, 230),
        high: Color.FromArgb(180, 245, 255),
        peak: Color.White,
        intensityCurve: 1.5f);

    // Matches the title bar's warm amber/orange accent for brand consistency.
    public static readonly BarColorTheme Sunset = new(
        low: Color.FromArgb(140, 70, 10),
        mid: Color.FromArgb(230, 120, 20),
        high: Color.FromArgb(255, 60, 30),
        peak: Color.FromArgb(255, 230, 160),
        intensityCurve: 1.5f);

    // Minimal single-hue intensity ramp: dim to saturated to bright cyan.
    public static readonly BarColorTheme MonoCyan = new(
        low: Color.FromArgb(10, 60, 70),
        mid: Color.FromArgb(0, 160, 190),
        high: Color.FromArgb(0, 230, 255),
        peak: Color.FromArgb(220, 255, 255),
        intensityCurve: 1.4f);

    // Retro 80s: purple through magenta to cyan, pairs well with Bricks mode.
    public static readonly BarColorTheme Synthwave = new(
        low: Color.FromArgb(60, 20, 120),
        mid: Color.FromArgb(200, 30, 160),
        high: Color.FromArgb(255, 80, 200),
        peak: Color.FromArgb(120, 230, 255),
        intensityCurve: 1.5f);

    public static BarColorTheme Resolve(string name) => (name ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "ice" => Ice,
        "sunset" => Sunset,
        "monocyan" or "mono" or "monochromecyan" => MonoCyan,
        "synthwave" => Synthwave,
        _ => ClassicSmooth, // "classicsmooth", "classic", "", or unrecognized
    };
}
