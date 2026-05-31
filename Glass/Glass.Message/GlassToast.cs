using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

public enum ToastPosition
{
    BottomRight,
    BottomLeft,
    TopRight,
    TopLeft,
    BottomCenter,
    TopCenter,
}

public sealed class GlassToastOptions
{
    public string        Message  { get; set; } = string.Empty;
    public string        Title    { get; set; }
    public MessageBoxIcon Icon    { get; set; } = MessageBoxIcon.None;
    public GlassTheme    Theme    { get; set; }
    public int           DurationMs { get; set; } = 4_000;
    public ToastPosition Position   { get; set; } = ToastPosition.BottomRight;
    public Action        OnClick    { get; set; }

    public bool? UseRoundedCorners { get; set; }
}

public static class GlassToast
{
    private static readonly object _lock = new object();
    private static readonly List<ToastForm> _active = new List<ToastForm>();


    public static void Show(string message, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, DurationMs = durationMs });

    public static void Show(string message, string title, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, DurationMs = durationMs });

    public static void Show(string message, string title, MessageBoxIcon icon, int durationMs = 4_000)
        => Show(new GlassToastOptions { Message = message, Title = title, Icon = icon, DurationMs = durationMs });

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


    private static void PositionToast(ToastForm form, ToastPosition pos)
    {
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        const int margin = 12;

        int stackedH = 0;
        lock (_lock)
        {
            foreach (var f in _active)
                if (f != form && !f.IsDisposed && f.Position == pos)
                    stackedH += f.Height + margin;
        }

        form.Location = CalcToastLocation(screen, form.Width, form.Height, pos, stackedH, margin);
    }

    private static void ReStack(ToastPosition pos)
    {
        var moves = new List<(ToastForm form, Point location)>();

        lock (_lock)
        {
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            const int margin = 12;
            int stackedH = 0;

            foreach (var f in _active)
            {
                if (f.IsDisposed || f.Position != pos) continue;
                moves.Add((f, CalcToastLocation(screen, f.Width, f.Height, pos, stackedH, margin)));
                stackedH += f.Height + margin;
            }
        }

        foreach (var (form, loc) in moves)
            if (!form.IsDisposed)
                form.Location = loc;
    }

    private static Point CalcToastLocation(Rectangle screen, int w, int h,
                                            ToastPosition pos, int stackedH, int margin)
    {
        return pos switch
        {
            ToastPosition.BottomRight  => new Point(screen.Right  - w - margin,
                                                    screen.Bottom - h - margin - stackedH),
            ToastPosition.BottomLeft   => new Point(screen.Left   + margin,
                                                    screen.Bottom - h - margin - stackedH),
            ToastPosition.TopRight     => new Point(screen.Right  - w - margin,
                                                    screen.Top    + margin + stackedH),
            ToastPosition.TopLeft      => new Point(screen.Left   + margin,
                                                    screen.Top    + margin + stackedH),
            ToastPosition.BottomCenter => new Point(screen.Left   + (screen.Width - w) / 2,
                                                    screen.Bottom - h - margin - stackedH),
            _                          => new Point(screen.Left   + (screen.Width - w) / 2,
                                                    screen.Top    + margin + stackedH),
        };
    }

    internal sealed class ToastForm : Form
    {
        private readonly GlassToastOptions _opts;
        private readonly GlassTheme        _theme;
        private readonly Bitmap            _icon;
        private readonly int               _effectiveRadius;
        private bool                       _dwmRounded;
        internal ToastPosition Position => _opts.Position;

        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _stayTimer;
        private bool   _fadingOut;
        private int    _fadeStep;
        private const int _fadeTicks  = 7;
        private const int _toastWidth = 360;
        private const int _pad        = 12;
        private const int _iconW      = 20;
        private const int _iconGap    = 8;

        public ToastForm(GlassToastOptions opts)
        {
            _opts  = opts;
            _theme = opts.Theme;
            _icon  = GlassDialog.GetCachedSystemIcon(opts.Icon);

            bool rounded = opts.UseRoundedCorners ?? GlassMessage.UseRoundedCorners;
            _effectiveRadius = rounded ? _theme.CornerRadius : 0;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            StartPosition   = FormStartPosition.Manual;
            Opacity         = 0.0;
            BackColor       = _theme.BackgroundBottom;
            Cursor          = Cursors.Hand;
            AccessibleRole  = AccessibleRole.Alert;
            AccessibleName  = string.IsNullOrEmpty(opts.Title) ? opts.Message : opts.Title;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            MeasureAndSize();
            ApplyRegion();

            Click += (s, e) => { opts.OnClick?.Invoke(); BeginDismiss(); };
        }

        private void MeasureAndSize()
        {
            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon  = _icon != null;
            var textX    = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW    = _toastWidth - textX - _pad;

            int titleH = 0;
            if (hasTitle)
                titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
                    new Size(textW, int.MaxValue), TextFormatFlags.SingleLine).Height;

            int msgH = TextRenderer.MeasureText(_opts.Message, _theme.MessageFont,
                new Size(textW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;

            int gap      = (hasTitle && msgH > 0) ? 3 : 0;
            int contentH = titleH + gap + msgH;
            int totalH   = _pad + Math.Max(contentH, hasIcon ? _iconW : 0) + _pad;
            ClientSize   = new Size(_toastWidth, totalH);
        }

        private void ApplyRegion()
        {
            if (_effectiveRadius <= 0 || _dwmRounded) { Region = null; return; }
            using var path = GlassDialog.RoundRect(new Rectangle(0, 0, Width, Height), _effectiveRadius);
            Region = new Region(path);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_effectiveRadius > 0 && GlassDialog.EnableModernCorners(Handle))
            {
                _dwmRounded = true;
                Region = null;
                Invalidate();
            }
        }

        protected override void OnLoad(EventArgs e) { base.OnLoad(e); StartFade(fadingIn: true); }

        private void StartFade(bool fadingIn)
        {
            _fadingOut = !fadingIn;
            _fadeStep  = 0;
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (s, ev) =>
            {
                _fadeStep++;
                var t      = Math.Min(1.0, (double)_fadeStep / _fadeTicks);
                var eased  = t * t * (3.0 - 2.0 * t);

                Opacity = _fadingOut
                    ? _theme.Opacity * (1.0 - eased)
                    : _theme.Opacity * eased;

                if (_fadeStep >= _fadeTicks)
                {
                    _fadeTimer.Stop(); _fadeTimer.Dispose(); _fadeTimer = null;
                    if (!_fadingOut)
                    {
                        _stayTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1, _opts.DurationMs) };
                        _stayTimer.Tick += (ss, ee) => { _stayTimer.Stop(); _stayTimer.Dispose(); _stayTimer = null; BeginDismiss(); };
                        _stayTimer.Start();
                    }
                    else Close();
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
            g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
            g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;

            var w = ClientSize.Width;
            var h = ClientSize.Height;
            var r = _dwmRounded ? 0 : _effectiveRadius;

            using (var path  = GlassDialog.RoundRect(new Rectangle(0, 0, w, h), r))
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical))
                g.FillPath(brush, path);

            using (var borderPath = GlassDialog.RoundRect(new Rectangle(0, 0, w - 1, h - 1), r))
            {
                using var glow = new Pen(Color.FromArgb(60, _theme.BorderColor), 3f);
                using var edge = new Pen(Color.FromArgb(190, _theme.BorderColor), 1f);
                g.DrawPath(glow, borderPath);
                g.DrawPath(edge, borderPath);
            }

            var hasTitle = !string.IsNullOrEmpty(_opts.Title);
            var hasIcon  = _icon != null;
            var textX    = _pad + (hasIcon ? _iconW + _iconGap : 0);
            var textW    = w - textX - _pad;

            if (hasIcon)
                g.DrawImage(_icon, new Rectangle(_pad, _pad, _iconW, _iconW));

            var y = _pad;
            int titleH = 0;
            if (hasTitle)
            {
                titleH = TextRenderer.MeasureText(_opts.Title, _theme.TitleFont,
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
                _fadeTimer?.Stop(); _fadeTimer?.Dispose();
                _stayTimer?.Stop(); _stayTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
