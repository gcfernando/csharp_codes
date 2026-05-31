// -----------------------------------------------------------------------------
//  Glass.Message — the colour, font, and shape palette a dialog paints itself
//  with. Ships with a handful of ready-made presets and can auto-detect the
//  user's current Windows light/dark/high-contrast preference.
//
//  File        : GlassTheme.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Glass;

/// <summary>
/// Describes how a <see cref="GlassDialog"/> (and <see cref="GlassToast"/>) looks:
/// gradients, accent and text colours, corner radii, fonts, and window opacity.
/// Use a built-in preset (<see cref="Dark"/>, <see cref="Light"/>, <see cref="Mica"/>,
/// …) or construct your own.
/// </summary>
public sealed class GlassTheme : IDisposable
{
    // Window body gradient (top → bottom).
    public Color BackgroundTop { get; set; } = Color.FromArgb(15, 23, 42);
    public Color BackgroundBottom { get; set; } = Color.FromArgb(7, 11, 22);

    // Title-bar gradient (top → bottom).
    public Color TitleBarTop { get; set; } = Color.FromArgb(22, 40, 68);
    public Color TitleBarBottom { get; set; } = Color.FromArgb(13, 24, 42);

    // Window edge glow and the accent used for focus, progress, and highlights.
    public Color BorderColor { get; set; } = Color.FromArgb(56, 189, 248);
    public Color AccentColor { get; set; } = Color.FromArgb(14, 165, 233);

    public Color TitleColor { get; set; } = Color.FromArgb(200, 235, 255);
    public Color MessageColor { get; set; } = Color.FromArgb(210, 220, 235);

    // Button text plus the top/bottom of its glossy fill gradient.
    public Color ButtonForeColor { get; set; } = Color.FromArgb(224, 242, 254);
    public Color ButtonFillTop { get; set; } = Color.FromArgb(20, 40, 72);
    public Color ButtonFillBottom { get; set; } = Color.FromArgb(11, 24, 46);

    public Color CheckBoxColor { get; set; } = Color.FromArgb(56, 189, 248);
    public Color InputBackColor { get; set; } = Color.FromArgb(12, 20, 38);
    public Color InputForeColor { get; set; } = Color.FromArgb(210, 220, 235);

    /// <summary>Corner radius of the window (in device-independent pixels). 0 = square.</summary>
    public int CornerRadius { get; set; } = 8;
    /// <summary>Corner radius of the buttons. 0 = square.</summary>
    public int ButtonCornerRadius { get; set; } = 5;

    public Font TitleFont { get; set; } = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
    public Font MessageFont { get; set; } = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    public Font ButtonFont { get; set; } = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>Base window opacity. Mica/Acrylic backdrops may reduce this further at runtime.</summary>
    public double Opacity { get; set; } = 0.97;

    private bool _disposed;

    // Presets are shared singletons, so their fonts must outlive any one dialog.
    // This flag makes Dispose() a no-op for them while still letting user-created
    // themes free their fonts normally.
    private bool _isPreset;

    /// <summary>
    /// Releases the theme's fonts. Safe to call more than once, and intentionally
    /// does nothing for the shared built-in presets (see <see cref="_isPreset"/>).
    /// </summary>
    public void Dispose()
    {
        if (_disposed || _isPreset)
        {
            return;
        }

        _disposed = true;
        TitleFont?.Dispose();
        MessageFont?.Dispose();
        ButtonFont?.Dispose();
    }

    // Marks a theme as a shared preset so it is exempt from disposal.
    private static GlassTheme Preset(GlassTheme t) { t._isPreset = true; return t; }

    /// <summary>The default palette (the dark blue theme).</summary>
    public static GlassTheme Default { get; } = Preset(new GlassTheme());

    /// <summary>A dark blue palette.</summary>
    public static GlassTheme Dark { get; } = Preset(new GlassTheme());

    /// <summary>A bright palette tuned for light mode.</summary>
    public static GlassTheme Light { get; } = Preset(new GlassTheme
    {
        BackgroundTop = Color.FromArgb(245, 248, 255),
        BackgroundBottom = Color.FromArgb(230, 238, 252),
        TitleBarTop = Color.FromArgb(215, 228, 252),
        TitleBarBottom = Color.FromArgb(198, 216, 248),
        BorderColor = Color.FromArgb(0, 120, 212),
        AccentColor = Color.FromArgb(0, 90, 180),
        TitleColor = Color.FromArgb(12, 20, 44),
        MessageColor = Color.FromArgb(28, 38, 58),
        ButtonForeColor = Color.FromArgb(12, 20, 44),
        ButtonFillTop = Color.FromArgb(200, 220, 250),
        ButtonFillBottom = Color.FromArgb(172, 204, 242),
        CheckBoxColor = Color.FromArgb(0, 120, 212),
        InputBackColor = Color.FromArgb(255, 255, 255),
        InputForeColor = Color.FromArgb(16, 24, 48),
        Opacity = 0.98,
    });

