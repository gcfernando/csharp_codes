using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Glass;

/// <summary>Visual style settings for a <see cref="GlassMessage"/> dialog.</summary>
public sealed class GlassTheme
{
    // ── Background ────────────────────────────────────────────────────────
    /// <summary>Top colour of the body gradient.</summary>
    public Color BackgroundTop    { get; set; } = Color.FromArgb(15, 23, 42);
    /// <summary>Bottom colour of the body gradient.</summary>
    public Color BackgroundBottom { get; set; } = Color.FromArgb(7, 11, 22);

    // ── Title bar ─────────────────────────────────────────────────────────
    /// <summary>Top colour of the title-bar gradient.</summary>
    public Color TitleBarTop      { get; set; } = Color.FromArgb(22, 40, 68);
    /// <summary>Bottom colour of the title-bar gradient.</summary>
    public Color TitleBarBottom   { get; set; } = Color.FromArgb(13, 24, 42);

    // ── Accent & border ───────────────────────────────────────────────────
    /// <summary>Glow border colour.</summary>
    public Color BorderColor      { get; set; } = Color.FromArgb(56, 189, 248);
    /// <summary>Interactive accent colour (focus rings, links, countdown arc).</summary>
    public Color AccentColor      { get; set; } = Color.FromArgb(14, 165, 233);

    // ── Text ──────────────────────────────────────────────────────────────
    /// <summary>Title text colour.</summary>
    public Color TitleColor       { get; set; } = Color.FromArgb(186, 230, 253);
    /// <summary>Message body text colour.</summary>
    public Color MessageColor     { get; set; } = Color.FromArgb(203, 213, 225);

    // ── Buttons ───────────────────────────────────────────────────────────
    /// <summary>Button label colour.</summary>
    public Color ButtonForeColor  { get; set; } = Color.FromArgb(224, 242, 254);
    /// <summary>Top fill colour for the button gradient (resting state).</summary>
    public Color ButtonFillTop    { get; set; } = Color.FromArgb(18, 38, 68);
    /// <summary>Bottom fill colour for the button gradient (resting state).</summary>
    public Color ButtonFillBottom { get; set; } = Color.FromArgb(10, 22, 42);

    // ── Controls ──────────────────────────────────────────────────────────
    /// <summary>Checkbox/toggle accent colour.</summary>
    public Color CheckBoxColor    { get; set; } = Color.FromArgb(56, 189, 248);
    /// <summary>Background colour for text input fields.</summary>
    public Color InputBackColor   { get; set; } = Color.FromArgb(14, 22, 40);
    /// <summary>Text colour for text input fields.</summary>
    public Color InputForeColor   { get; set; } = Color.FromArgb(203, 213, 225);

    // ── Shape ─────────────────────────────────────────────────────────────
    /// <summary>Corner radius of the dialog window in pixels (at 96 DPI).</summary>
    public int CornerRadius       { get; set; } = 8;
    /// <summary>Corner radius of each button in pixels (at 96 DPI).</summary>
    public int ButtonCornerRadius { get; set; } = 5;

    // ── Typography ────────────────────────────────────────────────────────
    /// <summary>Font used for the dialog title.</summary>
    public Font TitleFont   { get; set; } = new Font("Segoe UI", 11f,   FontStyle.Bold,    GraphicsUnit.Point);
    /// <summary>Font used for the message body.</summary>
    public Font MessageFont { get; set; } = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    /// <summary>Font used for button labels.</summary>
    public Font ButtonFont  { get; set; } = new Font("Segoe UI", 9.5f,  FontStyle.Regular, GraphicsUnit.Point);

    // ── Window ────────────────────────────────────────────────────────────
    /// <summary>Target window opacity (0.0 – 1.0).</summary>
    public double Opacity { get; set; } = 0.97;

    // ═══════════════════════════════════════════════════════════════════════
    // Built-in presets
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Original deep midnight-blue dark theme (backward-compatible default).</summary>
    public static GlassTheme Default { get; } = new GlassTheme();

    /// <summary>Explicit dark theme — same palette as <see cref="Default"/>.</summary>
    public static GlassTheme Dark { get; } = new GlassTheme();

    /// <summary>Clean light theme for bright-mode environments.</summary>
    public static GlassTheme Light { get; } = new GlassTheme
    {
        BackgroundTop    = Color.FromArgb(245, 248, 255),
        BackgroundBottom = Color.FromArgb(232, 238, 252),
        TitleBarTop      = Color.FromArgb(215, 228, 252),
        TitleBarBottom   = Color.FromArgb(200, 216, 248),
        BorderColor      = Color.FromArgb(0,   120, 212),
        AccentColor      = Color.FromArgb(0,    90, 180),
        TitleColor       = Color.FromArgb(16,   24,  48),
        MessageColor     = Color.FromArgb(30,   40,  60),
        ButtonForeColor  = Color.FromArgb(16,   24,  48),
        ButtonFillTop    = Color.FromArgb(200,  220, 250),
        ButtonFillBottom = Color.FromArgb(175,  205, 242),
        CheckBoxColor    = Color.FromArgb(0,    120, 212),
        InputBackColor   = Color.FromArgb(255,  255, 255),
        InputForeColor   = Color.FromArgb(16,   24,  48),
        Opacity = 0.98,
    };

