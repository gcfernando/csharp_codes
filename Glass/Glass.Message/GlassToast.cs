using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassToast — non-blocking, auto-dismissing corner notifications.
// ─────────────────────────────────────────────────────────────────────────

/// <summary>Screen corner for <see cref="GlassToast"/> notifications.</summary>
public enum ToastPosition
{
    /// <summary>Bottom-right corner (default).</summary>
    BottomRight,
    /// <summary>Bottom-left corner.</summary>
    BottomLeft,
    /// <summary>Top-right corner.</summary>
    TopRight,
    /// <summary>Top-left corner.</summary>
    TopLeft,
    /// <summary>Bottom-center of the screen.</summary>
    BottomCenter,
    /// <summary>Top-center of the screen.</summary>
    TopCenter,
}

/// <summary>Configuration for a <see cref="GlassToast"/> notification.</summary>
public sealed class GlassToastOptions
{
    /// <summary>Notification message (required).</summary>
    public string Message       { get; set; } = string.Empty;
    /// <summary>Optional bold title above the message.</summary>
    public string Title         { get; set; }
    /// <summary>System icon (optional).</summary>
    public MessageBoxIcon Icon  { get; set; } = MessageBoxIcon.None;
    /// <summary>Visual theme; defaults to <see cref="GlassMessage.DefaultTheme"/>.</summary>
    public GlassTheme Theme     { get; set; }
    /// <summary>How long the toast stays visible before auto-dismissing (ms). Default 4 000.</summary>
    public int DurationMs       { get; set; } = 4_000;
    /// <summary>Screen corner where the toast appears. Default <see cref="ToastPosition.BottomRight"/>.</summary>
    public ToastPosition Position { get; set; } = ToastPosition.BottomRight;
    /// <summary>Callback invoked when the user clicks the toast body.</summary>
    public Action OnClick       { get; set; }
}

/// <summary>
/// Displays lightweight, auto-dismissing corner notifications that do not block the caller.
/// Multiple toasts stack automatically without overlapping.
/// </summary>
public static class GlassToast
{
    private static readonly object _lock   = new object();
    private static readonly List<ToastForm> _active = new List<ToastForm>();

    // ── Simple overloads ──────────────────────────────────────────────────

