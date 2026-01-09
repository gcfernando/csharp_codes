using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Spectrum
{
    // Developer : Gehan Fernando Extended modes + performance-safe drawing: GPT-5.2 Thinking edition

    [Description("Vertical spectrum-style progress bar optimized for real-time updates")]
    [Category("VerticalProgressBar")]
    [RefreshProperties(RefreshProperties.All)]
    public class VerticalProgressBar : ProgressBar
    {
        // ========================= Shared timer for all instances (low overhead) =========================
        private static readonly object _lock = new object();
        private static Timer _timer;
        private static readonly List<WeakReference<VerticalProgressBar>> _instances =
            new List<WeakReference<VerticalProgressBar>>(128);

        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private static long _lastTicks;

        // ========================= Internal state =========================
        private volatile int _targetValue;
        private float _displayValue;

        // Peak hold
        private float _peakValue;
        private float _peakHoldLeftMs;

        // ========================= Cached geometry + colors =========================
        private readonly List<Rectangle> _brickRects = new List<Rectangle>(256);
        private readonly List<float> _brickT = new List<float>(256);          // 0 bottom -> 1 top
        private readonly List<Color> _brickHeatColors = new List<Color>(256); // cached heat colors

        private int _cachedWidth = -1;
        private int _cachedHeight = -1;
        private bool _colorsDirty = true;

        // ========================= Reusable GDI objects =========================
        private SolidBrush _backgroundBrush;
        private readonly SolidBrush _workBrush = new SolidBrush(Color.Black);
        private readonly SolidBrush _brickShadeBrush = new SolidBrush(Color.FromArgb(65, 0, 0, 0));
        private SolidBrush _brickHighlightBrush;
        private readonly Pen _borderPen = new Pen(Color.FromArgb(120, 0, 0, 0), 1f);

        // Peak marker brush (no per-frame alloc)
        private readonly SolidBrush _peakBrush = new SolidBrush(Color.White);
        private Color _cachedPeakColor = Color.Empty;

        // Reusable pen for "Line/Wave" modes
        private readonly Pen _linePen = new Pen(Color.Lime, 2f);
        private Color _cachedLineColor = Color.Empty;
        private float _cachedLineWidth = -1f;

        // Cached gradient brush for Gradient mode (avoid per-frame alloc)
        private LinearGradientBrush _gradientBrush;
        private Rectangle _cachedGradientRect;
        private bool _gradientDirty = true;

        // Wave points cache (avoid per-frame alloc)
        private readonly Point[] _wavePoints = new Point[96]; // fixed capacity; we use only part

        // ========================= Visualization (NO new public properties) =========================
        private enum VisualizationMode
        {
            Bricks,
            Dots,
            Center,
            Mirror,

            // Added
            Line,
            Wave,
            Gradient,
            Pulse,
            Spectrum
        }

        // Cached Tag -> mode (no parsing every tick)
        private object _cachedTagObject;
        private string _cachedTagText;
        private VisualizationMode _cachedMode = VisualizationMode.Bricks;

        private VisualizationMode GetVisualizationModeCached()
        {
            var tagObj = Tag;
            if (!ReferenceEquals(tagObj, _cachedTagObject))
            {
                _cachedTagObject = tagObj;
                _cachedTagText = tagObj?.ToString();
                _cachedMode = ParseVisualizationMode(_cachedTagText);
            }
            return _cachedMode;
        }

        private static VisualizationMode ParseVisualizationMode(string tagText)
        {
            if (string.IsNullOrWhiteSpace(tagText))
            {
                return VisualizationMode.Bricks;
            }

            // allow "Mode|Index"
            var pipeIndex = tagText.IndexOf('|');
            if (pipeIndex >= 0)
            {
                tagText = tagText.Substring(0, pipeIndex);
            }

            tagText = tagText.Trim();

            // fast paths (avoid ToLowerInvariant alloc when possible)
            if (tagText == "Bricks" || tagText == "LED" || tagText == "Led" || tagText == "bricks" || tagText == "led")
            {
                return VisualizationMode.Bricks;
            }

            if (tagText == "Dots" || tagText == "dots")
            {
                return VisualizationMode.Dots;
            }

            if (tagText == "Center" || tagText == "center")
            {
                return VisualizationMode.Center;
            }

            if (tagText == "Mirror" || tagText == "mirror")
            {
                return VisualizationMode.Mirror;
            }

            if (tagText == "Line" || tagText == "line")
            {
                return VisualizationMode.Line;
            }

            if (tagText == "Wave" || tagText == "wave")
            {
                return VisualizationMode.Wave;
            }

            if (tagText == "Gradient" || tagText == "gradient")
            {
                return VisualizationMode.Gradient;
            }

            if (tagText == "Pulse" || tagText == "pulse")
            {
                return VisualizationMode.Pulse;
            }

            if (tagText == "Spectrum" || tagText == "spectrum")
            {
                return VisualizationMode.Spectrum;
            }

            // fallback
            tagText = tagText.ToLowerInvariant();
            switch (tagText)
            {
                case "bricks":
                case "led":
                    return VisualizationMode.Bricks;
                case "dots":
                    return VisualizationMode.Dots;
                case "center":
                    return VisualizationMode.Center;
                case "mirror":
                    return VisualizationMode.Mirror;
                case "line":
                    return VisualizationMode.Line;
                case "wave":
                    return VisualizationMode.Wave;
                case "gradient":
                    return VisualizationMode.Gradient;
                case "pulse":
                    return VisualizationMode.Pulse;
                case "spectrum":
                    return VisualizationMode.Spectrum;
                default:
                    return VisualizationMode.Bricks;
            }
        }

        private static bool ModeSnapsDown(VisualizationMode mode)
            => mode == VisualizationMode.Center || mode == VisualizationMode.Mirror;

        // Peak marker makes sense for typical level displays; keep it for all except Center/Mirror.
        private static bool ModeHasPeakMarker(VisualizationMode mode)
            => mode != VisualizationMode.Center && mode != VisualizationMode.Mirror;

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
                BuildBrickHighlightBrush();
                Invalidate();
            }
        }
        private int _highlightAlpha = 55;

        [Category("Appearance")]
        [Description("High quality rendering (anti-alias / high quality). Turn OFF for max FPS.")]
        public bool HighQualityRendering { get; set; } = false;

        // ========================= Heatmap =========================
        [Category("Heatmap")]
        [Description("Enable heatmap coloring (green -> yellow -> orange -> red).")]
        public bool HeatmapEnabled
        {
            get => _heatmapEnabled;
            set { _heatmapEnabled = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private bool _heatmapEnabled = true;

        [Category("Heatmap")]
        [Description("Bottom (low) color.")]
        public Color HeatLowColor
        {
            get => _heatLowColor;
            set { _heatLowColor = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private Color _heatLowColor = Color.FromArgb(0, 255, 80);

        [Category("Heatmap")]
        [Description("Mid (yellow) color.")]
        public Color HeatMidColor
        {
            get => _heatMidColor;
            set { _heatMidColor = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private Color _heatMidColor = Color.FromArgb(255, 235, 0);

        [Category("Heatmap")]
        [Description("High (orange) color.")]
        public Color HeatHighColor
        {
            get => _heatHighColor;
            set { _heatHighColor = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private Color _heatHighColor = Color.FromArgb(255, 140, 0);

        [Category("Heatmap")]
        [Description("Peak (top) color. Set to RED for spectrum look.")]
        public Color HeatPeakColor
        {
            get => _heatPeakColor;
            set { _heatPeakColor = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private Color _heatPeakColor = Color.Red;

        [Category("Heatmap")]
        [Description("Boost low-level visibility (1.0=linear; 1.3..2.2 looks better for audio).")]
        public float HeatIntensityCurve
        {
            get => _heatCurve;
            set { _heatCurve = Math.Max(0.15f, value); _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private float _heatCurve = 1.6f;

        [Category("Heatmap")]
        [Description("Enable a stronger emphasis for the top zone (more 'spectrum-like').")]
        public bool TopEmphasisEnabled
        {
            get => _topEmphasisEnabled;
            set { _topEmphasisEnabled = value; _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private bool _topEmphasisEnabled = true;

        [Category("Heatmap")]
        [Description("Where emphasis starts (0..1). Example: 0.85 means top 15% emphasized.")]
        public float TopEmphasisStart
        {
            get => _topEmphasisStart;
            set { _topEmphasisStart = Clamp01(value); _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private float _topEmphasisStart = 0.85f;

        [Category("Heatmap")]
        [Description("How strong the emphasis is (0..1). Suggest 0.25..0.55.")]
        public float TopEmphasisStrength
        {
            get => _topEmphasisStrength;
            set { _topEmphasisStrength = Clamp01(value); _colorsDirty = true; _gradientDirty = true; Invalidate(); }
        }
        private float _topEmphasisStrength = 0.45f;

        // ========================= Peak hold =========================
        [Category("Peak Hold")]
        [Description("Enable peak hold marker line/dot.")]
        public bool PeakHoldEnabled { get; set; } = true;

        [Category("Peak Hold")]
        [Description("Peak hold time (ms).")]
        public int PeakHoldMilliseconds { get; set; } = 140;

        [Category("Peak Hold")]
        [Description("Peak decay per 60fps tick (kept for compatibility).")]
        public float PeakDecayPerTick { get; set; } = 1.2f;

        [Category("Peak Hold")]
        [Description("Peak marker thickness in pixels (line mode).")]
        public int PeakLineThickness { get; set; } = 2;

        [Category("Peak Hold")]
        [Description("Peak marker color.")]
        public Color PeakLineColor { get; set; } = Color.FromArgb(255, 255, 255);

        // ========================= Performance =========================
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
        [Description("Response time in ms. Lower = snappier, less lag. Typical: 20..70.")]
        public int ResponseTimeMs { get; set; } = 45;

        [Category("Performance")]
        [Description("If target jumps up more than this many units, snap immediately. 0 disables.")]
        public int SnapUpThreshold { get; set; } = 6;

        // ========================= ctor / dispose =========================
        public VerticalProgressBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;

            _backgroundBrush = new SolidBrush(BackColor);
            BuildBrickHighlightBrush();

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

                _backgroundBrush?.Dispose();
                _brickHighlightBrush?.Dispose();
                _borderPen?.Dispose();
                _peakBrush?.Dispose();

                _workBrush?.Dispose();
                _brickShadeBrush?.Dispose();

                _linePen?.Dispose();
                _gradientBrush?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            _backgroundBrush?.Dispose();
            _backgroundBrush = new SolidBrush(BackColor);
            Invalidate();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            _colorsDirty = true;
            _gradientDirty = true;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _cachedWidth = -1;
            _cachedHeight = -1;
            _colorsDirty = true;
            _gradientDirty = true;
            Invalidate();
        }

        // Keep base.Value usable; treat it as "target"
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
            var bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            ConfigureGraphicsQuality(e.Graphics);

            // Background + border
            e.Graphics.FillRectangle(_backgroundBrush, bounds);
            e.Graphics.DrawRectangle(_borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            var padding = BrickPadding;
            var innerX = bounds.X + padding;
            var innerW = bounds.Width - (padding * 2);
            var topInner = bounds.Top + padding;
            var bottomInner = bounds.Bottom - padding;

            if (innerW <= 0 || bottomInner <= topInner)
            {
                return;
            }

            var innerH = Math.Max(1, bottomInner - topInner);

            float range = Math.Max(1, Maximum - Minimum);
            var fillPercent = Clamp01((_displayValue - Minimum) / range);

            var filledH = (int)Math.Round(innerH * fillPercent);
            var topLimit = bottomInner - filledH;

            var mode = GetVisualizationModeCached();

            // NO "break logic": use if/else with early return semantics
            if (mode == VisualizationMode.Bricks)
            {
                DrawMode_Bricks(e.Graphics, bounds, topLimit);
            }
            else if (mode == VisualizationMode.Dots)
            {
                DrawMode_Dots(e.Graphics, bounds, topLimit);
            }
            else if (mode == VisualizationMode.Center)
            {
                DrawMode_Center(e.Graphics, innerX, innerW, topInner, innerH, fillPercent);
            }
            else if (mode == VisualizationMode.Mirror)
            {
                DrawMode_Mirror(e.Graphics, innerX, innerW, topInner, innerH, fillPercent);
            }
            else if (mode == VisualizationMode.Line)
            {
                DrawMode_Line(e.Graphics, innerX, innerW, topInner, bottomInner, innerH, fillPercent);
            }
            else if (mode == VisualizationMode.Wave)
            {
                DrawMode_Wave(e.Graphics, innerX, innerW, topInner, bottomInner, innerH, fillPercent);
            }
            else if (mode == VisualizationMode.Gradient)
            {
                DrawMode_Gradient(e.Graphics, innerX, innerW, topInner, bottomInner, innerH, fillPercent);
            }
            else if (mode == VisualizationMode.Pulse)
            {
                DrawMode_Pulse(e.Graphics, bounds, topLimit, fillPercent);
            }
            else // Spectrum
            {
                DrawMode_Spectrum(e.Graphics, bounds, topLimit, fillPercent);
            }

            if (PeakHoldEnabled && ModeHasPeakMarker(mode))
            {
                EnsurePeakBrushUpToDate();

                // Dots mode uses dot marker; everything else uses line marker (including Line/Wave/Gradient/Pulse/Spectrum)
                if (mode == VisualizationMode.Dots)
                {
                    DrawPeakMarker_Dot(e.Graphics, bounds, innerX, innerW, topInner, bottomInner, innerH, range);
                }
                else
                {
                    DrawPeakMarker_Line(e.Graphics, innerX, innerW, topInner, bottomInner, innerH, range);
                }
            }
        }

        private void ConfigureGraphicsQuality(Graphics g)
        {
            if (HighQualityRendering)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            }
            else
            {
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.None;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
            }
        }

        // ========================= Mode Draw: Bricks =========================
        private void DrawMode_Bricks(Graphics g, Rectangle bounds, int topLimit)
        {
            EnsureBrickGeometry(bounds);
            if (_colorsDirty)
            {
                RebuildBrickHeatColors();
            }

            for (var i = 0; i < _brickRects.Count; i++)
            {
                var brickRect = _brickRects[i];
                var isActive = brickRect.Top >= topLimit;

                if (isActive)
                {
                    _workBrush.Color = HeatmapEnabled
                        ? _brickHeatColors[i]
                        : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                    g.FillRectangle(_workBrush, brickRect);

                    if (brickRect.Height >= 3)
                    {
                        var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                        g.FillRectangle(_brickShadeBrush, shadeRect);
                    }

                    if (BrickHighlight && _brickHighlightBrush != null && brickRect.Height >= 5)
                    {
                        g.FillRectangle(_brickHighlightBrush,
                            brickRect.X + 1,
                            brickRect.Y + 3,
                            Math.Max(1, brickRect.Width - 2),
                            1);
                    }
                }
                else
                {
                    _workBrush.Color = Color.FromArgb(30, 255, 255, 255);
                    g.FillRectangle(_workBrush, brickRect);
                }
            }
        }

        // ========================= Mode Draw: Dots =========================
        private void DrawMode_Dots(Graphics g, Rectangle bounds, int topLimit)
        {
            EnsureBrickGeometry(bounds);
            if (_colorsDirty)
            {
                RebuildBrickHeatColors();
            }

            // Active dots
            for (var i = 0; i < _brickRects.Count; i++)
            {
                var rect = _brickRects[i];
                if (rect.Top < topLimit)
                {
                    continue;
                }

                _workBrush.Color = HeatmapEnabled
                    ? _brickHeatColors[i]
                    : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                var dotSize = GetDotSizeFromBrick(rect);
                var x = rect.X + ((rect.Width - dotSize) / 2);
                var y = rect.Y + ((rect.Height - dotSize) / 2);
                g.FillEllipse(_workBrush, x, y, dotSize, dotSize);
            }

            // Inactive dots (faint)
            _workBrush.Color = Color.FromArgb(20, 255, 255, 255);
            for (var i = 0; i < _brickRects.Count; i++)
            {
                var rect = _brickRects[i];
                if (rect.Top >= topLimit)
                {
                    continue;
                }

                var dotSize = GetDotSizeFromBrick(rect);
                var x = rect.X + ((rect.Width - dotSize) / 2);
                var y = rect.Y + ((rect.Height - dotSize) / 2);
                g.FillEllipse(_workBrush, x, y, dotSize, dotSize);
            }
        }

        private static int GetDotSizeFromBrick(Rectangle brickRect)
        {
            var dotSize = Math.Min(brickRect.Width, brickRect.Height) - 1;
            return dotSize < 1 ? 1 : dotSize;
        }

        // ========================= Mode Draw: Center =========================
        private void DrawMode_Center(Graphics g, int innerX, int innerW, int topInner, int innerH, float fillPercent)
        {
            var centerY = topInner + (innerH / 2);
            var halfFill = (int)Math.Round(innerH * fillPercent / 2f);

            _workBrush.Color = GetLevelColor(fillPercent);

            var upRect = new Rectangle(innerX, centerY - halfFill, innerW, halfFill);
            if (upRect.Height > 0)
            {
                g.FillRectangle(_workBrush, upRect);
            }

            var downRect = new Rectangle(innerX, centerY, innerW, halfFill);
            if (downRect.Height > 0)
            {
                g.FillRectangle(_workBrush, downRect);
            }

            _workBrush.Color = Color.FromArgb(20, 255, 255, 255);
            g.FillRectangle(_workBrush, new Rectangle(innerX, topInner, innerW, innerH));
        }

        // ========================= Mode Draw: Mirror =========================
        private void DrawMode_Mirror(Graphics g, int innerX, int innerW, int topInner, int innerH, float fillPercent)
        {
            var halfFill = (int)Math.Round(innerH * fillPercent / 2f);

            _workBrush.Color = GetLevelColor(fillPercent);

            var bottomRect = new Rectangle(innerX, topInner + innerH - halfFill, innerW, halfFill);
            if (bottomRect.Height > 0)
            {
                g.FillRectangle(_workBrush, bottomRect);
            }

            var topRect = new Rectangle(innerX, topInner, innerW, halfFill);
            if (topRect.Height > 0)
            {
                g.FillRectangle(_workBrush, topRect);
            }

            _workBrush.Color = Color.FromArgb(20, 255, 255, 255);
            g.FillRectangle(_workBrush, new Rectangle(innerX, topInner, innerW, innerH));
        }

        // ========================= NEW: Mode Draw: Line ========================= Music-style
        // solid column, ultra fast, looks clean at high FPS.
        private void DrawMode_Line(Graphics g, int innerX, int innerW, int topInner, int bottomInner, int innerH, float fillPercent)
        {
            // faint track
            _workBrush.Color = Color.FromArgb(22, 255, 255, 255);
            g.FillRectangle(_workBrush, new Rectangle(innerX, topInner, innerW, innerH));

            var filledH = (int)Math.Round(innerH * fillPercent);
            if (filledH <= 0)
            {
                return;
            }

            var fillRect = new Rectangle(innerX, bottomInner - filledH, innerW, filledH);

            _workBrush.Color = GetLevelColor(fillPercent);
            g.FillRectangle(_workBrush, fillRect);

            EnsureLinePenUpToDate(fillPercent, 2f);

            // edge line for crispness
            g.DrawRectangle(_linePen, fillRect.X, fillRect.Y, fillRect.Width - 1, fillRect.Height - 1);
        }

        // ========================= NEW: Mode Draw: Gradient ========================= Full-height
        // heat gradient masked by current level (classic meter feel).
        private void DrawMode_Gradient(Graphics g, int innerX, int innerW, int topInner, int bottomInner, int innerH, float fillPercent)
        {
            var fullRect = new Rectangle(innerX, topInner, innerW, innerH);

            // faint track
            _workBrush.Color = Color.FromArgb(18, 255, 255, 255);
            g.FillRectangle(_workBrush, fullRect);

            var filledH = (int)Math.Round(innerH * fillPercent);
            if (filledH <= 0)
            {
                return;
            }

            var fillRect = new Rectangle(innerX, bottomInner - filledH, innerW, filledH);

            EnsureGradientBrush(fullRect);
            g.FillRectangle(_gradientBrush, fillRect);

            // subtle shade cap (top of the fill) like a "glow"
            if (filledH >= 3)
            {
                _workBrush.Color = Color.FromArgb(45, 255, 255, 255);
                g.FillRectangle(_workBrush, new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, 2));
            }
        }

        private void EnsureGradientBrush(Rectangle fullRect)
        {
            if (!_gradientDirty &&
                _gradientBrush != null &&
                _cachedGradientRect == fullRect)
            {
                return;
            }

            _gradientDirty = false;
            _cachedGradientRect = fullRect;

            _gradientBrush?.Dispose();

            // NOTE: LinearGradientBrush goes Top->Bottom; we want bottom=low, top=peak. We'll build
            // with blend positions to approximate your HSV heat curve zones.
            var topColor = HeatmapEnabled ? HeatPeakColor : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);
            var bottomColor = HeatmapEnabled ? HeatLowColor : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

            _gradientBrush = new LinearGradientBrush(fullRect, topColor, bottomColor, LinearGradientMode.Vertical);

            if (!HeatmapEnabled)
            {
                return;
            }

            // 4-point blend: top(peak), high, mid, bottom(low) positions are normalized from 0(top)
            // to 1(bottom)
            var cb = new ColorBlend(4)
            {
                Colors = new[]
                {
                    HeatPeakColor,
                    HeatHighColor,
                    HeatMidColor,
                    HeatLowColor
                },
                Positions = new[]
                {
                    0.00f,
                    0.18f,  // roughly your 0.82..1 top zone squeezed near top
                    0.45f,  // mid zone
                    1.00f
                }
            };

            // Top emphasis: push more red/orange near the top (still no per-frame)
            if (TopEmphasisEnabled)
            {
                // shift peak dominance a bit lower
                cb.Positions[1] = Clamp01(0.10f + (0.12f * (1f - TopEmphasisStart)));
            }

            _gradientBrush.InterpolationColors = cb;
        }

        // ========================= NEW: Mode Draw: Wave ========================= Musician feel:
        // animated waveform inside the filled area, fast polyline, no allocations.
        private void DrawMode_Wave(Graphics g, int innerX, int innerW, int topInner, int bottomInner, int innerH, float fillPercent)
        {
            // faint track
            _workBrush.Color = Color.FromArgb(18, 255, 255, 255);
            g.FillRectangle(_workBrush, new Rectangle(innerX, topInner, innerW, innerH));

            var filledH = (int)Math.Round(innerH * fillPercent);
            if (filledH <= 0)
            {
                return;
            }

            var fillRect = new Rectangle(innerX, bottomInner - filledH, innerW, filledH);

            // base fill (dim) so wave reads well
            var baseColor = GetLevelColor(fillPercent);
            _workBrush.Color = Color.FromArgb(120, baseColor.R, baseColor.G, baseColor.B);
            g.FillRectangle(_workBrush, fillRect);

            // waveform line
            var t = (float)_stopwatch.Elapsed.TotalSeconds;

            // amplitude scales with width and level (nice musical behavior)
            var amp = Math.Max(1f, innerW * 0.22f * (0.25f + (0.75f * fillPercent)));
            var freq = 2.2f; // cycles per second
            var phase = t * (float)(Math.PI * 2) * freq;

            // number of points (cap by fixed array length)
            var points = Math.Min(_wavePoints.Length, Math.Max(12, fillRect.Height / 4));
            var midX = innerX + (innerW / 2);

            // step along Y (top->bottom)
            var stepY = points <= 1 ? 1 : (float)fillRect.Height / (points - 1);
            for (var i = 0; i < points; i++)
            {
                var y = fillRect.Y + (int)Math.Round(i * stepY);
                var yy01 = fillRect.Height <= 1 ? 0f : (float)i / (points - 1); // 0 top..1 bottom

                // stronger wiggle near the top (like harmonics lighting up)
                var localAmp = amp * (0.35f + (0.65f * (1f - yy01)));

                var x = midX + (int)Math.Round(((float)Math.Sin(phase + (yy01 * 6.0f))) * localAmp);
                if (x < innerX)
                {
                    x = innerX;
                }

                if (x > innerX + innerW - 1)
                {
                    x = innerX + innerW - 1;
                }

                _wavePoints[i] = new Point(x, y);
            }

            EnsureLinePenUpToDate(fillPercent, 2.2f);

            // draw waveform
            if (points >= 2)
            {
                for (var i = 1; i < points; i++)
                {
                    g.DrawLine(_linePen, _wavePoints[i - 1], _wavePoints[i]);
                }
            }
        }

        // ========================= NEW: Mode Draw: Pulse ========================= Bricks with
        // time-based "breathing" intensity; still cached geometry/colors.
        private void DrawMode_Pulse(Graphics g, Rectangle bounds, int topLimit, float fillPercent)
        {
            EnsureBrickGeometry(bounds);
            if (_colorsDirty)
            {
                RebuildBrickHeatColors();
            }

            var t = (float)_stopwatch.Elapsed.TotalSeconds;

            // musically nice: pulse rate increases slightly with level
            var rate = 1.2f + (2.2f * fillPercent);
            var pulse = 0.55f + (0.45f * (float)Math.Sin(t * (float)(Math.PI * 2.0) * rate));
            pulse = Clamp01(pulse);

            for (var i = 0; i < _brickRects.Count; i++)
            {
                var brickRect = _brickRects[i];
                var isActive = brickRect.Top >= topLimit;

                if (isActive)
                {
                    var c = HeatmapEnabled ? _brickHeatColors[i] : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                    // pulse as brightness via alpha mix toward white (fast RGB lerp)
                    var strength = 0.10f + (0.35f * pulse);
                    var cp = LerpRgb(c, Color.White, strength);

                    _workBrush.Color = cp;
                    g.FillRectangle(_workBrush, brickRect);

                    if (brickRect.Height >= 3)
                    {
                        var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                        g.FillRectangle(_brickShadeBrush, shadeRect);
                    }

                    if (BrickHighlight && _brickHighlightBrush != null && brickRect.Height >= 5)
                    {
                        g.FillRectangle(_brickHighlightBrush,
                            brickRect.X + 1,
                            brickRect.Y + 3,
                            Math.Max(1, brickRect.Width - 2),
                            1);
                    }
                }
                else
                {
                    _workBrush.Color = Color.FromArgb(22, 255, 255, 255);
                    g.FillRectangle(_workBrush, brickRect);
                }
            }
        }

        // ========================= NEW: Mode Draw: Spectrum ========================= “Spectrum
        // analyzer” feel: bricks + stronger top glow + cleaner inactive base.
        private void DrawMode_Spectrum(Graphics g, Rectangle bounds, int topLimit, float fillPercent)
        {
            EnsureBrickGeometry(bounds);
            if (_colorsDirty)
            {
                RebuildBrickHeatColors();
            }

            // Slightly stronger inactive background than Bricks so it reads like a spectrum column
            for (var i = 0; i < _brickRects.Count; i++)
            {
                var brickRect = _brickRects[i];
                var isActive = brickRect.Top >= topLimit;

                if (isActive)
                {
                    var c = HeatmapEnabled ? _brickHeatColors[i] : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                    // Extra "spectrum" pop in the top band
                    var tt = _brickT[i]; // 0 bottom -> 1 top
                    if (TopEmphasisEnabled && tt >= TopEmphasisStart)
                    {
                        var u = (tt - TopEmphasisStart) / Math.Max(0.0001f, 1f - TopEmphasisStart);
                        var boost = 0.10f + (0.25f * u);
                        c = LerpRgb(c, HeatPeakColor, Clamp01(boost));
                    }

                    _workBrush.Color = c;
                    g.FillRectangle(_workBrush, brickRect);

                    // inner shade
                    if (brickRect.Height >= 3)
                    {
                        var shadeRect = new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2);
                        g.FillRectangle(_brickShadeBrush, shadeRect);
                    }

                    // glow line for top-ish bricks (cheap, looks good)
                    if (tt > 0.72f && brickRect.Height >= 4)
                    {
                        _workBrush.Color = Color.FromArgb(60, 255, 255, 255);
                        g.FillRectangle(_workBrush, new Rectangle(brickRect.X + 1, brickRect.Y + 1, Math.Max(1, brickRect.Width - 2), 1));
                    }

                    if (BrickHighlight && _brickHighlightBrush != null && brickRect.Height >= 5)
                    {
                        g.FillRectangle(_brickHighlightBrush,
                            brickRect.X + 1,
                            brickRect.Y + 3,
                            Math.Max(1, brickRect.Width - 2),
                            1);
                    }
                }
                else
                {
                    // spectrum-style "grid" background
                    _workBrush.Color = Color.FromArgb(16, 255, 255, 255);
                    g.FillRectangle(_workBrush, brickRect);
                }
            }

            // subtle overall gloss proportional to level (no extra objects)
            if (fillPercent > 0.01f)
            {
                var pad = BrickPadding;
                var innerX = bounds.X + pad;
                var innerW = bounds.Width - (pad * 2);
                var top = bounds.Top + pad;
                var bottom = bounds.Bottom - pad;
                var innerH = Math.Max(1, bottom - top);
                var filledH = (int)Math.Round(innerH * fillPercent);
                var fillRect = new Rectangle(innerX, bottom - filledH, innerW, filledH);

                _workBrush.Color = Color.FromArgb((int)(18 + (30 * fillPercent)), 255, 255, 255);
                if (fillRect.Height >= 6)
                {
                    g.FillRectangle(_workBrush, new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, 2));
                }
            }
        }

        private void EnsureLinePenUpToDate(float level01, float width)
        {
            var c = GetLevelColor(level01);

            if (_cachedLineColor != c || Math.Abs(_cachedLineWidth - width) > 0.001f)
            {
                _cachedLineColor = c;
                _cachedLineWidth = width;
                _linePen.Color = c;
                _linePen.Width = width;

                // square caps are faster and crisp; round looks nicer if HQ, but keep it stable
                _linePen.StartCap = LineCap.Square;
                _linePen.EndCap = LineCap.Square;
                _linePen.LineJoin = LineJoin.Miter;
            }
        }

        // ========================= Peak markers =========================
        private void EnsurePeakBrushUpToDate()
        {
            if (_cachedPeakColor != PeakLineColor)
            {
                _cachedPeakColor = PeakLineColor;
                _peakBrush.Color = PeakLineColor;
            }
        }

        private void DrawPeakMarker_Line(Graphics g, int innerX, int innerW, int topInner, int bottomInner, int innerH, float range)
        {
            var pv = Clamp(_peakValue, Minimum, Maximum);
            var peakPercent = Clamp01((pv - Minimum) / range);
            var peakY = bottomInner - (int)Math.Round(innerH * peakPercent);

            var thickness = Math.Max(1, PeakLineThickness);
            var half = thickness / 2;

            var peakRect = new Rectangle(innerX, peakY - half, innerW, thickness);
            if (peakRect.Top < topInner)
            {
                peakRect.Y = topInner;
            }

            if (peakRect.Bottom > bottomInner)
            {
                peakRect.Y = bottomInner - thickness;
            }

            g.FillRectangle(_peakBrush, peakRect);
        }

        private void DrawPeakMarker_Dot(Graphics g, Rectangle bounds, int innerX, int innerW, int topInner, int bottomInner, int innerH, float range)
        {
            EnsureBrickGeometry(bounds);
            if (_brickRects.Count == 0)
            {
                return;
            }

            var pv = Clamp(_peakValue, Minimum, Maximum);
            var peakPercent = Clamp01((pv - Minimum) / range);
            var peakY = bottomInner - (int)Math.Round(innerH * peakPercent);

            var dotSize = GetDotSizeFromBrick(_brickRects[0]);
            var x = innerX + ((innerW - dotSize) / 2);
            var y = peakY - (dotSize / 2);

            if (y < topInner)
            {
                y = topInner;
            }

            if (y + dotSize > bottomInner)
            {
                y = bottomInner - dotSize;
            }

            g.FillEllipse(_peakBrush, x, y, dotSize, dotSize);
        }

        // ========================= Geometry + heatmap cache =========================
        private void EnsureBrickGeometry(Rectangle bounds)
        {
            if (bounds.Width == _cachedWidth && bounds.Height == _cachedHeight)
            {
                return;
            }

            _cachedWidth = bounds.Width;
            _cachedHeight = bounds.Height;

            _brickRects.Clear();
            _brickT.Clear();
            _brickHeatColors.Clear();

            var pad = BrickPadding;
            var innerX = bounds.X + pad;
            var innerW = bounds.Width - (pad * 2);
            var top = bounds.Top + pad;
            var bottom = bounds.Bottom - pad;

            if (innerW <= 0 || bottom <= top)
            {
                return;
            }

            var brickHeight = Math.Max(1, BrickHeight);
            var gap = Math.Max(0, BrickGap);

            var y = bottom - brickHeight;
            while (y >= top)
            {
                var rect = new Rectangle(innerX, y, innerW, brickHeight);
                _brickRects.Add(rect);

                var centerY = rect.Top + (rect.Height * 0.5f);
                var innerH = Math.Max(1f, bottom - top);
                var t = (bottom - centerY) / innerH; // 0 bottom -> 1 top
                _brickT.Add(Clamp01(t));

                y -= brickHeight + gap;
            }

            for (var i = 0; i < _brickRects.Count; i++)
            {
                _brickHeatColors.Add(Color.Empty);
            }

            _colorsDirty = true;
            _gradientDirty = true;
        }

        private void RebuildBrickHeatColors()
        {
            _colorsDirty = false;

            if (!HeatmapEnabled || _brickRects.Count == 0)
            {
                return;
            }

            var curve = Math.Max(0.15f, HeatIntensityCurve);

            for (var i = 0; i < _brickRects.Count; i++)
            {
                var t = _brickT[i];

                t = (float)Math.Pow(t, 1.0f / curve);
                var c = HeatmapColorHsv(t, HeatLowColor, HeatMidColor, HeatHighColor, HeatPeakColor);

                if (TopEmphasisEnabled && t >= TopEmphasisStart)
                {
                    var u = (t - TopEmphasisStart) / Math.Max(0.0001f, 1f - TopEmphasisStart);
                    var strength = TopEmphasisStrength * (0.25f + (0.75f * u));
                    c = LerpRgb(c, HeatPeakColor, strength);
                }

                _brickHeatColors[i] = c;
            }
        }

        private void BuildBrickHighlightBrush()
        {
            _brickHighlightBrush?.Dispose();
            if (!BrickHighlight || HighlightAlpha <= 0)
            {
                _brickHighlightBrush = null;
                return;
            }
            _brickHighlightBrush = new SolidBrush(Color.FromArgb(HighlightAlpha, 255, 255, 255));
        }

        private Color GetLevelColor(float level01)
        {
            var c = HeatmapEnabled
                ? HeatmapColorHsv(level01, HeatLowColor, HeatMidColor, HeatHighColor, HeatPeakColor)
                : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

            if (HeatmapEnabled && TopEmphasisEnabled && level01 >= TopEmphasisStart)
            {
                var u = (level01 - TopEmphasisStart) / Math.Max(0.0001f, 1f - TopEmphasisStart);
                var strength = TopEmphasisStrength * (0.25f + (0.75f * u));
                c = LerpRgb(c, HeatPeakColor, strength);
            }

            return c;
        }

        // ========================= Animation =========================
        private void AnimateStep(float dtSeconds, float intervalMs)
        {
            var mode = GetVisualizationModeCached();

            var target = _targetValue;
            var current = _displayValue;

            // Center/Mirror: snap down (no falling)
            if (ModeSnapsDown(mode) && target < current)
            {
                _displayValue = target;
            }
            else
            {
                // snap up for transients
                if (SnapUpThreshold > 0 && target > current && (target - current) >= SnapUpThreshold)
                {
                    _displayValue = target;
                }
                else
                {
                    // exponential smoothing
                    var tau = Math.Max(0.01f, ResponseTimeMs / 1000f);
                    var alpha = 1f - (float)Math.Exp(-dtSeconds / tau);

                    _displayValue = current + ((target - current) * alpha);

                    if (Math.Abs(_displayValue - target) < 0.05f)
                    {
                        _displayValue = target;
                    }
                }
            }

            // Peak rules
            if (!ModeHasPeakMarker(mode))
            {
                _peakValue = _displayValue;
                _peakHoldLeftMs = 0;
            }
            else
            {
                UpdatePeakHold(intervalMs);
            }
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

            // time-based decay (smooth at any FPS); PeakDecayPerTick tuned for 60fps
            var decayPer60FpsTick = Math.Max(0.01f, PeakDecayPerTick);
            var decayScale = intervalMs / (1000f / 60f);   // 1.0 at 60fps
            var decayAmount = decayPer60FpsTick * decayScale;

            _peakValue -= decayAmount;
            if (_peakValue < _displayValue)
            {
                _peakValue = _displayValue;
            }
        }

        // ========================= Register / Unregister + timer =========================
        private void RegisterInstance()
        {
            lock (_lock)
            {
                _instances.Add(new WeakReference<VerticalProgressBar>(this));
                EnsureTimerConfigured();
            }
        }

        private void UnregisterInstance()
        {
            lock (_lock)
            {
                for (var i = _instances.Count - 1; i >= 0; i--)
                {
                    if (!_instances[i].TryGetTarget(out var ctrl) || ctrl == this || ctrl.IsDisposed)
                    {
                        _instances.RemoveAt(i);
                    }
                }

                if (_instances.Count == 0 && _timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                    _lastTicks = 0;
                }
            }
        }

        private void EnsureTimerConfigured()
        {
            lock (_lock)
            {
                if (_timer == null)
                {
                    _timer = new Timer();
                    _timer.Tick += (s, e) => TickAll();
                    _lastTicks = 0;
                }

                var interval = Math.Max(4, (int)Math.Round(1000.0 / Math.Max(15, AnimationFps)));
                if (_timer.Interval != interval)
                {
                    _timer.Interval = interval;
                }

                if (!_timer.Enabled)
                {
                    _timer.Start();
                }
            }
        }

        private static void TickAll()
        {
            lock (_lock)
            {
                if (_instances.Count == 0)
                {
                    return;
                }

                var now = _stopwatch.ElapsedTicks;
                var last = _lastTicks == 0 ? now : _lastTicks;
                _lastTicks = now;

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

                for (var i = _instances.Count - 1; i >= 0; i--)
                {
                    if (!_instances[i].TryGetTarget(out var ctrl) || ctrl.IsDisposed)
                    {
                        _instances.RemoveAt(i);
                        continue;
                    }

                    var beforeDisplay = ctrl._displayValue;
                    var beforePeak = ctrl._peakValue;

                    ctrl.AnimateStep(dt, intervalMs);

                    if (Math.Abs(ctrl._displayValue - beforeDisplay) > 0.001f ||
                        Math.Abs(ctrl._peakValue - beforePeak) > 0.001f)
                    {
                        ctrl.Invalidate();
                    }
                }

                if (_instances.Count == 0 && _timer != null)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                    _lastTicks = 0;
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

        // ========================= Color helpers (HSV) =========================
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

            var u2 = (t - 0.82f) / (1f - 0.82f);
            return LerpHsv(high, peak, u2);
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

            h = delta < 0.00001f
                ? 0f
                : max == r ? 60f * ((g - b) / delta % 6f) : max == g ? 60f * (((b - r) / delta) + 2f) : 60f * (((r - g) / delta) + 4f);

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

            var rr = (int)Math.Round((r1 + m) * 255);
            var gg = (int)Math.Round((g1 + m) * 255);
            var bb = (int)Math.Round((b1 + m) * 255);

            return Color.FromArgb(Clamp(rr, 0, 255), Clamp(gg, 0, 255), Clamp(bb, 0, 255));
        }

        // ========================= Helpers =========================
        private static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        private static int Clamp(int v, float lo, float hi) => Clamp(v, (int)lo, (int)hi); // safety
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}