// -----------------------------------------------------------------------------
//  Glass.Message — the kind of inline input field a dialog hosts, if any.
//
//  File        : GlassInputMode.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

namespace Glass;

/// <summary>
/// Selects the inline input control shown inside a dialog so it can prompt the
/// user for a value as well as a button choice.
/// </summary>
public enum GlassInputMode
{
    /// <summary>No input field — the dialog only collects a button result.</summary>
    None,

    /// <summary>A single-line text box.</summary>
    Text,

    /// <summary>A masked text box with a reveal toggle and a Caps Lock warning.</summary>
    Password,

    /// <summary>A multi-line text box with a vertical scroll bar.</summary>
    Multiline,

    /// <summary>A read-only drop-down list the user picks an item from.</summary>
    Dropdown,
}
