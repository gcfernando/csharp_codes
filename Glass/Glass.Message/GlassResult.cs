using System.Windows.Forms;

namespace Glass;

/// <summary>
/// Rich result returned by <see cref="GlassBuilder.ShowEx"/>.
/// Carries the button pressed, the optional "don't show again" checkbox state,
/// and any text the user typed in an inline input field.
/// </summary>
public sealed class GlassResult
{
    /// <summary>Which dialog button the user pressed.</summary>
    public DialogResult Button { get; }

    /// <summary>
    /// Checked state of the "don't show again" checkbox.
    /// Always <c>false</c> when no checkbox was added.
    /// </summary>
    public bool CheckBoxChecked { get; }

    /// <summary>
    /// Text entered (or selected) in the inline input field.
    /// Always <see cref="string.Empty"/> when no input control was added.
    /// </summary>
    public string InputText { get; }

    internal GlassResult(DialogResult button, bool checkBoxChecked, string inputText)
    {
        Button        = button;
        CheckBoxChecked = checkBoxChecked;
        InputText     = inputText ?? string.Empty;
    }

    /// <summary>Implicit conversion so existing <c>if (result == DialogResult.OK)</c> code still compiles.</summary>
    public static implicit operator DialogResult(GlassResult r) => r.Button;
}
