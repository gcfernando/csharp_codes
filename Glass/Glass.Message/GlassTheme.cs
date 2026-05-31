using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Glass;

public sealed class GlassTheme : IDisposable
{
    public Color BackgroundTop    { get; set; } = Color.FromArgb(15, 23, 42);
    public Color BackgroundBottom { get; set; } = Color.FromArgb(7, 11, 22);

    public Color TitleBarTop    { get; set; } = Color.FromArgb(22, 40, 68);
    public Color TitleBarBottom { get; set; } = Color.FromArgb(13, 24, 42);

    public Color BorderColor { get; set; } = Color.FromArgb(56, 189, 248);
    public Color AccentColor { get; set; } = Color.FromArgb(14, 165, 233);

    public Color TitleColor   { get; set; } = Color.FromArgb(200, 235, 255);
    public Color MessageColor { get; set; } = Color.FromArgb(210, 220, 235);

    public Color ButtonForeColor  { get; set; } = Color.FromArgb(224, 242, 254);
    public Color ButtonFillTop    { get; set; } = Color.FromArgb(20, 40, 72);
    public Color ButtonFillBottom { get; set; } = Color.FromArgb(11, 24, 46);

    public Color CheckBoxColor  { get; set; } = Color.FromArgb(56, 189, 248);
    public Color InputBackColor { get; set; } = Color.FromArgb(12, 20, 38);
    public Color InputForeColor { get; set; } = Color.FromArgb(210, 220, 235);

    public int CornerRadius       { get; set; } = 8;
    public int ButtonCornerRadius { get; set; } = 5;

    public Font TitleFont   { get; set; } = new Font("Segoe UI", 11f,   FontStyle.Bold,    GraphicsUnit.Point);
    public Font MessageFont { get; set; } = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    public Font ButtonFont  { get; set; } = new Font("Segoe UI", 10f,   FontStyle.Regular, GraphicsUnit.Point);

    public double Opacity { get; set; } = 0.97;

    private bool _disposed;

    private bool _isPreset;

    public void Dispose()
    {
        if (_disposed || _isPreset) return;
        _disposed = true;
        TitleFont?.Dispose();
        MessageFont?.Dispose();
        ButtonFont?.Dispose();
    }

    private static GlassTheme Preset(GlassTheme t) { t._isPreset = true; return t; }


    public static GlassTheme Default { get; } = Preset(new GlassTheme());

    public static GlassTheme Dark { get; } = Preset(new GlassTheme());

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

    public static GlassTheme HighContrast { get; } = BuildHighContrast();

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


    public static GlassTheme AutoDetect()
    {
        if (SystemInformation.HighContrast) return HighContrast;
        return IsSystemDark() ? Dark : Light;
    }

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
