namespace Glass;

/// <summary>Inline user-input control rendered inside a <see cref="GlassMessage"/> dialog.</summary>
public enum GlassInputMode
{
    /// <summary>No input control (default).</summary>
    None,

    /// <summary>Single-line plain-text field.</summary>
    Text,

    /// <summary>Single-line password field (characters are masked).</summary>
    Password,

    /// <summary>Multi-line text area with a vertical scroll bar.</summary>
    Multiline,

    /// <summary>Drop-down combo box with a fixed list of choices.</summary>
    Dropdown,
}
