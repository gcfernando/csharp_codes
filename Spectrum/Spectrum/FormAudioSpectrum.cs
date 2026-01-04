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
        private List<VerticalProgressBar> _progressList = null;

        private MMDevice device = null;
        private Analyzer _analyzer = null;

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
            Int_barList();

            _analyzer = new Analyzer();

            Analyzer.OnChange += new OnChangeHandler(Spectrum_Change);

            var enumerator = new MMDeviceEnumerator();

            device =
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
            if (InvokeRequired)
            {
                _ = BeginInvoke(new Action<object, OnChangeEventArgs>(Spectrum_Change), obj, e);
                return;
            }

            var spectrum = e.Spectrumdata;
            var count = Math.Min(spectrum.Count, _progressList.Count);

            // Update values
            for (var i = 0; i < count; i++)
            {
                _progressList[i].Value = spectrum[i];
            }

            // Optional: if spectrum has fewer bins than bars, clear remaining bars
            for (var i = count; i < _progressList.Count; i++)
            {
                _progressList[i].Value = 0;
            }
        }

        private void Int_barList()
        {
            _progressList = new List<VerticalProgressBar>(82);

            var basePoint = 16;

            for (var i = 1; i <= 82; i++)
            {
                var progress = new VerticalProgressBar
                {
                    BackColor = Color.FromArgb(50, 50, 50),
                    Maximum = 255,
                    Name = $"ProgressBar_{i:00}",
                    Tag = i.ToString(),
                    Size = new Size(14, 320),
                    Location = new Point(basePoint, 50),
                    Visible = true,

                    PeakHoldMilliseconds = 300,
                    PeakDecayPerTick = 1.2f,
                    PeakLineThickness = 2,
                    TopEmphasisStart = 0.85f,
                    TopEmphasisStrength = 0.45f
                };

                _progressList.Add(progress);
                ambiance_ThemeSpectrum.Controls.Add(progress);

                basePoint += 13;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Analyzer.OnChange -= new OnChangeHandler(Spectrum_Change);
            base.OnFormClosed(e);
        }
    }
}