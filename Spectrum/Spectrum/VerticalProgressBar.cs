// Developed by Gehan Fernando

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Spectrum;

[Description("Vertical spectrum-style progress bar")]
[Category("VerticalProgressBar")]
[RefreshProperties(RefreshProperties.All)]
public sealed class VerticalProgressBar : ProgressBar
{
    // ========================= Shared timer for all instances (low overhead) =========================
    private static readonly object s_lock = new();
    private static System.Windows.Forms.Timer s_timer;
    private static readonly List<WeakReference<VerticalProgressBar>> s_instances =
        new(128);

    private static readonly Stopwatch s_stopwatch = Stopwatch.StartNew();
    private static long s_lastTicks;
    private static int s_idleTicks; // consecutive ticks with no visual change
    private const int SleepAfterIdleTicks = 12; // ~200ms at 60fps

    // ========================= Internal state =========================
    private volatile int _targetValue;
    private float _displayValue;

    // Peak hold
    private float _peakValue;
    private float _peakHoldLeftMs;

    // Quantized render state (prevents micro invalidation jitter -> flicker at silence)
    private int _lastDisplayQ = int.MinValue;
    private int _lastPeakQ = int.MinValue;
    private int _lastAnimQ = int.MinValue; // for wave/pulse time animation
    private VisualizationMode _lastModeQ = (VisualizationMode)(-1);

    // ========================= Cached geometry + colors =========================
    private readonly List<Rectangle> _brickRects = new(256);
    private readonly List<float> _brickT = new(256);          // 0 bottom -> 1 top
    private readonly List<Color> _brickHeatColors = new(256); // cached heat colors

    private int _cachedWidth = -1;
    private int _cachedHeight = -1;
    private bool _colorsDirty = true;

    // ========================= Reusable GDI objects =========================
    private SolidBrush _backgroundBrush;
    private readonly SolidBrush _workBrush = new(Color.Black);
    private readonly SolidBrush _brickShadeBrush = new(Color.FromArgb(65, 0, 0, 0));
    private SolidBrush _brickHighlightBrush;
    private readonly Pen _borderPen = new(Color.FromArgb(120, 0, 0, 0), 1f);

    // Peak marker brush (no per-frame alloc)
    private readonly SolidBrush _peakBrush = new(Color.White);
    private Color _cachedPeakColor = Color.Empty;

    // Reusable pen for Wave mode
    private readonly Pen _wavePen = new(Color.Lime, 2f);
    private Color _cachedWaveColor = Color.Empty;
    private float _cachedWaveWidth = -1f;

    // Wave points cache (avoid per-frame alloc)
    private readonly System.Drawing.Point[] _wavePoints = new System.Drawing.Point[96];

    // ========================= Visualization (NO new public properties) =========================
    private enum VisualizationMode
    {
        Bricks,
        Dots,
        Center,
        Mirror,
        Wave,
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
        if (tagText is "Bricks" or "LED" or "Led" or "bricks" or "led")
        {
            return VisualizationMode.Bricks;
        }

        if (tagText is "Dots" or "dots")
        {
            return VisualizationMode.Dots;
        }

        if (tagText is "Center" or "center")
        {
            return VisualizationMode.Center;
        }

        if (tagText is "Mirror" or "mirror")
        {
            return VisualizationMode.Mirror;
        }

        if (tagText is "Wave" or "wave")
        {
            return VisualizationMode.Wave;
        }

        if (tagText is "Pulse" or "pulse")
        {
            return VisualizationMode.Pulse;
        }

        if (tagText is "Spectrum" or "spectrum")
        {
            return VisualizationMode.Spectrum;
        }

        tagText = tagText.ToLowerInvariant();
        return tagText switch
        {
            "bricks" or "led" => VisualizationMode.Bricks,
            "dots" => VisualizationMode.Dots,
            "center" => VisualizationMode.Center,
            "mirror" => VisualizationMode.Mirror,
            "wave" => VisualizationMode.Wave,
            "pulse" => VisualizationMode.Pulse,
            "spectrum" => VisualizationMode.Spectrum,
            _ => VisualizationMode.Bricks,
        };
    }

    private static bool ModeSnapsDown(VisualizationMode mode)
        => mode is VisualizationMode.Center or VisualizationMode.Mirror;

    private static bool ModeHasPeakMarker(VisualizationMode mode)
        => mode is not VisualizationMode.Center and not VisualizationMode.Mirror;

