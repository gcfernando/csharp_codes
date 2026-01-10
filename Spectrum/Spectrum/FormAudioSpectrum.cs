using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Spectrum
{
    // Developer : Gehan Fernando
    public partial class FormAudioSpectrum : Form
    {
        private const int BarCount = 83;

        private List<VerticalProgressBar> _progressList;

        private MMDevice _device;
        private Analyzer _analyzer;

        // Optional: remove low-level flicker without changing mapping
        private const int NoiseGateThreshold = 6; // 0 disables; typical 4..10

        public FormAudioSpectrum() => InitializeComponent();

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

            InitializeBars(visualMode);

            _analyzer = new Analyzer();
            Analyzer.OnChange += new OnChangeHandler(Spectrum_Change);

            var enumerator = new MMDeviceEnumerator();
            _device =
                SafeGetDefault(enumerator, Role.Multimedia) ??
                SafeGetDefault(enumerator, Role.Console) ??
                SafeGetDefault(enumerator, Role.Communications);

            MMDevice SafeGetDefault(MMDeviceEnumerator en, Role role)
            {
                try
                {
                    return en.GetDefaultAudioEndpoint(DataFlow.Render, role);
                }
                catch
                {
                    return null;
                }
            }

            Taskbar.SetState(Handle, Taskbar.TaskbarStates.NoProgress);
        }

        private void InitializeBars(string visualMode)
        {
            _progressList = new List<VerticalProgressBar>(BarCount);

            var x = 11;

            for (var i = 1; i <= BarCount; i++)
            {
                var progress = new VerticalProgressBar
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    Maximum = 255,
                    Name = $"ProgressBar_{i:00}",
                    Tag = $"{visualMode}|{i}",
                    Size = new Size(14, 320),
                    Location = new Point(x, 50),
                    Visible = true
                };

                ApplyMeterPreset(progress, visualMode);

                _progressList.Add(progress);
                ambiance_ThemeSpectrum.Controls.Add(progress);

                x += 13;
            }
        }

        private void Spectrum_Change(object obj, OnChangeEventArgs e)
        {
            // If Analyzer calls from non-UI thread, marshal once.
            if (InvokeRequired)
            {
                _ = BeginInvoke(new Action<object, OnChangeEventArgs>(Spectrum_Change), obj, e);
                return;
            }

            var spectrum = e.Spectrumdata;
            if (spectrum == null || _progressList == null || _progressList.Count == 0)
            {
                return;
            }

            var count = Math.Min(spectrum.Count, _progressList.Count);

            // REAL mapping: bar i displays spectrum[i] (no remapping / no redistribution) Cheap
            // “silence flicker” suppression (still real mapping)
            for (var i = 0; i < count; i++)
            {
                var v = spectrum[i];
                if (v < NoiseGateThreshold)
                {
                    v = 0;
                }

                _progressList[i].SetTargetValueThreadSafe(v);
            }

            // Safe fallback if bins < bars
            for (var i = count; i < _progressList.Count; i++)
            {
                _progressList[i].SetTargetValueThreadSafe(0);
            }
        }

        // Real-world ballistics presets (per style).
        private static void ApplyMeterPreset(VerticalProgressBar progress, string mode)
        {
            // Global visuals (music-friendly)
            progress.AnimationFps = 60;

            // These 3 mostly affect appearance, not timing. They give a typical “meter heat” look
            // (green->yellow->red emphasis).
            progress.TopEmphasisStart = 0.88f;
            progress.TopEmphasisStrength = 0.60f;
            progress.HeatIntensityCurve = 1.85f;

            // Peak marker thickness (modes using peak marker)
            progress.PeakLineThickness = 2;

            // Robust string compare (no ToLower allocations)
            bool Is(string s) => string.Equals(mode, s, StringComparison.OrdinalIgnoreCase);

            if (Is("Bricks") || Is("LED"))
            {
                // Classic LED peak meter: quick response, readable hold
                progress.ResponseTimeMs = 20;
                progress.SnapUpThreshold = 2;
                progress.PeakHoldMilliseconds = 280;
                progress.PeakDecayPerTick = 1.15f;
            }
            else if (Is("Dots"))
            {
                // Dots tend to look flickery, so slightly slower
                progress.ResponseTimeMs = 26;
                progress.SnapUpThreshold = 3;
                progress.PeakHoldMilliseconds = 240;
                progress.PeakDecayPerTick = 1.10f;
            }
            else if (Is("Center"))
            {
                // Center is an “energy from center” style: slower feels more musical
                progress.ResponseTimeMs = 65;
                progress.SnapUpThreshold = 6;
                progress.PeakHoldMilliseconds = 0; // ignored by Center/Mirror rules
                progress.PeakDecayPerTick = 1.0f;
            }
            else if (Is("Mirror"))
            {
                // Mirror also “energy” style but slightly faster than Center
                progress.ResponseTimeMs = 55;
                progress.SnapUpThreshold = 6;
                progress.PeakHoldMilliseconds = 0; // ignored
                progress.PeakDecayPerTick = 1.0f;
            }
            else if (Is("Wave"))
            {
                // Wave looks best with medium smoothing (not twitchy)
                progress.ResponseTimeMs = 38;
                progress.SnapUpThreshold = 3;
                progress.PeakHoldMilliseconds = 200;
                progress.PeakDecayPerTick = 1.05f;
            }
            else if (Is("Pulse"))
            {
                // Pulse is intentionally “breathing”, so slower
                progress.ResponseTimeMs = 90;
                progress.SnapUpThreshold = 5;
                progress.PeakHoldMilliseconds = 180;
                progress.PeakDecayPerTick = 0.95f;
            }
            else // Spectrum (default)
            {
                // Analyzer column: fast + readable peaks
                progress.ResponseTimeMs = 18;
                progress.SnapUpThreshold = 2;
                progress.PeakHoldMilliseconds = 320;
                progress.PeakDecayPerTick = 1.20f;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Analyzer.OnChange -= new OnChangeHandler(Spectrum_Change);
            base.OnFormClosed(e);
        }
    }
}