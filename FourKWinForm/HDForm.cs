using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FourKWinForm;
public partial class HDForm : Form
{
    public HDForm()
    {
        InitializeComponent();
    }

    private void buttonMessage_Click(object sender, EventArgs e)
    {
        var name = textBoxName.Text;
        var country = textBoxCountry.Text;

        var message = $"Hello my name is {name},\r\nI am from {country}";

        MessageBox.Show(message, ExtractHeader().Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private HDForm ExtractHeader()
    {
        return this;
    }

    private void buttonExit_Click(object sender, EventArgs e)
    {
        Application.ExitThread();
        Application.Exit();
    }
}