    private static bool ModeHasTimeAnimation(VisualizationMode mode)
        => mode is VisualizationMode.Wave or VisualizationMode.Pulse;

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
        get;
        set
        {
            field = Clamp(value, 0, 255);
            BuildBrickHighlightBrush();
            Invalidate();
        }
    } = 55;

    [Category("Appearance")]
    [Description("High quality rendering (anti-alias / high quality). Turn OFF for max FPS.")]
    public bool HighQualityRendering { get; set; } = false;

    // ========================= Heatmap =========================
    [Category("Heatmap")]
    [Description("Enable heatmap coloring (green -> yellow -> orange -> red).")]
    public bool HeatmapEnabled
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = true;

    [Category("Heatmap")]
    [Description("Bottom (low) color.")]
    public Color HeatLowColor
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = Color.FromArgb(0, 255, 80);

    [Category("Heatmap")]
    [Description("Mid (yellow) color.")]
    public Color HeatMidColor
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = Color.FromArgb(255, 235, 0);

    [Category("Heatmap")]
    [Description("High (orange) color.")]
    public Color HeatHighColor
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = Color.FromArgb(255, 140, 0);

    [Category("Heatmap")]
    [Description("Peak (top) color. Set to RED for spectrum look.")]
    public Color HeatPeakColor
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = Color.Red;

    [Category("Heatmap")]
    [Description("Boost low-level visibility (1.0=linear; 1.3..2.2 looks better for audio).")]
    public float HeatIntensityCurve
    {
        get;
        set { field = Math.Max(0.15f, value); _colorsDirty = true; Invalidate(); }
    } = 1.6f;

    [Category("Heatmap")]
    [Description("Enable a stronger emphasis for the top zone (more 'spectrum-like').")]
    public bool TopEmphasisEnabled
    {
        get;
        set { field = value; _colorsDirty = true; Invalidate(); }
    } = true;

    [Category("Heatmap")]
    [Description("Where emphasis starts (0..1). Example: 0.85 means top 15% emphasized.")]
    public float TopEmphasisStart
    {
        get;
        set { field = Clamp01(value); _colorsDirty = true; Invalidate(); }
    } = 0.85f;

    [Category("Heatmap")]
    [Description("How strong the emphasis is (0..1). Suggest 0.25..0.55.")]
    public float TopEmphasisStrength
    {
        get;
        set { field = Clamp01(value); _colorsDirty = true; Invalidate(); }
    } = 0.45f;

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
        get;
        set
        {
            field = Clamp(value, 15, 240);
            EnsureTimerConfigured();
        }
    } = 60;

    [Category("Performance")]
    [Description("Response time in ms. Lower = snappier, less lag. Typical: 20..70.")]
    public int ResponseTimeMs { get; set; } = 45;

    // >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>
    // NEW (backward-compatible): Asymmetric ballistics for real PPM feel (fast attack + slow release)
    // Default: disabled => behavior identical to your current production build.
    [Category("Performance")]
    [Description("Enable asymmetric ballistics: fast attack (ResponseTimeMs) + slow release (ReleaseTimeMs). Default OFF.")]
    public bool UseAsymmetricBallistics { get; set; } = false;

    [Category("Performance")]
    [Description("Release time in ms for falling values when UseAsymmetricBallistics is ON. 0 = use ResponseTimeMs.")]
    public int ReleaseTimeMs
    {
        get;
        set => field = Clamp(value, 0, 5000);
    } = 0;

    // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<

    [Category("Performance")]
    [Description("If target jumps up more than this many units, snap immediately. 0 disables.")]
    public int SnapUpThreshold { get; set; } = 6;

    // Snap-to-min guard to stop “silent flicker” due to tiny float decay + invalidations
    private const float SilentSnapEpsilon = 0.06f;

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

            _wavePen?.Dispose();
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
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _cachedWidth = -1;
        _cachedHeight = -1;
        _colorsDirty = true;

        // geometry changes affect “render quantization”, reset so next frame repaints once
        _lastDisplayQ = int.MinValue;
        _lastPeakQ = int.MinValue;
        _lastAnimQ = int.MinValue;
        _lastModeQ = (VisualizationMode)(-1);

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

            // wake shared timer if it slept
            EnsureTimerConfigured();
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

        // IMPORTANT: wake timer on target update (fixes “sleep” edge cases)
        EnsureTimerConfigured();

        if (InvokeRequired)
        {
            try
            {
                _ = BeginInvoke(() =>
                {
                    if (!IsDisposed)
                    {
                        _targetValue = v;
                    }
                });
            }
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
            DrawMode_CenterBricks(e.Graphics, bounds, topInner, bottomInner, innerH, fillPercent);
        }
        else if (mode == VisualizationMode.Mirror)
        {
            DrawMode_MirrorBricks(e.Graphics, bounds, topInner, bottomInner, innerH, fillPercent);
        }
        else if (mode == VisualizationMode.Wave)
        {
            DrawMode_Wave(e.Graphics, innerX, innerW, topInner, bottomInner, innerH, fillPercent);
        }
        else if (mode == VisualizationMode.Pulse)
        {
            DrawMode_Pulse(e.Graphics, bounds, topLimit, fillPercent);
        }
        else
        {
            DrawMode_Spectrum(e.Graphics, bounds, topLimit, fillPercent);
        }

        if (PeakHoldEnabled && ModeHasPeakMarker(mode))
        {
            EnsurePeakBrushUpToDate();

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
                    g.FillRectangle(_brickShadeBrush, new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2));
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

    // ========================= Mode Draw: Center (BRICKS) =========================
    private void DrawMode_CenterBricks(Graphics g, Rectangle bounds, int topInner, int bottomInner, int innerH, float fillPercent)
    {
        EnsureBrickGeometry(bounds);
        if (_colorsDirty)
        {
            RebuildBrickHeatColors();
        }

        var centerY = topInner + (innerH / 2);
        var halfFillH = (int)Math.Round(innerH * fillPercent / 2f);

        var activeTop = centerY - halfFillH;
        var activeBottom = centerY + halfFillH;

        for (var i = 0; i < _brickRects.Count; i++)
        {
            var brickRect = _brickRects[i];
            var cy = brickRect.Top + (brickRect.Height / 2);
            var isActive = cy >= activeTop && cy <= activeBottom;

            if (isActive)
            {
                _workBrush.Color = HeatmapEnabled
                    ? _brickHeatColors[i]
                    : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                g.FillRectangle(_workBrush, brickRect);

                if (brickRect.Height >= 3)
                {
                    g.FillRectangle(_brickShadeBrush, new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2));
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

    // ========================= Mode Draw: Mirror (BRICKS) =========================
    private void DrawMode_MirrorBricks(Graphics g, Rectangle bounds, int topInner, int bottomInner, int innerH, float fillPercent)
    {
        EnsureBrickGeometry(bounds);
        if (_colorsDirty)
        {
            RebuildBrickHeatColors();
        }

        var halfFillH = (int)Math.Round(innerH * fillPercent / 2f);

        var topZoneBottom = topInner + halfFillH;
        var bottomZoneTop = bottomInner - halfFillH;

        for (var i = 0; i < _brickRects.Count; i++)
        {
            var brickRect = _brickRects[i];
            var cy = brickRect.Top + (brickRect.Height / 2);

            var isActive = (halfFillH > 0) && (cy <= topZoneBottom || cy >= bottomZoneTop);

            if (isActive)
            {
                _workBrush.Color = HeatmapEnabled
                    ? _brickHeatColors[i]
                    : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                g.FillRectangle(_workBrush, brickRect);

                if (brickRect.Height >= 3)
                {
                    g.FillRectangle(_brickShadeBrush, new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2));
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

    // ========================= Mode Draw: Wave =========================
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
        var t = (float)s_stopwatch.Elapsed.TotalSeconds;

        var amp = Math.Max(1f, innerW * 0.22f * (0.25f + (0.75f * fillPercent)));
        var freq = 2.2f;
        var phase = t * (float)(Math.PI * 2) * freq;

        var points = Math.Min(_wavePoints.Length, Math.Max(12, fillRect.Height / 4));
        var midX = innerX + (innerW / 2);

        var stepY = points <= 1 ? 1 : (float)fillRect.Height / (points - 1);
        for (var i = 0; i < points; i++)
        {
            var y = fillRect.Y + (int)Math.Round(i * stepY);
            var yy01 = fillRect.Height <= 1 ? 0f : (float)i / (points - 1);

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

            _wavePoints[i] = new System.Drawing.Point(x, y);
        }

        EnsureWavePenUpToDate(fillPercent, 2.2f);

        if (points >= 2)
        {
            for (var i = 1; i < points; i++)
            {
                g.DrawLine(_wavePen, _wavePoints[i - 1], _wavePoints[i]);
            }
        }
    }

    private void EnsureWavePenUpToDate(float level01, float width)
    {
        var c = GetLevelColor(level01);

        if (_cachedWaveColor != c || Math.Abs(_cachedWaveWidth - width) > 0.001f)
        {
            _cachedWaveColor = c;
            _cachedWaveWidth = width;
            _wavePen.Color = c;
            _wavePen.Width = width;

            _wavePen.StartCap = LineCap.Square;
            _wavePen.EndCap = LineCap.Square;
            _wavePen.LineJoin = LineJoin.Miter;
        }
    }

    // ========================= Mode Draw: Pulse =========================
    private void DrawMode_Pulse(Graphics g, Rectangle bounds, int topLimit, float fillPercent)
    {
        EnsureBrickGeometry(bounds);
        if (_colorsDirty)
        {
            RebuildBrickHeatColors();
        }

        var t = (float)s_stopwatch.Elapsed.TotalSeconds;

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

                var strength = 0.10f + (0.35f * pulse);
                var cp = LerpRgb(c, Color.White, strength);

                _workBrush.Color = cp;
                g.FillRectangle(_workBrush, brickRect);

                if (brickRect.Height >= 3)
                {
                    g.FillRectangle(_brickShadeBrush, new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2));
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

    // ========================= Mode Draw: Spectrum =========================
    private void DrawMode_Spectrum(Graphics g, Rectangle bounds, int topLimit, float fillPercent)
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
                var c = HeatmapEnabled ? _brickHeatColors[i] : (ForeColor.IsEmpty ? Color.LimeGreen : ForeColor);

                var tt = _brickT[i];
                if (TopEmphasisEnabled && tt >= TopEmphasisStart)
                {
                    var u = (tt - TopEmphasisStart) / Math.Max(0.0001f, 1f - TopEmphasisStart);
                    var boost = 0.10f + (0.25f * u);
                    c = LerpRgb(c, HeatPeakColor, Clamp01(boost));
                }

                _workBrush.Color = c;
                g.FillRectangle(_workBrush, brickRect);

                if (brickRect.Height >= 3)
                {
                    g.FillRectangle(_brickShadeBrush, new Rectangle(brickRect.X, brickRect.Y, brickRect.Width, 2));
                }

                if (tt > 0.72f && brickRect.Height >= 4)
                {
                    _workBrush.Color = Color.FromArgb(60, 255, 255, 255);
                    g.FillRectangle(_workBrush,
                        new Rectangle(brickRect.X + 1, brickRect.Y + 1, Math.Max(1, brickRect.Width - 2), 1));
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
                _workBrush.Color = Color.FromArgb(16, 255, 255, 255);
                g.FillRectangle(_workBrush, brickRect);
            }
        }

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

        // keep capacity, just clear (no new allocations)
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
        var innerH = Math.Max(1f, bottom - top);

        while (y >= top)
        {
            var rect = new Rectangle(innerX, y, innerW, brickHeight);
            _brickRects.Add(rect);

            var centerY = rect.Top + (rect.Height * 0.5f);
            var t = (bottom - centerY) / innerH; // 0 bottom -> 1 top
            _brickT.Add(Clamp01(t));

            y -= brickHeight + gap;
        }

        // mirror list length without allocating per element later
        for (var i = 0; i < _brickRects.Count; i++)
        {
            _brickHeatColors.Add(Color.Empty);
        }

        _colorsDirty = true;
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
                // exponential smoothing (optionally asymmetric: attack vs release) Backward
                // compatible: UseAsymmetricBallistics=false OR ReleaseTimeMs=0 => uses
                // ResponseTimeMs for both directions (original behavior)
                int tauMs;
                if (UseAsymmetricBallistics && target < current && ReleaseTimeMs > 0)
                {
                    tauMs = ReleaseTimeMs;  // release
                }
                else
                {
                    tauMs = ResponseTimeMs; // attack (or symmetric)
                }

                var tau = Math.Max(0.01f, tauMs / 1000f);
                var alpha = 1f - (float)Math.Exp(-dtSeconds / tau);

                _displayValue = current + ((target - current) * alpha);

                // hard snap when close enough
                if (Math.Abs(_displayValue - target) < 0.05f)
                {
                    _displayValue = target;
                }
            }
        }

        // Fix silent flicker: when target is Minimum and we're near it, snap fully to Minimum
        if (target <= Minimum && _displayValue <= (Minimum + SilentSnapEpsilon))
        {
            _displayValue = Minimum;
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

            // also snap peak to min at silence (prevents a “1px” wandering line)
            if (_targetValue <= Minimum && _displayValue <= Minimum + SilentSnapEpsilon && _peakValue <= Minimum + 0.25f)
            {
                _peakValue = Minimum;
                _peakHoldLeftMs = 0;
            }
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

        var decayPer60FpsTick = Math.Max(0.01f, PeakDecayPerTick);
        var decayScale = intervalMs / (1000f / 60f);
        var decayAmount = decayPer60FpsTick * decayScale;

        _peakValue -= decayAmount;
        if (_peakValue < _displayValue)
        {
            _peakValue = _displayValue;
        }

        if (_peakValue < Minimum)
        {
            _peakValue = Minimum;
        }
    }

    // Computes whether this instance needs repaint, using quantization to avoid micro-jitter invalidations.
    private bool ComputeShouldInvalidate(VisualizationMode mode)
    {
        // Quantize level & peak into 0..1024 buckets (cheap & stable)
        var min = Minimum;
        var max = Maximum;
        var range = Math.Max(1, max - min);

        var d01 = Clamp01((_displayValue - min) / range);
        var p01 = Clamp01((_peakValue - min) / range);

        // 1024 steps = stable, but still smooth for audio bars
        var dq = (int)((d01 * 1024f) + 0.5f);
        var pq = (int)((p01 * 1024f) + 0.5f);

        // time animation bucket only when actually visible (level > 0)
        var aq = 0;
        if (ModeHasTimeAnimation(mode) && dq > 0)
        {
            // ~30 fps animation bucket
            aq = (int)(s_stopwatch.ElapsedMilliseconds / 33L);
        }

        var changed =
            dq != _lastDisplayQ ||
            pq != _lastPeakQ ||
            aq != _lastAnimQ ||
            mode != _lastModeQ;

        _lastDisplayQ = dq;
        _lastPeakQ = pq;
        _lastAnimQ = aq;
        _lastModeQ = mode;

        return changed;
    }

    // ========================= Register / Unregister + timer =========================
    private void RegisterInstance()
    {
        lock (s_lock)
        {
            s_instances.Add(new WeakReference<VerticalProgressBar>(this));
            EnsureTimerConfigured_NoLock();
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
                s_idleTicks = 0;
            }
        }
    }

    private void EnsureTimerConfigured()
    {
        lock (s_lock)
        {
            EnsureTimerConfigured_NoLock();
        }
    }

    private void EnsureTimerConfigured_NoLock()
    {
        if (s_timer == null)
        {
            s_timer = new System.Windows.Forms.Timer();
            s_timer.Tick += (s, e) => TickAll();
            s_lastTicks = 0;
            s_idleTicks = 0;
        }

        var fps = Math.Max(15, AnimationFps);
        var interval = Math.Max(4, (int)Math.Round(1000.0 / fps));

        if (s_timer.Interval != interval)
        {
            s_timer.Interval = interval;
        }

        if (!s_timer.Enabled)
        {
            s_timer.Start();
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

            // Compute dt
            var now = s_stopwatch.ElapsedTicks;
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

            var anyInvalidated = false;

            for (var i = s_instances.Count - 1; i >= 0; i--)
            {
                if (!s_instances[i].TryGetTarget(out var ctrl) || ctrl.IsDisposed)
                {
                    s_instances.RemoveAt(i);
                    continue;
                }

                ctrl.AnimateStep(dt, intervalMs);

                var mode = ctrl.GetVisualizationModeCached();
                if (ctrl.ComputeShouldInvalidate(mode))
                {
                    ctrl.Invalidate();
                    anyInvalidated = true;
                }
            }

            if (!anyInvalidated)
            {
                s_idleTicks++;
                if (s_idleTicks >= SleepAfterIdleTicks && s_timer != null)
                {
                    // Sleep timer when absolutely stable; it will be woken by Value/SetTargetValueThreadSafe
                    s_timer.Stop();
                    s_idleTicks = 0;
                    s_lastTicks = 0;
                }
            }
            else
            {
                s_idleTicks = 0;
            }

            if (s_instances.Count == 0 && s_timer != null)
            {
                s_timer.Stop();
                s_timer.Dispose();
                s_timer = null;
                s_lastTicks = 0;
                s_idleTicks = 0;
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
        { dh -= 360f; }
        if (dh < -180f)
        { dh += 360f; }

        var h = ah + (dh * t);
        if (h < 0)
        { h += 360f; }
        if (h >= 360f)
        { h -= 360f; }

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
            : max == r ? 60f * ((g - b) / delta % 6f)
            : max == g ? 60f * (((b - r) / delta) + 2f)
            : 60f * (((r - g) / delta) + 4f);

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
    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
}