using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Spectrum
{
    // Developer : Gehan Fernando

    [Description("Vertical spectrum-style progress bar optimized for real-time updates")]
    [Category("VerticalProgressBar")]
    [RefreshProperties(RefreshProperties.All)]
    public class VerticalProgressBar : ProgressBar
    {
        // ========================= Shared timer for all instances (low overhead) =========================
        private static readonly object s_lock = new object();
        private static Timer s_timer;
        private static readonly List<WeakReference<VerticalProgressBar>> s_instances =
            new List<WeakReference<VerticalProgressBar>>(128);

        private static readonly Stopwatch s_sw = Stopwatch.StartNew();
        private static long s_lastTicks;

        // ========================= Internal state =========================
        private volatile int _targetValue;
        private float _displayValue;

        // Peak hold (visual)
        private float _peakValue;
        private float _peakHoldLeftMs;

        // ========================= Cached geometry + colors (performance) =========================
        private readonly List<Rectangle> _bricks = new List<Rectangle>(256);
        private readonly List<float> _brickT = new List<float>(256);      // 0 bottom -> 1 top
        private readonly List<Color> _brickColors = new List<Color>(256);  // cached heat colors

        private int _cachedW = -1, _cachedH = -1;
        private bool _colorsDirty = true;

        // Reusable GDI objects (no per-frame allocations)
        private SolidBrush _backBrush;
        private readonly SolidBrush _workBrush = new SolidBrush(Color.Black);
        private readonly SolidBrush _brickShadeBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0));
        private SolidBrush _highlightBrush;
        private readonly Pen _borderPen = new Pen(Color.FromArgb(120, 0, 0, 0), 1f);

        // ========================= Appearance - bricks =========================
        [Category("Appearance")]
        [Description("Padding inside control.")]
        public int BrickPadding { get; set; } = 2;

        [Category("Appearance")]
        [Description("Height of each brick segment in pixels.")]
        public int BrickHeight { get; set; } = 6;

        [Category("Appearance")]
        [Description("Gap between bricks in pixels.")]
        public int BrickGap { get; set; } = 2;

        [Category("Appearance")]
        [Description("Draw a subtle highlight line inside each brick.")]
        public bool BrickHighlight { get; set; } = true;

        [Category("Appearance")]
        [Description("Opacity of brick highlight (0..255).")]
        public int HighlightAlpha
        {
            get => _highlightAlpha;
            set
            {
                _highlightAlpha = Clamp(value, 0, 255);
                BuildHighlightBrush();
                Invalidate();
            }
        }
        private int _highlightAlpha = 55;

        [Category("Appearance")]
        [Description("High quality rendering (anti-alias / high quality). Turn OFF for max FPS.")]
        public bool HighQualityRendering { get; set; } = false;

        // ========================= Heatmap (better gradient + RED at top) =========================
        [Category("Heatmap")]
        [Description("Enable heatmap coloring (green -> yellow -> orange -> red).")]
        public bool HeatmapEnabled
        {
            get => _heatmapEnabled;
            set { _heatmapEnabled = value; _colorsDirty = true; Invalidate(); }
        }
        private bool _heatmapEnabled = true;

        [Category("Heatmap")]
        [Description("Bottom (low) color.")]
        public Color HeatLowColor
        {
            get => _heatLowColor;
            set { _heatLowColor = value; _colorsDirty = true; Invalidate(); }
        }
        private Color _heatLowColor = Color.FromArgb(0, 255, 80);

        [Category("Heatmap")]
        [Description("Mid (yellow) color.")]
        public Color HeatMidColor
        {
            get => _heatMidColor;
            set { _heatMidColor = value; _colorsDirty = true; Invalidate(); }
        }
        private Color _heatMidColor = Color.FromArgb(255, 235, 0);

        [Category("Heatmap")]
        [Description("High (orange) color.")]
        public Color HeatHighColor
        {
            get => _heatHighColor;
            set { _heatHighColor = value; _colorsDirty = true; Invalidate(); }
        }
        private Color _heatHighColor = Color.FromArgb(255, 140, 0);

        [Category("Heatmap")]
        [Description("Peak (top) color. Set to RED for spectrum look.")]
        public Color HeatPeakColor
        {
            get => _heatPeakColor;
            set { _heatPeakColor = value; _colorsDirty = true; Invalidate(); }
        }
        private Color _heatPeakColor = Color.Red;

        [Category("Heatmap")]
        [Description("Boost low-level visibility (1.0=linear; 1.3..2.2 looks better for audio).")]
        public float HeatIntensityCurve
        {
            get => _heatCurve;
            set { _heatCurve = Math.Max(0.15f, value); _colorsDirty = true; Invalidate(); }
        }
        private float _heatCurve = 1.6f;

        // ===== Visual only: Top red emphasis zone (kept as you had) =====
        [Category("Heatmap")]
        [Description("Enable a stronger emphasis for the top zone (more 'spectrum-like').")]
        public bool TopEmphasisEnabled
        {
            get => _topEmphasisEnabled;
            set { _topEmphasisEnabled = value; _colorsDirty = true; Invalidate(); }
        }
        private bool _topEmphasisEnabled = true;

        [Category("Heatmap")]
        [Description("Where emphasis starts (0..1). Example: 0.85 means top 15% emphasized.")]
        public float TopEmphasisStart
        {
            get => _topEmphasisStart;
            set { _topEmphasisStart = Clamp01(value); _colorsDirty = true; Invalidate(); }
        }
        private float _topEmphasisStart = 0.85f;

        [Category("Heatmap")]
        [Description("How strong the emphasis is (0..1). Suggest 0.25..0.55.")]
        public float TopEmphasisStrength
        {
            get => _topEmphasisStrength;
            set { _topEmphasisStrength = Clamp01(value); _colorsDirty = true; Invalidate(); }
        }
        private float _topEmphasisStrength = 0.45f;

        // ========================= Peak hold (kept / compatible names) =========================
        [Category("Peak Hold")]
        [Description("Enable peak hold marker line.")]
        public bool PeakHoldEnabled { get; set; } = true;

        [Category("Peak Hold")]
        [Description("Peak hold time (ms).")]
        public int PeakHoldMilliseconds { get; set; } = 140;

        [Category("Peak Hold")]
        [Description("Peak decay per tick. (kept as requested)")]
        public float PeakDecayPerTick { get; set; } = 1.2f;

        [Category("Peak Hold")]
        [Description("Peak marker thickness in pixels. (kept as requested)")]
        public int PeakLineThickness { get; set; } = 2;

        [Category("Peak Hold")]
        [Description("Peak marker color.")]
        public Color PeakLineColor { get; set; } = Color.FromArgb(255, 255, 255);

        // ========================= Performance / Real-time update tuning =========================
        [Category("Performance")]
        [Description("Shared animation FPS (15..240). Default 60.")]
        public int AnimationFps
        {
            get => _animationFps;
            set
            {
                _animationFps = Clamp(value, 15, 240);
                EnsureTimerConfigured();
            }
        }
        private int _animationFps = 60;

        [Category("Performance")]
        [Description("Response time in ms. Lower = snappier, less lag. Typical: 30..80.")]
        public int ResponseTimeMs { get; set; } = 45;

        [Category("Performance")]
        [Description("If target jumps up more than this many units, snap immediately (reduces perceived lag). 0 disables.")]
        public int SnapUpThreshold { get; set; } = 6;

        // ========================= ctor / dispose =========================
        public VerticalProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;

            _backBrush = new SolidBrush(BackColor);
            BuildHighlightBrush();

            _targetValue = base.Value;
            _displayValue = base.Value;

            _peakValue = _displayValue;
            _peakHoldLeftMs = 0;

            RegisterInstance();
            UpdateStyles();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterInstance();

                _backBrush?.Dispose();
                _workBrush?.Dispose();
                _brickShadeBrush?.Dispose();
                _highlightBrush?.Dispose();
                _borderPen?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            _backBrush?.Dispose();
            _backBrush = new SolidBrush(BackColor);
            Invalidate();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            _colorsDirty = true;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _cachedW = -1;
            _cachedH = -1;
            _colorsDirty = true;
            Invalidate();
        }

        // keep base.Value usable; we treat it as "target"
        public new int Value
        {
            get => base.Value;
            set
            {
                var v = Clamp(value, Minimum, Maximum);
                base.Value = v;
                _targetValue = v;
            }
        }

        // Thread-safe helper for audio thread
        public void SetTargetValueThreadSafe(int value)
        {
            if (IsDisposed)
            {
                return;
            }

            var v = Clamp(value, Minimum, Maximum);
            if (InvokeRequired)
            {
                try
                { _ = BeginInvoke((Action)(() => _targetValue = v)); }
                catch { }
            }
            else
            {
                _targetValue = v;
            }
        }

        // ========================= Paint =========================
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
            else
            {
                e.Graphics.SmoothingMode = SmoothingMode.None;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
                e.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            }

            // Background (NO rounded corners)
            e.Graphics.FillRectangle(_backBrush, r);

            // Border
            e.Graphics.DrawRectangle(_borderPen, r.X, r.Y, r.Width - 1, r.Height - 1);

            EnsureGeometry(r);
            if (_colorsDirty)
            {
                RebuildColorCache();
            }

            // compute fill
            float range = Math.Max(1, Maximum - Minimum);
            var p = Clamp01((_displayValue - Minimum) / range);

            var pad = BrickPadding;
            var innerX = r.X + pad;
            var innerW = r.Width - (pad * 2);
            var topInner = r.Top + pad;
            var bottom = r.Bottom - pad;

            if (innerW <= 0 || bottom <= topInner)
            {
                return;
            }

            var innerH = Math.Max(1, bottom - topInner);
            var filledH = (int)Math.Round(innerH * p);
            var topLimit = bottom - filledH;

            // Draw bricks (from bottom->top list)
            for (var i = 0; i < _bricks.Count; i++)
            {
                var brickRect = _bricks[i];
                var active = brickRect.Top >= topLimit;

                if (active)
                {
                    _workBrush.Color = HeatmapEnabled ? _brickColors[i] : ForeColor.IsEmpty ? Color.LimeGreen : ForeColor;

                    e.Graphics.FillRectangle(_workBrush, brickRect);

                    // shade line near top of brick
                    if (brickRect.Height >= 3)
                    {
                        var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                        e.Graphics.FillRectangle(_brickShadeBrush, shadeRect);
                    }

                    // highlight line
                    if (BrickHighlight && _highlightBrush != null && brickRect.Height >= 5)
                    {
                        e.Graphics.FillRectangle(_highlightBrush,
                            brickRect.X + 1,
                            brickRect.Y + 3,
                            Math.Max(1, brickRect.Width - 2),
                            1);
                    }
                }
                else
                {
                    // inactive brick
                    _workBrush.Color = Color.FromArgb(30, 255, 255, 255);
                    e.Graphics.FillRectangle(_workBrush, brickRect);
                }
            }

            // Peak hold marker line
            if (PeakHoldEnabled)
            {
                var pv = Clamp(_peakValue, Minimum, Maximum);
                var pp = Clamp01((pv - Minimum) / range);

                var py = bottom - (int)Math.Round(innerH * pp);
                var thickness = Math.Max(1, PeakLineThickness);
                var half = thickness / 2;

                var peakRect = new Rectangle(innerX, py - half, innerW, thickness);
                if (peakRect.Top < topInner)
                {
                    peakRect.Y = topInner;
                }

                if (peakRect.Bottom > bottom)
                {
                    peakRect.Y = bottom - thickness;
                }

                using (var pb = new SolidBrush(PeakLineColor))
                {
                    e.Graphics.FillRectangle(pb, peakRect);
                }
            }
        }

        // ========================= Geometry + Heatmap cache =========================
        private void EnsureGeometry(Rectangle r)
        {
            if (r.Width == _cachedW && r.Height == _cachedH)
            {
                return;
            }

            _cachedW = r.Width;
            _cachedH = r.Height;

            _bricks.Clear();
            _brickT.Clear();
            _brickColors.Clear();

            var pad = BrickPadding;
            var innerX = r.X + pad;
            var innerW = r.Width - (pad * 2);
            var top = r.Top + pad;
            var bottom = r.Bottom - pad;

            if (innerW <= 0 || bottom <= top)
            {
                return;
            }

            var brickH = Math.Max(1, BrickHeight);
            var gap = Math.Max(0, BrickGap);

            // bottom -> top bricks
            var y = bottom - brickH;
            while (y >= top)
            {
                var rect = new Rectangle(innerX, y, innerW, brickH);
                _bricks.Add(rect);

                // 0 bottom -> 1 top based on brick center
                var centerY = rect.Top + (rect.Height * 0.5f);
                var innerH = Math.Max(1f, bottom - top);
                var t = (bottom - centerY) / innerH;
                _brickT.Add(Clamp01(t));

                y -= brickH + gap;
            }

            for (var i = 0; i < _bricks.Count; i++)
            {
                _brickColors.Add(Color.Empty);
            }

            _colorsDirty = true;
        }

        private void RebuildColorCache()
        {
            _colorsDirty = false;
            if (!HeatmapEnabled || _bricks.Count == 0)
            {
                return;
            }

            var curve = Math.Max(0.15f, HeatIntensityCurve);

            for (var i = 0; i < _bricks.Count; i++)
            {
                var t = _brickT[i];

                // boost lows (audio looks better)
                t = (float)Math.Pow(t, 1.0f / curve);

                // smoother than RGB: HSV interpolation
                var c = HeatmapColorHsv(t, HeatLowColor, HeatMidColor, HeatHighColor, HeatPeakColor);

                // Top red emphasis: push toward RED at top
                if (TopEmphasisEnabled && t >= TopEmphasisStart)
                {
                    var u = (t - TopEmphasisStart) / Math.Max(0.0001f, 1f - TopEmphasisStart); // 0..1
                    var strength = TopEmphasisStrength * (0.25f + (0.75f * u));
                    c = LerpRgb(c, HeatPeakColor, strength);
                }

                _brickColors[i] = c;
            }
        }

        private void BuildHighlightBrush()
        {
            _highlightBrush?.Dispose();
            if (!BrickHighlight || HighlightAlpha <= 0)
            {
                _highlightBrush = null;
                return;
            }
            _highlightBrush = new SolidBrush(Color.FromArgb(HighlightAlpha, 255, 255, 255));
        }

        // =========================
        // Animation: dt-based smoothing (less lag) =========================
        private void AnimateStep(float dtSeconds, float intervalMs)
        {
            var tv = _targetValue;
            var dv = _displayValue;

            // snap up for fast rising spectrum
            if (SnapUpThreshold > 0 && tv > dv && (tv - dv) >= SnapUpThreshold)
            {
                _displayValue = tv;
            }
            else
            {
                // dt-based exponential smoothing
                var tau = Math.Max(0.01f, ResponseTimeMs / 1000f);
                var alpha = 1f - (float)Math.Exp(-dtSeconds / tau);

                _displayValue = dv + ((tv - dv) * alpha);

                // deadband
                if (Math.Abs(_displayValue - tv) < 0.05f)
                {
                    _displayValue = tv;
                }
            }

            UpdatePeakHold(intervalMs);
        }

        private void UpdatePeakHold(float intervalMs)
        {
            if (!PeakHoldEnabled)
            {
                _peakValue = _displayValue;
                _peakHoldLeftMs = 0;
                return;
            }

            if (_displayValue >= _peakValue)
            {
                _peakValue = _displayValue;
                _peakHoldLeftMs = Math.Max(0, PeakHoldMilliseconds);
                return;
            }

            if (_peakHoldLeftMs > 0)
            {
                _peakHoldLeftMs -= intervalMs;
                if (_peakHoldLeftMs < 0)
                {
                    _peakHoldLeftMs = 0;
                }

                return;
            }

            // Keep your original behavior: decay per tick
            _peakValue -= Math.Max(0.01f, PeakDecayPerTick);
            if (_peakValue < _displayValue)
            {
                _peakValue = _displayValue;
            }
        }

        // ========================= Register / Unregister + timer =========================
        private void RegisterInstance()
        {
            lock (s_lock)
            {
                s_instances.Add(new WeakReference<VerticalProgressBar>(this));
                EnsureTimerConfigured();
            }
        }

        private void UnregisterInstance()
        {
            lock (s_lock)
            {
                for (var i = s_instances.Count - 1; i >= 0; i--)
                {
                    if (!s_instances[i].TryGetTarget(out var ctrl) || ctrl == this || ctrl.IsDisposed)
                    {
                        s_instances.RemoveAt(i);
                    }
                }

                if (s_instances.Count == 0 && s_timer != null)
                {
                    s_timer.Stop();
                    s_timer.Dispose();
                    s_timer = null;
                    s_lastTicks = 0;
                }
            }
        }

        private void EnsureTimerConfigured()
        {
            lock (s_lock)
            {
                if (s_timer == null)
                {
                    s_timer = new Timer();
                    s_timer.Tick += (s, e) => TickAll();
                    s_lastTicks = 0;
                }

                var interval = Math.Max(4, (int)Math.Round(1000.0 / Math.Max(15, AnimationFps)));
                if (s_timer.Interval != interval)
                {
                    s_timer.Interval = interval;
                }

                if (!s_timer.Enabled)
                {
                    s_timer.Start();
                }
            }
        }

        private static void TickAll()
        {
            lock (s_lock)
            {
                if (s_instances.Count == 0)
                {
                    return;
                }

                var now = s_sw.ElapsedTicks;
                var last = s_lastTicks == 0 ? now : s_lastTicks;
                s_lastTicks = now;

                var dt = (float)((now - last) / (double)Stopwatch.Frequency);
                if (dt <= 0)
                {
                    dt = 1f / 60f;
                }

                if (dt > 0.2f)
                {
                    dt = 0.2f;
                }

                var intervalMs = dt * 1000f;

                for (var i = s_instances.Count - 1; i >= 0; i--)
                {
                    if (!s_instances[i].TryGetTarget(out var ctrl) || ctrl.IsDisposed)
                    {
                        s_instances.RemoveAt(i);
                        continue;
                    }

                    var before = ctrl._displayValue;
                    var beforePeak = ctrl._peakValue;

                    ctrl.AnimateStep(dt, intervalMs);

                    // repaint only if visible change
                    if (Math.Abs(ctrl._displayValue - before) > 0.001f ||
                        Math.Abs(ctrl._peakValue - beforePeak) > 0.001f)
                    {
                        ctrl.Invalidate();
                    }
                }

                if (s_instances.Count == 0 && s_timer != null)
                {
                    s_timer.Stop();
                    s_timer.Dispose();
                    s_timer = null;
                    s_lastTicks = 0;
                }
            }
        }

        // ========================= ProgressBar vertical style =========================
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x04; // PBS_VERTICAL
                return cp;
            }
        }

        // ========================= Color helpers (HSV gradient looks better than RGB) =========================
        private static Color HeatmapColorHsv(float t, Color low, Color mid, Color high, Color peak)
        {
            t = Clamp01(t);

            if (t <= 0.55f)
            {
                var u = t / 0.55f;
                return LerpHsv(low, mid, u);
            }
            if (t <= 0.82f)
            {
                var u = (t - 0.55f) / (0.82f - 0.55f);
                return LerpHsv(mid, high, u);
            }
            else
            {
                var u = (t - 0.82f) / (1f - 0.82f);
                return LerpHsv(high, peak, u);
            }
        }

        private static Color LerpRgb(Color a, Color b, float t)
        {
            t = Clamp01(t);
            var r = a.R + (int)((b.R - a.R) * t);
            var g = a.G + (int)((b.G - a.G) * t);
            var bl = a.B + (int)((b.B - a.B) * t);
            var al = a.A + (int)((b.A - a.A) * t);
            return Color.FromArgb(Clamp(al, 0, 255), Clamp(r, 0, 255), Clamp(g, 0, 255), Clamp(bl, 0, 255));
        }

        private static Color LerpHsv(Color a, Color b, float t)
        {
            t = Clamp01(t);

            RgbToHsv(a, out var ah, out var asat, out var av);
            RgbToHsv(b, out var bh, out var bsat, out var bv);

            // shortest hue path
            var dh = bh - ah;
            if (dh > 180f)
            {
                dh -= 360f;
            }

            if (dh < -180f)
            {
                dh += 360f;
            }

            var h = ah + (dh * t);
            if (h < 0)
            {
                h += 360f;
            }

            if (h >= 360f)
            {
                h -= 360f;
            }

            var s = asat + ((bsat - asat) * t);
            var v = av + ((bv - av) * t);

            var alpha = a.A + (int)((b.A - a.A) * t);
            var rgb = HsvToRgb(h, s, v);
            return Color.FromArgb(Clamp(alpha, 0, 255), rgb.R, rgb.G, rgb.B);
        }

        private static void RgbToHsv(Color c, out float h, out float s, out float v)
        {
            var r = c.R / 255f;
            var g = c.G / 255f;
            var b = c.B / 255f;

            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            if (delta < 0.00001f)
            {
                h = 0f;
            }
            else
            {
                h = max == r ? 60f * ((g - b) / delta % 6f) : max == g ? 60f * (((b - r) / delta) + 2f) : 60f * (((r - g) / delta) + 4f);
            }

            if (h < 0)
            {
                h += 360f;
            }

            s = max <= 0 ? 0 : (delta / max);
            v = max;
        }

        private static Color HsvToRgb(float h, float s, float v)
        {
            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60f % 2) - 1));
            var m = v - c;

            float r1, g1, b1;
            if (h < 60)
            { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120)
            { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180)
            { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240)
            { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300)
            { r1 = x; g1 = 0; b1 = c; }
            else
            { r1 = c; g1 = 0; b1 = x; }

            var r = (int)Math.Round((r1 + m) * 255);
            var g = (int)Math.Round((g1 + m) * 255);
            var b = (int)Math.Round((b1 + m) * 255);

            return Color.FromArgb(Clamp(r, 0, 255), Clamp(g, 0, 255), Clamp(b, 0, 255));
        }

        private static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}