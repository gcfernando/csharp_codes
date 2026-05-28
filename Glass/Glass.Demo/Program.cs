using System;
using System.Drawing;
using System.Windows.Forms;
using Glass;

namespace Glass.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new DemoForm());
    }
}

internal sealed class DemoForm : Form
{
    public DemoForm()
    {
        Text            = "Glass.Message v2 — Feature Gallery";
        ClientSize      = new Size(600, 700);
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = Color.FromArgb(18, 26, 46);
        ForeColor       = Color.FromArgb(200, 215, 240);
        Font            = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        BuildUI();
    }

    private void BuildUI()
    {
        var demos = new (string Label, Action Action)[]
        {
            ("Drop-in Replace (Show)",                    Demo_Basic),
            ("Dark / Light / Mica / HC / Classic Themes", Demo_Themes),
            ("Auto-detect OS Theme",                      Demo_AutoTheme),
            ("Fluent Builder API",                        Demo_Builder),
            ("Countdown Auto-Close  (5 s)",               Demo_Countdown),
            ("\"Don't Show Again\" Checkbox",             Demo_CheckBox),
            ("Inline Text Input",                         Demo_Input),
            ("Password Input",                            Demo_Password),
            ("Drop-down Input",                           Demo_Dropdown),
            ("Expandable Detail Section",                 Demo_Detail),
            ("Determinate Progress Bar",                  Demo_Progress),
            ("Indeterminate Progress Bar",                Demo_ProgressMarquee),
            ("Custom Bitmap Icon",                        Demo_CustomIcon),
            ("Ctrl+C  Copy to Clipboard",                 Demo_Copy),
            ("SlideDown Animation",                       Demo_Slide),
            ("RTL (Right-to-Left) Layout",                Demo_RTL),
            ("Toast — Bottom-Right",                      Demo_Toast),
            ("Toast — Stacking",                          Demo_ToastStack),
            ("Async ShowAsync",                           Demo_Async),
            ("All Buttons + ShowEx Rich Result",          Demo_ShowEx),
        };

        var grid = new TableLayoutPanel
        {
            Dock            = DockStyle.Fill,
            ColumnCount     = 2,
            RowCount        = 10,
            Padding         = new Padding(14, 10, 14, 14),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            BackColor       = Color.Transparent,
        };
        _ = grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _ = grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        for (var r = 0; r < 10; r++)
            _ = grid.RowStyles.Add(new RowStyle(SizeType.Percent, 10f));

        for (var i = 0; i < demos.Length; i++)
        {
            var col      = i < 10 ? 0 : 1;
            var row      = i < 10 ? i : i - 10;
            var captured = demos[i].Action;
            var btn = new Button
            {
                Text      = demos[i].Label,
                Dock      = DockStyle.Fill,
                Margin    = col == 0 ? new Padding(0, 0, 6, 5) : new Padding(6, 0, 0, 5),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(210, 230, 255),
                BackColor = Color.FromArgb(22, 38, 68),
                Cursor    = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(56, 100, 180);
            btn.Click += (s, e) => captured();
            grid.Controls.Add(btn, col, row);
        }
        Controls.Add(grid);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Demos — content is SHORT / MEDIUM / LONG, deliberately mixed.
    // ═══════════════════════════════════════════════════════════════════════

    // ── SHORT (1–2 lines) ─────────────────────────────────────────────────

    private static void Demo_Basic()
        => GlassMessage.Show(
            "The selected printer 'HP LaserJet Pro M404dn' is offline.",
            "Printer Offline",
            MessageBoxIcon.Warning);

    private static void Demo_AutoTheme()
    {
        var theme    = GlassTheme.AutoDetect();
        var modeName = GlassTheme.IsSystemDark() ? "Dark" : "Light";
        _ = GlassMessage.Create($"Windows is in {modeName} mode. Theme matched automatically.")
            .Title("System Theme Detected")
            .Icon(MessageBoxIcon.Information)
            .Theme(theme)
            .Show();
    }

    private static void Demo_Input()
    {
        var r = GlassMessage.Create("Enter a new name for the selected folder:")
            .Title("Rename Folder")
            .Icon(MessageBoxIcon.Question)
            .InputText("Folder name", "Client Projects — Q1 2025")
            .Buttons("Rename", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK && !string.IsNullOrWhiteSpace(r.InputText))
            _ = GlassMessage.Show(
                $"Renamed to \"{r.InputText}\" successfully.",
                "Rename Complete", MessageBoxIcon.Information);
    }

    private static void Demo_ProgressMarquee()
        => GlassMessage.Create("Verifying your Office 2024 licence with Microsoft servers…")
            .Title("Activating Microsoft Office")
            .Icon(MessageBoxIcon.Information)
            .ProgressIndeterminate()
            .Buttons("Cancel")
            .Show();

    private static void Demo_CustomIcon()
    {
        using var bmp = new Bitmap(48, 48);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(System.Drawing.Brushes.DodgerBlue, 2, 2, 44, 44);
            g.DrawString("G", new Font("Segoe UI", 26f, FontStyle.Bold),
                System.Drawing.Brushes.White, new PointF(10, 6));
        }
        _ = GlassMessage.Create("Glass.Message v2.0 is active. Custom branding applied.")
            .Title("Glass.Message — Ready")
            .Icon(bmp)
            .Buttons(MessageBoxButtons.OK)
            .Show();
    }

    private static void Demo_Slide()
        => GlassMessage.Create(
                "Glass.Message 2.1 is available.\n" +
                "Adds Windows 11 24H2 support and fixes 3 reported bugs.")
            .Title("Update Available")
            .Icon(MessageBoxIcon.Information)
            .Buttons("Install Now", "Later")
            .Animation(GlassAnimation.SlideDown)
            .Show();

    // ── MEDIUM (3–5 lines) ────────────────────────────────────────────────

    private static void Demo_Themes()
    {
        // Each theme card: SHORT message that suits the theme's personality
        _ = GlassMessage.Create(
                "Night mode active.\nBlue light filter: 85%  ·  Colour temp: 3,200 K")
            .Title("Display Profile — Dark")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.Dark).Show();

        _ = GlassMessage.Create(
                "Light mode active.\nColour temperature: 6,500 K  ·  Calibrated for sRGB")
            .Title("Display Profile — Light")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.Light).Show();

