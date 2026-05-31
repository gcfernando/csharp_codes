// -----------------------------------------------------------------------------
//  Glass.Message — the open/close transition used when a dialog appears.
//
//  File        : GlassAnimation.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

namespace Glass;

/// <summary>
/// Controls how a <see cref="GlassDialog"/> animates as it opens and closes.
/// </summary>
public enum GlassAnimation
{
    /// <summary>Fade the window opacity in and out. This is the default.</summary>
    Fade,

    /// <summary>Fade in while sliding down slightly into place, and reverse on close.</summary>
    SlideDown,

    /// <summary>Fade in while growing from 90% to full size, and shrink back on close.</summary>
    Scale,

    /// <summary>Show and hide instantly, with no animation. Useful for automated tests.</summary>
    None,
}
