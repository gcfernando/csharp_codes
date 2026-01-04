using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Spectrum
{
    // Developed by Gehan Fernando
    [Description("VerticalProgressBar Maximum Value")]
    [Category("VerticalProgressBar")]
    [RefreshProperties(RefreshProperties.All)]
    public class VerticalProgressBar : ProgressBar
    {
        private const float SmoothFactor = 0.35f;

        private static readonly object s_lock = new object();
        private static Timer s_timer;
        private static readonly List<WeakReference<VerticalProgressBar>> s_instances =
            new List<WeakReference<VerticalProgressBar>>(128);

        private float _displayValue;
        private int _targetValue;

        private SolidBrush _backBrush;
        private SolidBrush _fillBrush;
        private SolidBrush _brickShadeBrush;
        private readonly Pen _borderPen;

        // ===== Brick look settings =====
        [Category("Appearance")]
        [Description("Height of each brick segment in pixels.")]
        public int BrickHeight { get; set; } = 8;

        [Category("Appearance")]
        [Description("Gap between bricks in pixels.")]
        public int BrickGap { get; set; } = 2;

        [Category("Appearance")]
        [Description("Inner padding for brick fill (pixels).")]
        public int BrickPadding { get; set; } = 2;

        // ===== Beauty options (non-breaking) =====
        [Category("Appearance")]
        [Description("Corner radius in pixels. 0 = square (default).")]
        public int CornerRadius { get; set; } = 0;

        [Category("Appearance")]
        [Description("Draw a subtle highlight line inside each brick.")]
        public bool BrickHighlight { get; set; } = true;

        [Category("Appearance")]
        [Description("Opacity of brick highlight (0..255).")]
        public int HighlightAlpha { get; set; } = 55;

        public VerticalProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            SetStyle(ControlStyles.Opaque, true);

            _backBrush = new SolidBrush(BackColor);
            _fillBrush = new SolidBrush(ForeColor);
            _brickShadeBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
            _borderPen = new Pen(Color.FromArgb(110, 0, 0, 0)); // slightly nicer than 90

            _targetValue = base.Value;
            _displayValue = base.Value;

            RegisterInstance();
            UpdateStyles();
        }

        public new int Value
        {
            get => base.Value;
            set
            {
                var v = value;
                if (v < Minimum)
                {
                    v = Minimum;
                }

                if (v > Maximum)
                {
                    v = Maximum;
                }

                if (base.Value != v)
                {
                    base.Value = v;
                }

                _targetValue = v;

                Invalidate();
            }
        }

        public new int Maximum
        {
            get => base.Maximum;
            set
            {
                if (base.Maximum == value)
                {
                    return;
                }

                base.Maximum = value;

                if (base.Value > base.Maximum)
                {
                    base.Value = base.Maximum;
                }

                if (_targetValue > base.Maximum)
                {
                    _targetValue = base.Maximum;
                }

                if (_displayValue > base.Maximum)
                {
                    _displayValue = base.Maximum;
                }

                Invalidate();
            }
        }

        public new int Minimum
        {
            get => base.Minimum;
            set
            {
                if (base.Minimum == value)
                {
                    return;
                }

                base.Minimum = value;

                if (base.Value < base.Minimum)
                {
                    base.Value = base.Minimum;
                }

                if (_targetValue < base.Minimum)
                {
                    _targetValue = base.Minimum;
                }

                if (_displayValue < base.Minimum)
                {
                    _displayValue = base.Minimum;
                }

                Invalidate();
            }
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);

            _fillBrush?.Dispose();
            _fillBrush = new SolidBrush(ForeColor);

            _brickShadeBrush?.Dispose();
            _brickShadeBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));

            Invalidate();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            _backBrush?.Dispose();
            _backBrush = new SolidBrush(BackColor);
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterInstance();

                _backBrush?.Dispose();
                _fillBrush?.Dispose();
                _brickShadeBrush?.Dispose();
                _borderPen?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // no-op: we paint everything in OnPaint
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var r = ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0)
            {
                return;
            }

            // Background
            if (CornerRadius > 0)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(r, CornerRadius))
                {
                    e.Graphics.FillPath(_backBrush, path);
                }
            }
            else
            {
                e.Graphics.FillRectangle(_backBrush, r);
            }

            var range = Maximum - Minimum;
            if (range <= 0)
            {
                DrawBorder(e, r);
                return;
            }

            var dv = _displayValue;
            if (dv < Minimum)
            {
                dv = Minimum;
            }

            if (dv > Maximum)
            {
                dv = Maximum;
            }

            var percent = (dv - Minimum) / range;
            if (percent <= 0f)
            {
                DrawBorder(e, r);
                return;
            }

            var fillHeight = (int)((r.Height * percent) + 0.5f);
            if (fillHeight > r.Height)
            {
                fillHeight = r.Height;
            }

            var pad = BrickPadding;
            var innerX = r.X + pad;
            var innerW = r.Width - (pad * 2);
            if (innerW <= 0)
            {
                DrawBorder(e, r);
                return;
            }

            var bottom = r.Bottom - pad;
            var topLimit = bottom - fillHeight;

            var brickH = BrickHeight < 1 ? 1 : BrickHeight;
            var gap = BrickGap < 0 ? 0 : BrickGap;

            // highlight brush created once per paint (very small), only if enabled
            SolidBrush hiBrush = null;
            if (BrickHighlight && HighlightAlpha > 0)
            {
                var a = HighlightAlpha;
                if (a > 255)
                {
                    a = 255;
                }

                hiBrush = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
            }

            // bricks bottom -> top
            for (var y = bottom; y > topLimit;)
            {
                var h = brickH;
                var brickTop = y - h;

                if (brickTop < topLimit)
                {
                    brickTop = topLimit;
                    h = y - brickTop;
                }

                if (h <= 0)
                {
                    break;
                }

                var brickRect = new Rectangle(innerX, brickTop, innerW, h);

                e.Graphics.FillRectangle(_fillBrush, brickRect);

                // shade at top of each brick (gives depth)
                if (brickRect.Height >= 3)
                {
                    var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                    e.Graphics.FillRectangle(_brickShadeBrush, shadeRect);
                }

                // optional highlight (beauty)
                if (hiBrush != null && brickRect.Height >= 5)
                {
                    // thin line slightly below shade
                    e.Graphics.FillRectangle(hiBrush, brickRect.X + 1, brickRect.Y + 3, brickRect.Width - 2, 1);
                }

                y = brickTop - gap;
            }

            hiBrush?.Dispose();

            DrawBorder(e, r);
        }

        private void DrawBorder(PaintEventArgs e, Rectangle r)
        {
            if (CornerRadius > 0)
            {
                // border for rounded case (anti-aliased already)
                using (var path = RoundedRect(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), CornerRadius))
                {
                    e.Graphics.DrawPath(_borderPen, path);
                }
            }
            else
            {
                e.Graphics.DrawRectangle(_borderPen, 0, 0, r.Width - 1, r.Height - 1);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var d = radius * 2;
            if (d <= 0)
            {
                var p = new GraphicsPath();
                p.AddRectangle(r);
                return p;
            }

            if (d > r.Width)
            {
                d = r.Width;
            }

            if (d > r.Height)
            {
                d = r.Height;
            }

            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ===== shared animation loop =====
        private void RegisterInstance()
        {
            lock (s_lock)
            {
                s_instances.Add(new WeakReference<VerticalProgressBar>(this));

                if (s_timer == null)
                {
                    s_timer = new Timer { Interval = 1000 / 60 };
                    s_timer.Tick += (s, e) => AnimateAll();
                    s_timer.Start();
                }
            }
        }

        private void UnregisterInstance()
        {
            lock (s_lock)
            {
                for (var i = s_instances.Count - 1; i >= 0; i--)
                {
                    if (!s_instances[i].TryGetTarget(out var ctrl) ||
                        ctrl.IsDisposed ||
                        ReferenceEquals(ctrl, this))
                    {
                        s_instances.RemoveAt(i);
                    }
                }
            }
        }

        private static void AnimateAll()
        {
            lock (s_lock)
            {
                for (var i = s_instances.Count - 1; i >= 0; i--)
                {
                    if (!s_instances[i].TryGetTarget(out var ctrl) || ctrl.IsDisposed)
                    {
                        s_instances.RemoveAt(i);
                        continue;
                    }

                    var dv = ctrl._displayValue;
                    float tv = ctrl._targetValue;
                    var delta = tv - dv;

                    if (delta > -0.25f && delta < 0.25f)
                    {
                        if (dv != tv)
                        {
                            ctrl._displayValue = tv;
                            ctrl.Invalidate();
                        }
                        continue;
                    }

                    ctrl._displayValue = dv + (delta * SmoothFactor);
                    ctrl.Invalidate();
                }

                if (s_instances.Count == 0 && s_timer != null)
                {
                    s_timer.Stop();
                    s_timer.Dispose();
                    s_timer = null;
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x04; // PBS_VERTICAL
                return cp;
            }
        }
    }
}