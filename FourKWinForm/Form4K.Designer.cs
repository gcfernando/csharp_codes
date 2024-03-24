namespace FourKWinForm;

partial class Form4K
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form4K));
        labelName = new Label();
        textBoxName = new TextBox();
        labelCountry = new Label();
        textBoxCountry = new TextBox();
        buttonShow = new Button();
        SuspendLayout();
        // 
        // labelName
        // 
        resources.ApplyResources(labelName, "labelName");
        labelName.Name = "labelName";
        // 
        // textBoxName
        // 
        resources.ApplyResources(textBoxName, "textBoxName");
        textBoxName.Name = "textBoxName";
        // 
        // labelCountry
        // 
        resources.ApplyResources(labelCountry, "labelCountry");
        labelCountry.Name = "labelCountry";
        // 
        // textBoxCountry
        // 
        resources.ApplyResources(textBoxCountry, "textBoxCountry");
        textBoxCountry.Name = "textBoxCountry";
        // 
        // buttonShow
        // 
        resources.ApplyResources(buttonShow, "buttonShow");
        buttonShow.Name = "buttonShow";
        buttonShow.UseVisualStyleBackColor = true;
        buttonShow.Click += buttonShow_Click;
        // 
        // Form4K
        // 
        resources.ApplyResources(this, "$this");
        AutoScaleMode = AutoScaleMode.Dpi;
        Controls.Add(buttonShow);
        Controls.Add(textBoxCountry);
        Controls.Add(labelCountry);
        Controls.Add(textBoxName);
        Controls.Add(labelName);
        KeyPreview = true;
        Name = "Form4K";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label labelName;
    private TextBox textBoxName;
    private Label labelCountry;
    private TextBox textBoxCountry;
    private Button buttonShow;
}
