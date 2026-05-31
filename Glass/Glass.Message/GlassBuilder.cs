// -----------------------------------------------------------------------------
//  Glass.Message — fluent builder for dialogs that go beyond a plain message box.
//  Every method returns the same builder instance so calls can be chained, and
//  Show()/ShowEx() hand the assembled configuration off to GlassMessage.
//
//  File        : GlassBuilder.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Glass;

/// <summary>
/// Chainable builder for composing a dialog from optional parts — title, icon,
/// buttons, animation, input field, checkbox, progress bar, detail panel, and so
/// on. Obtain one from <see cref="GlassMessage.Create"/>.
/// </summary>
public sealed class GlassBuilder
{
    // The message is the one required value, captured at construction; everything
    // else mirrors GlassDialogConfig and starts at the same sensible defaults.
    private readonly string _message;

    private string _title = string.Empty;
    private MessageBoxIcon _icon = MessageBoxIcon.None;
    private Bitmap _customIcon;
    private MessageBoxButtons _buttons = MessageBoxButtons.OK;
    private MessageBoxDefaultButton _defaultButton = MessageBoxDefaultButton.Button1;
    private GlassTheme _theme;
    private IWin32Window _owner;
    private string[] _customLabels;

    private GlassAnimation _animation = GlassAnimation.Fade;

    private int _autoCloseMs;

    private string _checkBoxLabel;
    private bool _checkBoxDefault;

    private GlassInputMode _inputMode = GlassInputMode.None;
    private string _inputPlaceholder;
    private string[] _inputDropdownItems;
    private string _inputDefault;

    private string _detailText;

    private bool _showProgress;
    private int _progressValue = -1;
    private int _progressMax = 100;

    private bool _rightToLeft;

    // Nullable so "never called" stays distinct from an explicit true/false and
    // can fall back to the global GlassMessage.UseRoundedCorners setting.
    private bool? _roundedCorners;

    internal GlassBuilder(string message) => _message = message ?? string.Empty;

    // --- Simple single-value setters -----------------------------------------
    /// <summary>Sets the title-bar caption.</summary>
    public GlassBuilder Title(string title) { _title = title ?? string.Empty; return this; }
    /// <summary>Uses one of the standard system icons.</summary>
    public GlassBuilder Icon(MessageBoxIcon i) { _icon = i; _customIcon = null; return this; }
    /// <summary>Uses a custom bitmap as the dialog icon (e.g. a product logo).</summary>
    public GlassBuilder Icon(Bitmap bitmap) { _customIcon = bitmap; return this; }
    /// <summary>Selects which button is focused (and is the auto-close target) by default.</summary>
    public GlassBuilder Default(MessageBoxDefaultButton d) { _defaultButton = d; return this; }
    /// <summary>Overrides the theme for this dialog only.</summary>
    public GlassBuilder Theme(GlassTheme t) { _theme = t; return this; }
    /// <summary>Sets the owner window so the dialog centres on and stays above it.</summary>
    public GlassBuilder Owner(IWin32Window o) { _owner = o; return this; }
    /// <summary>Chooses the open/close animation.</summary>
    public GlassBuilder Animation(GlassAnimation a) { _animation = a; return this; }

    /// <summary>Uses a standard button set (OK, OKCancel, YesNo, …).</summary>
    public GlassBuilder Buttons(MessageBoxButtons buttons)
    {
        _buttons = buttons;
        _customLabels = null;
        return this;
    }

    /// <summary>
    /// Supplies custom button captions. The number of labels picks the closest
    /// standard layout (1 → OK, 2 → OK/Cancel, 3+ → Yes/No/Cancel) so the dialog
    /// still maps each click onto a meaningful <see cref="DialogResult"/>.
    /// </summary>
    public GlassBuilder Buttons(params string[] labels)
    {
        if (labels == null || labels.Length == 0)
        {
            return this;
        }

        _customLabels = labels;
        _buttons = labels.Length switch
        {
            1 => MessageBoxButtons.OK,
            2 => MessageBoxButtons.OKCancel,
            _ => MessageBoxButtons.YesNoCancel,
        };
        return this;
    }

