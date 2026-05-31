// -----------------------------------------------------------------------------
//  Glass.Message — the immutable-by-convention bag of settings that describes a
//  single dialog. Built by GlassBuilder (or GlassMessage) and consumed by
//  GlassDialog when it lays itself out.
//
//  File        : GlassDialogConfig.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System.Drawing;
using System.Windows.Forms;

namespace Glass;

/// <summary>
/// Plain data object carrying every option a <see cref="GlassDialog"/> needs.
/// Keeping this separate from the form keeps the public API (builder + facade)
/// decoupled from the WinForms implementation.
/// </summary>
internal sealed class GlassDialogConfig
{
    // --- Core content ---------------------------------------------------------
    public string Message { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.None;
    public Bitmap CustomIcon { get; set; }
    public MessageBoxButtons Buttons { get; set; } = MessageBoxButtons.OK;
    public MessageBoxDefaultButton DefaultButton { get; set; } = MessageBoxDefaultButton.Button1;
    public GlassTheme Theme { get; set; }

    /// <summary>Optional per-button captions that override the default OK/Cancel/etc. text.</summary>
    public string[] CustomLabels { get; set; }

    /// <summary>Entrance/exit animation. Defaults to a gentle fade.</summary>
    public GlassAnimation Animation { get; set; } = GlassAnimation.Fade;

    /// <summary>When greater than zero, the dialog auto-confirms after this many milliseconds.</summary>
    public int AutoCloseMs { get; set; }

    // --- Optional checkbox ----------------------------------------------------
    public string CheckBoxLabel { get; set; }
    public bool CheckBoxDefault { get; set; }

    // --- Optional inline input ------------------------------------------------
    public GlassInputMode InputMode { get; set; } = GlassInputMode.None;
    public string InputPlaceholder { get; set; }
    public string[] InputDropdownItems { get; set; }
    public string InputDefault { get; set; }

    /// <summary>Text revealed by the expandable "Show details" section (e.g. a stack trace).</summary>
    public string DetailText { get; set; }

    // --- Optional progress bar ------------------------------------------------
    public bool ShowProgress { get; set; }

    /// <summary>Current value, or -1 for an indeterminate (marquee) bar.</summary>
    public int ProgressValue { get; set; } = -1;
    public int ProgressMax { get; set; } = 100;

    public bool RightToLeft { get; set; }

    /// <summary>
    /// Per-dialog rounded-corner override. <c>null</c> means "fall back to the
    /// global <see cref="GlassMessage.UseRoundedCorners"/> setting".
    /// </summary>
    public bool? UseRoundedCorners { get; set; }

    // --- Convenience flags used by the layout pass ----------------------------
    public bool HasCheckBox => CheckBoxLabel != null;
    public bool HasInput => InputMode != GlassInputMode.None;
    public bool HasDetail => DetailText != null;
    public bool HasProgress => ShowProgress;
}
