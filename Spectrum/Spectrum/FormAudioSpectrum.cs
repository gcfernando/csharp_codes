using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Spectrum;

// Developer: Gehan Fernando
public partial class FormAudioSpectrum : Form
{
    private const int BAR_COUNT = 83;
    private const int NOISE_GATE_THRESHOLD = 6;

    // Performance: Array instead of List for direct indexing without bounds checks
    private VerticalProgressBar[] _progressBars;

    // Pre-allocated buffer to avoid per-frame allocations
    private readonly byte[] _spectrumBuffer;

    // Coalescing mechanism to prevent BeginInvoke flooding
    private volatile bool _updatePending;
    private readonly object _updateLock = new();

    private MMDevice _device;
    private Analyzer _analyzer;
    private bool _isDisposed;

    public FormAudioSpectrum()
    {
        InitializeComponent();

        // Pre-allocate buffer once
        _spectrumBuffer = new byte[BAR_COUNT];

        // Enable double buffering for flicker-free rendering
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
        {
            TopMost = !TopMost;
        }
    }

    private void FormAudioSpectrum_Load(object sender, EventArgs e)
    {
        var visualMode = ConfigurationManager.AppSettings["Mode"];
        visualMode = string.IsNullOrWhiteSpace(visualMode) ? "Spectrum" : visualMode.Trim();

        InitializeBarsOptimized(visualMode);

        _analyzer = new Analyzer();
        Analyzer.OnChange += Spectrum_Change;

        _device = GetDefaultAudioDevice();

        Taskbar.SetState(Handle, Taskbar.TaskbarStates.NoProgress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MMDevice GetDefaultAudioDevice()
    {
        var enumerator = new MMDeviceEnumerator();

        MMDevice device = null;

        // Try Multimedia role first
        try
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (device != null)
            {
                return device;
            }
        }
        catch { /* Continue */ }

        // Fallback to Console
        try
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (device != null)
            {
                return device;
            }
        }
        catch { /* Continue */ }

        // Final fallback to Communications
        try
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
        }
        catch { /* All attempts failed */ }

