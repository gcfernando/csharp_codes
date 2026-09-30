using System;

namespace Spectrum.Dsp;

/// <summary>
/// Conversion from band power to the fixed display scale.
///
/// Quantity: band power relative to the power of a full-scale sine (A = 1, mean square 0.5),
/// so a full-scale sine whose energy falls entirely inside one band reads 0 dBFS (sine-referenced,
/// as in AES17). Power is converted with 10·log10 because it is already a power quantity.
///
/// Display range is fixed (no per-frame or adaptive normalization):
/// <list type="bullet">
/// <item><see cref="CeilingDb"/> = 0 dBFS: the largest level a non-clipping signal can put into one band.</item>
/// <item><see cref="FloorDb"/> = −72 dBFS: ≈12 bits of range. Typical dense-mix bands sit between −10 and −50 dBFS,
/// quiet but meaningful high-frequency content (cymbal air, reverb tails) around −60, so it stays visible
/// while the bar height still resolves ~0.3 dB per byte step.</item>
/// </list>
/// The mapping between the two is linear in dB: normalized = clamp((dB − floor) / (ceiling − floor), 0, 1).
/// </summary>
internal static class LevelScale
{
    public const double CeilingDb = 0.0;
    public const double FloorDb = -72.0;

    /// <summary>Power below which the level is treated as digital silence (−300 dB); guards log10(0).</summary>
    public const double MinPower = 1e-30;

    public const double SilenceDb = -300.0;

    public static double PowerToDb(double relativePower)
    {
        // NaN fails every comparison, so it falls through to silence along with 0, negatives and denormal-scale values.
        if (!(relativePower > MinPower))
        {
            return SilenceDb;
        }

        if (double.IsPositiveInfinity(relativePower))
        {
            return CeilingDb;
        }

        return 10.0 * Math.Log10(relativePower);
    }

    /// <summary>Normalized height in [0, 1]. Never returns NaN or Infinity.</summary>
    public static double Normalize(double levelDb)
    {
        if (double.IsNaN(levelDb))
        {
            return 0.0;
        }

        var n = (levelDb - FloorDb) / (CeilingDb - FloorDb);
        return n <= 0.0 ? 0.0 : (n >= 1.0 ? 1.0 : n);
    }

    /// <summary>Normalized height quantized to the 0..255 byte used by the published spectrum.</summary>
    public static byte ToDisplayByte(double levelDb) => (byte)Math.Round(Normalize(levelDb) * 255.0);
}
