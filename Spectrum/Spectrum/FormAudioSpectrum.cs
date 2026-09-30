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
    // Release: exponential time constant; a natural decay of about 0.65 s to 10 % of the height.
    // Peak hold: marker holds 300 ms, then falls at PeakDecayPerTick per 1/60 s, independent of the bar.
    internal const int SPECTRUM_ATTACK_MS = 45;
    internal const int SPECTRUM_RELEASE_MS = 280;
    internal const int SPECTRUM_PEAK_HOLD_MS = 300;

    // Frequency axis: landmark labels placed with the same logarithmic mapping as the bars (BandPlan).
    private static readonly double[] s_axisLandmarksHz = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
    private const float AXIS_LABEL_HEIGHT = 14f;   // design-time pixels at the 378 px reference height
    private const int AXIS_LABEL_MIN_GAP = 4;

    // Geometry used before the device sample rate is known (identical for every rate >= 41.8 kHz).
    private static readonly BandPlan s_defaultPlan = BandPlan.CreateLogarithmic(BAR_COUNT, 20, 20000, 48000);

    private VerticalProgressBar[] _progressBars;
    private Label[] _axisLabels;
    private BandPlan _layoutPlan;
    private string _visualMode;
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
                    Visible = true
                };

                ApplyMeterPresetOptimized(progress, visualMode);

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
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }

        RecalculateBarLayout();
    }

    private void RecalculateBarLayout()
    {
        if (_progressBars == null) return;

        var cw = ambiance_ThemeSpectrum.ClientSize.Width;
        var ch = ambiance_ThemeSpectrum.ClientSize.Height;
        if (cw <= 0 || ch <= 0) return;

        var scaleY = (float)ch / 378f;
        var startY = Math.Max(0, (int)Math.Round(50f * scaleY));
        var labelH = Math.Max(10, (int)Math.Round(AXIS_LABEL_HEIGHT * scaleY));
        var barH   = Math.Max(10, ch - startY - Math.Max(0, (int)Math.Round(8f * scaleY)) - labelH);

        // Keep a margin on both sides that scales with the container width.
        // Float stride within the available area so all 83 bars fit exactly, with no side overflow.
        var marginX = Math.Max(4, (int)Math.Round(11f * (float)cw / 1184f));
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

            LayoutAxisLabels(marginX, strideF, startY + barH + 1, cw);
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
                progress.ReleaseTimeMs = 220;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "dots":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 130;
                progress.ReleaseTimeMs = 480;
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
                progress.ReleaseTimeMs = 280;
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
                progress.ReleaseTimeMs = 340;
                progress.PeakHoldMilliseconds = 120;
                break;

            case "pulse":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 130;
                progress.ReleaseTimeMs = 400;
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
    }
}