    /// <summary>
    /// Windows 11 Mica-inspired theme with a cooler, more desaturated palette.
    /// The DWM Mica / Acrylic backdrop is applied automatically when supported.
    /// </summary>
    public static GlassTheme Mica { get; } = new GlassTheme
    {
        BackgroundTop    = Color.FromArgb(24, 24, 36),
        BackgroundBottom = Color.FromArgb(14, 14, 24),
        TitleBarTop      = Color.FromArgb(30, 32, 52),
        TitleBarBottom   = Color.FromArgb(20, 22, 40),
        BorderColor      = Color.FromArgb(90,  110, 200),
        AccentColor      = Color.FromArgb(60,  100, 220),
        TitleColor       = Color.FromArgb(200, 210, 240),
        MessageColor     = Color.FromArgb(180, 190, 220),
        ButtonForeColor  = Color.FromArgb(210, 220, 250),
        ButtonFillTop    = Color.FromArgb(32,  42,  78),
        ButtonFillBottom = Color.FromArgb(18,  26,  54),
        CheckBoxColor    = Color.FromArgb(90,  120, 220),
        InputBackColor   = Color.FromArgb(18,  20,  36),
        InputForeColor   = Color.FromArgb(180, 190, 220),
        Opacity = 0.88,
    };

    /// <summary>
    /// Windows High Contrast theme using <see cref="SystemColors"/> exclusively.
    /// Automatically selected by <see cref="AutoDetect"/> when High Contrast is active.
    /// </summary>
    public static GlassTheme HighContrast { get; } = BuildHighContrast();

    /// <summary>Classic Windows "gray" system-chrome look (no rounded corners).</summary>
    public static GlassTheme WindowsClassic { get; } = new GlassTheme
    {
        BackgroundTop    = SystemColors.Control,
        BackgroundBottom = SystemColors.ControlDark,
        TitleBarTop      = SystemColors.ActiveCaption,
        TitleBarBottom   = SystemColors.GradientActiveCaption,
        BorderColor      = SystemColors.ActiveBorder,
        AccentColor      = SystemColors.Highlight,
        TitleColor       = SystemColors.ActiveCaptionText,
        MessageColor     = SystemColors.ControlText,
        ButtonForeColor  = SystemColors.ControlText,
        ButtonFillTop    = SystemColors.ButtonHighlight,
        ButtonFillBottom = SystemColors.ButtonFace,
        CheckBoxColor    = SystemColors.Highlight,
        InputBackColor   = SystemColors.Window,
        InputForeColor   = SystemColors.WindowText,
        CornerRadius       = 0,
        ButtonCornerRadius = 0,
        Opacity = 1.0,
    };

    // ═══════════════════════════════════════════════════════════════════════
    // Auto-detection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the preset that best matches the current Windows appearance settings:
    /// <see cref="HighContrast"/> → <see cref="Dark"/> → <see cref="Light"/>.
    /// Falls back to <see cref="Default"/> on older OS versions.
    /// </summary>
    public static GlassTheme AutoDetect()
    {
        if (SystemInformation.HighContrast) return HighContrast;
        return IsSystemDark() ? Dark : Light;
    }

    /// <summary>
    /// Returns <c>true</c> when Windows is configured to use a dark app theme
    /// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme = 0</c>).
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

    // ── Helpers ───────────────────────────────────────────────────────────

    private static GlassTheme BuildHighContrast() => new GlassTheme
    {
        BackgroundTop    = SystemColors.Window,
        BackgroundBottom = SystemColors.Window,
        TitleBarTop      = SystemColors.ActiveCaption,
        TitleBarBottom   = SystemColors.ActiveCaption,
        BorderColor      = SystemColors.ActiveBorder,
        AccentColor      = SystemColors.Highlight,
        TitleColor       = SystemColors.ActiveCaptionText,
        MessageColor     = SystemColors.WindowText,
        ButtonForeColor  = SystemColors.ControlText,
        ButtonFillTop    = SystemColors.ButtonFace,
        ButtonFillBottom = SystemColors.ButtonFace,
        CheckBoxColor    = SystemColors.Highlight,
        InputBackColor   = SystemColors.Window,
        InputForeColor   = SystemColors.WindowText,
        CornerRadius       = 0,
        ButtonCornerRadius = 0,
        Opacity = 1.0,
    };
}
