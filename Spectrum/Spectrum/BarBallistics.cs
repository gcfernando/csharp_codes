using System;
using System.Collections.Generic;

namespace Spectrum;

/// <summary>
/// Presentation ballistics for one bar, driven only by elapsed time so behaviour is independent of the
/// UI refresh rate. Values are in display units (0..fullScale, linear in dB of the measured band level).
/// <list type="bullet">
/// <item>Attack: slew-rate limited rise; a full-scale rise takes <c>attackMs</c>.</item>
/// <item>Release: exponential approach to the target with time constant <c>releaseMs</c>
///   (coefficient 1 − e^(−Δt/τ), exact for any Δt).</item>
/// <item>Peak marker: independent state — captures the bar level, holds for <c>holdMs</c>, then falls linearly.</item>
/// </list>
/// </summary>
internal static class BarBallistics
{
    /// <summary>Residual distance below which the release is considered settled.</summary>
    public const float SettleEpsilon = 0.05f;

    public static float StepLevel(float current, float target, float dtSeconds, float fullScale, int attackMs, int releaseMs, bool snapDown)
    {
        if (target > current)
        {
            var attackRate = fullScale / Math.Max(0.001f, attackMs / 1000f);
            return Math.Min(target, current + (attackRate * dtSeconds));
        }

        if (target < current)
        {
            if (snapDown)
            {
                return target;
            }

            var tau = Math.Max(0.001f, releaseMs / 1000f);
            var alpha = 1f - (float)Math.Exp(-dtSeconds / tau);
            var next = current + ((target - current) * alpha);

            return Math.Abs(next - target) < SettleEpsilon ? target : next;
        }

        return current;
    }

    /// <summary>
    /// Number of lit bricks (a bottom-up prefix) for a level whose top is at pixel row <paramref name="topLimit"/>.
    /// A brick lights when the level covers its centre; with <paramref name="hysteresisPx"/> &gt; 0 the next brick turns
    /// on only when the level is that far above its centre, and the top lit brick turns off only when the level is
    /// more than that far below its centre, so a level hovering at a boundary does not flicker. Idempotent: applying it
    /// again for the same level returns the same count.
    /// </summary>
    /// <param name="centresBottomUp">Pixel row of each brick centre, bottom brick first (rows decrease upward).</param>
    public static int StepLitBricks(int currentLit, IReadOnlyList<int> centresBottomUp, int topLimit, int hysteresisPx)
    {
        var count = centresBottomUp.Count;
        var n = currentLit < 0 ? 0 : (currentLit > count ? count : currentLit);

        while (n < count && centresBottomUp[n] - hysteresisPx >= topLimit)
        {
            n++;
        }

        while (n > 0 && centresBottomUp[n - 1] + hysteresisPx < topLimit)
        {
            n--;
        }

        return n;
    }

    /// <param name="decayPerSecond">Linear fall rate of the marker after the hold time, in display units per second.</param>
    public static void StepPeak(ref float peak, ref float holdLeftMs, float level, float dtMs, int holdMs, float decayPerSecond, float minimum)
    {
        if (level >= peak)
        {
            peak = level;
            holdLeftMs = Math.Max(0, holdMs);
            return;
        }

        if (holdLeftMs > 0)
        {
            holdLeftMs -= dtMs;
            if (holdLeftMs < 0)
            {
                holdLeftMs = 0;
            }

            return;
        }

        peak -= decayPerSecond * (dtMs / 1000f);
        if (peak < level)
        {
            peak = level;
        }

        if (peak < minimum)
        {
            peak = minimum;
        }
    }
}
