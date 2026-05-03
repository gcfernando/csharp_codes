// ============================ FormAudioSpectrum.cs ============================
// Developed by Gehan Fernando (optimized + UI-safe)

using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Spectrum;

public partial class FormAudioSpectrum : Form
{
    private const int BAR_COUNT = 83;
    private const int NOISE_GATE_THRESHOLD = 2; // was 6 — high-freq bars have low energy; gate was killing real signal

    private VerticalProgressBar[] _progressBars;
    private string _visualMode;
    private readonly byte[] _spectrumBuffer;
    // Second buffer for lock-protected read in ApplySpectrumToUI (avoids race with background writes)
    private readonly byte[] _applyBuffer;

    // Coalescing mechanism to prevent BeginInvoke flooding
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

        // Keep the window in bounds when the user changes screen resolution or DPI
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
                    Size = new Size(14, 320),         // placeholder; RecalculateBarLayout corrects below
                    Location = new Point(11 + i * 13, 50),
                    Visible = true
                };

                ApplyMeterPresetOptimized(progress, visualMode);

                _progressBars[i] = progress;
                ambiance_ThemeSpectrum.Controls.Add(progress);
            }
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }

        // Apply DPI-aware positions immediately — handles startup on non-96-DPI monitors
        RecalculateBarLayout();
    }

    // ========================= Layout =========================

    // Called on DPI change or resolution change; scales bar positions to the container's
    // current pixel size so bars always fill the window correctly.
    private void RecalculateBarLayout()
    {
        if (_progressBars == null) return;

        var cw = ambiance_ThemeSpectrum.ClientSize.Width;
        var ch = ambiance_ThemeSpectrum.ClientSize.Height;
        if (cw <= 0 || ch <= 0) return;

        var scaleY = (float)ch / 378f;
        var startY = Math.Max(0, (int)Math.Round(50f * scaleY));
        var barH   = Math.Max(10, ch - startY - Math.Max(0, (int)Math.Round(8f * scaleY)));

        // Keep a margin on both sides that scales with the container width.
        // Float stride within the available area so all 83 bars fit exactly, with no side overflow.
        var marginX  = Math.Max(4, (int)Math.Round(11f * (float)cw / 1184f));
        var availW   = cw - 2 * marginX;
        var strideF  = (float)availW / BAR_COUNT;

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
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }
    }

    // ========================= Display settings / DPI =========================

    private const int WM_DPICHANGED = 0x02E0;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        // WM_DPICHANGED: Windows has already resized the form — re-fit bars to the new pixel size
        if (m.Msg == WM_DPICHANGED)
            RecalculateBarLayout();
    }

    private void OnDisplaySettingsChanged(object sender, EventArgs e)
    {
        // DisplaySettingsChanged fires on a background thread — marshal to UI thread
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnDisplaySettingsChanged(sender, e)));
            return;
        }

        // Keep the window fully visible within the working area of whichever screen it is on
        var screen = Screen.FromControl(this);
        var wa = screen.WorkingArea;

        var newLeft = Math.Max(wa.Left, Math.Min(Left, wa.Right  - Width));
        var newTop  = Math.Max(wa.Top,  Math.Min(Top,  wa.Bottom - Height));
        if (newLeft != Left || newTop != Top)
            Location = new Point(newLeft, newTop);

        // Re-fit bars in case the form was rescaled by the OS
        RecalculateBarLayout();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Spectrum_Change(object obj, OnChangeEventArgs e)
    {
        if (_isDisposed || _progressBars == null) return;

        var spectrum = e.Spectrumdata;
        if (spectrum == null || spectrum.Count == 0) return;

        // Copy inside lock: Analyzer now fires from a thread-pool thread (System.Timers.Timer),
        // so _spectrumBuffer must be guarded against concurrent reads in ApplySpectrumToUI.
        lock (_updateLock)
        {
            if (_updatePending) return;
            _updatePending = true;

            var count = Math.Min(spectrum.Count, BAR_COUNT);
            for (var i = 0; i < count; i++)
            {
                var v = spectrum[i];
                _spectrumBuffer[i] = v < NOISE_GATE_THRESHOLD ? (byte)0 : v;
            }
            for (var i = count; i < BAR_COUNT; i++)
                _spectrumBuffer[i] = 0;
        }

        if (InvokeRequired)
            _ = BeginInvoke(new Action(ApplySpectrumToUI));
        else
            ApplySpectrumToUI();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplySpectrumToUI()
    {
        // Snapshot under lock so a concurrent background tick cannot stomp _spectrumBuffer
        // while we iterate it below.
        lock (_updateLock)
        {
            _updatePending = false;
            Buffer.BlockCopy(_spectrumBuffer, 0, _applyBuffer, 0, BAR_COUNT);
        }

        // No SuspendLayout here. Bars are fixed; layout work is wasted per frame.
        for (var i = 0; i < BAR_COUNT; i++)
        {
            _progressBars[i].SetTargetValueUI(_applyBuffer[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMeterPresetOptimized(VerticalProgressBar progress, string mode)
    {
        // Shared visuals (not part of standards)
        progress.AnimationFps = 60;
        progress.TopEmphasisStart = 0.88f;
        progress.TopEmphasisStrength = 0.60f;
        progress.HeatIntensityCurve = 1.85f;
        progress.PeakLineThickness = 2;

        // Safe defaults
        progress.SnapUpThreshold = 0;
        progress.PeakDecayPerTick = 1.15f;

        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();

        switch (mode)
        {
            case "bricks":
                progress.UseAsymmetricBallistics = false; // symmetric behavior
                progress.ResponseTimeMs = 65;            // τ ≈ 65 ms
                progress.ReleaseTimeMs = 65;             // explicit (not used when asymmetric=false)
                progress.PeakHoldMilliseconds = 0;       // VU is not a peak meter
                break;

            case "dots":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 5;            // fast integration (≈ IEC Type I)
                progress.ReleaseTimeMs = 738;           // τ from 20 dB in 1.7 s
                progress.PeakHoldMilliseconds = 0;      // standards do not require peak-hold
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
                progress.ResponseTimeMs = 10;           // integration (≈ IEC Type II)
                progress.ReleaseTimeMs = 1013;          // τ from 24 dB in 2.8 s
                progress.PeakHoldMilliseconds = 0;      // standards do not require peak-hold
                break;

            case "center":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 90;
                progress.ReleaseTimeMs = 650;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "mirror":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 80;
                progress.ReleaseTimeMs = 650;
                progress.PeakHoldMilliseconds = 0;
                break;

            case "wave":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 60;
                progress.ReleaseTimeMs = 500;
                progress.PeakHoldMilliseconds = 100;
                break;

            case "pulse":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 140;
                progress.ReleaseTimeMs = 900;
                progress.PeakHoldMilliseconds = 0;
                break;

            default:
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 7;
                progress.ReleaseTimeMs = 300;
                progress.PeakHoldMilliseconds = 300;
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
    }
}