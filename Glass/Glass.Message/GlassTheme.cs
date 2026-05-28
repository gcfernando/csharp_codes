using System.Drawing;

namespace Glass;

// -------------------------------------------------------------------------
// GlassTheme — visual style settings.
// -------------------------------------------------------------------------
public sealed class GlassTheme
{
    // Deep midnight blue palette with electric sky-blue accents.
    // All hues share the same blue temperature — harmonious and cool on the eyes.
    public Color BackgroundTop { get; set; } = Color.FromArgb(15, 23, 42);   // #0F172A
    public Color BackgroundBottom { get; set; } = Color.FromArgb(7, 11, 22);   // #070B16
    public Color TitleBarTop { get; set; } = Color.FromArgb(22, 40, 68);   // #162844
    public Color TitleBarBottom { get; set; } = Color.FromArgb(13, 24, 42);   // #0D182A
    public Color BorderColor { get; set; } = Color.FromArgb(56, 189, 248);   // #38BDF8 sky
    public Color AccentColor { get; set; } = Color.FromArgb(14, 165, 233);   // #0EA5E9
    public Color TitleColor { get; set; } = Color.FromArgb(186, 230, 253);  // #BAE6FD
    public Color MessageColor { get; set; } = Color.FromArgb(203, 213, 225);  // #CBD5E1 slate
    public Color ButtonForeColor { get; set; } = Color.FromArgb(224, 242, 254);  // #E0F2FE

    public int CornerRadius { get; set; } = 8;
    public int ButtonCornerRadius { get; set; } = 5;

    public Font TitleFont { get; set; } = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
    public Font MessageFont { get; set; } = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    public Font ButtonFont { get; set; } = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

    public double Opacity { get; set; } = 0.97;

    public static GlassTheme Default { get; } = new GlassTheme();
}
