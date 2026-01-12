// ============================ FormAudioSpectrum.cs ============================
// Developed by Gehan Fernando (optimized + UI-safe)

using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Spectrum;

public partial class FormAudioSpectrum : Form
{
    private const int BAR_COUNT = 83;
    private const int NOISE_GATE_THRESHOLD = 6;

    private VerticalProgressBar[] _progressBars;
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

        _spectrumBuffer = new byte[BAR_COUNT];

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
        var visualMode = ConfigurationManager.AppSettings["Mode"];
        visualMode = string.IsNullOrWhiteSpace(visualMode) ? "Spectrum" : visualMode.Trim();

        InitializeBarsOptimized(visualMode);

        _analyzer = new Analyzer();
        Analyzer.OnChange += Spectrum_Change;

        _device = GetDefaultAudioDevice();

        // If you have this helper in your project, keep it:
        Taskbar.SetState(Handle, Taskbar.TaskbarStates.NoProgress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MMDevice GetDefaultAudioDevice()
    {
        var enumerator = new MMDeviceEnumerator();

        // Try Multimedia role first, then Console, then Communications.
        try
        {
            var d = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (d != null) return d;
        }
        catch { }

        try
        {
            var d = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (d != null) return d;
        }
        catch { }

        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
        }
        catch
        {
            return null;
        }
    }

    private void InitializeBarsOptimized(string visualMode)
    {
        _progressBars = new VerticalProgressBar[BAR_COUNT];

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
            ambiance_ThemeSpectrum.ResumeLayout(true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Spectrum_Change(object obj, OnChangeEventArgs e)
    {
        if (_isDisposed || _progressBars == null) return;

        var spectrum = e.Spectrumdata;
        if (spectrum == null || spectrum.Count == 0) return;

        lock (_updateLock)
        {
            if (_updatePending) return;
            _updatePending = true;
        }

        // Copy to local buffer immediately (thread-safe)
        var count = Math.Min(spectrum.Count, BAR_COUNT);

        for (var i = 0; i < count; i++)
        {
            var v = spectrum[i];
            _spectrumBuffer[i] = v < NOISE_GATE_THRESHOLD ? (byte)0 : v;
        }

        for (var i = count; i < BAR_COUNT; i++)
            _spectrumBuffer[i] = 0;

        if (InvokeRequired)
            _ = BeginInvoke(new Action(ApplySpectrumToUI));
        else
            ApplySpectrumToUI();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplySpectrumToUI()
    {
        lock (_updateLock) _updatePending = false;

        // No SuspendLayout here. Bars are fixed; layout work is wasted per frame.
        for (var i = 0; i < BAR_COUNT; i++)
        {
            _progressBars[i].SetTargetValueUI(_spectrumBuffer[i]);
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

        if (_analyzer != null)
        {
            Analyzer.OnChange -= Spectrum_Change;
            _analyzer.Dispose();
            _analyzer = null;
        }

        _device = null;

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