        return device;
    }

    private void InitializeBarsOptimized(string visualMode)
    {
        // Array for O(1) access without List overhead
        _progressBars = new VerticalProgressBar[BAR_COUNT];

        // Suspend layout updates until all controls added
        ambiance_ThemeSpectrum.SuspendLayout();

        try
        {
            var xPosition = 11;
            var barSize = new Size(14, 320);
            var yPosition = 50;
            var backgroundColor = Color.FromArgb(50, 50, 50);

            for (var i = 0; i < BAR_COUNT; i++)
            {
                var progress = new VerticalProgressBar
                {
                    BackColor = backgroundColor,
                    Maximum = 255,
                    Name = $"ProgressBar_{i + 1:D2}",
                    Tag = $"{visualMode}|{i + 1}",
                    Size = barSize,
                    Location = new Point(xPosition, yPosition),
                    Visible = true
                };

                ApplyMeterPresetOptimized(progress, visualMode);

                _progressBars[i] = progress;
                ambiance_ThemeSpectrum.Controls.Add(progress);

                xPosition += 13;
            }
        }
        finally
        {
            // Resume and trigger single layout pass
            ambiance_ThemeSpectrum.ResumeLayout(true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Spectrum_Change(object obj, OnChangeEventArgs e)
    {
        // Fast path: early exit checks
        if (_isDisposed || _progressBars == null)
        {
            return;
        }

        var spectrum = e.Spectrumdata;
        if (spectrum == null || spectrum.Count == 0)
        {
            return;
        }

        // CRITICAL OPTIMIZATION: Coalesce rapid updates Only one BeginInvoke queued at a time -
        // prevents message queue flooding
        lock (_updateLock)
        {
            if (_updatePending)
            {
                return;
            }

            _updatePending = true;
        }

        // Copy data to local buffer immediately (thread-safe)
        var count = Math.Min(spectrum.Count, BAR_COUNT);

        for (var i = 0; i < count; i++)
        {
            var value = spectrum[i];
            _spectrumBuffer[i] = value < NOISE_GATE_THRESHOLD ? (byte)0 : value;
        }

        // Clear remaining bars if spectrum has fewer values
        for (var i = count; i < BAR_COUNT; i++)
        {
            _spectrumBuffer[i] = 0;
        }

        // Marshal to UI thread only once per update cycle
        if (InvokeRequired)
        {
            _ = BeginInvoke(new Action(ApplySpectrumToUI));
        }
        else
        {
            ApplySpectrumToUI();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplySpectrumToUI()
    {
        // Reset pending flag
        lock (_updateLock)
        {
            _updatePending = false;
        }

        // Batch update: suspend layout for all 83 bar updates
        ambiance_ThemeSpectrum.SuspendLayout();

        try
        {
            // Direct array indexing - JIT eliminates bounds checks in Release mode
            for (var i = 0; i < BAR_COUNT; i++)
            {
                _progressBars[i].SetTargetValueThreadSafe(_spectrumBuffer[i]);
            }
        }
        finally
        {
            // Single layout pass for all changes
            ambiance_ThemeSpectrum.ResumeLayout(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyMeterPresetOptimized(VerticalProgressBar progress, string mode)
    {
        // Global visuals (unchanged)
        progress.AnimationFps = 60;
        progress.TopEmphasisStart = 0.88f;
        progress.TopEmphasisStrength = 0.60f;
        progress.HeatIntensityCurve = 1.85f;
        progress.PeakLineThickness = 2;

        // Defaults for safety (preserve current behavior unless overridden per-mode)
        progress.UseAsymmetricBallistics = false;
        progress.ReleaseTimeMs = 0; // 0 => use ResponseTimeMs (symmetric)
        progress.SnapUpThreshold = 0;

        mode = (mode ?? string.Empty).Trim().ToLowerInvariant();

        switch (mode)
        {
            case "bricks":
                progress.UseAsymmetricBallistics = false; // VU is roughly symmetric
                progress.ReleaseTimeMs = 0;
                progress.ResponseTimeMs = 65;

                progress.SnapUpThreshold = 0;        // VU shouldn't "teleport"
                progress.PeakHoldMilliseconds = 0;    // VU is not a peak meter
                progress.PeakDecayPerTick = 1.10f;   // irrelevant when hold=0; keep safe
                break;

            case "led":
                progress.UseAsymmetricBallistics = true;

                progress.ResponseTimeMs = 10;        // attack/integration
                progress.ReleaseTimeMs = 1500;       // slow return (seconds-scale feel)

                progress.SnapUpThreshold = 0;        // PPM integrates; snapping defeats the point
                progress.PeakHoldMilliseconds = 0;    // true PPM doesn't need peak-hold (optional)
                progress.PeakDecayPerTick = 1.05f;   // marker decay; keep modest
                break;

            case "dots":
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 50;        // moderately quick attack
                progress.ReleaseTimeMs = 650;        // readable decay

                progress.SnapUpThreshold = 0;
                progress.PeakHoldMilliseconds = 120; // short hold helps dotted visuals
                progress.PeakDecayPerTick = 1.08f;
                break;

            case "wave":
                // Aesthetic waveform: stable motion, avoid twitch
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 60;
                progress.ReleaseTimeMs = 500;

                progress.SnapUpThreshold = 0;
                progress.PeakHoldMilliseconds = 100;
                progress.PeakDecayPerTick = 1.08f;
                break;

            case "pulse":
                // Aesthetic breathing: slow envelope
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 140;
                progress.ReleaseTimeMs = 900;

                progress.SnapUpThreshold = 0;
                progress.PeakHoldMilliseconds = 0;
                progress.PeakDecayPerTick = 1.00f;
                break;

            case "center":
                // "Energy field" centered: medium attack, medium/slow release for musical motion
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 80;
                progress.ReleaseTimeMs = 650;

                progress.SnapUpThreshold = 0;
                progress.PeakHoldMilliseconds = 0;
                progress.PeakDecayPerTick = 1.00f;
                break;

            case "mirror":
                // Mirror motion reads best with slightly quicker attack but still smooth decay
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 70;
                progress.ReleaseTimeMs = 600;

                progress.SnapUpThreshold = 0;
                progress.PeakHoldMilliseconds = 0;
                progress.PeakDecayPerTick = 1.00f;
                break;

            default:
                // SPECTRUM ANALYZER / DAW-STYLE BARS (common practice): fairly quick attack, slower
                // release to avoid "sparkle" / jitter.
                progress.UseAsymmetricBallistics = true;
                progress.ResponseTimeMs = 20;        // fast attack for transients
                progress.ReleaseTimeMs = 280;       // short-ish release for lively spectrum

                progress.SnapUpThreshold = 0;       // smoothing already handles this
                progress.PeakHoldMilliseconds = 250;
                progress.PeakDecayPerTick = 1.15f;
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
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // Unsubscribe from events
        if (_analyzer != null)
        {
            Analyzer.OnChange -= Spectrum_Change;
            _analyzer.Dispose();
            _analyzer = null;
        }

        // Dispose audio device
        if (_device != null)
        {
            _device = null;
        }

        // Dispose all progress bars
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