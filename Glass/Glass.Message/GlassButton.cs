// -----------------------------------------------------------------------------
//  Glass.Message — the owner-drawn push button used inside a dialog. It renders a
//  glossy, themed pill with an animated hover glow and a high-contrast fallback,
//  caching its brushes and pens so repainting during animation stays cheap.
//
//  File        : GlassButton.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Glass;

/// <summary>
/// A fully custom-painted <see cref="Button"/> matching the Glass aesthetic.
/// </summary>
internal sealed class GlassButton : Button
{
    private readonly GlassTheme _theme;
    private readonly int _cornerRadius;
    private bool _hovered, _focused, _pressed;

    // Hover animation: _hoverT eases 0→1 toward _targetHover, driven by a timer.
    private float _hoverT;
    private bool _targetHover;
    private System.Windows.Forms.Timer _transTimer;

    // Cached GDI+ resources. These are rebuilt only when the input that shapes
    // them changes (size, hover amount, alpha) to avoid per-frame allocations.
    private GraphicsPath _path;
    private Size _pathSize;
    private LinearGradientBrush _fillBrush;
    private float _fillBrushT = float.NaN;
    private bool _fillBrushPressed = false;
    private Pen _cachedGlowPen;
    private int _cachedGlowA = -1;
    private Pen _cachedEdgePen;
    private int _cachedEdgeA = -1;

