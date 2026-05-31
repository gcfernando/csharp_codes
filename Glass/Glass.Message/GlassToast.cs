// -----------------------------------------------------------------------------
//  Glass.Message — lightweight, non-modal toast notifications. Toasts fade in at
//  a screen corner, stack neatly when several are visible, auto-dismiss after a
//  delay, and can run an action when clicked.
//
//  File        : GlassToast.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

/// <summary>Screen corner (or centre edge) a toast anchors to.</summary>
public enum ToastPosition
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft,
    BottomCenter,
    TopCenter,
}

/// <summary>Configuration for a single toast notification.</summary>
public sealed class GlassToastOptions
{
    public string Message { get; set; } = string.Empty;
    public string Title { get; set; }
    public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.None;
    public GlassTheme Theme { get; set; }

    /// <summary>How long the toast stays fully visible before fading out.</summary>
    public int DurationMs { get; set; } = 4_000;
    public ToastPosition Position { get; set; } = ToastPosition.BottomRight;

    /// <summary>Invoked if the user clicks the toast (the toast also dismisses).</summary>
    public Action OnClick { get; set; }

    /// <summary>Per-toast rounded-corner override; <c>null</c> falls back to the global setting.</summary>
    public bool? UseRoundedCorners { get; set; }
}

/// <summary>
/// Static façade for showing toasts. Tracks the live toasts so they can be
/// stacked and re-stacked as others appear and disappear.
/// </summary>
public static class GlassToast
{
    // Guards the shared list because toasts may be created/closed from the UI
    // thread at different times; the list is read while computing stack offsets.
    private static readonly object _lock = new();
    private static readonly List<ToastForm> _active = [];

