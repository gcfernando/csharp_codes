using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassButton — smooth-transition, cached-path premium button.
// ─────────────────────────────────────────────────────────────────────────
internal sealed class GlassButton : Button
{
    private readonly GlassTheme _theme;
    private bool  _hovered, _focused, _pressed;
    private float _hoverT;       // 0 = resting, 1 = fully hovered/focused
    private bool  _targetHover;
    private System.Windows.Forms.Timer _transTimer;
    private GraphicsPath _path;
    private Size         _pathSize;

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

    // ── State transitions ─────────────────────────────────────────────────

    protected override void OnMouseEnter(EventArgs e) { _hovered = true;  StartTransition(true);         base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; StartTransition(_focused);     base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true;  Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e)   { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnter(EventArgs e) { _focused = true;  StartTransition(true);      base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { _focused = false; StartTransition(_hovered);  base.OnLeave(e); }

    protected override void OnResize(EventArgs e)
    {
        _path?.Dispose();
        _path = null;
        base.OnResize(e);
    }

    private void StartTransition(bool toHover)
    {
        _targetHover = toHover;
        if (_transTimer == null)
        {
            _transTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _transTimer.Tick += (s, ev) =>
            {
                _hoverT = _targetHover
                    ? Math.Min(1f, _hoverT + 0.12f)
                    : Math.Max(0f, _hoverT - 0.12f);
                Invalidate();
                if (_hoverT <= 0f || _hoverT >= 1f)
                    _transTimer.Stop();
            };
        }
        _transTimer.Start();
    }

    // ── Cached rounded path ───────────────────────────────────────────────

    private GraphicsPath CachedPath
    {
        get
        {
            if (_path == null || _pathSize != Size)
            {
                _path?.Dispose();
                _pathSize = Size;
                _path = GlassDialog.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), _theme.ButtonCornerRadius);
            }
            return _path;
        }
    }

    // ── Paint ─────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        GlassDialog.SetQuality(g);

        if (SystemInformation.HighContrast) { PaintHighContrast(g); return; }

        var path = CachedPath;
        var r    = _theme.ButtonCornerRadius;
        var t    = _pressed ? 0f : _hoverT;

        // ── Fill gradient (smooth interpolation) ──────────────────────────
        Color fillTop, fillBot;
        if (_pressed)
        {
            fillTop = Darken(_theme.ButtonFillTop,    50);
            fillBot = Darken(_theme.ButtonFillBottom, 40);
        }
        else
        {
            fillTop = Blend(_theme.ButtonFillTop,    Lighten(_theme.ButtonFillTop,    40), t);
            fillBot = Blend(_theme.ButtonFillBottom, Lighten(_theme.ButtonFillBottom, 30), t);
        }

        using (var fill = new LinearGradientBrush(
            new Rectangle(0, 0, Width, Height), fillTop, fillBot, LinearGradientMode.Vertical))
            g.FillPath(fill, path);

        // ── Gloss shelf (top third, clipped to shape) ─────────────────────
        var glossH    = Math.Max(1, Height / 3);
        var glossRect = new Rectangle(1, 1, Width - 2, glossH);
        using (var gp = GlassDialog.RoundRect(glossRect, Math.Max(1, r - 1)))
        using (var gl = new LinearGradientBrush(glossRect,
            Color.FromArgb((int)(15 + t * 30), 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.SetClip(path);
            g.FillPath(gl, gp);
            g.ResetClip();
        }

        // ── Border: outer glow + crisp edge ───────────────────────────────
        using (var glowPen = new Pen(Color.FromArgb((int)(30 + t * 40), _theme.BorderColor), 3f))
            g.DrawPath(glowPen, path);
        using (var edgePen = new Pen(Color.FromArgb(_pressed ? 200 : (int)(90 + t * 120), _theme.BorderColor), 1f))
            g.DrawPath(edgePen, path);

        // ── Focus ring ────────────────────────────────────────────────────
        if (_focused && !_pressed)
        {
            var fr = new Rectangle(2, 2, Width - 5, Height - 5);
            using var fp  = GlassDialog.RoundRect(fr, Math.Max(1, r - 2));
            using var fp2 = new Pen(Color.FromArgb(150, _theme.AccentColor), 1f) { DashStyle = DashStyle.Dot };
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

    private static Color Blend(Color a, Color b, float t)
        => Color.FromArgb(
            Math.Max(0, Math.Min(255, (int)(a.A + (b.A - a.A) * t))),
            Math.Max(0, Math.Min(255, (int)(a.R + (b.R - a.R) * t))),
            Math.Max(0, Math.Min(255, (int)(a.G + (b.G - a.G) * t))),
            Math.Max(0, Math.Min(255, (int)(a.B + (b.B - a.B) * t))));

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transTimer?.Stop();
            _transTimer?.Dispose();
            _path?.Dispose();
        }
        base.Dispose(disposing);
    }
}