    internal GlassButton(GlassTheme theme, int cornerRadius)
    {
        _theme = theme;
        _cornerRadius = Math.Max(0, cornerRadius);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = theme.ButtonForeColor;
        Font = theme.ButtonFont;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.PushButton;
        // We paint every pixel ourselves, double-buffered, to keep the gloss and
        // glow flicker-free.
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.Opaque, true);
    }

    // Honour an access key (&O for "&OK") even though the button is owner-drawn.
    protected override bool ProcessMnemonic(char charCode)
    {
        if (CanSelect && IsMnemonic(charCode, Text))
        {
            _ = Focus();
            PerformClick();
            return true;
        }
        return base.ProcessMnemonic(charCode);
    }

    // Hover/focus both feed the same glow animation; the button keeps glowing
    // while focused even after the mouse leaves.
    protected override void OnMouseEnter(EventArgs e) { _hovered = true; StartTransition(true); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; StartTransition(_focused); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnter(EventArgs e) { _focused = true; StartTransition(true); base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { _focused = false; StartTransition(_hovered); base.OnLeave(e); }

    protected override void OnResize(EventArgs e)
    {
        // Size-dependent caches must be dropped when the button is resized.
        _path?.Dispose();
        _path = null;
        _fillBrush?.Dispose();
        _fillBrush = null;
        _fillBrushT = float.NaN;
        base.OnResize(e);
    }

    // Steps _hoverT toward the target by a fixed amount each tick and stops once
    // it reaches either end, so an idle button burns no timer cycles.
    private void StartTransition(bool toHover)
    {
        _targetHover = toHover;
        if (_transTimer == null)
        {
            _transTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _transTimer.Tick += (s, ev) =>
            {
                _hoverT = _targetHover
                    ? Math.Min(1f, _hoverT + 0.14f)
                    : Math.Max(0f, _hoverT - 0.14f);
                Invalidate();
                if (_hoverT is <= 0f or >= 1f)
                {
                    _transTimer.Stop();
                }
            };
        }
        _transTimer.Start();
    }

    // Rounded-rectangle outline, rebuilt only when the control size changes.
    private GraphicsPath CachedPath
    {
        get
        {
            if (_path == null || _pathSize != Size)
            {
                _path?.Dispose();
                _pathSize = Size;
                _path = GlassDialog.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius);
            }
            return _path;
        }
    }

    // Vertical fill gradient. Pressed looks darker; otherwise the colours lighten
    // with the hover amount. Reused unless the hover amount or pressed state moved
    // enough to matter (a small tolerance avoids rebuilding on every sub-pixel step).
    private LinearGradientBrush GetFillBrush(float t, bool pressed)
    {
        if (_fillBrush != null
            && Math.Abs(_fillBrushT - t) <= 0.02f
            && _fillBrushPressed == pressed)
        {
            return _fillBrush;
        }

        _fillBrush?.Dispose();

        Color top, bot;
        if (pressed)
        {
            top = Darken(_theme.ButtonFillTop, 55);
            bot = Darken(_theme.ButtonFillBottom, 45);
        }
        else
        {
            top = Blend(_theme.ButtonFillTop, Lighten(_theme.ButtonFillTop, 45), t);
            bot = Blend(_theme.ButtonFillBottom, Lighten(_theme.ButtonFillBottom, 35), t);
        }

        _fillBrush = new LinearGradientBrush(
            new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
            top, bot, LinearGradientMode.Vertical);
        _fillBrushT = t;
        _fillBrushPressed = pressed;
        return _fillBrush;
    }

    // The glow (wide, soft) and edge (thin, crisp) pens are keyed purely on their
    // alpha, which is the only thing that varies frame to frame.
    private Pen GetGlowPen(int alpha)
    {
        if (_cachedGlowA != alpha)
        {
            _cachedGlowPen?.Dispose();
            _cachedGlowPen = new Pen(Color.FromArgb(alpha, _theme.BorderColor), 3f);
            _cachedGlowA = alpha;
        }
        return _cachedGlowPen;
    }

    private Pen GetEdgePen(int alpha)
    {
        if (_cachedEdgeA != alpha)
        {
            _cachedEdgePen?.Dispose();
            _cachedEdgePen = new Pen(Color.FromArgb(alpha, _theme.BorderColor), 1f);
            _cachedEdgeA = alpha;
        }
        return _cachedEdgePen;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        GlassDialog.SetQuality(g);

        // Fill behind the rounded corners with the dialog's gradient so the button
        // blends into the window instead of showing a transparent-key fringe.
        GlassDialog.PaintThemedBackground(g, this, _theme);

        if (SystemInformation.HighContrast)
        { PaintHighContrast(g); return; }

        var path = CachedPath;
        // While pressed we drop the hover glow to 0 for a flatter "pushed" look.
        var t = _pressed ? 0f : _hoverT;

        g.FillPath(GetFillBrush(t, _pressed), path);

        // Pressed: a short inner top shadow to suggest the surface has sunk.
        if (_pressed)
        {
            var shadowH = Math.Max(1, Height / 4);
            using var shadow = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), shadowH),
                Color.FromArgb(45, 0, 0, 0), Color.FromArgb(0, 0, 0, 0),
                LinearGradientMode.Vertical);
            g.SetClip(path);
            g.FillRectangle(shadow, 0, 0, Width, shadowH);
            g.ResetClip();
        }

        // Glossy highlight across the top third, brightening on hover.
        var glossH = Math.Max(1, Height / 3);
        var glossRect = new Rectangle(1, 1, Width - 2, glossH);
        using (var gp = GlassDialog.RoundRect(glossRect, Math.Max(0, _cornerRadius - 1)))
        using (var gl = new LinearGradientBrush(
            new Rectangle(glossRect.X, glossRect.Y, Math.Max(1, glossRect.Width), Math.Max(1, glossRect.Height)),
            Color.FromArgb((int)(18 + (t * 34)), 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.SetClip(path);
            g.FillPath(gl, gp);
            g.ResetClip();
        }

        // Outer glow then crisp edge; both intensify with hover.
        g.DrawPath(GetGlowPen((int)(28 + (t * 45))), path);
        g.DrawPath(GetEdgePen(_pressed ? 210 : (int)(85 + (t * 130))), path);

        // Dotted focus ring (keyboard navigation), inset from the edge.
        if (_focused && !_pressed)
        {
            var fr = new Rectangle(2, 2, Width - 5, Height - 5);
            var focusR = Math.Max(0, _cornerRadius - 2);
            using var fp = GlassDialog.RoundRect(fr, focusR);
            using var fp2 = new Pen(Color.FromArgb(160, _theme.AccentColor), 1f)
            { DashStyle = DashStyle.Dot };
            g.DrawPath(fp2, fp);
        }

        // Caption, nudged down a pixel when pressed to reinforce the push.
        var textRect = new Rectangle(0, _pressed ? 1 : 0, Width, Height - (_pressed ? 1 : 0));
        var flags = TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues)
        {
            flags |= TextFormatFlags.HidePrefix;
        }

        TextRenderer.DrawText(g, Text, Font, textRect,
            _pressed ? _theme.AccentColor : ForeColor,
            Color.Transparent, flags);
    }

    // Plain, system-coloured rendering for High Contrast mode, where the gloss and
    // gradients would obscure the OS accessibility colours.
    private void PaintHighContrast(Graphics g)
    {
        var rect = ClientRectangle;
        g.FillRectangle(_pressed ? SystemBrushes.Highlight : SystemBrushes.ButtonFace, rect);
        using var pen = new Pen(SystemColors.ControlText, _focused ? 2f : 1f);
        g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        var flags = TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues)
        {
            flags |= TextFormatFlags.HidePrefix;
        }

        TextRenderer.DrawText(g, Text, Font, rect,
            _pressed ? SystemColors.HighlightText : SystemColors.ControlText,
            Color.Transparent, flags);
    }

    // --- Small colour helpers (all clamp to the valid 0-255 channel range) ----
    private static Color Blend(Color a, Color b, float t)
        => Color.FromArgb(
            Math.Max(0, Math.Min(255, (int)(a.A + ((b.A - a.A) * t)))),
            Math.Max(0, Math.Min(255, (int)(a.R + ((b.R - a.R) * t)))),
            Math.Max(0, Math.Min(255, (int)(a.G + ((b.G - a.G) * t)))),
            Math.Max(0, Math.Min(255, (int)(a.B + ((b.B - a.B) * t)))));

    private static Color Lighten(Color c, int amount)
        => Color.FromArgb(c.A, Math.Min(255, c.R + amount),
                               Math.Min(255, c.G + amount),
                               Math.Min(255, c.B + amount));

    private static Color Darken(Color c, int amount)
        => Color.FromArgb(c.A, Math.Max(0, c.R - amount),
                               Math.Max(0, c.G - amount),
                               Math.Max(0, c.B - amount));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transTimer?.Stop();
            _transTimer?.Dispose();
            _path?.Dispose();
            _fillBrush?.Dispose();
            _cachedGlowPen?.Dispose();
            _cachedEdgePen?.Dispose();
        }
        base.Dispose(disposing);
    }
}
