using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Spectrum.Dsp;

namespace Spectrum;

public partial class FormAudioSpectrum : Form
{
    private const int BAR_COUNT = 83;
    private const int NOISE_GATE_THRESHOLD = 2;

    // Default "Spectrum" (analyzer) mode presentation ballistics, all driven by elapsed time.
    // Attack: a full-scale (72 dB) rise completes in 45 ms, about 1.5 analysis hops (~32 ms each), so the bar interpolates
    // between analysis frames without adding more than about one hop of visible lag to transients.
    // Release: exponential time constant; a natural decay of about 0.9 s to 10 % of the height.
    // Peak hold: marker holds 300 ms, then falls at PeakDecayPerTick per 1/60 s, independent of the bar.
    internal const int SPECTRUM_ATTACK_MS = 45;
    internal const int SPECTRUM_RELEASE_MS = 380;
    internal const int SPECTRUM_PEAK_HOLD_MS = 300;

    // Frequency axis: landmark labels placed with the same logarithmic mapping as the bars (BandPlan).
    private static readonly double[] s_axisLandmarksHz = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
    private const float AXIS_LABEL_HEIGHT = 14f;   // design-time pixels at the 378 px reference height
    private const int AXIS_LABEL_MIN_GAP = 4;

    // Level (dB) axis: landmark labels placed on both sides of the bars, mapped with the same
    // LevelScale.Normalize used to size the bars, so a label always lines up with the bar height
    // it names. Present from startup, like the frequency axis.
    private static readonly double[] s_axisLandmarksDb = { 0, -12, -24, -36, -48, -60, -72 };

    // Reference-scale tick lines drawn inside every bar (excludes the 0/-72 extremes, which already
    // coincide with each bar's own top/bottom border).
    private static readonly float[] s_gridlineLevels = BuildGridlineLevels();

    private static float[] BuildGridlineLevels()
    {
        var levels = new float[s_axisLandmarksDb.Length - 2];
        for (var i = 1; i < s_axisLandmarksDb.Length - 1; i++)
            levels[i - 1] = (float)LevelScale.Normalize(s_axisLandmarksDb[i]);
        return levels;
    }

    // Geometry used before the device sample rate is known (identical for every rate >= 41.8 kHz).
    private static readonly BandPlan s_defaultPlan = BandPlan.CreateLogarithmic(BAR_COUNT, 20, 20000, 48000);

    private VerticalProgressBar[] _progressBars;
    private Label[] _axisLabels;
    private Label[] _dbAxisLabelsLeft;
    private Label[] _dbAxisLabelsRight;

    // Mirrored copies of the dB labels, used only in Center/Mirror mode: those modes fill the bar
    // symmetrically about its vertical middle, so most landmark values occur at two heights (equidistant
    // above and below centre) instead of one. Hidden/unused in every other mode.
    private Label[] _dbAxisLabelsLeftMirror;
    private Label[] _dbAxisLabelsRightMirror;

    private BandPlan _layoutPlan;
    private string _visualMode;
    private string _barTheme;
    private readonly byte[] _spectrumBuffer;
    private readonly byte[] _applyBuffer;

    private volatile bool _updatePending;
    private readonly object _updateLock = new();

    private Analyzer _analyzer;
    private volatile bool _isDisposed;

    public FormAudioSpectrum()
    {
        InitializeComponent();

        _spectrumBuffer = new byte[BAR_COUNT];
        _applyBuffer = new byte[BAR_COUNT];

        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint,
            true);

