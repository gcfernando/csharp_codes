using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Spectrum
{
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

        // Peak hold (visual only)
        private float _peakValue;
        private int _peakHoldTicksLeft;

        private SolidBrush _backBrush;
        private SolidBrush _fillBrush;          // fallback if Heatmap disabled
        private readonly SolidBrush _brickShadeBrush;
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

        // ===== Clarity / premium look =====
        [Category("Appearance")]
        [Description("Corner radius in pixels. 0 = square (default).")]
        public int CornerRadius { get; set; } = 0;

        [Category("Appearance")]
        [Description("Draw a subtle highlight line inside each brick.")]
        public bool BrickHighlight { get; set; } = true;

        [Category("Appearance")]
        [Description("Opacity of brick highlight (0..255).")]
        public int HighlightAlpha { get; set; } = 55;

        [Category("Appearance")]
        [Description("High quality rendering (anti-alias / high quality).")]
        public bool HighQualityRendering { get; set; } = true;

        // ===== Option B: Heatmap =====
        [Category("Heatmap")]
        [Description("Enable heatmap coloring (Green -> Yellow -> Orange -> Red).")]
        public bool HeatmapEnabled { get; set; } = true;

        [Category("Heatmap")]
        [Description("Low level color (bottom).")]
        public Color HeatLowColor { get; set; } = Color.FromArgb(0, 255, 80);

        [Category("Heatmap")]
        [Description("Mid level color.")]
        public Color HeatMidColor { get; set; } = Color.FromArgb(255, 235, 0);

        [Category("Heatmap")]
        [Description("High level color.")]
        public Color HeatHighColor { get; set; } = Color.FromArgb(255, 140, 0);

        [Category("Heatmap")]
        [Description("Peak color (top).")]
        public Color HeatPeakColor { get; set; } = Color.FromArgb(255, 40, 40);

        [Category("Heatmap")]
        [Description("Boost low-level visibility (1.0 = linear, 1.4..2.2 = more lively lows).")]
        public float HeatIntensityCurve { get; set; } = 1.6f;

        // ===== Visual only: Top red emphasis zone =====
        [Category("Heatmap")]
        [Description("Enable a stronger emphasis for the top zone (more 'spectrum-like').")]
        public bool TopEmphasisEnabled { get; set; } = true;

        [Category("Heatmap")]
        [Description("Where emphasis starts (0..1). Example: 0.82 means top 18% emphasized.")]
        public float TopEmphasisStart { get; set; } = 0.85f;

        [Category("Heatmap")]
        [Description("How strong the emphasis is (0..1). Suggest 0.25..0.55.")]
        public float TopEmphasisStrength { get; set; } = 0.45f;

        // ===== Visual only: Peak hold marker line =====
        [Category("Peak Hold")]
        [Description("Enable peak hold marker line (visual only).")]
        public bool PeakHoldEnabled { get; set; } = true;

        [Category("Peak Hold")]
        [Description("Hold time for peak marker in milliseconds.")]
        public int PeakHoldMilliseconds { get; set; } = 300;

        [Category("Peak Hold")]
        [Description("Peak marker decay per tick after hold. (0.5..4.0 typical)")]
        public float PeakDecayPerTick { get; set; } = 1.2f;

        [Category("Peak Hold")]
        [Description("Peak marker thickness in pixels.")]
        public int PeakLineThickness { get; set; } = 2;

        [Category("Peak Hold")]
        [Description("Peak marker color.")]
        public Color PeakLineColor { get; set; } = Color.FromArgb(255, 255, 255);

        public VerticalProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            SetStyle(ControlStyles.Opaque, true);

            _backBrush = new SolidBrush(BackColor);
            _fillBrush = new SolidBrush(ForeColor);
            _brickShadeBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0));
            _borderPen = new Pen(Color.FromArgb(120, 0, 0, 0));

            _targetValue = base.Value;
            _displayValue = base.Value;

            _peakValue = _displayValue;
            _peakHoldTicksLeft = 0;

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

                if (_peakValue > base.Maximum)
                {
                    _peakValue = base.Maximum;
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

                if (_peakValue < base.Minimum)
                {
                    _peakValue = base.Minimum;
                }

                Invalidate();
            }
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            _fillBrush?.Dispose();
            _fillBrush = new SolidBrush(ForeColor);
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

            if (HighQualityRendering)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            }

            // Background
            if (CornerRadius > 0)
            {
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
            dv = Math.Max(Minimum, Math.Min(Maximum, dv));

            var percent = (dv - Minimum) / range;
            if (percent <= 0f)
            {
                DrawBorder(e, r);
                return;
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
            var topInner = r.Top + pad;

            var fillHeight = (int)((r.Height * percent) + 0.5f);
            fillHeight = Math.Min(fillHeight, r.Height);

            var topLimit = bottom - fillHeight;

            var brickH = BrickHeight < 1 ? 1 : BrickHeight;
            var gap = BrickGap < 0 ? 0 : BrickGap;

            SolidBrush hiBrush = null;
            if (BrickHighlight && HighlightAlpha > 0)
            {
                var a = Math.Min(255, HighlightAlpha);
                hiBrush = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
            }

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

                if (HeatmapEnabled)
                {
                    var centerY = brickRect.Top + (brickRect.Height * 0.5f);
                    var innerHeight = Math.Max(1f, bottom - topInner);
                    var t = (bottom - centerY) / innerHeight; // 0 bottom -> 1 top
                    t = Clamp01(t);

                    var curve = Math.Max(0.2f, HeatIntensityCurve);
                    t = (float)Math.Pow(t, 1.0f / curve);

                    var c = HeatmapColor(t, HeatLowColor, HeatMidColor, HeatHighColor, HeatPeakColor);

                    if (TopEmphasisEnabled)
                    {
                        var start = Clamp01(TopEmphasisStart);
                        var strength = Clamp01(TopEmphasisStrength);

                        if (t >= start)
                        {
                            var u = (t - start) / Math.Max(0.0001f, 1f - start);
                            var target = Lerp(HeatPeakColor, Color.White, 0.18f);
                            c = Lerp(c, target, strength * (0.35f + (0.65f * u)));
                        }
                    }

                    if (brickRect.Height >= 3)
                    {
                        using (var lg = new LinearGradientBrush(
                            brickRect,
                            Lighten(c, 0.18f),
                            Darken(c, 0.15f),
                            LinearGradientMode.Vertical))
                        {
                            e.Graphics.FillRectangle(lg, brickRect);
                        }
                    }
                    else
                    {
                        using (var b = new SolidBrush(c))
                        {
                            e.Graphics.FillRectangle(b, brickRect);
                        }
                    }
                }
                else
                {
                    e.Graphics.FillRectangle(_fillBrush, brickRect);
                }

                if (brickRect.Height >= 3)
                {
                    var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                    e.Graphics.FillRectangle(_brickShadeBrush, shadeRect);
                }

                if (hiBrush != null && brickRect.Height >= 5)
                {
                    e.Graphics.FillRectangle(hiBrush, brickRect.X + 1, brickRect.Y + 3, brickRect.Width - 2, 1);
                }

                y = brickTop - gap;
            }

            hiBrush?.Dispose();

            // Peak hold marker line (visual only)
            if (PeakHoldEnabled)
            {
                var pv = Math.Max(Minimum, Math.Min(Maximum, _peakValue));
                var p = Clamp01((pv - Minimum) / range);

                var py = bottom - (int)(((bottom - topInner) * p) + 0.5f);

                var thickness = Math.Max(1, PeakLineThickness);
                var half = thickness / 2;

                var peakRect = new Rectangle(innerX, py - half, innerW, thickness);

                if (peakRect.Top < topInner)
                {
                    peakRect.Y = topInner;
                }

                if (peakRect.Bottom > bottom)
                {
                    peakRect.Y = bottom - peakRect.Height;
                }

                using (var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                {
                    var shadowRect = peakRect;
                    shadowRect.Y += 1;
                    e.Graphics.FillRectangle(shadow, shadowRect);
                }
                using (var pb = new SolidBrush(PeakLineColor))
                {
                    e.Graphics.FillRectangle(pb, peakRect);
                }
            }

            DrawBorder(e, r);
        }

        private void DrawBorder(PaintEventArgs e, Rectangle r)
        {
            if (CornerRadius > 0)
            {
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

            d = Math.Min(d, r.Width);
            d = Math.Min(d, r.Height);

            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color HeatmapColor(float t, Color low, Color mid, Color high, Color peak)
        {
            if (t <= 0.55f)
            {
                var u = t / 0.55f;
                return Lerp(low, mid, u);
            }
            if (t <= 0.85f)
            {
                var u = (t - 0.55f) / 0.30f;
                return Lerp(mid, high, u);
            }
            var v = (t - 0.85f) / 0.15f;
            return Lerp(high, peak, v);
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            t = Clamp01(t);
            var r = a.R + (int)((b.R - a.R) * t);
            var g = a.G + (int)((b.G - a.G) * t);
            var bl = a.B + (int)((b.B - a.B) * t);
            return Color.FromArgb(255, r, g, bl);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static Color Lighten(Color c, float amount)
        {
            amount = Clamp01(amount);
            var r = c.R + (int)((255 - c.R) * amount);
            var g = c.G + (int)((255 - c.G) * amount);
            var b = c.B + (int)((255 - c.B) * amount);
            return Color.FromArgb(c.A, r, g, b);
        }

        private static Color Darken(Color c, float amount)
        {
            amount = Clamp01(amount);
            var r = (int)(c.R * (1f - amount));
            var g = (int)(c.G * (1f - amount));
            var b = (int)(c.B * (1f - amount));
            return Color.FromArgb(c.A, r, g, b);
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
                var intervalMs = s_timer?.Interval ?? 16;

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
                            ctrl.UpdatePeakHold(intervalMs);
                            ctrl.Invalidate();
                        }
                        else
                        {
                            ctrl.UpdatePeakHold(intervalMs);
                        }
                        continue;
                    }

                    ctrl._displayValue = dv + (delta * SmoothFactor);
                    ctrl.UpdatePeakHold(intervalMs);
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

        private void UpdatePeakHold(int timerIntervalMs)
        {
            if (!PeakHoldEnabled)
            {
                _peakValue = _displayValue;
                _peakHoldTicksLeft = 0;
                return;
            }

            var holdTicks = Math.Max(1, (int)Math.Ceiling(PeakHoldMilliseconds / Math.Max(1.0, timerIntervalMs)));

            if (_displayValue >= _peakValue)
            {
                _peakValue = _displayValue;
                _peakHoldTicksLeft = holdTicks;
                return;
            }

            if (_peakHoldTicksLeft > 0)
            {
                _peakHoldTicksLeft--;
                return;
            }

            var decay = Math.Max(0.1f, PeakDecayPerTick);
            _peakValue -= decay;

            if (_peakValue < _displayValue)
            {
                _peakValue = _displayValue;
            }

            if (_peakValue < Minimum)
            {
                _peakValue = Minimum;
            }

            if (_peakValue > Maximum)
            {
                _peakValue = Maximum;
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