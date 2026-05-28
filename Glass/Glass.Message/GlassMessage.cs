using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassMessage — static façade.  Drop-in replacement for MessageBox.
// All original overloads are preserved; new capabilities are additive.
// ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// Modern, animated, themed replacement for <see cref="MessageBox"/>.
/// All existing <c>MessageBox.Show(…)</c> call sites compile unchanged.
/// </summary>
public static class GlassMessage
{
    // ── Global default theme ──────────────────────────────────────────────

    /// <summary>
    /// Application-wide default theme applied when no theme is specified in a
    /// <c>Show</c> call or on a <see cref="GlassBuilder"/>.
    /// Defaults to <see cref="GlassTheme.Default"/> (deep midnight-blue dark).
    /// </summary>
    public static GlassTheme DefaultTheme { get; set; } = GlassTheme.Default;

    // ══════════════════════════════════════════════════════════════════════
    // Classic overloads — identical signatures to MessageBox.Show(…)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Shows a dialog with <paramref name="message"/> and an OK button.</summary>
    public static DialogResult Show(string message)
        => Core(null, message, string.Empty, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    /// <summary>Shows a dialog with a <paramref name="title"/> and an OK button.</summary>
    public static DialogResult Show(string message, string title)
        => Core(null, message, title, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    /// <summary>Shows a dialog with an icon and an OK button.</summary>
    public static DialogResult Show(string message, string title, MessageBoxIcon icon)
        => Core(null, message, title, icon,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    /// <summary>Shows a dialog with an icon and a custom button set.</summary>
    public static DialogResult Show(string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons)
        => Core(null, message, title, icon, buttons,
                MessageBoxDefaultButton.Button1, null);

    /// <summary>Shows a dialog with full classic parameters.</summary>
    public static DialogResult Show(string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton)
        => Core(null, message, title, icon, buttons, defaultButton, null);

    /// <summary>Shows a dialog owned by <paramref name="owner"/>.</summary>
    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton)
        => Core(owner, message, title, icon, buttons, defaultButton, null);

    /// <summary>Shows a dialog with an explicit <paramref name="theme"/>.</summary>
    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton, GlassTheme theme)
        => Core(owner, message, title, icon, buttons, defaultButton, theme);

    // ══════════════════════════════════════════════════════════════════════
    // Async overloads — non-blocking; awaitable from async methods
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a non-blocking dialog and returns a <see cref="Task{DialogResult}"/>
    /// that completes when the user closes it.
    /// </summary>
    /// <param name="message">Message body.</param>
    /// <param name="title">Window title.</param>
    /// <param name="icon">Icon style.</param>
    /// <param name="buttons">Button set.</param>
    /// <param name="cancellationToken">
    /// When cancelled the dialog is dismissed and the task resolves to <see cref="DialogResult.Cancel"/>.
    /// </param>
    public static Task<DialogResult> ShowAsync(
        string                message,
        string                title             = "",
        MessageBoxIcon        icon              = MessageBoxIcon.None,
        MessageBoxButtons     buttons           = MessageBoxButtons.OK,
        CancellationToken     cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, null, cancellationToken);

    /// <summary>
    /// Shows a non-blocking dialog with an explicit theme and returns a
    /// <see cref="Task{DialogResult}"/> that completes when the user closes it.
    /// </summary>
    public static Task<DialogResult> ShowAsync(
        string                message,
        string                title,
        MessageBoxIcon        icon,
        MessageBoxButtons     buttons,
        GlassTheme            theme,
        CancellationToken     cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, theme, cancellationToken);

    // ══════════════════════════════════════════════════════════════════════
    // Fluent entry point
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a <see cref="GlassBuilder"/> for fluent configuration.
    /// <code>GlassMessage.Create("Saved!").Title("Success").Icon(MessageBoxIcon.Information).Show();</code>
    /// </summary>
    public static GlassBuilder Create(string message) => new GlassBuilder(message);

    // ══════════════════════════════════════════════════════════════════════
    // Internal core methods
    // ══════════════════════════════════════════════════════════════════════

    internal static DialogResult Core(
        IWin32Window           owner,
        string                 message,
        string                 title,
        MessageBoxIcon         icon,
        MessageBoxButtons      buttons,
        MessageBoxDefaultButton defaultButton,
        GlassTheme             theme,
        string[]               customLabels = null)
    {
        var config = new GlassDialogConfig
        {
            Message      = message      ?? string.Empty,
            Title        = title        ?? string.Empty,
            Icon         = icon,
            Buttons      = buttons,
            DefaultButton = defaultButton,
            Theme        = theme ?? DefaultTheme ?? GlassTheme.Default,
            CustomLabels = customLabels,
        };
        using var dlg = new GlassDialog(config);
        return owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
    }

    internal static GlassResult CoreEx(IWin32Window owner, GlassDialogConfig config)
    {
        config.Theme ??= DefaultTheme ?? GlassTheme.Default;
        using var dlg = new GlassDialog(config);
        var result    = owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
        return new GlassResult(result, dlg.CheckBoxChecked, dlg.InputText);
    }

    private static Task<DialogResult> CoreAsync(
        IWin32Window           owner,
        string                 message,
        string                 title,
        MessageBoxIcon         icon,
        MessageBoxButtons      buttons,
        MessageBoxDefaultButton defaultButton,
        GlassTheme             theme,
        CancellationToken      ct)
    {
        var tcs    = new TaskCompletionSource<DialogResult>(
                         TaskCreationOptions.RunContinuationsAsynchronously);
        var config = new GlassDialogConfig
        {
            Message       = message      ?? string.Empty,
            Title         = title        ?? string.Empty,
            Icon          = icon,
            Buttons       = buttons,
            DefaultButton = defaultButton,
            Theme         = theme ?? DefaultTheme ?? GlassTheme.Default,
        };

        var dlg = new GlassDialog(config);

        CancellationTokenRegistration reg = default;
        if (ct.CanBeCanceled)
        {
            reg = ct.Register(() =>
            {
                if (!dlg.IsDisposed && dlg.IsHandleCreated)
                    dlg.BeginInvoke(new System.Action(() =>
                    {
                        if (!dlg.IsDisposed) dlg.Close();
                    }));
            });
        }

        dlg.FormClosed += (s, e) =>
        {
            reg.Dispose();
            var r = dlg.DialogResult == DialogResult.None
                ? DialogResult.Cancel
                : dlg.DialogResult;
            tcs.TrySetResult(r);
            dlg.Dispose();
        };

        if (owner != null)
            dlg.Show(owner);
        else
            dlg.Show();

        return tcs.Task;
    }
}
