using System.Drawing;
using System.Windows.Forms;

namespace Glass;

internal sealed class GlassDialogConfig
{
    public string                  Message       { get; set; } = string.Empty;
    public string                  Title         { get; set; } = string.Empty;
    public MessageBoxIcon          Icon          { get; set; } = MessageBoxIcon.None;
    public Bitmap                  CustomIcon    { get; set; }
    public MessageBoxButtons       Buttons       { get; set; } = MessageBoxButtons.OK;
    public MessageBoxDefaultButton DefaultButton { get; set; } = MessageBoxDefaultButton.Button1;
    public GlassTheme              Theme         { get; set; }
    public string[]                CustomLabels  { get; set; }

    public GlassAnimation Animation { get; set; } = GlassAnimation.Fade;

    public int AutoCloseMs { get; set; }

    public string CheckBoxLabel   { get; set; }
    public bool   CheckBoxDefault { get; set; }

    public GlassInputMode InputMode          { get; set; } = GlassInputMode.None;
    public string         InputPlaceholder   { get; set; }
    public string[]       InputDropdownItems { get; set; }
    public string         InputDefault       { get; set; }

    public string DetailText { get; set; }

    public bool ShowProgress  { get; set; }
    public int  ProgressValue { get; set; } = -1;
    public int  ProgressMax   { get; set; } = 100;

    public bool RightToLeft { get; set; }

    public bool? UseRoundedCorners { get; set; }

    public bool HasCheckBox => CheckBoxLabel != null;
    public bool HasInput    => InputMode != GlassInputMode.None;
    public bool HasDetail   => DetailText != null;
    public bool HasProgress => ShowProgress;
}
