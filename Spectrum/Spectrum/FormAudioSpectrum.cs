using System;
using System.Configuration;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Spectrum
{
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
        private readonly object _updateLock = new object();

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
            // Global visuals
            progress.AnimationFps = 60;
            progress.TopEmphasisStart = 0.88f;
            progress.TopEmphasisStrength = 0.60f;
            progress.HeatIntensityCurve = 1.85f;
            progress.PeakLineThickness = 2;

            mode = (mode ?? string.Empty).Trim().ToLowerInvariant();

            switch (mode)
            {
                case "bricks":
                    // VU-style behavior (slow averaging, musical) Real VU meters integrate ~300 ms
                    progress.ResponseTimeMs = 300;         // VU-like envelope
                    progress.SnapUpThreshold = 6;          // avoid transient snap
                    progress.PeakHoldMilliseconds = 200;   // VU meters don’t emphasize peaks
                    progress.PeakDecayPerTick = 1.10f;     // slow visual fall
                    break;

                case "led":
                    // Peak/PPM-style behavior IEC PPM: fast attack, slow release
                    progress.ResponseTimeMs = 10;          // fast attack
                    progress.SnapUpThreshold = 2;          // catch transients
                    progress.PeakHoldMilliseconds = 350;   // typical studio peak hold
                    progress.PeakDecayPerTick = 1.05f;     // slow readable decay
                    break;

                case "dots":
                    // Analyzer but visually sensitive → extra smoothing
                    progress.ResponseTimeMs = 40;
                    progress.SnapUpThreshold = 4;
                    progress.PeakHoldMilliseconds = 180;
                    progress.PeakDecayPerTick = 1.08f;
                    break;

                case "wave":
                    // Flowing display, not a meter
                    progress.ResponseTimeMs = 60;
                    progress.SnapUpThreshold = 4;
                    progress.PeakHoldMilliseconds = 150;
                    progress.PeakDecayPerTick = 1.08f;
                    break;

                case "pulse":
                    // Aesthetic breathing, not measurement
                    progress.ResponseTimeMs = 120;
                    progress.SnapUpThreshold = 7;
                    progress.PeakHoldMilliseconds = 0;
                    progress.PeakDecayPerTick = 1.00f;
                    break;

                case "center":
                    // Energy field style, not technical meter
                    progress.ResponseTimeMs = 80;
                    progress.SnapUpThreshold = 7;
                    progress.PeakHoldMilliseconds = 0;
                    progress.PeakDecayPerTick = 1.00f;
                    break;

                case "mirror":
                    // Rhythmic, smooth
                    progress.ResponseTimeMs = 65;
                    progress.SnapUpThreshold = 6;
                    progress.PeakHoldMilliseconds = 0;
                    progress.PeakDecayPerTick = 1.00f;
                    break;

                default:
                    // SPECTRUM ANALYZER (fast, modern DAW-style)
                    progress.ResponseTimeMs = 20;
                    progress.SnapUpThreshold = 2;
                    progress.PeakHoldMilliseconds = 300;
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
}