    /// <summary>A neutral palette tuned to sit well on a Windows 11 Mica backdrop.</summary>
    public static GlassTheme Mica { get; } = Preset(new GlassTheme
    {
        BackgroundTop = Color.FromArgb(24, 24, 36),
        BackgroundBottom = Color.FromArgb(14, 14, 24),
        TitleBarTop = Color.FromArgb(30, 32, 52),
        TitleBarBottom = Color.FromArgb(20, 22, 40),
        BorderColor = Color.FromArgb(90, 110, 200),
        AccentColor = Color.FromArgb(60, 100, 220),
        TitleColor = Color.FromArgb(205, 215, 245),
        MessageColor = Color.FromArgb(182, 192, 222),
        ButtonForeColor = Color.FromArgb(210, 220, 250),
        ButtonFillTop = Color.FromArgb(32, 42, 78),
        ButtonFillBottom = Color.FromArgb(18, 26, 54),
        CheckBoxColor = Color.FromArgb(90, 120, 220),
        InputBackColor = Color.FromArgb(18, 20, 36),
        InputForeColor = Color.FromArgb(182, 192, 222),
        Opacity = 0.88,
    });

    /// <summary>A palette sourced entirely from Windows system colours for accessibility.</summary>
    public static GlassTheme HighContrast { get; } = BuildHighContrast();

    /// <summary>A square-cornered, opaque palette matching traditional Windows chrome.</summary>
    public static GlassTheme WindowsClassic { get; } = Preset(new GlassTheme
    {
        BackgroundTop = SystemColors.Control,
        BackgroundBottom = SystemColors.ControlDark,
        TitleBarTop = SystemColors.ActiveCaption,
        TitleBarBottom = SystemColors.GradientActiveCaption,
        BorderColor = SystemColors.ActiveBorder,
        AccentColor = SystemColors.Highlight,
        TitleColor = SystemColors.ActiveCaptionText,
        MessageColor = SystemColors.ControlText,
        ButtonForeColor = SystemColors.ControlText,
        ButtonFillTop = SystemColors.ButtonHighlight,
        ButtonFillBottom = SystemColors.ButtonFace,
        CheckBoxColor = SystemColors.Highlight,
        InputBackColor = SystemColors.Window,
        InputForeColor = SystemColors.WindowText,
        CornerRadius = 0,
        ButtonCornerRadius = 0,
        Opacity = 1.0,
    });

    /// <summary>
    /// Picks the most appropriate preset for the current session: high contrast if
    /// it is enabled, otherwise the dark or light palette to match Windows.
    /// </summary>
    public static GlassTheme AutoDetect() => SystemInformation.HighContrast ? HighContrast : IsSystemDark() ? Dark : Light;

    /// <summary>
    /// Reads the user's "Apps" colour-mode preference from the registry. Returns
    /// <c>true</c> for dark mode and defaults to dark if the value can't be read.
    /// </summary>
    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }

    // High contrast is built via a method (rather than an inline initializer) only
    // so the property initializer order stays easy to read above.
    private static GlassTheme BuildHighContrast() => Preset(new GlassTheme
    {
        BackgroundTop = SystemColors.Window,
        BackgroundBottom = SystemColors.Window,
        TitleBarTop = SystemColors.ActiveCaption,
        TitleBarBottom = SystemColors.ActiveCaption,
        BorderColor = SystemColors.ActiveBorder,
        AccentColor = SystemColors.Highlight,
        TitleColor = SystemColors.ActiveCaptionText,
        MessageColor = SystemColors.WindowText,
        ButtonForeColor = SystemColors.ControlText,
        ButtonFillTop = SystemColors.ButtonFace,
        ButtonFillBottom = SystemColors.ButtonFace,
        CheckBoxColor = SystemColors.Highlight,
        InputBackColor = SystemColors.Window,
        InputForeColor = SystemColors.WindowText,
        CornerRadius = 0,
        ButtonCornerRadius = 0,
        Opacity = 1.0,
    });
}
