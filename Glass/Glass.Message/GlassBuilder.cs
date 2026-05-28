using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassBuilder — fluent API.  Chain options then call .Show() or .ShowEx().
// ─────────────────────────────────────────────────────────────────────────

/// <summary>Fluent builder for <see cref="GlassMessage"/> dialogs.</summary>
/// <example><code>
/// var result = GlassMessage.Create("Delete 12 files?")
///     .Title("Confirm")
///     .Icon(MessageBoxIcon.Warning)
///     .Buttons("Delete", "Cancel")
///     .CheckBox("Don't ask again")
///     .AutoClose(10_000)
///     .ShowEx();
///
/// if (result.Button == DialogResult.OK &amp;&amp; result.CheckBoxChecked)
///     SuppressFutureWarnings();
/// </code></example>
public sealed class GlassBuilder
{
    private readonly string _message;

    // ── Core ──────────────────────────────────────────────────────────────
    private string                 _title         = string.Empty;
    private MessageBoxIcon         _icon          = MessageBoxIcon.None;
    private Bitmap                 _customIcon;
    private MessageBoxButtons      _buttons       = MessageBoxButtons.OK;
    private MessageBoxDefaultButton _defaultButton = MessageBoxDefaultButton.Button1;
    private GlassTheme             _theme;
    private IWin32Window           _owner;
    private string[]               _customLabels;

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

    // ═══════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════
    internal GlassBuilder(string message) => _message = message ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // Core chainable methods  (all return `this` for chaining)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Sets the window title.</summary>
    public GlassBuilder Title(string title)     { _title = title ?? string.Empty; return this; }

    /// <summary>Sets one of the standard system icons.</summary>
    public GlassBuilder Icon(MessageBoxIcon i)  { _icon = i; _customIcon = null; return this; }

    /// <summary>Sets a custom bitmap icon (overrides <see cref="Icon(MessageBoxIcon)"/>).</summary>
    public GlassBuilder Icon(Bitmap bitmap)     { _customIcon = bitmap; return this; }

    /// <summary>Sets the default focused button.</summary>
    public GlassBuilder Default(MessageBoxDefaultButton d) { _defaultButton = d; return this; }

    /// <summary>Sets the visual theme for this dialog only.</summary>
    public GlassBuilder Theme(GlassTheme t)    { _theme = t; return this; }

    /// <summary>Sets the owner window (for modality and multi-monitor centering).</summary>
    public GlassBuilder Owner(IWin32Window o)  { _owner = o; return this; }

    /// <summary>Sets the entrance/exit animation style.</summary>
    public GlassBuilder Animation(GlassAnimation a) { _animation = a; return this; }

    // ── Buttons ───────────────────────────────────────────────────────────

    /// <summary>Sets a standard button combination.</summary>
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
        _customLabels = labels;
        _buttons = labels.Length switch
        {
            1 => MessageBoxButtons.OK,
            2 => MessageBoxButtons.OKCancel,
            _ => MessageBoxButtons.YesNoCancel,
        };
        return this;
    }

    // ── Auto-close countdown ──────────────────────────────────────────────

    /// <summary>
    /// Auto-dismisses the dialog after <paramref name="milliseconds"/> ms,
    /// clicking the default button.  Shows a live countdown on the button label
    /// and a ring arc in the button panel.
    /// </summary>
    public GlassBuilder AutoClose(int milliseconds)
    {
        _autoCloseMs = milliseconds;
        return this;
    }

    // ── Checkbox ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a "Don't show again" checkbox (or any label you choose).
    /// Read the result via <see cref="GlassResult.CheckBoxChecked"/> from <see cref="ShowEx"/>.
    /// </summary>
    public GlassBuilder CheckBox(string label, bool defaultChecked = false)
    {
        _checkBoxLabel   = label;
        _checkBoxDefault = defaultChecked;
        return this;
    }

    // ── Inline input ──────────────────────────────────────────────────────

    /// <summary>Adds a single-line text input field.</summary>
    public GlassBuilder InputText(string placeholder = "", string defaultValue = "")
    {
        _inputMode        = GlassInputMode.Text;
        _inputPlaceholder = placeholder;
        _inputDefault     = defaultValue;
        return this;
    }

    /// <summary>Adds a password field (characters are masked).</summary>
    public GlassBuilder InputPassword(string placeholder = "")
    {
        _inputMode        = GlassInputMode.Password;
        _inputPlaceholder = placeholder;
        return this;
    }

    /// <summary>Adds a multi-line text area.</summary>
    public GlassBuilder InputMultiline(string placeholder = "", string defaultValue = "")
    {
        _inputMode        = GlassInputMode.Multiline;
        _inputPlaceholder = placeholder;
        _inputDefault     = defaultValue;
        return this;
    }

    /// <summary>Adds a drop-down combo box with <paramref name="items"/>.</summary>
    public GlassBuilder InputDropdown(IEnumerable<string> items, string defaultItem = null)
    {
        _inputMode          = GlassInputMode.Dropdown;
        _inputDropdownItems = [.. items];
        _inputDefault       = defaultItem;
        return this;
    }

    // ── Expandable detail ─────────────────────────────────────────────────

    /// <summary>
    /// Adds a collapsible "Show details ▼" section containing <paramref name="detail"/>.
    /// Ideal for stack traces, verbose logs, or extended diagnostics.
    /// </summary>
    public GlassBuilder Detail(string detail)
    {
        _detailText = detail;
        return this;
    }

    // ── Progress bar ──────────────────────────────────────────────────────

    /// <summary>Adds an indeterminate (marquee) progress bar.</summary>
    public GlassBuilder ProgressIndeterminate()
    {
        _showProgress  = true;
        _progressValue = -1;
        return this;
    }

    /// <summary>
    /// Adds a determinate progress bar.
    /// <paramref name="value"/> must be between 0 and <paramref name="max"/>.
    /// </summary>
    public GlassBuilder Progress(int value, int max = 100)
    {
        _showProgress  = true;
        _progressValue = value;
        _progressMax   = max;
        return this;
    }

    // ── Layout ────────────────────────────────────────────────────────────

    /// <summary>Mirrors the dialog layout for right-to-left languages (Arabic, Hebrew, etc.).</summary>
    public GlassBuilder RightToLeft(bool enable = true)
    {
        _rightToLeft = enable;
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Show methods
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows the dialog and returns the standard <see cref="DialogResult"/>.
    /// Use <see cref="ShowEx"/> when you also need checkbox or input results.
    /// </summary>
    public DialogResult Show() => GlassMessage.CoreEx(_owner, BuildConfig()).Button;

    /// <summary>
    /// Shows the dialog and returns a rich <see cref="GlassResult"/> that includes
    /// the button pressed, checkbox state, and any input text.
    /// </summary>
    public GlassResult ShowEx() => GlassMessage.CoreEx(_owner, BuildConfig());

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
    };
}
