using System.Windows.Forms;

namespace Glass;

public sealed class GlassResult
{
    public DialogResult Button { get; }

    public bool CheckBoxChecked { get; }

    public string InputText { get; }

    internal GlassResult(DialogResult button, bool checkBoxChecked, string inputText)
    {
        Button        = button;
        CheckBoxChecked = checkBoxChecked;
        InputText     = inputText ?? string.Empty;
    }

    public static implicit operator DialogResult(GlassResult r) => r.Button;
}
