// -----------------------------------------------------------------------------
//  Glass.Message — the rich result returned by the builder's ShowEx() call.
//
//  File        : GlassResult.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System.Windows.Forms;

namespace Glass;

/// <summary>
/// Captures everything a dialog can hand back: the button the user pressed, the
/// state of the optional checkbox, and any text entered into the input field.
/// </summary>
/// <remarks>
/// An instance converts implicitly to <see cref="DialogResult"/>, so callers that
/// only care about the button can use it exactly like a classic message-box result.
/// </remarks>
public sealed class GlassResult
{
    /// <summary>The button the user clicked (or the dialog's escape/timeout result).</summary>
    public DialogResult Button { get; }

    /// <summary>Whether the optional "don't show again"-style checkbox was ticked.</summary>
    public bool CheckBoxChecked { get; }

    /// <summary>The text the user entered, or <see cref="string.Empty"/> when there was no input field.</summary>
    public string InputText { get; }

    internal GlassResult(DialogResult button, bool checkBoxChecked, string inputText)
    {
        Button = button;
        CheckBoxChecked = checkBoxChecked;
        // Normalise to empty so callers never have to null-check the input text.
        InputText = inputText ?? string.Empty;
    }

    /// <summary>
    /// Lets a <see cref="GlassResult"/> be used wherever a <see cref="DialogResult"/>
    /// is expected, keeping drop-in compatibility with the classic API.
    /// </summary>
    public static implicit operator DialogResult(GlassResult r) => r.Button;
}
