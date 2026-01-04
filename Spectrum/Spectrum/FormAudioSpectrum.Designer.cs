namespace Spectrum
{
    partial class FormAudioSpectrum
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormAudioSpectrum));
            this.ambiance_ThemeSpectrum = new Ambiance_ThemeContainer();
            this.ambiance_LabelDeveloper = new Ambiance_Label();
            this.ambiance_ControlBox = new Ambiance_ControlBox();
            this.ambiance_ThemeSpectrum.SuspendLayout();
            this.SuspendLayout();
            // 
            // ambiance_ThemeSpectrum
            // 
            this.ambiance_ThemeSpectrum.BackColor = System.Drawing.Color.Black;
            this.ambiance_ThemeSpectrum.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ambiance_ThemeSpectrum.Controls.Add(this.ambiance_LabelDeveloper);
            this.ambiance_ThemeSpectrum.Controls.Add(this.ambiance_ControlBox);
            this.ambiance_ThemeSpectrum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ambiance_ThemeSpectrum.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ambiance_ThemeSpectrum.Location = new System.Drawing.Point(0, 0);
            this.ambiance_ThemeSpectrum.Name = "ambiance_ThemeSpectrum";
            this.ambiance_ThemeSpectrum.Padding = new System.Windows.Forms.Padding(20, 56, 20, 16);
            this.ambiance_ThemeSpectrum.RoundCorners = true;
            this.ambiance_ThemeSpectrum.Sizable = false;
            this.ambiance_ThemeSpectrum.Size = new System.Drawing.Size(1100, 378);
            this.ambiance_ThemeSpectrum.SmartBounds = true;
            this.ambiance_ThemeSpectrum.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ambiance_ThemeSpectrum.TabIndex = 0;
            this.ambiance_ThemeSpectrum.Text = "Audio Spectrum Analyzer";
            // 
            // ambiance_LabelDeveloper
            // 
            this.ambiance_LabelDeveloper.AutoSize = true;
            this.ambiance_LabelDeveloper.BackColor = System.Drawing.Color.Transparent;
            this.ambiance_LabelDeveloper.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ambiance_LabelDeveloper.ForeColor = System.Drawing.Color.DimGray;
            this.ambiance_LabelDeveloper.Location = new System.Drawing.Point(857, 20);
            this.ambiance_LabelDeveloper.Name = "ambiance_LabelDeveloper";
            this.ambiance_LabelDeveloper.Size = new System.Drawing.Size(231, 15);
            this.ambiance_LabelDeveloper.TabIndex = 5;
            this.ambiance_LabelDeveloper.Text = "Developed by ::. Gehan Fernando.";
            // 
            // ambiance_ControlBox
            // 
            this.ambiance_ControlBox.BackColor = System.Drawing.Color.Transparent;
            this.ambiance_ControlBox.EnableMaximize = false;
            this.ambiance_ControlBox.Font = new System.Drawing.Font("Marlett", 7F);
            this.ambiance_ControlBox.Location = new System.Drawing.Point(5, 13);
            this.ambiance_ControlBox.Name = "ambiance_ControlBox";
            this.ambiance_ControlBox.Size = new System.Drawing.Size(44, 22);
            this.ambiance_ControlBox.TabIndex = 0;
            // 
            // FormAudioSpectrum
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(1100, 378);
            this.Controls.Add(this.ambiance_ThemeSpectrum);
            this.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(261, 65);
            this.Name = "FormAudioSpectrum";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Audio Spectrum Analyzer";
            this.TransparencyKey = System.Drawing.Color.Fuchsia;
            this.Load += new System.EventHandler(this.FormAudioSpectrum_Load);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FormAudioSpectrum_KeyUp);
            this.ambiance_ThemeSpectrum.ResumeLayout(false);
            this.ambiance_ThemeSpectrum.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private Ambiance_ThemeContainer ambiance_ThemeSpectrum;
        private Ambiance_ControlBox ambiance_ControlBox;
        private Ambiance_Label ambiance_LabelDeveloper;
    }
}

