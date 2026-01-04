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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormAudioSpectrum));
            this.timerSpectrum = new System.Windows.Forms.Timer(this.components);
            this.ambiance_ThemeSpectrum = new Ambiance_ThemeContainer();
            this.ambiance_LabelDeveloper = new Ambiance_Label();
            this.ambiance_ControlBox = new Ambiance_ControlBox();
            this.progressBarRight = new Ambiance_ProgressBar();
            this.progressBarLeft = new Ambiance_ProgressBar();
            this.labelLine = new System.Windows.Forms.Label();
            this.labelRight = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.labelLeft = new System.Windows.Forms.Label();
            this.pictureBoxSpeaker = new System.Windows.Forms.PictureBox();
            this.ambiance_ThemeSpectrum.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSpeaker)).BeginInit();
            this.SuspendLayout();
            // 
            // timerSpectrum
            // 
            this.timerSpectrum.Interval = 25;
            this.timerSpectrum.Tick += new System.EventHandler(this.timerSpectrum_Tick);
            // 
            // ambiance_ThemeSpectrum
            // 
            this.ambiance_ThemeSpectrum.BackColor = System.Drawing.Color.Black;
            this.ambiance_ThemeSpectrum.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ambiance_ThemeSpectrum.Controls.Add(this.ambiance_LabelDeveloper);
            this.ambiance_ThemeSpectrum.Controls.Add(this.ambiance_ControlBox);
            this.ambiance_ThemeSpectrum.Controls.Add(this.progressBarRight);
            this.ambiance_ThemeSpectrum.Controls.Add(this.progressBarLeft);
            this.ambiance_ThemeSpectrum.Controls.Add(this.labelLine);
            this.ambiance_ThemeSpectrum.Controls.Add(this.labelRight);
            this.ambiance_ThemeSpectrum.Controls.Add(this.pictureBox1);
            this.ambiance_ThemeSpectrum.Controls.Add(this.labelLeft);
            this.ambiance_ThemeSpectrum.Controls.Add(this.pictureBoxSpeaker);
            this.ambiance_ThemeSpectrum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ambiance_ThemeSpectrum.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ambiance_ThemeSpectrum.Location = new System.Drawing.Point(0, 0);
            this.ambiance_ThemeSpectrum.Name = "ambiance_ThemeSpectrum";
            this.ambiance_ThemeSpectrum.Padding = new System.Windows.Forms.Padding(20, 56, 20, 16);
            this.ambiance_ThemeSpectrum.RoundCorners = true;
            this.ambiance_ThemeSpectrum.Sizable = false;
            this.ambiance_ThemeSpectrum.Size = new System.Drawing.Size(1100, 378);
            this.ambiance_ThemeSpectrum.SmartBounds = true;
            this.ambiance_ThemeSpectrum.StartPosition = System.Windows.Forms.FormStartPosition.WindowsDefaultLocation;
            this.ambiance_ThemeSpectrum.TabIndex = 0;
            this.ambiance_ThemeSpectrum.Text = "Audio Spectrum Analyzer";
            // 
            // ambiance_LabelDeveloper
            // 
            this.ambiance_LabelDeveloper.AutoSize = true;
            this.ambiance_LabelDeveloper.BackColor = System.Drawing.Color.Transparent;
            this.ambiance_LabelDeveloper.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ambiance_LabelDeveloper.ForeColor = System.Drawing.Color.Silver;
            this.ambiance_LabelDeveloper.Location = new System.Drawing.Point(15, 102);
            this.ambiance_LabelDeveloper.Name = "ambiance_LabelDeveloper";
            this.ambiance_LabelDeveloper.Size = new System.Drawing.Size(210, 15);
            this.ambiance_LabelDeveloper.TabIndex = 5;
            this.ambiance_LabelDeveloper.Text = "Developer ::. Gehan Fernando.";
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
            // progressBarRight
            // 
            this.progressBarRight.BackColor = System.Drawing.Color.White;
            this.progressBarRight.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.progressBarRight.DrawHatch = true;
            this.progressBarRight.Location = new System.Drawing.Point(564, 62);
            this.progressBarRight.Maximum = 100;
            this.progressBarRight.Minimum = 0;
            this.progressBarRight.MinimumSize = new System.Drawing.Size(58, 20);
            this.progressBarRight.Name = "progressBarRight";
            this.progressBarRight.ShowPercentage = false;
            this.progressBarRight.Size = new System.Drawing.Size(246, 20);
            this.progressBarRight.TabIndex = 4;
            this.progressBarRight.Value = 0;
            this.progressBarRight.ValueAlignment = Ambiance_ProgressBar.Alignment.Right;
            // 
            // progressBarLeft
            // 
            this.progressBarLeft.BackColor = System.Drawing.Color.White;
            this.progressBarLeft.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.progressBarLeft.DrawHatch = true;
            this.progressBarLeft.Location = new System.Drawing.Point(125, 62);
            this.progressBarLeft.Maximum = 100;
            this.progressBarLeft.Minimum = 0;
            this.progressBarLeft.MinimumSize = new System.Drawing.Size(58, 20);
            this.progressBarLeft.Name = "progressBarLeft";
            this.progressBarLeft.ShowPercentage = false;
            this.progressBarLeft.Size = new System.Drawing.Size(246, 20);
            this.progressBarLeft.TabIndex = 2;
            this.progressBarLeft.Value = 0;
            this.progressBarLeft.ValueAlignment = Ambiance_ProgressBar.Alignment.Right;
            // 
            // labelLine
            // 
            this.labelLine.BackColor = System.Drawing.Color.White;
            this.labelLine.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.labelLine.ForeColor = System.Drawing.Color.White;
            this.labelLine.Location = new System.Drawing.Point(10, 100);
            this.labelLine.Name = "labelLine";
            this.labelLine.Size = new System.Drawing.Size(1081, 2);
            this.labelLine.TabIndex = 5;
            // 
            // labelRight
            // 
            this.labelRight.AutoSize = true;
            this.labelRight.BackColor = System.Drawing.Color.Transparent;
            this.labelRight.Font = new System.Drawing.Font("Consolas", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelRight.ForeColor = System.Drawing.Color.Red;
            this.labelRight.Location = new System.Drawing.Point(498, 56);
            this.labelRight.Name = "labelRight";
            this.labelRight.Size = new System.Drawing.Size(60, 32);
            this.labelRight.TabIndex = 3;
            this.labelRight.Text = "R :";
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.Transparent;
            this.pictureBox1.BackgroundImage = global::Spectrum.Properties.Resources.Speaker;
            this.pictureBox1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.pictureBox1.Location = new System.Drawing.Point(457, 55);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(35, 35);
            this.pictureBox1.TabIndex = 10;
            this.pictureBox1.TabStop = false;
            // 
            // labelLeft
            // 
            this.labelLeft.AutoSize = true;
            this.labelLeft.BackColor = System.Drawing.Color.Transparent;
            this.labelLeft.Font = new System.Drawing.Font("Consolas", 20.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLeft.ForeColor = System.Drawing.Color.Red;
            this.labelLeft.Location = new System.Drawing.Point(59, 56);
            this.labelLeft.Name = "labelLeft";
            this.labelLeft.Size = new System.Drawing.Size(60, 32);
            this.labelLeft.TabIndex = 1;
            this.labelLeft.Text = "L :";
            // 
            // pictureBoxSpeaker
            // 
            this.pictureBoxSpeaker.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxSpeaker.BackgroundImage = global::Spectrum.Properties.Resources.Speaker;
            this.pictureBoxSpeaker.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.pictureBoxSpeaker.Location = new System.Drawing.Point(18, 55);
            this.pictureBoxSpeaker.Name = "pictureBoxSpeaker";
            this.pictureBoxSpeaker.Size = new System.Drawing.Size(35, 35);
            this.pictureBoxSpeaker.TabIndex = 7;
            this.pictureBoxSpeaker.TabStop = false;
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
            this.Text = "Audio Spectrum Analyzer";
            this.TransparencyKey = System.Drawing.Color.Fuchsia;
            this.Load += new System.EventHandler(this.FormAudioSpectrum_Load);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.FormAudioSpectrum_KeyUp);
            this.ambiance_ThemeSpectrum.ResumeLayout(false);
            this.ambiance_ThemeSpectrum.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxSpeaker)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timerSpectrum;
        private Ambiance_ThemeContainer ambiance_ThemeSpectrum;
        private System.Windows.Forms.Label labelLine;
        private System.Windows.Forms.Label labelRight;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label labelLeft;
        private System.Windows.Forms.PictureBox pictureBoxSpeaker;
        private Ambiance_ProgressBar progressBarLeft;
        private Ambiance_ProgressBar progressBarRight;
        private Ambiance_ControlBox ambiance_ControlBox;
        private Ambiance_Label ambiance_LabelDeveloper;
    }
}

