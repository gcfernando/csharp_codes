using System.Drawing;
using System.Windows.Forms;

namespace Glass;

/// <summary>All options that configure a single <see cref="GlassDialog"/> session.
/// Built by <see cref="GlassBuilder"/> or created directly by <see cref="GlassMessage"/>.</summary>
internal sealed class GlassDialogConfig
{
    // ── Core (mirrors classic MessageBox parameters) ──────────────────────
    public string          Message       { get; set; } = string.Empty;
    public string          Title         { get; set; } = string.Empty;
    public MessageBoxIcon  Icon          { get; set; } = MessageBoxIcon.None;
    public Bitmap          CustomIcon    { get; set; }
    public MessageBoxButtons Buttons     { get; set; } = MessageBoxButtons.OK;
    public MessageBoxDefaultButton DefaultButton { get; set; } = MessageBoxDefaultButton.Button1;
    public GlassTheme      Theme         { get; set; }
    public string[]        CustomLabels  { get; set; }

    // ── Animation ─────────────────────────────────────────────────────────
    public GlassAnimation  Animation     { get; set; } = GlassAnimation.Fade;

    // ── Auto-close countdown (0 = disabled) ──────────────────────────────
    public int             AutoCloseMs   { get; set; }

    // ── "Don't show again" checkbox ───────────────────────────────────────
    public string          CheckBoxLabel { get; set; }     // null = no checkbox
    public bool            CheckBoxDefault { get; set; }

    // ── Inline input control ──────────────────────────────────────────────
    public GlassInputMode  InputMode     { get; set; } = GlassInputMode.None;
    public string          InputPlaceholder { get; set; }
    public string[]        InputDropdownItems { get; set; }
    public string          InputDefault  { get; set; }

    // ── Expandable detail section ─────────────────────────────────────────
    public string          DetailText    { get; set; }     // null = no detail

    // ── Progress bar ──────────────────────────────────────────────────────
    public bool            ShowProgress  { get; set; }
    public int             ProgressValue { get; set; } = -1;  // -1 = indeterminate
    public int             ProgressMax   { get; set; } = 100;

    // ── Layout ────────────────────────────────────────────────────────────
    public bool            RightToLeft   { get; set; }

    // ── Derived helpers ───────────────────────────────────────────────────
    public bool HasCheckBox  => CheckBoxLabel != null;
    public bool HasInput     => InputMode != GlassInputMode.None;
    public bool HasDetail    => DetailText != null;
    public bool HasProgress  => ShowProgress;
}
