namespace FourKWinForm;

partial class HDForm
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
        labelName = new Label();
        textBoxName = new TextBox();
        labelCountry = new Label();
        textBoxCountry = new TextBox();
        buttonMessage = new Button();
        buttonExit = new Button();
        SuspendLayout();
        // 
        // labelName
        // 
        labelName.AutoSize = true;
        labelName.Location = new Point(22, 43);
        labelName.Name = "labelName";
        labelName.Size = new Size(39, 15);
        labelName.TabIndex = 0;
        labelName.Text = "Name";
        // 
        // textBoxName
        // 
        textBoxName.BorderStyle = BorderStyle.FixedSingle;
        textBoxName.Location = new Point(86, 43);
        textBoxName.Name = "textBoxName";
        textBoxName.Size = new Size(186, 23);
        textBoxName.TabIndex = 1;
        // 
        // labelCountry
        // 
        labelCountry.AutoSize = true;
        labelCountry.Location = new Point(22, 72);
        labelCountry.Name = "labelCountry";
        labelCountry.Size = new Size(50, 15);
        labelCountry.TabIndex = 2;
        labelCountry.Text = "Country";
        // 
        // textBoxCountry
        // 
        textBoxCountry.BorderStyle = BorderStyle.FixedSingle;
        textBoxCountry.Location = new Point(86, 72);
        textBoxCountry.Name = "textBoxCountry";
        textBoxCountry.Size = new Size(186, 23);
        textBoxCountry.TabIndex = 3;
        // 
        // buttonMessage
        // 
        buttonMessage.Location = new Point(86, 101);
        buttonMessage.Name = "buttonMessage";
        buttonMessage.Size = new Size(186, 50);
        buttonMessage.TabIndex = 4;
        buttonMessage.Text = "Message";
        buttonMessage.UseVisualStyleBackColor = true;
        buttonMessage.Click += buttonMessage_Click;
        // 
        // buttonExit
        // 
        buttonExit.Location = new Point(86, 157);
        buttonExit.Name = "buttonExit";
        buttonExit.Size = new Size(186, 50);
        buttonExit.TabIndex = 5;
        buttonExit.Text = "Exit";
        buttonExit.UseVisualStyleBackColor = true;
        buttonExit.Click += buttonExit_Click;
        // 
        // HDForm
        // 
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackgroundImageLayout = ImageLayout.None;
        ClientSize = new Size(284, 261);
        Controls.Add(buttonExit);
        Controls.Add(buttonMessage);
        Controls.Add(textBoxCountry);
        Controls.Add(labelCountry);
        Controls.Add(textBoxName);
        Controls.Add(labelName);
        KeyPreview = true;
        MaximizeBox = false;
        MinimumSize = new Size(300, 300);
        Name = "HDForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "HD Form";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label labelName;
    private TextBox textBoxName;
    private Label labelCountry;
    private TextBox textBoxCountry;
    private Button buttonMessage;
    private Button buttonExit;
}