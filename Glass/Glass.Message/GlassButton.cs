using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassButton — premium custom-painted button.
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
        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }
    protected override void OnEnter(EventArgs e) { _focused = true; Invalidate(); base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { _focused = false; Invalidate(); base.OnLeave(e); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        GlassDialog.SetQuality(g);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var r = _theme.ButtonCornerRadius;

        using (var path = GlassDialog.RoundRect(rect, r))
        {
            Color fillTop, fillBot;
            if (_pressed)
            {
                fillTop = Color.FromArgb(10, 60, 105);
                fillBot = Color.FromArgb(6, 36, 68);
            }
            else if (_hovered || _focused)
            {
                fillTop = Color.FromArgb(22, 88, 148);
                fillBot = Color.FromArgb(13, 52, 96);
            }
            else
            {
                fillTop = Color.FromArgb(18, 38, 68);
                fillBot = Color.FromArgb(10, 22, 42);
            }

            using (var fill = new LinearGradientBrush(
                       new Rectangle(0, 0, Width, Height),
                       fillTop, fillBot, LinearGradientMode.Vertical))
            {
                g.FillPath(fill, path);
            }

            var glossH = Math.Max(1, (Height - 2) / 2);
            var glossRect = new Rectangle(1, 1, Width - 2, glossH);
            using (var gp = GlassDialog.RoundRect(glossRect, Math.Max(1, r - 1)))
            using (var gl = new LinearGradientBrush(
                       glossRect,
                       Color.FromArgb(_hovered ? 20 : 12, 255, 255, 255),
                       Color.FromArgb(0, 255, 255, 255),
                       LinearGradientMode.Vertical))
            {
                g.FillPath(gl, gp);
            }

            var borderAlpha = _pressed ? 255 : _hovered || _focused ? 210 : 110;
            using (var pen = new Pen(Color.FromArgb(borderAlpha, _theme.BorderColor), 1f))
            {
                g.DrawPath(pen, path);
            }

            if (_focused && !_pressed)
            {
                var fr = new Rectangle(2, 2, Width - 5, Height - 5);
                using var fp = GlassDialog.RoundRect(fr, Math.Max(1, r - 2));
                using var pen = new Pen(Color.FromArgb(150, _theme.AccentColor), 1f)
                { DashStyle = DashStyle.Dot };
                g.DrawPath(pen, fp);
            }
        }

        var flags = TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues)
        {
            flags |= TextFormatFlags.HidePrefix;
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle,
            _pressed ? _theme.AccentColor : ForeColor,
            Color.Transparent, flags);
    }
}