        _ = GlassMessage.Create(
                "Mica backdrop applied.\n" +
                "Desktop wallpaper is sampled every 30 s to keep\n" +
                "the translucent tint in sync with your background.")
            .Title("Mica Backdrop Active")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.Mica).Show();

        _ = GlassMessage.Create(
                "High Contrast #2 active.\n" +
                "Contrast ratio: 7.4 : 1  ·  Meets WCAG 2.1 Level AAA\n" +
                "All colours sourced from Windows system settings.")
            .Title("Accessibility — High Contrast")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.HighContrast).Show();

        _ = GlassMessage.Create("Classic rendering active. Hardware acceleration disabled.")
            .Title("Windows Classic Mode")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.WindowsClassic).Show();
    }

    private static void Demo_Builder()
        => GlassMessage.Create(
                "Annual_Report_Q4_2025.xlsx has unsaved changes.\n\n" +
                "Last auto-save: 18 minutes ago  ·  File size: 3.7 MB\n" +
                "Closing now will discard all edits since the last save.")
            .Title("Unsaved Changes")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Save", "Don't Save", "Cancel")
            .Default(MessageBoxDefaultButton.Button1)
            .Show();

    private static void Demo_Password()
    {
        var r = GlassMessage.Create(
                "Authentication required to connect to ContosoERP.\n\n" +
                "Server: sql-prod-01.contoso.com\n" +
                "Domain: CONTOSO  ·  Auth: Windows Integrated")
            .Title("Sign In — Contoso ERP")
            .Icon(MessageBoxIcon.Warning)
            .InputPassword("Active Directory password")
            .Buttons("Connect", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK)
            _ = GlassMessage.Show(
                $"Connected to sql-prod-01.contoso.com\n" +
                $"Credential length: {r.InputText.Length} chars  ·  Session valid for 8 hours",
                "Signed In", MessageBoxIcon.Information);
    }

    private static void Demo_Dropdown()
    {
        var r = GlassMessage.Create(
                "Choose the output format for 'Annual_Report_Q4_2025.pdf'.\n\n" +
                "47 pages  ·  3.7 MB  ·  Destination: Downloads folder")
            .Title("Export Document")
            .Icon(MessageBoxIcon.Question)
            .InputDropdown(
                ["PDF — Portable Document Format",
                 "Word (.docx) — Microsoft Word",
                 "Excel (.xlsx) — Microsoft Excel",
                 "CSV — Comma-Separated Values",
                 "HTML — Web Page",
                 "Plain Text (.txt)"],
                "PDF — Portable Document Format")
            .Buttons("Export", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK)
            _ = GlassMessage.Show(
                $"Exporting as {r.InputText}\n" +
                "Saved to: C:\\Users\\Gehan\\Downloads\\Annual_Report_Q4_2025",
                "Export Complete", MessageBoxIcon.Information);
    }

    private static void Demo_Progress()
        => GlassMessage.Create(
                "Backing up 'Documents' to OneDrive…\n\n" +
                "3,847 of 5,120 files transferred  ·  1.4 GB of 2.1 GB\n" +
                "Estimated time remaining: 4 minutes  ·  Speed: 6.2 MB/s")
            .Title("OneDrive Backup in Progress")
            .Icon(MessageBoxIcon.Information)
            .Progress(75, 100)
            .Buttons("Cancel Backup")
            .Show();

    private static void Demo_RTL()
        => GlassMessage.Create(
                "فشل حفظ الملف: تقرير_الربع_الرابع.docx\n\n" +
                "القرص الصلب ممتلئ. المساحة المتاحة: ٠ بايت.\n" +
                "يُرجى تحرير مساحة وإعادة المحاولة.")
            .Title("فشل الحفظ — القرص ممتلئ")
            .Icon(MessageBoxIcon.Error)
            .Buttons(MessageBoxButtons.RetryCancel)
            .RightToLeft()
            .Show();

    // ── LONG (6 + lines, rich context) ────────────────────────────────────

    private static void Demo_Countdown()
        => GlassMessage.Create(
                "Your Contoso Portal session is about to expire.\n\n" +
                "Security policy (POL-2024-AUTH-07) requires automatic sign-out\n" +
                "after 15 minutes of inactivity. Any unsaved form data will be lost.\n\n" +
                "Active connections that will be interrupted:\n" +
                "  • ContosoERP — 3 unsaved purchase order records\n" +
                "  • SharePoint  — 'Q4 Budget.xlsx' is checked out\n" +
                "  • Teams call  — 1 participant waiting in lobby\n\n" +
                "Click 'Stay Signed In' to extend your session by 30 minutes.")
            .Title("Session Expiring — Contoso Portal")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Stay Signed In", "Sign Out Now")
            .AutoClose(5_000)
            .Animation(GlassAnimation.SlideDown)
            .Show();

    private static void Demo_CheckBox()
    {
        var r = GlassMessage.Create(
                "Drive C: has only 4.2 GB of free space remaining (of 512 GB total).\n\n" +
                "Windows Update requires at least 8 GB free to install the\n" +
                "KB5040442 security patch (June 2026 Patch Tuesday, Critical).\n\n" +
                "Largest items consuming disk space:\n" +
                "  • C:\\Windows\\Temp                       18.4 GB\n" +
                "  • C:\\Users\\Gehan\\Downloads               11.2 GB\n" +
                "  • Hibernation file  (hiberfil.sys)        8.0 GB\n" +
                "  • WinSxS component store                  6.3 GB\n\n" +
                "Open Disk Cleanup to recover space immediately.")
            .Title("Low Disk Space — C:\\")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Open Disk Cleanup", "Dismiss")
            .CheckBox("Don't warn me again for drive C:\\")
            .ShowEx();

        if (r.CheckBoxChecked)
            _ = GlassMessage.Show(
                "Disk space warnings for C:\\ have been suppressed.\n" +
                "Re-enable in Settings → System → Storage → Notifications.",
                "Warning Suppressed", MessageBoxIcon.Information);
    }

    private static void Demo_Detail()
        => GlassMessage.Create(
                "OneDrive failed to sync 'Annual_Report_Q4_2025.xlsx'.\n\n" +
                "The file is locked by Microsoft Excel. Close all workbooks\n" +
                "that reference this file, then click Retry.")
            .Title("Sync Error — OneDrive")
            .Icon(MessageBoxIcon.Error)
            .Detail(
                "System.IO.IOException: The process cannot access the file\n" +
                "'C:\\Users\\Gehan\\OneDrive\\Finance\\Annual_Report_Q4_2025.xlsx'\n" +
                "because it is being used by another process.\n\n" +
                "   at System.IO.FileStream.ValidateFileHandle(SafeFileHandle handle)\n" +
                "   at System.IO.FileStream.Init(String path, FileMode mode, FileAccess access)\n" +
                "   at Microsoft.OneDrive.Sync.Engine.FileUploader.OpenForRead(String path)\n" +
                "   at Microsoft.OneDrive.Sync.Engine.FileUploader.UploadChunked(SyncItem item)\n" +
                "   at Microsoft.OneDrive.Sync.Engine.SyncWorker.ProcessQueue() line 284\n\n" +
                "Win32 error:     0x80070020 — ERROR_SHARING_VIOLATION\n" +
                "OneDrive build:  24.086.0428.0001\n" +
                "Correlation ID:  a3f8b2e1-4d9c-47f2-8b1a-c6e5d0f92341\n" +
                "Timestamp:       2026-05-28  09:52:14 UTC")
            .Buttons(MessageBoxButtons.RetryCancel)
            .Show();

    private static void Demo_Copy()
        => GlassMessage.Create(
                "Connection to the database server failed.\n\n" +
                "Server      SQL-PROD-01\\SQLEXPRESS\n" +
                "Database    ContosoERP_Production\n" +
                "User        CONTOSO\\svc_erp\n" +
                "Auth        Windows Integrated Security\n" +
                "Error       08001 — SQL Server does not exist or access denied\n" +
                "Timeout     30 s  (0 of 3 retries attempted)\n" +
                "Timestamp   2026-05-28  09:52:14 UTC\n" +
                "Machine     DESKTOP-G7K4P21  (10.0.1.55)\n\n" +
                "Press Ctrl+C to copy this message for the support desk.")
            .Title("Database Connection Failed")
            .Icon(MessageBoxIcon.Error)
            .Buttons(MessageBoxButtons.RetryCancel)
            .Show();

    private static async void Demo_Async()
    {
        var r = await GlassMessage.ShowAsync(
            "Your local workspace has uncommitted changes and is out of sync\n" +
            "with the remote repository.\n\n" +
            "Pending changes:\n" +
            "  • GlassDialog.cs    +247 / −83 lines  (modified)\n" +
            "  • GlassTheme.cs      +12 / −4 lines   (modified)\n" +
            "  • GlassToast.cs       +5 / −2 lines   (modified)\n" +
            "  • CHANGELOG.md        new file\n\n" +
            "Push now to back up to Contoso DevOps (origin/main).\n" +
            "Next scheduled auto-push: Today at 6:00 PM",
            "Sync Workspace — Contoso DevOps",
            MessageBoxIcon.Question,
            MessageBoxButtons.OKCancel);

        _ = GlassMessage.Show(
            r == DialogResult.OK
                ? "Push started.\nWorkspace will be up to date in a few seconds."
                : "Push skipped. Auto-push will run at 6:00 PM.",
            r == DialogResult.OK ? "Pushing to origin/main…" : "Sync Deferred",
            r == DialogResult.OK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static void Demo_ShowEx()
    {
        var r = GlassMessage.Create(
                "Adobe Acrobat Reader DC (v24.3.21, 698 MB) will be permanently removed\n" +
                "from this computer.\n\n" +
                "The following will be deleted:\n" +
                "  • All program files under C:\\Program Files\\Adobe\n" +
                "  • Desktop and Start Menu shortcuts\n" +
                "  • Browser PDF plug-ins  (Chrome, Edge, Firefox)\n" +
                "  • Cached files in AppData\\Local\\Adobe\n\n" +
                "Your PDF files in Documents and Downloads are NOT affected.\n" +
                "This action cannot be undone.")
            .Title("Uninstall Adobe Acrobat Reader DC")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Uninstall", "Repair", "Cancel")
            .CheckBox("Also remove personal settings and reading history")
            .ShowEx();

        if (r.Button != DialogResult.Cancel)
            _ = GlassMessage.Show(
                $"Action:              {(r.Button == DialogResult.OK ? "Uninstall" : "Repair")}\n" +
                $"Remove preferences:  {(r.CheckBoxChecked ? "Yes" : "No")}\n\n" +
                "The operation would begin here in a real application.",
                "Confirmed", MessageBoxIcon.Information);
    }

    // ── Toast demos (always short — toasts are glanceable) ────────────────

    private static void Demo_Toast()
    {
        GlassToast.Show(new GlassToastOptions
        {
            Message    = "Invoice_March_2026.pdf saved to SharePoint · Finance",
            Title      = "Upload Complete",
            Icon       = MessageBoxIcon.Information,
            DurationMs = 4_000,
            Position   = ToastPosition.BottomRight,
        });
    }

    private static void Demo_ToastStack()
    {
        GlassToast.Show("Build succeeded — 0 errors, 2 warnings",
            "Glass.Message", MessageBoxIcon.Information);
        GlassToast.Show("22 / 22 tests passed · Coverage 91.4%",
            "Test Run Complete", MessageBoxIcon.Information);
        GlassToast.Show("Deploying to staging-01.contoso.com…",
            "CI/CD Pipeline", MessageBoxIcon.Warning);
    }
}
