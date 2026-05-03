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
    private const int NOISE_GATE_THRESHOLD = 2;

    private VerticalProgressBar[] _progressBars;
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
        var barH   = Math.Max(10, ch - startY - Math.Max(0, (int)Math.Round(8f * scaleY)));

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
        }
        finally
        {
            ambiance_ThemeSpectrum.ResumeLayout(false);
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
        lock (_updateLock)
        {
            _updatePending = false;
            Buffer.BlockCopy(_spectrumBuffer, 0, _applyBuffer, 0, BAR_COUNT);
        }

        for (var i = 0; i < BAR_COUNT; i++)
        {
            _progressBars[i].SetTargetValueUI(_applyBuffer[i]);
        }
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
                progress.ResponseTimeMs = 110;
                progress.ReleaseTimeMs = 280;
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