using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Glass;

public static class GlassMessage
{

    public static GlassTheme DefaultTheme { get; set; } = GlassTheme.Default;

    public static bool UseRoundedCorners { get; set; } = false;


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


    public static Task<DialogResult> ShowAsync(
        string            message,
        string            title             = "",
        MessageBoxIcon    icon              = MessageBoxIcon.None,
        MessageBoxButtons buttons           = MessageBoxButtons.OK,
        CancellationToken cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, null, cancellationToken);

    public static Task<DialogResult> ShowAsync(
        string            message,
        string            title,
        MessageBoxIcon    icon,
        MessageBoxButtons buttons,
        GlassTheme        theme,
        CancellationToken cancellationToken = default)
        => CoreAsync(null, message, title, icon, buttons,
                     MessageBoxDefaultButton.Button1, theme, cancellationToken);


    public static GlassBuilder Create(string message) => new GlassBuilder(message);


    private static GlassDialogConfig BasicConfig(
        string message, string title, MessageBoxIcon icon, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton, GlassTheme theme, string[] customLabels)
        => new GlassDialogConfig
        {
            Message       = message ?? string.Empty,
            Title         = title   ?? string.Empty,
            Icon          = icon,
            Buttons       = buttons,
            DefaultButton = defaultButton,
            Theme         = theme ?? DefaultTheme ?? GlassTheme.Default,
            CustomLabels  = customLabels,
        };

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
        using var dlg = new GlassDialog(
            BasicConfig(message, title, icon, buttons, defaultButton, theme, customLabels));
        return owner == null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
    }

    internal static GlassResult CoreEx(IWin32Window owner, GlassDialogConfig config)
    {
        config.Theme ??= DefaultTheme ?? GlassTheme.Default;
        config.UseRoundedCorners ??= UseRoundedCorners;
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
        var tcs = new TaskCompletionSource<DialogResult>(
                      TaskCreationOptions.RunContinuationsAsynchronously);
        var dlg = new GlassDialog(
            BasicConfig(message, title, icon, buttons, defaultButton, theme, null));

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

        if (owner != null) dlg.Show(owner);
        else               dlg.Show();

        return tcs.Task;
    }
}
