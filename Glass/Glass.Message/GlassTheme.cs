using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Glass;

/// <summary>
/// Visual style settings for a <see cref="GlassMessage"/> dialog.
/// Implements <see cref="IDisposable"/> — call <see cref="Dispose"/> when you
/// are done with a custom theme to free its GDI Font handles.
/// <b>Do not dispose the built-in static presets</b> (<see cref="Default"/>,
/// <see cref="Light"/>, <see cref="Mica"/>, etc.) — they are protected.
/// </summary>
public sealed class GlassTheme : IDisposable
{
    // ── Background ────────────────────────────────────────────────────────
    public Color BackgroundTop    { get; set; } = Color.FromArgb(15, 23, 42);
    public Color BackgroundBottom { get; set; } = Color.FromArgb(7, 11, 22);

    // ── Title bar ─────────────────────────────────────────────────────────
    public Color TitleBarTop    { get; set; } = Color.FromArgb(22, 40, 68);
    public Color TitleBarBottom { get; set; } = Color.FromArgb(13, 24, 42);

    // ── Accent & border ───────────────────────────────────────────────────
    public Color BorderColor { get; set; } = Color.FromArgb(56, 189, 248);
    public Color AccentColor { get; set; } = Color.FromArgb(14, 165, 233);

    // ── Text ──────────────────────────────────────────────────────────────
    public Color TitleColor   { get; set; } = Color.FromArgb(200, 235, 255);
    public Color MessageColor { get; set; } = Color.FromArgb(210, 220, 235);

    // ── Buttons ───────────────────────────────────────────────────────────
    public Color ButtonForeColor  { get; set; } = Color.FromArgb(224, 242, 254);
    public Color ButtonFillTop    { get; set; } = Color.FromArgb(20, 40, 72);
    public Color ButtonFillBottom { get; set; } = Color.FromArgb(11, 24, 46);

    // ── Controls ──────────────────────────────────────────────────────────
    public Color CheckBoxColor  { get; set; } = Color.FromArgb(56, 189, 248);
    public Color InputBackColor { get; set; } = Color.FromArgb(12, 20, 38);
    public Color InputForeColor { get; set; } = Color.FromArgb(210, 220, 235);

    // ── Shape ─────────────────────────────────────────────────────────────
    /// <summary>Dialog corner radius when rounded corners are enabled (96 DPI pixels).</summary>
    public int CornerRadius       { get; set; } = 8;
    /// <summary>Button corner radius when rounded corners are enabled (96 DPI pixels).</summary>
    public int ButtonCornerRadius { get; set; } = 5;

    // ── Typography ────────────────────────────────────────────────────────
    public Font TitleFont   { get; set; } = new Font("Segoe UI", 11f,   FontStyle.Bold,    GraphicsUnit.Point);
    public Font MessageFont { get; set; } = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    public Font ButtonFont  { get; set; } = new Font("Segoe UI", 10f,   FontStyle.Regular, GraphicsUnit.Point);

    // ── Window ────────────────────────────────────────────────────────────
    public double Opacity { get; set; } = 0.97;

    // ═══════════════════════════════════════════════════════════════════════
    // IDisposable — disposes the three Font GDI handles
    // ═══════════════════════════════════════════════════════════════════════
    private bool _disposed;

    // Preset guard: static presets set this flag so Dispose() is a no-op.
    private bool _isPreset;

    public void Dispose()
    {
        if (_disposed || _isPreset) return;
        _disposed = true;
        TitleFont?.Dispose();
        MessageFont?.Dispose();
        ButtonFont?.Dispose();
    }

    // ── Preset factory helper (marks the instance as protected) ───────────
    private static GlassTheme Preset(GlassTheme t) { t._isPreset = true; return t; }

    // ═══════════════════════════════════════════════════════════════════════
    // Built-in presets — safe to use indefinitely; never dispose these.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Deep midnight-blue dark theme (default).</summary>
    public static GlassTheme Default { get; } = Preset(new GlassTheme());

    /// <summary>Explicit dark theme — same palette as <see cref="Default"/>.</summary>
    public static GlassTheme Dark { get; } = Preset(new GlassTheme());

    /// <summary>Clean light theme for bright-mode environments.</summary>
    public static GlassTheme Light { get; } = Preset(new GlassTheme
    {
        BackgroundTop    = Color.FromArgb(245, 248, 255),
        BackgroundBottom = Color.FromArgb(230, 238, 252),
        TitleBarTop      = Color.FromArgb(215, 228, 252),
        TitleBarBottom   = Color.FromArgb(198, 216, 248),
        BorderColor      = Color.FromArgb(0,   120, 212),
        AccentColor      = Color.FromArgb(0,    90, 180),
        TitleColor       = Color.FromArgb(12,   20,  44),
        MessageColor     = Color.FromArgb(28,   38,  58),
        ButtonForeColor  = Color.FromArgb(12,   20,  44),
        ButtonFillTop    = Color.FromArgb(200,  220, 250),
        ButtonFillBottom = Color.FromArgb(172,  204, 242),
        CheckBoxColor    = Color.FromArgb(0,    120, 212),
        InputBackColor   = Color.FromArgb(255,  255, 255),
        InputForeColor   = Color.FromArgb(16,   24,  48),
        Opacity = 0.98,
    });

    /// <summary>Windows 11 Mica-inspired theme. DWM backdrop applied automatically when supported.</summary>
    public static GlassTheme Mica { get; } = Preset(new GlassTheme
    {
        BackgroundTop    = Color.FromArgb(24, 24, 36),
        BackgroundBottom = Color.FromArgb(14, 14, 24),
        TitleBarTop      = Color.FromArgb(30, 32, 52),
        TitleBarBottom   = Color.FromArgb(20, 22, 40),
        BorderColor      = Color.FromArgb(90,  110, 200),
        AccentColor      = Color.FromArgb(60,  100, 220),
        TitleColor       = Color.FromArgb(205, 215, 245),
        MessageColor     = Color.FromArgb(182, 192, 222),
        ButtonForeColor  = Color.FromArgb(210, 220, 250),
        ButtonFillTop    = Color.FromArgb(32,  42,  78),
        ButtonFillBottom = Color.FromArgb(18,  26,  54),
        CheckBoxColor    = Color.FromArgb(90,  120, 220),
        InputBackColor   = Color.FromArgb(18,  20,  36),
        InputForeColor   = Color.FromArgb(182, 192, 222),
        Opacity = 0.88,
    });

    /// <summary>High Contrast theme using <see cref="SystemColors"/>. Auto-selected by <see cref="AutoDetect"/>.</summary>
    public static GlassTheme HighContrast { get; } = BuildHighContrast();

    /// <summary>Classic Windows "gray" system-chrome look (sharp rectangular corners).</summary>
    public static GlassTheme WindowsClassic { get; } = Preset(new GlassTheme
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
    });

    // ═══════════════════════════════════════════════════════════════════════
    // Auto-detection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Returns the preset best matching current Windows appearance (HighContrast → Dark → Light).</summary>
    public static GlassTheme AutoDetect()
    {
        if (SystemInformation.HighContrast) return HighContrast;
        return IsSystemDark() ? Dark : Light;
    }

    /// <summary>Returns <c>true</c> when Windows is in dark app mode.</summary>
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

    private static GlassTheme BuildHighContrast() => Preset(new GlassTheme
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
    });
}
