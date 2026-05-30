using System.Drawing;
using System.Windows.Forms;

namespace Glass;

/// <summary>All options that configure a single <see cref="GlassDialog"/> session.</summary>
internal sealed class GlassDialogConfig
{
    // ── Core ──────────────────────────────────────────────────────────────
    public string                  Message       { get; set; } = string.Empty;
    public string                  Title         { get; set; } = string.Empty;
    public MessageBoxIcon          Icon          { get; set; } = MessageBoxIcon.None;
    public Bitmap                  CustomIcon    { get; set; }
    public MessageBoxButtons       Buttons       { get; set; } = MessageBoxButtons.OK;
    public MessageBoxDefaultButton DefaultButton { get; set; } = MessageBoxDefaultButton.Button1;
    public GlassTheme              Theme         { get; set; }
    public string[]                CustomLabels  { get; set; }

    // ── Animation ─────────────────────────────────────────────────────────
    public GlassAnimation Animation { get; set; } = GlassAnimation.Fade;

    // ── Auto-close (0 = disabled) ─────────────────────────────────────────
    public int AutoCloseMs { get; set; }

    // ── Checkbox ──────────────────────────────────────────────────────────
    public string CheckBoxLabel   { get; set; }
    public bool   CheckBoxDefault { get; set; }

    // ── Inline input ──────────────────────────────────────────────────────
    public GlassInputMode InputMode          { get; set; } = GlassInputMode.None;
    public string         InputPlaceholder   { get; set; }
    public string[]       InputDropdownItems { get; set; }
    public string         InputDefault       { get; set; }

    // ── Expandable detail ─────────────────────────────────────────────────
    public string DetailText { get; set; }

    // ── Progress bar ──────────────────────────────────────────────────────
    public bool ShowProgress  { get; set; }
    public int  ProgressValue { get; set; } = -1;
    public int  ProgressMax   { get; set; } = 100;

    // ── Layout ────────────────────────────────────────────────────────────
    public bool RightToLeft { get; set; }

    // ── Shape ─────────────────────────────────────────────────────────────
    /// <summary>
    /// <c>true</c> = rounded corners; <c>false</c> = sharp; <c>null</c> (default) =
    /// inherit from <see cref="GlassMessage.UseRoundedCorners"/>.
    /// </summary>
    public bool? UseRoundedCorners { get; set; }

    // ── Derived helpers ───────────────────────────────────────────────────
    public bool HasCheckBox => CheckBoxLabel != null;
    public bool HasInput    => InputMode != GlassInputMode.None;
    public bool HasDetail   => DetailText != null;
    public bool HasProgress => ShowProgress;
}