    /// <summary>Enables (or disables) Windows 11 rounded corners for this dialog.</summary>
    public GlassBuilder RoundedCorners(bool enable = true)
    {
        _roundedCorners = enable;
        return this;
    }

    /// <summary>Auto-confirms the default button after the given delay (clamped to ≥ 0).</summary>
    public GlassBuilder AutoClose(int milliseconds) { _autoCloseMs = Math.Max(0, milliseconds); return this; }

    /// <summary>Adds a checkbox (e.g. "Don't show this again") below the message.</summary>
    public GlassBuilder CheckBox(string label, bool defaultChecked = false)
    {
        _checkBoxLabel = label;
        _checkBoxDefault = defaultChecked;
        return this;
    }

    /// <summary>Adds a single-line text input.</summary>
    public GlassBuilder InputText(string placeholder = "", string defaultValue = "")
    {
        _inputMode = GlassInputMode.Text;
        _inputPlaceholder = placeholder;
        _inputDefault = defaultValue;
        return this;
    }

    /// <summary>Adds a masked password input (with reveal toggle and Caps Lock hint).</summary>
    public GlassBuilder InputPassword(string placeholder = "")
    {
        _inputMode = GlassInputMode.Password;
        _inputPlaceholder = placeholder;
        return this;
    }

    /// <summary>Adds a multi-line text input.</summary>
    public GlassBuilder InputMultiline(string placeholder = "", string defaultValue = "")
    {
        _inputMode = GlassInputMode.Multiline;
        _inputPlaceholder = placeholder;
        _inputDefault = defaultValue;
        return this;
    }

    /// <summary>Adds a drop-down list. The optional <paramref name="defaultItem"/> is pre-selected.</summary>
    public GlassBuilder InputDropdown(IEnumerable<string> items, string defaultItem = null)
    {
        _inputMode = GlassInputMode.Dropdown;
        _inputDropdownItems = [.. items];
        _inputDefault = defaultItem;
        return this;
    }

    /// <summary>Adds an expandable "Show details" panel (handy for stack traces and diagnostics).</summary>
    public GlassBuilder Detail(string detail) { _detailText = detail; return this; }

    /// <summary>Adds an indeterminate (marquee) progress bar.</summary>
    public GlassBuilder ProgressIndeterminate()
    {
        _showProgress = true;
        _progressValue = -1;
        return this;
    }

    /// <summary>Adds a determinate progress bar at <paramref name="value"/> of <paramref name="max"/>.</summary>
    public GlassBuilder Progress(int value, int max = 100)
    {
        _showProgress = true;
        _progressValue = value;
        _progressMax = max;
        return this;
    }

    /// <summary>Mirrors the layout for right-to-left languages.</summary>
    public GlassBuilder RightToLeft(bool enable = true) { _rightToLeft = enable; return this; }

    /// <summary>Shows the dialog modally and returns just the button that was pressed.</summary>
    public DialogResult Show() => GlassMessage.CoreEx(_owner, BuildConfig()).Button;

    /// <summary>Shows the dialog modally and returns the full <see cref="GlassResult"/> (button + checkbox + input).</summary>
    public GlassResult ShowEx() => GlassMessage.CoreEx(_owner, BuildConfig());

    // Snapshots the accumulated fields into the config the dialog consumes.
    private GlassDialogConfig BuildConfig() => new()
    {
        Message = _message,
        Title = _title,
        Icon = _icon,
        CustomIcon = _customIcon,
        Buttons = _buttons,
        DefaultButton = _defaultButton,
        Theme = _theme ?? GlassMessage.DefaultTheme ?? GlassTheme.Default,
        CustomLabels = _customLabels,
        Animation = _animation,
        AutoCloseMs = _autoCloseMs,
        CheckBoxLabel = _checkBoxLabel,
        CheckBoxDefault = _checkBoxDefault,
        InputMode = _inputMode,
        InputPlaceholder = _inputPlaceholder,
        InputDropdownItems = _inputDropdownItems,
        InputDefault = _inputDefault,
        DetailText = _detailText,
        ShowProgress = _showProgress,
        ProgressValue = _progressValue,
        ProgressMax = _progressMax,
        RightToLeft = _rightToLeft,
        UseRoundedCorners = _roundedCorners,
    };
}
