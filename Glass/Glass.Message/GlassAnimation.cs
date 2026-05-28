namespace Glass;

/// <summary>Entrance and exit animation style for <see cref="GlassMessage"/> dialogs.</summary>
public enum GlassAnimation
{
    /// <summary>Smooth opacity fade-in / fade-out (default).</summary>
    Fade,

    /// <summary>Slides in from slightly above the final position while fading, reverses on close.</summary>
    SlideDown,

    /// <summary>Scales from 90 % to full size while fading in (currently aliases to Fade).</summary>
    Scale,

    /// <summary>No animation — dialog appears and disappears instantly.</summary>
    None,
}
