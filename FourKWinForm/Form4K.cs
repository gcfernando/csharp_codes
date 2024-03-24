namespace FourKWinForm;

public partial class Form4K : Form
{
    public Form4K()
    {
        InitializeComponent();
    }

    private void buttonShow_Click(object sender, EventArgs e)
    {
        var name = textBoxName.Text.Trim();
        var country = textBoxCountry.Text.Trim();

        var message = $"Hello my name is {name}\r\nI am from {country}";

        MessageBox.Show(message, ExtractHeader().Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private Form4K ExtractHeader()
    {
        return this;
    }
}
