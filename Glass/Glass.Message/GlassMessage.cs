using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassMessage — static façade. Drop-in replacement for MessageBox.
// ─────────────────────────────────────────────────────────────────────────
public static class GlassMessage
{
    public static DialogResult Show(string message)
        => Core(null, message, string.Empty, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, GlassTheme.Default);

    public static DialogResult Show(string message, string title)
        => Core(null, message, title, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, GlassTheme.Default);

    public static DialogResult Show(string message, string title, MessageBoxIcon icon)
        => Core(null, message, title, icon,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, GlassTheme.Default);

    public static DialogResult Show(string message, string title, MessageBoxIcon icon,
                                    MessageBoxButtons buttons)
        => Core(null, message, title, icon, buttons,
                MessageBoxDefaultButton.Button1, GlassTheme.Default);

    public static DialogResult Show(string message, string title, MessageBoxIcon icon,
                                    MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton)
        => Core(null, message, title, icon, buttons, defaultButton, GlassTheme.Default);

    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton)
        => Core(owner, message, title, icon, buttons, defaultButton, GlassTheme.Default);

    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton, GlassTheme theme)
        => Core(owner, message, title, icon, buttons, defaultButton, theme);

    // Fluent entry point
    public static GlassBuilder Create(string message) => new(message);

    internal static DialogResult Core(IWin32Window owner, string message, string title,
                                      MessageBoxIcon icon, MessageBoxButtons buttons,
                                      MessageBoxDefaultButton defaultButton, GlassTheme theme,
                                      string[] customLabels = null)
    {
        using var dlg = new GlassDialog(
            message ?? string.Empty, title ?? string.Empty,
            icon, buttons, defaultButton, theme ?? GlassTheme.Default, customLabels);
        return owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
    }
}
