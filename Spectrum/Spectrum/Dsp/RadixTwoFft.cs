using System;

namespace Spectrum.Dsp;

/// <summary>
/// In-place iterative radix-2 complex FFT with precomputed twiddle factors and bit-reversal table.
/// Convention: X[k] = Σ x[n]·e^(−j·2π·k·n/N), no normalization (scaling is applied by the caller,
/// see <see cref="BandSpectrumAnalyzer"/>). One instance per transform length. The instance is
/// immutable after construction; callers own the (re, im) work arrays.
/// </summary>
internal sealed class RadixTwoFft
{
    // Twiddles stored per stage and contiguously: for a stage of butterfly span 2·h, the factors
    // e^(−j·2π·k/(2h)), k = 0..h−1, live at index h + k. Total length N (index 0 unused).
    private readonly double[] _cos;
    private readonly double[] _sin;
    private readonly int[] _swapA;
    private readonly int[] _swapB;

    public int Length { get; }

    public RadixTwoFft(int length)
    {
        if (length < 4 || (length & (length - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "FFT length must be a power of two >= 4.");
        }

        Length = length;

        _cos = new double[length];
        _sin = new double[length];
        for (var h = 1; h < length; h <<= 1)
        {
            for (var k = 0; k < h; k++)
            {
                var angle = -Math.PI * k / h;
                _cos[h + k] = Math.Cos(angle);
                _sin[h + k] = Math.Sin(angle);
            }
        }

        var bits = 0;
        while ((1 << bits) < length)
        {
            bits++;
        }

        // Only the index pairs that actually swap (i < reverse(i)).
        var pairs = 0;
        var reverse = new int[length];
        for (var i = 0; i < length; i++)
        {
            var r = 0;
            for (var b = 0; b < bits; b++)
            {
                r |= ((i >> b) & 1) << (bits - 1 - b);
            }

            reverse[i] = r;
            if (r > i)
            {
                pairs++;
            }
        }

        _swapA = new int[pairs];
        _swapB = new int[pairs];
        for (int i = 0, p = 0; i < length; i++)
        {
            if (reverse[i] > i)
            {
                _swapA[p] = i;
                _swapB[p] = reverse[i];
                p++;
            }
        }
    }

    /// <summary>Forward transform of (re, im) in place. Both arrays must have at least <see cref="Length"/> elements.</summary>
    public void Forward(double[] re, double[] im)
    {
        var n = Length;

        for (var p = 0; p < _swapA.Length; p++)
        {
            var i = _swapA[p];
            var j = _swapB[p];
            (re[i], re[j]) = (re[j], re[i]);
            (im[i], im[j]) = (im[j], im[i]);
        }

        // Stage 1 (span 2): twiddle 1.
        for (var a = 0; a < n; a += 2)
        {
            var tr = re[a + 1];
            var ti = im[a + 1];
            re[a + 1] = re[a] - tr;
            im[a + 1] = im[a] - ti;
            re[a] += tr;
            im[a] += ti;
        }

        // Stage 2 (span 4): twiddles 1 and −j.
        for (var a = 0; a < n; a += 4)
        {
            var tr = re[a + 2];
            var ti = im[a + 2];
            re[a + 2] = re[a] - tr;
            im[a + 2] = im[a] - ti;
            re[a] += tr;
            im[a] += ti;

            // (re + j·im)·(−j) = im − j·re
            tr = im[a + 3];
            ti = -re[a + 3];
            re[a + 3] = re[a + 1] - tr;
            im[a + 3] = im[a + 1] - ti;
            re[a + 1] += tr;
            im[a + 1] += ti;
        }

        var cos = _cos;
        var sin = _sin;
        for (var half = 4; half < n; half <<= 1)
        {
            var span = half << 1;
            for (var start = 0; start < n; start += span)
            {
                for (var k = 0; k < half; k++)
                {
                    var wr = cos[half + k];
                    var wi = sin[half + k];

                    var a = start + k;
                    var b = a + half;

                    var br = re[b];
                    var bi = im[b];
                    var tr = (br * wr) - (bi * wi);
                    var ti = (br * wi) + (bi * wr);

                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                }
            }
        }
    }
}