        UpdateStyles();
    }

    private void FormAudioSpectrum_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F12)
            TopMost = !TopMost;
    }

    private void FormAudioSpectrum_Load(object sender, EventArgs e)
    {
        _visualMode = ConfigurationManager.AppSettings["Mode"];
        _visualMode = string.IsNullOrWhiteSpace(_visualMode) ? "Spectrum" : _visualMode.Trim();

        _barTheme = ConfigurationManager.AppSettings["Theme"];
        _barTheme = string.IsNullOrWhiteSpace(_barTheme) ? "ClassicSmooth" : _barTheme.Trim();

        InitializeBarsOptimized(_visualMode);
        CenterToScreen();

        _analyzer = new Analyzer();
        Analyzer.OnChange += Spectrum_Change;

        Shown += (s, e) => RecalculateBarLayout();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        Taskbar.SetState(Handle, Taskbar.TaskbarStates.NoProgress);
    }

    private void InitializeBarsOptimized(string visualMode)
    {
        _progressBars = new VerticalProgressBar[BAR_COUNT];
        var theme = BarColorThemes.Resolve(_barTheme);

        ambiance_ThemeSpectrum.SuspendLayout();
        try
        {
            var backgroundColor = Color.FromArgb(50, 50, 50);

            for (var i = 0; i < BAR_COUNT; i++)
            {
                var progress = new VerticalProgressBar
                {
                    BackColor = backgroundColor,
                    Maximum = 255,
                    Name = $"ProgressBar_{i + 1:D2}",
                    Tag = $"{visualMode}|{i + 1}",
                    Size = new Size(14, 320),
                    Location = new Point(11 + i * 13, 50),
                    Visible = true,
                    GridlineLevels = s_gridlineLevels
                };

                ApplyMeterPresetOptimized(progress, visualMode);
                ApplyColorThemeOptimized(progress, theme);

                _progressBars[i] = progress;
                ambiance_ThemeSpectrum.Controls.Add(progress);
            }

            _axisLabels = new Label[s_axisLandmarksHz.Length];
            for (var i = 0; i < _axisLabels.Length; i++)
            {
                var hz = s_axisLandmarksHz[i];
                var label = new Label
                {
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(150, 150, 150),
                    Font = new Font("Segoe UI", 7f),
                    Text = hz >= 1000 ? $"{hz / 1000:0.#}k" : $"{hz:0}",
                    Name = $"AxisLabel_{hz:0}",
                };

                _axisLabels[i] = label;
                ambiance_ThemeSpectrum.Controls.Add(label);
            }

            _dbAxisLabelsLeft = new Label[s_axisLandmarksDb.Length];
            _dbAxisLabelsRight = new Label[s_axisLandmarksDb.Length];
            _dbAxisLabelsLeftMirror = new Label[s_axisLandmarksDb.Length];
            _dbAxisLabelsRightMirror = new Label[s_axisLandmarksDb.Length];
            for (var i = 0; i < s_axisLandmarksDb.Length; i++)
            {
                var db = s_axisLandmarksDb[i];
                var text = db.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

                var left = CreateDbAxisLabel(text, $"DbAxisLabelLeft_{text}");
                var right = CreateDbAxisLabel(text, $"DbAxisLabelRight_{text}");
                var leftMirror = CreateDbAxisLabel(text, $"DbAxisLabelLeftMirror_{text}");
                var rightMirror = CreateDbAxisLabel(text, $"DbAxisLabelRightMirror_{text}");

                _dbAxisLabelsLeft[i] = left;
                _dbAxisLabelsRight[i] = right;
                _dbAxisLabelsLeftMirror[i] = leftMirror;
                _dbAxisLabelsRightMirror[i] = rightMirror;
                ambiance_ThemeSpectrum.Controls.Add(left);
                ambiance_ThemeSpectrum.Controls.Add(right);
                ambiance_ThemeSpectrum.Controls.Add(leftMirror);
                ambiance_ThemeSpectrum.Controls.Add(rightMirror);
            }
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }

        RecalculateBarLayout();
    }

    private static Label CreateDbAxisLabel(string text, string name) => new()
    {
        AutoSize = true,
        BackColor = Color.Transparent,
        ForeColor = Color.FromArgb(150, 150, 150),
        Font = new Font("Segoe UI", 7f),
        Text = text,
        Name = name,
    };

    private int GetMaxDbAxisLabelWidth()
    {
        var max = 0;
        if (_dbAxisLabelsLeft != null)
        {
            foreach (var label in _dbAxisLabelsLeft)
                if (label != null) max = Math.Max(max, label.PreferredWidth);
        }
        return max;
    }

    private int GetMaxDbAxisLabelHeight()
    {
        var max = 0;
        if (_dbAxisLabelsLeft != null)
        {
            foreach (var label in _dbAxisLabelsLeft)
                if (label != null) max = Math.Max(max, label.PreferredHeight);
        }
        return max;
    }

    private void RecalculateBarLayout()
    {
        if (_progressBars == null) return;

        var cw = ambiance_ThemeSpectrum.ClientSize.Width;
        var ch = ambiance_ThemeSpectrum.ClientSize.Height;
        if (cw <= 0 || ch <= 0) return;

        var scaleY = (float)ch / 378f;
        var dbLabelH = Math.Max(0, GetMaxDbAxisLabelHeight());
        var dbLabelHalf = (dbLabelH + 1) / 2;
        var baseStartY = Math.Max(0, (int)Math.Round(50f * scaleY));
        var startY = Math.Max(0, baseStartY + dbLabelHalf);
        var labelH = Math.Max(10, (int)Math.Round(AXIS_LABEL_HEIGHT * scaleY));
        var bottomReserve = Math.Max(0, (int)Math.Round(8f * scaleY)) + dbLabelHalf + AXIS_LABEL_MIN_GAP + labelH;
        var barH   = Math.Max(10, ch - startY - bottomReserve);
        var axisLabelY = startY + barH + dbLabelHalf + AXIS_LABEL_MIN_GAP;

        // Keep a margin on both sides that scales with the container width, widened as needed so the
        // dB axis labels have room to sit left/right of the bars without overlapping them.
        var baseMarginX = Math.Max(4, (int)Math.Round(11f * (float)cw / 1184f));
        var dbLabelReserve = GetMaxDbAxisLabelWidth() + 2 * AXIS_LABEL_MIN_GAP;
        var marginX = Math.Max(baseMarginX, dbLabelReserve);

        // Float stride within the available area so all 83 bars fit exactly, with no side overflow.
        var availW  = cw - 2 * marginX;
        var strideF = (float)availW / BAR_COUNT;

        ambiance_ThemeSpectrum.SuspendLayout();
        try
        {
            for (var i = 0; i < BAR_COUNT; i++)
            {
                var x     = marginX + (int)(i       * strideF);
                var nextX = marginX + (int)((i + 1) * strideF);
                _progressBars[i].Location = new Point(x, startY);
                _progressBars[i].Size     = new Size(Math.Max(2, nextX - x), barH);
            }

            LayoutAxisLabels(marginX, strideF, axisLabelY, cw);

            // Line labels up with the bar's actual painted fill area, not its full control bounds: OnPaint
            // insets the fill by BrickPadding on every side, so the dB axis must use the same inset.
            var fillPadding = _progressBars.Length > 0 ? _progressBars[0].BrickPadding : 0;
            var fillTop = startY + fillPadding;
            var fillH = Math.Max(1, barH - 2 * fillPadding);
            var topBound = Math.Max(0, startY - dbLabelHalf);
            var bottomBound = axisLabelY - AXIS_LABEL_MIN_GAP;

            if (IsSymmetricFillMode(_visualMode, out var invertedFromCenter))
            {
                LayoutDbAxisLabelsSymmetric(marginX, fillTop, fillH, cw, topBound, bottomBound, invertedFromCenter);
            }
            else
            {
                HideDbAxisLabels(_dbAxisLabelsLeftMirror);
                HideDbAxisLabels(_dbAxisLabelsRightMirror);
                LayoutDbAxisLabels(marginX, fillTop, fillH, cw, topBound, bottomBound);
            }
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }
    }

    private void LayoutAxisLabels(int marginX, float strideF, int labelY, int containerWidth)
    {
        if (_axisLabels == null) return;

        var plan = _analyzer?.CurrentBandPlan ?? s_defaultPlan;
        _layoutPlan = plan;

        var previousRight = int.MinValue;
        for (var i = 0; i < _axisLabels.Length; i++)
        {
            var label = _axisLabels[i];
            var hz = s_axisLandmarksHz[i];

            // Never label frequencies the analysis does not cover (e.g. above Nyquist on a low-rate device).
            var inRange = hz >= plan.MinHz && hz <= plan.MaxHz;

            // Same mapping as the bars: bar i spans [i, i+1) in plan position units.
            var centerX = marginX + (int)Math.Round(plan.FrequencyToPosition(hz) * strideF);
            var w = label.PreferredWidth;
            var left = Math.Max(0, Math.Min(containerWidth - w, centerX - (w / 2)));

            // Skip labels that would collide with the previous one on narrow windows.
            var visible = inRange && left >= previousRight + AXIS_LABEL_MIN_GAP;
            label.Visible = visible;
            if (!visible) continue;

            label.Location = new Point(left, labelY);
            previousRight = left + w;
        }
    }

    // Level (dB) axis: positions mirror LevelScale.Normalize so a label's vertical centre lines up with the
    // bar height that dB value would produce (0 dBFS at the top, -72 dBFS floor at the bottom).
    private void LayoutDbAxisLabels(int marginX, int startY, int barH, int containerWidth, int topBound, int bottomBound)
    {
        if (_dbAxisLabelsLeft == null || _dbAxisLabelsRight == null) return;

        var previousBottomLeft = int.MinValue;
        var previousBottomRight = int.MinValue;

        for (var i = 0; i < s_axisLandmarksDb.Length; i++)
        {
            var db = s_axisLandmarksDb[i];
            var norm = LevelScale.Normalize(db);
            var centerY = startY + (int)Math.Round((1.0 - norm) * barH);

            var left = _dbAxisLabelsLeft[i];
            var top = centerY - left.Height / 2;
            var visibleLeft = top >= topBound && top + left.Height <= bottomBound
                && top >= previousBottomLeft + AXIS_LABEL_MIN_GAP;
            left.Visible = visibleLeft;
            if (visibleLeft)
            {
                left.Location = new Point(Math.Max(0, marginX - AXIS_LABEL_MIN_GAP - left.PreferredWidth), top);
                previousBottomLeft = top + left.Height;
            }

            var right = _dbAxisLabelsRight[i];
            var visibleRight = top >= topBound && top + right.Height <= bottomBound
                && top >= previousBottomRight + AXIS_LABEL_MIN_GAP;
            right.Visible = visibleRight;
            if (visibleRight)
            {
                right.Location = new Point(
                    Math.Min(containerWidth - right.PreferredWidth, containerWidth - marginX + AXIS_LABEL_MIN_GAP),
                    top);
                previousBottomRight = top + right.Height;
            }
        }
    }

    // Center/Mirror fill symmetrically about the bar's vertical middle instead of bottom-up, so their dB axis
    // must mirror the same way (see DrawMode_CenterBricks/DrawMode_MirrorBricks in VerticalProgressBar.cs):
    // Center grows from the middle (silence) out to the edges (0 dBFS at full scale), while Mirror grows from
    // the edges (silence) in to the middle (0 dBFS at full scale) - the exact opposite direction.
    // "invertedFromCenter" is true for Mirror, flipping which extreme sits at the middle vs. the edges.
    private static bool IsSymmetricFillMode(string visualMode, out bool invertedFromCenter)
    {
        switch ((visualMode ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "center":
                invertedFromCenter = false;
                return true;
            case "mirror":
                invertedFromCenter = true;
                return true;
            default:
                invertedFromCenter = false;
                return false;
        }
    }

    private static void HideDbAxisLabels(Label[] labels)
    {
        if (labels == null) return;
        foreach (var label in labels)
            if (label != null) label.Visible = false;
    }

    // Lays out the dB axis for Center/Mirror mode: each landmark's vertical distance from the bar's middle
    // is proportional to its normalized level (same LevelScale.Normalize used everywhere else), mirrored
    // above and below centre to match the bar's actual symmetric fill. A landmark whose distance rounds to
    // zero (the value that sits exactly at the middle) only needs one label, not an overlapping duplicate.
    private void LayoutDbAxisLabelsSymmetric(
        int marginX, int startY, int barH, int containerWidth, int topBound, int bottomBound, bool invertedFromCenter)
    {
        if (_dbAxisLabelsLeft == null || _dbAxisLabelsRight == null
            || _dbAxisLabelsLeftMirror == null || _dbAxisLabelsRightMirror == null) return;

        var midY = startY + (barH / 2);
        var halfBarH = barH / 2f;

        var previousBottomLeft = int.MinValue;
        var previousBottomRight = int.MinValue;
        var previousTopLeftMirror = int.MaxValue;
        var previousTopRightMirror = int.MaxValue;

        // Collision tracking below assumes labels are visited edge-first, walking inward toward the middle
        // (so each accepted label's bound only ever needs comparing against its immediate, already-placed
        // neighbour). s_axisLandmarksDb is ordered 0 down to -72, which is already edge-to-middle for Center
        // (0 dBFS sits at the edge) but middle-to-edge for Mirror (0 dBFS sits at the middle instead) - so
        // sort explicitly by descending distance-from-middle rather than relying on array order.
        var order = new int[s_axisLandmarksDb.Length];
        var distances = new int[s_axisLandmarksDb.Length];
        for (var i = 0; i < s_axisLandmarksDb.Length; i++)
        {
            order[i] = i;
            var norm = (float)LevelScale.Normalize(s_axisLandmarksDb[i]);
            if (invertedFromCenter) norm = 1f - norm;
            distances[i] = (int)Math.Round(halfBarH * norm);
        }
        Array.Sort(order, (a, b) => distances[b].CompareTo(distances[a]));

        foreach (var i in order)
        {
            var distance = distances[i];
            var upperCenterY = midY - distance;
            var lowerCenterY = midY + distance;

            var left = _dbAxisLabelsLeft[i];
            var top = upperCenterY - left.Height / 2;
            var visibleLeft = top >= topBound && top + left.Height <= bottomBound
                && top >= previousBottomLeft + AXIS_LABEL_MIN_GAP;
            left.Visible = visibleLeft;
            if (visibleLeft)
            {
                left.Location = new Point(Math.Max(0, marginX - AXIS_LABEL_MIN_GAP - left.PreferredWidth), top);
                previousBottomLeft = top + left.Height;
            }

            var right = _dbAxisLabelsRight[i];
            var visibleRight = top >= topBound && top + right.Height <= bottomBound
                && top >= previousBottomRight + AXIS_LABEL_MIN_GAP;
            right.Visible = visibleRight;
            if (visibleRight)
            {
                right.Location = new Point(
                    Math.Min(containerWidth - right.PreferredWidth, containerWidth - marginX + AXIS_LABEL_MIN_GAP),
                    top);
                previousBottomRight = top + right.Height;
            }

            // The mirrored (lower) copy is only needed once the upper/lower positions actually differ.
            var leftMirror = _dbAxisLabelsLeftMirror[i];
            var topMirror = lowerCenterY - leftMirror.Height / 2;
            var visibleLeftMirror = distance > 0 && topMirror >= topBound && topMirror + leftMirror.Height <= bottomBound
                && topMirror + leftMirror.Height <= previousTopLeftMirror - AXIS_LABEL_MIN_GAP;
            leftMirror.Visible = visibleLeftMirror;
            if (visibleLeftMirror)
            {
                leftMirror.Location = new Point(Math.Max(0, marginX - AXIS_LABEL_MIN_GAP - leftMirror.PreferredWidth), topMirror);
                previousTopLeftMirror = topMirror;
            }

            var rightMirror = _dbAxisLabelsRightMirror[i];
            var visibleRightMirror = distance > 0 && topMirror >= topBound && topMirror + rightMirror.Height <= bottomBound
                && topMirror + rightMirror.Height <= previousTopRightMirror - AXIS_LABEL_MIN_GAP;
            rightMirror.Visible = visibleRightMirror;
            if (visibleRightMirror)
            {
                rightMirror.Location = new Point(
                    Math.Min(containerWidth - rightMirror.PreferredWidth, containerWidth - marginX + AXIS_LABEL_MIN_GAP),
                    topMirror);
                previousTopRightMirror = topMirror;
            }
        }
    }

    private const int WM_DPICHANGED = 0x02E0;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_DPICHANGED)
            RecalculateBarLayout();
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnDisplaySettingsChanged(sender, e)));
            return;
        }

        var screen = Screen.FromControl(this);
        var wa = screen.WorkingArea;

        var newLeft = Math.Max(wa.Left, Math.Min(Left, wa.Right  - Width));
        var newTop  = Math.Max(wa.Top,  Math.Min(Top,  wa.Bottom - Height));
        if (newLeft != Left || newTop != Top)
            Location = new Point(newLeft, newTop);

        RecalculateBarLayout();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Spectrum_Change(object obj, OnChangeEventArgs e)
    {
        if (_isDisposed || _progressBars == null) return;

        var spectrum = e.Spectrumdata;
        if (spectrum == null || spectrum.Count == 0) return;

        bool postNeeded;
        lock (_updateLock)
        {
            // Latest frame wins: always overwrite the pending buffer so the UI never applies a stale frame,
            // but post at most one UI update at a time.
            var count = Math.Min(spectrum.Count, BAR_COUNT);
            for (var i = 0; i < count; i++)
            {
                var v = spectrum[i];
                _spectrumBuffer[i] = v < NOISE_GATE_THRESHOLD ? (byte)0 : v;
            }
            for (var i = count; i < BAR_COUNT; i++)
                _spectrumBuffer[i] = 0;

            postNeeded = !_updatePending;
            _updatePending = true;
        }

        if (!postNeeded) return;

        if (InvokeRequired)
            _ = BeginInvoke(new Action(ApplySpectrumToUI));
        else
            ApplySpectrumToUI();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplySpectrumToUI()
    {
        lock (_updateLock)
        {
            _updatePending = false;
            Buffer.BlockCopy(_spectrumBuffer, 0, _applyBuffer, 0, BAR_COUNT);
        }

        // An update posted before the form was cleaned up can still be dispatched afterwards.
        if (_isDisposed || _progressBars == null) return;

        for (var i = 0; i < BAR_COUNT; i++)
        {
            _progressBars[i].SetTargetValueUI(_applyBuffer[i]);
        }

        var plan = _analyzer?.CurrentBandPlan;
        if (plan != null && !ReferenceEquals(plan, _layoutPlan))
            RecalculateBarLayout();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyColorThemeOptimized(VerticalProgressBar progress, BarColorTheme theme)
    {
        progress.HeatLowColor = theme.Low;
        progress.HeatMidColor = theme.Mid;
        progress.HeatHighColor = theme.High;
        progress.HeatPeakColor = theme.Peak;
        progress.HeatIntensityCurve = theme.IntensityCurve;
        progress.PeakColorMatchesHeat = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMeterPresetOptimized(VerticalProgressBar progress, string mode)
    {
        progress.AnimationFps = 60;
        progress.TopEmphasisStart = 0.88f;
        progress.TopEmphasisStrength = 0.60f;
        progress.HeatIntensityCurve = 1.85f;
        progress.PeakLineThickness = 2;

        progress.PeakDecayPerTick = 1.15f;

        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();

        switch (mode)
        {
            case "bricks":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 110;
                progress.ReleaseTimeMs = 320;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "dots":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 130;
                progress.ReleaseTimeMs = 620;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "led":
            case "ppmi i":
            case "ppmi i b":
            case "ppmi i a":
            case "ppmi i bbc":
            case "ppmii":
            case "ppm2":
            case "iec2":
            case "bbc":
            case "ebu":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 90;
                progress.ReleaseTimeMs = 380;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "center":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 120;
                progress.ReleaseTimeMs = 260;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "mirror":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 120;
                progress.ReleaseTimeMs = 260;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "wave":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 110;
                progress.ReleaseTimeMs = 440;
                progress.PeakHoldMilliseconds = 120;
                break;

            case "pulse":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 130;
                progress.ReleaseTimeMs = 520;
                progress.PeakHoldMilliseconds = 0;
                break;

            default:
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = SPECTRUM_ATTACK_MS;
                progress.ReleaseTimeMs = SPECTRUM_RELEASE_MS;
                progress.PeakHoldMilliseconds = SPECTRUM_PEAK_HOLD_MS;
                break;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CleanupResources();
        base.OnFormClosed(e);
    }

    private void CleanupResources()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        if (_analyzer != null)
        {
            Analyzer.OnChange -= Spectrum_Change;
            _analyzer.Dispose();
            _analyzer = null;
        }

        if (_progressBars != null)
        {
            for (var i = 0; i < _progressBars.Length; i++)
            {
                _progressBars[i]?.Dispose();
                _progressBars[i] = null;
            }
            _progressBars = null;
        }

        if (_axisLabels != null)
        {
            for (var i = 0; i < _axisLabels.Length; i++)
            {
                var font = _axisLabels[i]?.Font;
                _axisLabels[i]?.Dispose();
                font?.Dispose();
                _axisLabels[i] = null;
            }
            _axisLabels = null;
        }

        DisposeDbAxisLabels(ref _dbAxisLabelsLeft);
        DisposeDbAxisLabels(ref _dbAxisLabelsRight);
        DisposeDbAxisLabels(ref _dbAxisLabelsLeftMirror);
        DisposeDbAxisLabels(ref _dbAxisLabelsRightMirror);
    }

    private static void DisposeDbAxisLabels(ref Label[] labels)
    {
        if (labels == null) return;

        for (var i = 0; i < labels.Length; i++)
        {
            var font = labels[i]?.Font;
            labels[i]?.Dispose();
            font?.Dispose();
            labels[i] = null;
        }
        labels = null;
    }
}