    /// <summary>Shows a simple message toast and returns immediately.</summary>
    public static void Show(string message, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, DurationMs = durationMs });

    /// <summary>Shows a toast with a title and returns immediately.</summary>
    public static void Show(string message, string title, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, DurationMs = durationMs });

    /// <summary>Shows a toast with an icon and returns immediately.</summary>
    public static void Show(string message, string title, MessageBoxIcon icon, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, Icon = icon, DurationMs = durationMs });

    /// <summary>Shows a toast with full options and returns immediately.</summary>
    public static void Show(GlassToastOptions options)
    {
        if (options == null) return;
        options.Theme ??= GlassMessage.DefaultTheme ?? GlassTheme.Default;

        var form = new ToastForm(options);

        lock (_lock) _active.Add(form);

        form.FormClosed += (s, e) =>
        {
            lock (_lock) _active.Remove(form);
            ReStack(options.Position);
        };

        PositionToast(form, options.Position);
        form.Show();
    }

    /// <summary>
    /// Shows a toast and returns a <see cref="Task"/> that completes when the toast dismisses.
    /// </summary>
    public static Task ShowAsync(GlassToastOptions options)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        options ??= new GlassToastOptions();
        options.Theme ??= GlassMessage.DefaultTheme ?? GlassTheme.Default;

        var form = new ToastForm(options);
        lock (_lock) _active.Add(form);

        form.FormClosed += (s, e) =>
        {
            lock (_lock) _active.Remove(form);
            ReStack(options.Position);
            tcs.TrySetResult(true);
        };

        PositionToast(form, options.Position);
        form.Show();
        return tcs.Task;
    }

    // ── Stack management ──────────────────────────────────────────────────
    private static void PositionToast(ToastForm form, ToastPosition pos)
    {
        var screen = Screen.PrimaryScreen.WorkingArea;
        const int margin = 12;

        int stackedH = 0;
        lock (_lock)
        {
            foreach (var f in _active)
            {
                if (f != form && !f.IsDisposed && f.Position == pos)
                    stackedH += f.Height + margin;
            }
        }

        int x, y;
        switch (pos)
        {
            case ToastPosition.BottomRight:
                x = screen.Right  - form.Width  - margin;
                y = screen.Bottom - form.Height - margin - stackedH;
                break;
            case ToastPosition.BottomLeft:
                x = screen.Left   + margin;
                y = screen.Bottom - form.Height - margin - stackedH;
                break;
            case ToastPosition.TopRight:
                x = screen.Right - form.Width - margin;
                y = screen.Top   + margin     + stackedH;
                break;
            case ToastPosition.TopLeft:
                x = screen.Left + margin;
                y = screen.Top  + margin + stackedH;
                break;
            case ToastPosition.BottomCenter:
                x = screen.Left + (screen.Width - form.Width) / 2;
                y = screen.Bottom - form.Height - margin - stackedH;
                break;
            default: // TopCenter
                x = screen.Left + (screen.Width - form.Width) / 2;
                y = screen.Top  + margin + stackedH;
                break;
        }

        form.Location = new Point(x, y);
    }

    private static void ReStack(ToastPosition pos)
    {
        // Re-position remaining active toasts after one closes
        lock (_lock)
        {
            int stackedH = 0;
            const int margin = 12;
            var screen = Screen.PrimaryScreen.WorkingArea;

            foreach (var f in _active)
            {
                if (f.IsDisposed || f.Position != pos) continue;
                int x, y;
                switch (pos)
                {
                    case ToastPosition.BottomRight:
                        x = screen.Right  - f.Width  - margin;
                        y = screen.Bottom - f.Height - margin - stackedH;
                        break;
                    case ToastPosition.BottomLeft:
                        x = screen.Left  + margin;
                        y = screen.Bottom - f.Height - margin - stackedH;
                        break;
                    case ToastPosition.TopRight:
                        x = screen.Right - f.Width - margin;
                        y = screen.Top   + margin  + stackedH;
                        break;
                    case ToastPosition.TopLeft:
                        x = screen.Left + margin;
                        y = screen.Top  + margin + stackedH;
                        break;
                    case ToastPosition.BottomCenter:
                        x = screen.Left + (screen.Width - f.Width) / 2;
                        y = screen.Bottom - f.Height - margin - stackedH;
                        break;
                    default:
                        x = screen.Left + (screen.Width - f.Width) / 2;
                        y = screen.Top  + margin + stackedH;
                        break;
                }
                if (!f.IsDisposed)
                {
                    f.Location = new Point(x, y);
                    stackedH += f.Height + margin;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ToastForm — the actual notification window
    // ═══════════════════════════════════════════════════════════════════════
    internal sealed class ToastForm : Form
    {
        private readonly GlassToastOptions _opts;
        private readonly GlassTheme        _theme;
        private readonly Bitmap            _icon;
        internal ToastPosition Position => _opts.Position;

        // Animation
        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _stayTimer;
        private bool   _fadingOut;
        private int    _fadeStep;
        private const int _fadeTicks   = 6;
        private const int _toastWidth  = 360;   // wide enough for one-line messages
        private const int _pad         = 12;
        private const int _iconW       = 20;    // rendered icon size
        private const int _iconGap     = 8;     // gap between icon column and text

        public ToastForm(GlassToastOptions opts)
        {
            _opts  = opts;
            _theme = opts.Theme;
            _icon  = ResolveIcon(opts.Icon);

            FormBorderStyle   = FormBorderStyle.None;
            ShowInTaskbar     = false;
            TopMost           = true;
            StartPosition     = FormStartPosition.Manual;
            Opacity           = 0.0;
            BackColor         = _theme.BackgroundBottom;
            Cursor            = Cursors.Hand;
            AccessibleRole    = AccessibleRole.Alert;
            AccessibleName    = string.IsNullOrEmpty(opts.Title) ? opts.Message : opts.Title;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            MeasureAndSize();
            ApplyRegion();

            Click += (s, e) =>
            {
                opts.OnClick?.Invoke();
                BeginDismiss();
            };
        }

        private void MeasureAndSize()
        {
            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon  = _icon != null;

            // Text column starts after icon (if any)
            var textX = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW = _toastWidth - textX - _pad;

            // Measure title with actual font metrics
            int titleH = 0;
            if (hasTitle)
                titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
                    new Size(textW, int.MaxValue),
                    TextFormatFlags.SingleLine).Height;

            // Measure message
            int msgH = TextRenderer.MeasureText(_opts.Message, _theme.MessageFont,
                new Size(textW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

            int gap      = (hasTitle && msgH > 0) ? 3 : 0;
            int contentH = titleH + gap + msgH;

            // Height = top pad + max(text content, icon) + bottom pad
            int totalH = _pad + Math.Max(contentH, hasIcon ? _iconW : 0) + _pad;
            ClientSize = new Size(_toastWidth, totalH);
        }

        private void ApplyRegion()
        {
            using var path = GlassDialog.RoundRect(
                new Rectangle(0, 0, Width, Height), _theme.CornerRadius);
            Region = new Region(path);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            StartFade(fadingIn: true);
        }

        private void StartFade(bool fadingIn)
        {
            _fadingOut = !fadingIn;
            _fadeStep  = 0;
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (s, ev) =>
            {
                _fadeStep++;
                var ratio = Math.Min(1.0, (double)_fadeStep / _fadeTicks);

                Opacity = _fadingOut
                    ? _theme.Opacity * (1.0 - ratio)
                    : _theme.Opacity * ratio;

                if (_fadeStep >= _fadeTicks)
                {
                    _fadeTimer.Stop(); _fadeTimer.Dispose(); _fadeTimer = null;

                    if (!_fadingOut)
                    {
                        // Stay visible for DurationMs, then fade out
                        _stayTimer = new System.Windows.Forms.Timer { Interval = _opts.DurationMs };
                        _stayTimer.Tick += (ss, ee) =>
                        {
                            _stayTimer.Stop(); _stayTimer.Dispose(); _stayTimer = null;
                            BeginDismiss();
                        };
                        _stayTimer.Start();
                    }
                    else
                    {
                        Close();
                    }
                }
            };
            _fadeTimer.Start();
        }

        internal void BeginDismiss()
        {
            _stayTimer?.Stop(); _stayTimer?.Dispose(); _stayTimer = null;
            _fadeTimer?.Stop(); _fadeTimer?.Dispose(); _fadeTimer = null;
            StartFade(fadingIn: false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            var r = _theme.CornerRadius;

            // Background
            using (var path  = GlassDialog.RoundRect(new Rectangle(0, 0, w, h), r))
            using (var brush = new LinearGradientBrush(new Rectangle(0, 0, w, h),
                _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical))
                g.FillPath(brush, path);

            // Border glow + edge
            using (var path = GlassDialog.RoundRect(new Rectangle(0, 0, w - 1, h - 1), r))
            {
                using var glow = new Pen(Color.FromArgb(60, _theme.BorderColor), 3f);
                using var edge = new Pen(Color.FromArgb(180, _theme.BorderColor), 1f);
                g.DrawPath(glow, path);
                g.DrawPath(edge, path);
            }

            // Left accent stripe
            using (var stripe = new LinearGradientBrush(
                new Rectangle(0, r, 3, Math.Max(1, h - r * 2)),
                _theme.AccentColor, _theme.BorderColor, LinearGradientMode.Vertical))
                g.FillRectangle(stripe, 0, r, 3, Math.Max(1, h - r * 2));

            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon  = _icon != null;
            var textX    = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW    = w - textX - _pad;

            // Icon — top-aligned with text content, never centred in the whole form
            if (hasIcon)
                g.DrawImage(_icon, new Rectangle(_pad, _pad, _iconW, _iconW));

            var y = _pad;

            // Title
            int titleH = 0;
            if (hasTitle)
            {
                titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
                    new Size(textW, int.MaxValue), TextFormatFlags.SingleLine).Height;
                TextRenderer.DrawText(g, _opts.Title, _theme.TitleFont,
                    new Rectangle(textX, y, textW, titleH),
                    _theme.TitleColor,
                    TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                y += titleH + 3;
            }

            // Message
            TextRenderer.DrawText(g, _opts.Message, _theme.MessageFont,
                new Rectangle(textX, y, textW, h - y - _pad),
                _theme.MessageColor,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        }

        private static Bitmap ResolveIcon(MessageBoxIcon icon)
        {
            return icon switch
            {
                MessageBoxIcon.Information => SystemIcons.Information.ToBitmap(),
                MessageBoxIcon.Question    => SystemIcons.Question.ToBitmap(),
                MessageBoxIcon.Warning     => SystemIcons.Warning.ToBitmap(),
                MessageBoxIcon.Error       => SystemIcons.Error.ToBitmap(),
                _                          => null,
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fadeTimer?.Stop(); _fadeTimer?.Dispose();
                _stayTimer?.Stop(); _stayTimer?.Dispose();
                _icon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
