using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Glass;

/// <summary>Fluent builder for <see cref="GlassMessage"/> dialogs.</summary>
public sealed class GlassBuilder
{
    private readonly string _message;

    // ── Core ──────────────────────────────────────────────────────────────
    private string                  _title         = string.Empty;
    private MessageBoxIcon          _icon          = MessageBoxIcon.None;
    private Bitmap                  _customIcon;
    private MessageBoxButtons       _buttons       = MessageBoxButtons.OK;
    private MessageBoxDefaultButton _defaultButton = MessageBoxDefaultButton.Button1;
    private GlassTheme              _theme;
    private IWin32Window            _owner;
    private string[]                _customLabels;

    // ── Animation ─────────────────────────────────────────────────────────
    private GlassAnimation _animation = GlassAnimation.Fade;

    // ── Auto-close ────────────────────────────────────────────────────────
    private int _autoCloseMs;

    // ── Checkbox ──────────────────────────────────────────────────────────
    private string _checkBoxLabel;
    private bool   _checkBoxDefault;

    // ── Input ─────────────────────────────────────────────────────────────
    private GlassInputMode _inputMode = GlassInputMode.None;
    private string         _inputPlaceholder;
    private string[]       _inputDropdownItems;
    private string         _inputDefault;

    // ── Detail ────────────────────────────────────────────────────────────
    private string _detailText;

    // ── Progress ──────────────────────────────────────────────────────────
    private bool _showProgress;
    private int  _progressValue = -1;
    private int  _progressMax   = 100;

    // ── Layout ────────────────────────────────────────────────────────────
    private bool _rightToLeft;

    // ── Shape — null means "inherit from GlassMessage.UseRoundedCorners" ──
    private bool? _roundedCorners;

    // ═══════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════
    internal GlassBuilder(string message) => _message = message ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // Chainable methods
    // ═══════════════════════════════════════════════════════════════════════

    public GlassBuilder Title(string title)      { _title = title ?? string.Empty; return this; }
    public GlassBuilder Icon(MessageBoxIcon i)   { _icon = i; _customIcon = null; return this; }
    public GlassBuilder Icon(Bitmap bitmap)      { _customIcon = bitmap; return this; }
    public GlassBuilder Default(MessageBoxDefaultButton d) { _defaultButton = d; return this; }
    public GlassBuilder Theme(GlassTheme t)      { _theme = t; return this; }
    public GlassBuilder Owner(IWin32Window o)    { _owner = o; return this; }
    public GlassBuilder Animation(GlassAnimation a) { _animation = a; return this; }

    // ── Buttons ───────────────────────────────────────────────────────────

    public GlassBuilder Buttons(MessageBoxButtons buttons)
    {
        _buttons      = buttons;
        _customLabels = null;
        return this;
    }

    /// <summary>
    /// Sets custom button labels.  1 label → OK, 2 → OKCancel, 3+ → YesNoCancel.
    /// </summary>
    public GlassBuilder Buttons(params string[] labels)
    {
        if (labels == null || labels.Length == 0) return this;
        _customLabels = labels;
        _buttons = labels.Length switch
        {
            1 => MessageBoxButtons.OK,
            2 => MessageBoxButtons.OKCancel,
            _ => MessageBoxButtons.YesNoCancel,
        };
        return this;
    }

    // ── Shape ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables rounded corners for this dialog.
    /// When not called, <see cref="GlassMessage.UseRoundedCorners"/> is used.
    /// </summary>
    public GlassBuilder RoundedCorners(bool enable = true)
    {
        _roundedCorners = enable;
        return this;
    }

    // ── Auto-close ────────────────────────────────────────────────────────

    public GlassBuilder AutoClose(int milliseconds) { _autoCloseMs = milliseconds; return this; }

    // ── Checkbox ──────────────────────────────────────────────────────────

    public GlassBuilder CheckBox(string label, bool defaultChecked = false)
    {
        _checkBoxLabel   = label;
        _checkBoxDefault = defaultChecked;
        return this;
    }

    // ── Inline input ──────────────────────────────────────────────────────

    public GlassBuilder InputText(string placeholder = "", string defaultValue = "")
    {
        _inputMode        = GlassInputMode.Text;
        _inputPlaceholder = placeholder;
        _inputDefault     = defaultValue;
        return this;
    }

    public GlassBuilder InputPassword(string placeholder = "")
    {
        _inputMode        = GlassInputMode.Password;
        _inputPlaceholder = placeholder;
        return this;
    }

    public GlassBuilder InputMultiline(string placeholder = "", string defaultValue = "")
    {
        _inputMode        = GlassInputMode.Multiline;
        _inputPlaceholder = placeholder;
        _inputDefault     = defaultValue;
        return this;
    }

    public GlassBuilder InputDropdown(IEnumerable<string> items, string defaultItem = null)
    {
        _inputMode          = GlassInputMode.Dropdown;
        _inputDropdownItems = [.. items];
        _inputDefault       = defaultItem;
        return this;
    }

    // ── Expandable detail ─────────────────────────────────────────────────

    public GlassBuilder Detail(string detail) { _detailText = detail; return this; }

    // ── Progress bar ──────────────────────────────────────────────────────

    public GlassBuilder ProgressIndeterminate()
    {
        _showProgress  = true;
        _progressValue = -1;
        return this;
    }

    public GlassBuilder Progress(int value, int max = 100)
    {
        _showProgress  = true;
        _progressValue = value;
        _progressMax   = max;
        return this;
    }

    // ── Layout ────────────────────────────────────────────────────────────

    public GlassBuilder RightToLeft(bool enable = true) { _rightToLeft = enable; return this; }

    // ═══════════════════════════════════════════════════════════════════════
    // Terminal methods
    // ═══════════════════════════════════════════════════════════════════════

    public DialogResult Show()   => GlassMessage.CoreEx(_owner, BuildConfig()).Button;
    public GlassResult  ShowEx() => GlassMessage.CoreEx(_owner, BuildConfig());

    // ═══════════════════════════════════════════════════════════════════════
    // Config assembly
    // ═══════════════════════════════════════════════════════════════════════
    private GlassDialogConfig BuildConfig() => new GlassDialogConfig
    {
        Message            = _message,
        Title              = _title,
        Icon               = _icon,
        CustomIcon         = _customIcon,
        Buttons            = _buttons,
        DefaultButton      = _defaultButton,
        Theme              = _theme ?? GlassMessage.DefaultTheme ?? GlassTheme.Default,
        CustomLabels       = _customLabels,
        Animation          = _animation,
        AutoCloseMs        = _autoCloseMs,
        CheckBoxLabel      = _checkBoxLabel,
        CheckBoxDefault    = _checkBoxDefault,
        InputMode          = _inputMode,
        InputPlaceholder   = _inputPlaceholder,
        InputDropdownItems = _inputDropdownItems,
        InputDefault       = _inputDefault,
        DetailText         = _detailText,
        ShowProgress       = _showProgress,
        ProgressValue      = _progressValue,
        ProgressMax        = _progressMax,
        RightToLeft        = _rightToLeft,
        // null means "inherit from GlassMessage.UseRoundedCorners" at dialog construction time
        UseRoundedCorners  = _roundedCorners,
    };
}
