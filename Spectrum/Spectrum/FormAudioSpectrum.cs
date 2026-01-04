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

            for (var i = 0; i < count; i++)
            {
                var bar = _progressList[i];
                var value = spectrum[i];

                bar.Value = value;

                var newColor = value <= 49
                    ? Color.Lime
                    : value <= 99 ? Color.LimeGreen : value <= 149 ? Color.GreenYellow : value <= 199 ? Color.Orange : Color.OrangeRed;

                if (bar.ForeColor != newColor)
                {
                    bar.ForeColor = newColor;
                }
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
                    Maximum = 255, // keep exact behavior
                    Name = string.Concat("ProgressBar", "_", i.ToString("00")),
                    Tag = i.ToString(),
                    Size = new Size(14, 320),
                    Location = new Point(basePoint, 50),
                    Visible = true
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