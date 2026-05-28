using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassBuilder — fluent API. Chain options then call .Show().
// ─────────────────────────────────────────────────────────────────────────
public sealed class GlassBuilder
{
    private readonly string _message;
    private string _title = string.Empty;
    private MessageBoxIcon _icon = MessageBoxIcon.None;
    private MessageBoxButtons _buttons = MessageBoxButtons.OK;
    private MessageBoxDefaultButton _defaultButton = MessageBoxDefaultButton.Button1;
    private GlassTheme _theme = GlassTheme.Default;
    private IWin32Window _owner;
    private string[] _customLabels;

    internal GlassBuilder(string message) => _message = message ?? string.Empty;

    public GlassBuilder Title(string title) { _title = title ?? string.Empty; return this; }
    public GlassBuilder Icon(MessageBoxIcon i) { _icon = i; return this; }
    public GlassBuilder Default(MessageBoxDefaultButton d) { _defaultButton = d; return this; }
    public GlassBuilder Theme(GlassTheme t) { _theme = t ?? GlassTheme.Default; return this; }
    public GlassBuilder Owner(IWin32Window o) { _owner = o; return this; }

    public GlassBuilder Buttons(MessageBoxButtons buttons)
    {
        _buttons = buttons;
        _customLabels = null;
        return this;
    }

    // Custom labels: 1 label → OK, 2 → OKCancel, 3+ → YesNoCancel.
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

    public DialogResult Show()
        => GlassMessage.Core(_owner, _message, _title, _icon,
                                _buttons, _defaultButton, _theme, _customLabels);
}
