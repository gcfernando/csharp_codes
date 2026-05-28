using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassButton — premium custom-painted button with HC-mode and accessibility.
// ─────────────────────────────────────────────────────────────────────────
internal sealed class GlassButton : Button
{
    private readonly GlassTheme _theme;
    private bool _hovered, _focused, _pressed;

    public GlassButton(GlassTheme theme)
    {
        _theme = theme;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = theme.ButtonForeColor;
        Font = theme.ButtonFont;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint  |
            ControlStyles.UserPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)  { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)    { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnter(EventArgs e) { _focused = true;  Invalidate(); base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { _focused = false; Invalidate(); base.OnLeave(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        GlassDialog.SetQuality(g);

        // High-contrast: defer entirely to system rendering
        if (SystemInformation.HighContrast)
        {
            PaintHighContrast(g);
            return;
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var r    = _theme.ButtonCornerRadius;

        using var path = GlassDialog.RoundRect(rect, r);

        // ── Fill gradient ─────────────────────────────────────────────────
        Color fillTop, fillBot;
        if (_pressed)
        {
            fillTop = Darken(_theme.ButtonFillTop,    50);
            fillBot = Darken(_theme.ButtonFillBottom, 40);
        }
        else if (_hovered || _focused)
        {
            fillTop = Lighten(_theme.ButtonFillTop,    40);
            fillBot = Lighten(_theme.ButtonFillBottom, 30);
        }
        else
        {
            fillTop = _theme.ButtonFillTop;
            fillBot = _theme.ButtonFillBottom;
        }

        using var fill = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height),
            fillTop, fillBot, LinearGradientMode.Vertical);
        g.FillPath(fill, path);

        // ── Gloss sheen ───────────────────────────────────────────────────
        var glossH    = Math.Max(1, (Height - 2) / 2);
        var glossRect = new Rectangle(1, 1, Width - 2, glossH);
        using var gp  = GlassDialog.RoundRect(glossRect, Math.Max(1, r - 1));
        using var gl  = new LinearGradientBrush(glossRect,
            Color.FromArgb(_hovered ? 20 : 12, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical);
        g.FillPath(gl, gp);

        // ── Border ────────────────────────────────────────────────────────
        var borderAlpha = _pressed ? 255 : _hovered || _focused ? 210 : 110;
        using var pen = new Pen(Color.FromArgb(borderAlpha, _theme.BorderColor), 1f);
        g.DrawPath(pen, path);

        // ── Focus ring ────────────────────────────────────────────────────
        if (_focused && !_pressed)
        {
            var fr = new Rectangle(2, 2, Width - 5, Height - 5);
            using var fp  = GlassDialog.RoundRect(fr, Math.Max(1, r - 2));
            using var fp2 = new Pen(Color.FromArgb(150, _theme.AccentColor), 1f)
                            { DashStyle = DashStyle.Dot };
            g.DrawPath(fp2, fp);
        }

        // ── Text ──────────────────────────────────────────────────────────
        var flags = TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter   |
                    TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;

        TextRenderer.DrawText(g, Text, Font, ClientRectangle,
            _pressed ? _theme.AccentColor : ForeColor,
            Color.Transparent, flags);
    }

    private void PaintHighContrast(Graphics g)
    {
        var rect = ClientRectangle;
        g.FillRectangle(_pressed ? SystemBrushes.Highlight : SystemBrushes.ButtonFace, rect);
        using var pen = new Pen(SystemColors.ControlText, _focused ? 2f : 1f);
        g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);

        var flags = TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter   |
                    TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;

        TextRenderer.DrawText(g, Text, Font, rect,
            _pressed ? SystemColors.HighlightText : SystemColors.ControlText,
            Color.Transparent, flags);
    }

    // ── Color helpers ─────────────────────────────────────────────────────

    private static Color Lighten(Color c, int amount)
        => Color.FromArgb(c.A,
            Math.Min(255, c.R + amount),
            Math.Min(255, c.G + amount),
            Math.Min(255, c.B + amount));

    private static Color Darken(Color c, int amount)
        => Color.FromArgb(c.A,
            Math.Max(0, c.R - amount),
            Math.Max(0, c.G - amount),
            Math.Max(0, c.B - amount));
}
