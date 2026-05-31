// -----------------------------------------------------------------------------
//  Glass.Message — the public entry point. These static overloads mirror the
//  shape of System.Windows.Forms.MessageBox so existing code can switch over by
//  changing nothing more than the type name, while ShowAsync/Create open the door
//  to the richer feature set.
//
//  File        : GlassMessage.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

/// <summary>
/// Drop-in modern replacement for <see cref="MessageBox"/>. The <c>Show</c>
/// overloads are signature-compatible with the framework's message box; use
/// <see cref="Create"/> for the fluent builder and <see cref="ShowAsync(string, string, MessageBoxIcon, MessageBoxButtons, CancellationToken)"/>
/// for non-blocking dialogs.
/// </summary>
public static class GlassMessage
{
    /// <summary>
    /// Theme applied when a caller doesn't specify one. Defaults to the built-in
    /// dark palette; assign <see cref="GlassTheme.AutoDetect"/> to follow Windows.
    /// </summary>
    public static GlassTheme DefaultTheme { get; set; } = GlassTheme.Default;

    /// <summary>
    /// Global default for rounded corners. Individual dialogs can override this
    /// through the builder; left off by default to match classic square chrome.
    /// </summary>
    public static bool UseRoundedCorners { get; set; } = false;

    // --- MessageBox-compatible synchronous overloads --------------------------
    // Each one simply fills in the defaults and forwards to Core(), so behaviour
    // stays identical no matter which overload a caller reaches for.

    public static DialogResult Show(string message)
        => Core(null, message, string.Empty, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string message, string title)
        => Core(null, message, title, MessageBoxIcon.None,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string message, string title, MessageBoxIcon icon)
        => Core(null, message, title, icon,
                MessageBoxButtons.OK, MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons)
        => Core(null, message, title, icon, buttons,
                MessageBoxDefaultButton.Button1, null);

    public static DialogResult Show(string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton)
        => Core(null, message, title, icon, buttons, defaultButton, null);

    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton)
        => Core(owner, message, title, icon, buttons, defaultButton, null);

    public static DialogResult Show(IWin32Window owner, string message, string title,
                                    MessageBoxIcon icon, MessageBoxButtons buttons,
                                    MessageBoxDefaultButton defaultButton, GlassTheme theme)
        => Core(owner, message, title, icon, buttons, defaultButton, theme);

    /// <summary>
    /// Shows the dialog without blocking the UI thread. The returned task completes
    /// with the chosen button once the dialog closes. The optional
    /// <paramref name="cancellationToken"/> closes the dialog (yielding
    /// <see cref="DialogResult.Cancel"/>) if it is cancelled while open.
    /// </summary>
    public static Task<DialogResult> ShowAsync(
        string message,
        string title = "",
        MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        CancellationToken cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, null, cancellationToken);

    /// <summary>
    /// Async overload that also accepts an explicit <paramref name="theme"/>.
    /// </summary>
    public static Task<DialogResult> ShowAsync(
        string message,
        string title,
        MessageBoxIcon icon,
        MessageBoxButtons buttons,
        GlassTheme theme,
        CancellationToken cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, theme, cancellationToken);

    /// <summary>
    /// Starts a fluent <see cref="GlassBuilder"/> for dialogs that need more than
    /// a message and buttons — input fields, checkboxes, progress, detail, etc.
    /// </summary>
    public static GlassBuilder Create(string message) => new(message);

    // Builds the config shared by the simple Show() overloads. Null arguments are
    // coalesced here so the dialog never has to defend against them, and the theme
    // falls back through DefaultTheme to the built-in default.
    private static GlassDialogConfig BasicConfig(
        string message, string title, MessageBoxIcon icon, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton, GlassTheme theme, string[] customLabels)
        => new()
        {
            Message = message ?? string.Empty,
            Title = title ?? string.Empty,
            Icon = icon,
            Buttons = buttons,
            DefaultButton = defaultButton,
            Theme = theme ?? DefaultTheme ?? GlassTheme.Default,
            CustomLabels = customLabels,
        };

    // Single place that actually constructs and shows a modal dialog. The dialog
    // is disposed via 'using' so its fonts/pens/timers are always released.
    internal static DialogResult Core(
        IWin32Window owner,
        string message,
        string title,
        MessageBoxIcon icon,
        MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton,
        GlassTheme theme,
        string[] customLabels = null)
    {
        using var dlg = new GlassDialog(
            BasicConfig(message, title, icon, buttons, defaultButton, theme, customLabels));
        return owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
    }

    // Extended modal path used by the builder: returns the full GlassResult so the
    // caller also gets the checkbox state and any typed input.
    internal static GlassResult CoreEx(IWin32Window owner, GlassDialogConfig config)
    {
        config.Theme ??= DefaultTheme ?? GlassTheme.Default;
        config.UseRoundedCorners ??= UseRoundedCorners;
        using var dlg = new GlassDialog(config);
        var result = owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
        return new GlassResult(result, dlg.CheckBoxChecked, dlg.InputText);
    }

    // Non-modal path backing ShowAsync. A TaskCompletionSource bridges the dialog's
    // FormClosed event to the awaiting caller; the dialog is shown modeless so the
    // message pump keeps running.
    private static Task<DialogResult> CoreAsync(
        IWin32Window owner,
        string message,
        string title,
        MessageBoxIcon icon,
        MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton,
        GlassTheme theme,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<DialogResult>(
                      TaskCreationOptions.RunContinuationsAsynchronously);
        var dlg = new GlassDialog(
            BasicConfig(message, title, icon, buttons, defaultButton, theme, null));

        // Cancellation marshals back onto the UI thread before touching the form,
        // since the token may be cancelled from any thread.
        CancellationTokenRegistration reg = default;
        if (ct.CanBeCanceled)
        {
            reg = ct.Register(() =>
            {
                if (!dlg.IsDisposed && dlg.IsHandleCreated)
                {
                    _ = dlg.BeginInvoke(new System.Action(() =>
                    {
                        if (!dlg.IsDisposed)
                        {
                            dlg.Close();
                        }
                    }));
                }
            });
        }

        dlg.FormClosed += (s, e) =>
        {
            reg.Dispose();
            // A dialog dismissed without a result (e.g. via cancellation) is
            // reported as Cancel for predictability.
            var r = dlg.DialogResult == DialogResult.None
                ? DialogResult.Cancel
                : dlg.DialogResult;
            _ = tcs.TrySetResult(r);
            dlg.Dispose();
        };

        if (owner != null)
        {
            dlg.Show(owner);
        }
        else
        {
            dlg.Show();
        }

        return tcs.Task;
    }
}