    /// <summary>Shows a message-only toast.</summary>
    public static void Show(string message, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, DurationMs = durationMs });

    /// <summary>Shows a toast with a title.</summary>
    public static void Show(string message, string title, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, DurationMs = durationMs });

    /// <summary>Shows a toast with a title and an icon.</summary>
    public static void Show(string message, string title, MessageBoxIcon icon, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, Icon = icon, DurationMs = durationMs });

    /// <summary>Shows a toast from a fully-populated options object.</summary>
    public static void Show(GlassToastOptions options)
    {
        if (options == null)
        {
            return;
        }

        options.Theme ??= GlassMessage.DefaultTheme ?? GlassTheme.Default;

        var form = new ToastForm(options);
        lock (_lock)
        {
            _active.Add(form);
        }

        // When this toast closes, re-pack the rest at its position to close the gap.
        form.FormClosed += (s, e) =>
        {
            lock (_lock)
            {
                _ = _active.Remove(form);
            }

            ReStack(options.Position);
        };

        PositionToast(form, options.Position);
        form.Show();
    }

    /// <summary>
    /// Shows a toast and returns a task that completes when it closes — handy for
    /// awaiting a notification before continuing.
    /// </summary>
    public static Task ShowAsync(GlassToastOptions options)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        options ??= new GlassToastOptions();
        options.Theme ??= GlassMessage.DefaultTheme ?? GlassTheme.Default;

        var form = new ToastForm(options);
        lock (_lock)
        {
            _active.Add(form);
        }

        form.FormClosed += (s, e) =>
        {
            lock (_lock)
            {
                _ = _active.Remove(form);
            }

            ReStack(options.Position);
            _ = tcs.TrySetResult(true);
        };

        PositionToast(form, options.Position);
        form.Show();
        return tcs.Task;
    }

    // Places a new toast, offsetting it past any toasts already at the same corner.
    private static void PositionToast(ToastForm form, ToastPosition pos)
    {
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        const int margin = 12;

        var stackedH = 0;
        lock (_lock)
        {
            foreach (var f in _active)
            {
                if (f != form && !f.IsDisposed && f.Position == pos)
                {
                    stackedH += f.Height + margin;
                }
            }
        }

        form.Location = CalcToastLocation(screen, form.Width, form.Height, pos, stackedH, margin);
    }

    // Recomputes every toast position after one closes so the stack stays tight.
    // Positions are gathered under the lock, then applied outside it to avoid
    // doing UI work while holding the lock.
    private static void ReStack(ToastPosition pos)
    {
        var moves = new List<(ToastForm form, Point location)>();

        lock (_lock)
        {
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            const int margin = 12;
            var stackedH = 0;

            foreach (var f in _active)
            {
                if (f.IsDisposed || f.Position != pos)
                {
                    continue;
                }

                moves.Add((f, CalcToastLocation(screen, f.Width, f.Height, pos, stackedH, margin)));
                stackedH += f.Height + margin;
            }
        }

        foreach (var (form, loc) in moves)
        {
            if (!form.IsDisposed)
            {
                form.Location = loc;
            }
        }
    }

    // Maps a position + stack offset onto an actual screen point. The stack grows
    // upward from the bottom corners and downward from the top corners.
    private static Point CalcToastLocation(Rectangle screen, int w, int h,
                                            ToastPosition pos, int stackedH, int margin)
    {
        return pos switch
        {
            ToastPosition.BottomRight => new Point(screen.Right - w - margin,
                                                    screen.Bottom - h - margin - stackedH),
            ToastPosition.BottomLeft => new Point(screen.Left + margin,
                                                    screen.Bottom - h - margin - stackedH),
            ToastPosition.TopRight => new Point(screen.Right - w - margin,
                                                    screen.Top + margin + stackedH),
            ToastPosition.TopLeft => new Point(screen.Left + margin,
                                                    screen.Top + margin + stackedH),
            ToastPosition.BottomCenter => new Point(screen.Left + ((screen.Width - w) / 2),
                                                    screen.Bottom - h - margin - stackedH),
            _ => new Point(screen.Left + ((screen.Width - w) / 2),
                                                    screen.Top + margin + stackedH),
        };
    }

    /// <summary>The borderless top-most window that actually draws a toast.</summary>
    internal sealed class ToastForm : Form
    {
        private readonly GlassToastOptions _opts;
        private readonly GlassTheme _theme;
        private readonly Bitmap _icon;
        private readonly int _effectiveRadius;
        private bool _dwmRounded;
        internal ToastPosition Position => _opts.Position;

        // Two timers: _fadeTimer animates opacity in/out; _stayTimer waits out the
        // visible duration in between.
        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _stayTimer;
        private bool _fadingOut;
        private int _fadeStep;
        private const int _fadeTicks = 7;
        private const int _toastWidth = 360;
        private const int _pad = 12;
        private const int _iconW = 20;
        private const int _iconGap = 8;

        public ToastForm(GlassToastOptions opts)
        {
            _opts = opts;
            _theme = opts.Theme;
            _icon = GlassDialog.GetCachedSystemIcon(opts.Icon);

            var rounded = opts.UseRoundedCorners ?? GlassMessage.UseRoundedCorners;
            _effectiveRadius = rounded ? _theme.CornerRadius : 0;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Opacity = 0.0;            // start hidden; OnLoad fades us in
            BackColor = _theme.BackgroundBottom;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Alert;
            AccessibleName = string.IsNullOrEmpty(opts.Title) ? opts.Message : opts.Title;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            MeasureAndSize();
            ApplyRegion();

            Click += (s, e) => { opts.OnClick?.Invoke(); BeginDismiss(); };
        }

        // Measures the title and word-wrapped message to size the toast to content,
        // keeping a fixed width and padding the icon column when present.
        private void MeasureAndSize()
        {
            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon = _icon != null;
            var textX = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW = _toastWidth - textX - _pad;

            var titleH = 0;
            if (hasTitle)
            {
                titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
                    new Size(textW, int.MaxValue), TextFormatFlags.SingleLine).Height;
            }

            var msgH = TextRenderer.MeasureText(_opts.Message, _theme.MessageFont,
                new Size(textW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

            var gap = (hasTitle && msgH > 0) ? 3 : 0;
            var contentH = titleH + gap + msgH;
            var totalH = _pad + Math.Max(contentH, hasIcon ? _iconW : 0) + _pad;
            ClientSize = new Size(_toastWidth, totalH);
        }

        // Software clipping region for rounded corners — only used when the OS
        // can't give us real DWM-rounded corners (set later in OnHandleCreated).
        private void ApplyRegion()
        {
            if (_effectiveRadius <= 0 || _dwmRounded)
            { Region = null; return; }
            using var path = GlassDialog.RoundRect(new Rectangle(0, 0, Width, Height), _effectiveRadius);
            Region = new Region(path);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Prefer crisp DWM corners on Windows 11; if we get them, drop the
            // jagged software region.
            if (_effectiveRadius > 0 && GlassDialog.EnableModernCorners(Handle))
            {
                _dwmRounded = true;
                Region = null;
                Invalidate();
            }
        }

        protected override void OnLoad(EventArgs e) { base.OnLoad(e); StartFade(fadingIn: true); }

        // Drives both the fade-in and fade-out with an eased opacity ramp. On a
        // completed fade-in it arms the stay timer; on a completed fade-out it closes.
        private void StartFade(bool fadingIn)
        {
            _fadingOut = !fadingIn;
            _fadeStep = 0;
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (s, ev) =>
            {
                _fadeStep++;
                var t = Math.Min(1.0, (double)_fadeStep / _fadeTicks);
                var eased = t * t * (3.0 - (2.0 * t));   // smoothstep

                Opacity = _fadingOut
                    ? _theme.Opacity * (1.0 - eased)
                    : _theme.Opacity * eased;

                if (_fadeStep >= _fadeTicks)
                {
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    _fadeTimer = null;
                    if (!_fadingOut)
                    {
                        _stayTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, _opts.DurationMs) };
                        _stayTimer.Tick += (ss, ee) => { _stayTimer.Stop(); _stayTimer.Dispose(); _stayTimer = null; BeginDismiss(); };
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

        // Cancels any pending timers and begins the fade-out. Safe to call from a
        // click or from the stay timer.
        internal void BeginDismiss()
        {
            _stayTimer?.Stop();
            _stayTimer?.Dispose();
            _stayTimer = null;
            _fadeTimer?.Stop();
            _fadeTimer?.Dispose();
            _fadeTimer = null;
            StartFade(fadingIn: false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            // When DWM rounds the window, draw squared content so it doesn't fight
            // the hardware corners.
            var r = _dwmRounded ? 0 : _effectiveRadius;

            using (var path = GlassDialog.RoundRect(new Rectangle(0, 0, w, h), r))
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical))
            {
                g.FillPath(brush, path);
            }

            // Soft glow plus crisp edge, same treatment as the dialog border.
            using (var borderPath = GlassDialog.RoundRect(new Rectangle(0, 0, w - 1, h - 1), r))
            {
                using var glow = new Pen(Color.FromArgb(60, _theme.BorderColor), 3f);
                using var edge = new Pen(Color.FromArgb(190, _theme.BorderColor), 1f);
                g.DrawPath(glow, borderPath);
                g.DrawPath(edge, borderPath);
            }

            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon = _icon != null;
            var textX = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW = w - textX - _pad;

            if (hasIcon)
            {
                g.DrawImage(_icon, new Rectangle(_pad, _pad, _iconW, _iconW));
            }

            var y = _pad;
            if (hasTitle)
            {
                var titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
        new Size(textW, int.MaxValue), TextFormatFlags.SingleLine).Height;
                TextRenderer.DrawText(g, _opts.Title, _theme.TitleFont,
                    new Rectangle(textX, y, textW, titleH), _theme.TitleColor,
                    TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                y += titleH + 3;
            }

            TextRenderer.DrawText(g, _opts.Message, _theme.MessageFont,
                new Rectangle(textX, y, textW, h - y - _pad), _theme.MessageColor,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fadeTimer?.Stop();
                _fadeTimer?.Dispose();
                _stayTimer?.Stop();
                _stayTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
