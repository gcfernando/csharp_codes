using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace Spectrum
{
    // Developer : Gehan Fernando
    public partial class FormAudioSpectrum : Form
    {
        private List<VerticalProgressBar> _progressList;

        private MMDevice _device;
        private Analyzer _analyzer;

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
            InitializeBars();

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

        private void Spectrum_Change(object obj, OnChangeEventArgs e)
        {
            // If Analyzer calls from non-UI thread, marshal once.
            if (InvokeRequired)
            {
                _ = BeginInvoke(new Action<object, OnChangeEventArgs>(Spectrum_Change), obj, e);
                return;
            }

            var spectrum = e.Spectrumdata;
            var count = Math.Min(spectrum.Count, _progressList.Count);

            // Real-time friendly: update target values (control animates smoothly)
            for (var i = 0; i < count; i++)
            {
                _progressList[i].SetTargetValueThreadSafe(spectrum[i]);
            }

            // Safe fallback if bins < bars
            for (var i = count; i < _progressList.Count; i++)
            {
                _progressList[i].SetTargetValueThreadSafe(0);
            }
        }

        private void InitializeBars()
        {
            const int barCount = 82;
            _progressList = new List<VerticalProgressBar>(barCount);

            var x = 16;

            for (var i = 1; i <= barCount; i++)
            {
                var mode = "Pulse"; // Bricks, Dots, Center, Mirror, Line, Wave, Gradient, Pulse, Spectrum

                var progress = new VerticalProgressBar
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    Maximum = 255,
                    Name = $"ProgressBar_{i:00}",
                    Tag = $"{mode}|{i}",
                    Size = new Size(14, 320),
                    Location = new Point(x, 50),
                    Visible = true,

                    TopEmphasisStart = 0.88f,
                    TopEmphasisStrength = 0.55f,
                    HeatIntensityCurve = 1.75f,
                    AnimationFps = 60
                };

                if (mode == "Bricks")
                {
                    progress.ResponseTimeMs = 26;
                    progress.SnapUpThreshold = 2;
                    progress.PeakHoldMilliseconds = 260;
                    progress.PeakDecayPerTick = 1.25f;
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Dots")
                {
                    progress.ResponseTimeMs = 34;
                    progress.SnapUpThreshold = 3;
                    progress.PeakHoldMilliseconds = 170;
                    progress.PeakDecayPerTick = 1.10f;
                    progress.PeakLineThickness = 2; // dots use dot marker, thickness is harmless
                }
                else if (mode == "Center")
                {
                    progress.ResponseTimeMs = 52;
                    progress.SnapUpThreshold = 5;
                    progress.PeakHoldMilliseconds = 0; // ignored by Center/Mirror peak rules
                    progress.PeakDecayPerTick = 1.0f;  // ignored
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Mirror")
                {
                    progress.ResponseTimeMs = 48;
                    progress.SnapUpThreshold = 5;
                    progress.PeakHoldMilliseconds = 0; // ignored
                    progress.PeakDecayPerTick = 1.0f;  // ignored
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Line")
                {
                    // super readable, "meter" feel
                    progress.ResponseTimeMs = 22;
                    progress.SnapUpThreshold = 2;
                    progress.PeakHoldMilliseconds = 220;
                    progress.PeakDecayPerTick = 1.20f;
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Gradient")
                {
                    // smooth but still quick; gradients look best without micro jitter
                    progress.ResponseTimeMs = 30;
                    progress.SnapUpThreshold = 2;
                    progress.PeakHoldMilliseconds = 240;
                    progress.PeakDecayPerTick = 1.15f;
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Wave")
                {
                    // wave should be "musical", not twitchy
                    progress.ResponseTimeMs = 40;
                    progress.SnapUpThreshold = 3;
                    progress.PeakHoldMilliseconds = 190;
                    progress.PeakDecayPerTick = 1.05f;
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Pulse")
                {
                    // pulse is an "energy" look: slower response so it breathes
                    progress.ResponseTimeMs = 60;
                    progress.SnapUpThreshold = 4;
                    progress.PeakHoldMilliseconds = 160;
                    progress.PeakDecayPerTick = 0.95f;
                    progress.PeakLineThickness = 2;
                }
                else if (mode == "Spectrum")
                {
                    // spectrum should feel like a real analyzer column (fast attack, longer hold)
                    progress.ResponseTimeMs = 24;
                    progress.SnapUpThreshold = 2;
                    progress.PeakHoldMilliseconds = 300;
                    progress.PeakDecayPerTick = 1.20f;
                    progress.PeakLineThickness = 2;
                }

                _progressList.Add(progress);
                ambiance_ThemeSpectrum.Controls.Add(progress);

                x += 13;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Analyzer.OnChange -= new OnChangeHandler(Spectrum_Change);
            base.OnFormClosed(e);
        }
    }
}