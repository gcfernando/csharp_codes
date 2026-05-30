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

        // Showcase the modern look: native Windows 11 rounded corners + an OS-matched theme.
        GlassMessage.UseRoundedCorners = true;
        GlassMessage.DefaultTheme      = GlassTheme.AutoDetect();

        Application.Run(new DemoForm());
    }
}

internal sealed class DemoForm : Form
{
    public DemoForm()
    {
        Text            = "Glass.Message v2.1 — Feature Gallery";
        ClientSize      = new Size(680, 760);
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
            ("Countdown Auto-Close  (10 s)",              Demo_Countdown),
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
            ("Scale Animation",                           Demo_Scale),
            ("RTL (Right-to-Left) Layout",                Demo_RTL),
            ("Security Alert — Sign-in",                  Demo_SecurityAlert),
            ("Windows Update — Release Notes",            Demo_ReleaseNotes),
            ("End-User Licence Agreement",               Demo_Eula),
            ("Storage Migration Wizard",                  Demo_Migration),
            ("Toast — Bottom-Right",                      Demo_Toast),
            ("Toast — Stacking",                          Demo_ToastStack),
            ("Toast — Four Corners",                      Demo_ToastCorners),
            ("Async ShowAsync",                           Demo_Async),
            ("All Buttons + ShowEx Rich Result",          Demo_ShowEx),
        };

        const int cols = 2;
        var rows = (demos.Length + cols - 1) / cols;

        var grid = new TableLayoutPanel
        {
            Dock            = DockStyle.Fill,
            ColumnCount     = cols,
            RowCount        = rows,
            Padding         = new Padding(14, 10, 14, 14),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            BackColor       = Color.Transparent,
        };
        for (var c = 0; c < cols; c++)
            _ = grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
        for (var r = 0; r < rows; r++)
            _ = grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

        for (var i = 0; i < demos.Length; i++)
        {
            // Column-major fill: first half goes down the left column, second half down the right.
            var col      = i / rows;
            var row      = i % rows;
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
                Padding   = new Padding(8, 0, 0, 0),
            };
            btn.FlatAppearance.BorderColor       = Color.FromArgb(56, 100, 180);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 52, 92);
            btn.Click += (s, e) => captured();
            grid.Controls.Add(btn, col, row);
        }
        Controls.Add(grid);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Demos — content is SHORT / MEDIUM / LONG, deliberately mixed and realistic.
    // ═══════════════════════════════════════════════════════════════════════

    // ── SHORT (1–2 lines) ─────────────────────────────────────────────────

    private static void Demo_Basic()
        => GlassMessage.Show(
            "The selected printer 'HP LaserJet Pro M404dn' is offline.\n" +
            "Check that it is powered on and connected to the network, then try again.",
            "Printer Offline",
            MessageBoxIcon.Warning);

    private static void Demo_AutoTheme()
    {
        var theme    = GlassTheme.AutoDetect();
        var modeName = GlassTheme.IsSystemDark() ? "Dark" : "Light";
        _ = GlassMessage.Create(
                $"Windows is currently in {modeName} mode, so Glass.Message selected its " +
                $"{modeName.ToLower()} palette automatically.\n\n" +
                "Switch your system theme in Settings → Personalisation → Colours and the next " +
                "dialog will follow it — no code changes required.")
            .Title("System Theme Detected")
            .Icon(MessageBoxIcon.Information)
            .Theme(theme)
            .Show();
    }

    private static void Demo_Input()
    {
        var r = GlassMessage.Create(
                "Enter a new name for the selected folder.\n\n" +
                "Names may contain letters, numbers, spaces, and the characters ( ) – _ , " +
                "but not \\ / : * ? \" < > |.")
            .Title("Rename Folder")
            .Icon(MessageBoxIcon.Question)
            .InputText("Folder name", "Client Projects — Q1 2026")
            .Buttons("Rename", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK && !string.IsNullOrWhiteSpace(r.InputText))
            _ = GlassMessage.Show(
                $"Renamed to \"{r.InputText}\" successfully.",
                "Rename Complete", MessageBoxIcon.Information);
    }

    private static void Demo_ProgressMarquee()
        => GlassMessage.Create(
                "Verifying your Microsoft 365 licence with activation servers…\n\n" +
                "This usually takes a few seconds. You can keep working while it completes.")
            .Title("Activating Microsoft 365")
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
        _ = GlassMessage.Create(
                "Glass.Message v2.1 is active and your custom branding has been applied.\n\n" +
                "Any 48×48 bitmap can be supplied as a dialog icon — perfect for product logos " +
                "and per-tenant theming.")
            .Title("Glass.Message — Ready")
            .Icon(bmp)
            .Buttons(MessageBoxButtons.OK)
            .Show();
    }

    private static void Demo_Slide()
        => GlassMessage.Create(
                "Glass.Message 2.1 is available.\n\n" +
                "• Native Windows 11 rounded corners and shadow\n" +
                "• Smoother, time-based open/close animations\n" +
                "• Builds for .NET Framework 4.8.1 and .NET 8 / 9 / 10")
            .Title("Update Available")
            .Icon(MessageBoxIcon.Information)
            .Buttons("Install Now", "Later")
            .Animation(GlassAnimation.SlideDown)
            .Show();

    private static void Demo_Scale()
        => GlassMessage.Create(
                "This dialog used the Scale animation — it grows from 90 % to full size as it " +
                "fades in, and shrinks back as it closes.\n\n" +
                "All animations are driven by a wall-clock timer, so they keep a constant duration " +
                "even on a busy UI thread.")
            .Title("Scale Animation")
            .Icon(MessageBoxIcon.Information)
            .Animation(GlassAnimation.Scale)
            .Buttons("Nice", "Again")
            .Show();

    // ── MEDIUM (3–5 lines) ────────────────────────────────────────────────

    private static void Demo_Themes()
    {
        _ = GlassMessage.Create(
                "Night mode active.\nBlue-light filter: 85 %  ·  Colour temperature: 3,200 K")
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
                "The desktop wallpaper is sampled continuously so the translucent tint stays in " +
                "sync with your background as it changes.")
            .Title("Mica Backdrop Active")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.Mica).Show();

        _ = GlassMessage.Create(
                "High Contrast active.\n" +
                "Contrast ratio: 7.4 : 1  ·  Meets WCAG 2.1 Level AAA\n" +
                "All colours are sourced from your Windows system settings.")
            .Title("Accessibility — High Contrast")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.HighContrast).Show();

        _ = GlassMessage.Create(
                "Classic rendering active. Hardware acceleration is disabled and corners are sharp, " +
                "matching the traditional Windows system-chrome look.")
            .Title("Windows Classic Mode")
            .Icon(MessageBoxIcon.Information)
            .Theme(GlassTheme.WindowsClassic).Show();
    }

    private static void Demo_Builder()
        => GlassMessage.Create(
                "Annual_Report_Q4_2025.xlsx has unsaved changes.\n\n" +
                "Last auto-save: 18 minutes ago  ·  File size: 3.7 MB\n" +
                "Closing now will discard every edit made since the last save, including the three " +
                "pivot tables you added to the 'Regional Breakdown' sheet.")
            .Title("Unsaved Changes")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Save", "Don't Save", "Cancel")
            .Default(MessageBoxDefaultButton.Button1)
            .Show();

    private static void Demo_Password()
    {
        var r = GlassMessage.Create(
                "Authentication is required to connect to Contoso ERP.\n\n" +
                "Server:  sql-prod-01.contoso.com\n" +
                "Domain:  CONTOSO   ·   Auth: Windows Integrated\n" +
                "Your password is transmitted over an encrypted TLS 1.3 channel and is never stored " +
                "on this device.")
            .Title("Sign In — Contoso ERP")
            .Icon(MessageBoxIcon.Warning)
            .InputPassword("Active Directory password")
            .Buttons("Connect", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK)
            _ = GlassMessage.Show(
                "Connected to sql-prod-01.contoso.com\n" +
                $"Credential length: {r.InputText.Length} chars  ·  Session valid for 8 hours",
                "Signed In", MessageBoxIcon.Information);
    }

    private static void Demo_Dropdown()
    {
        var r = GlassMessage.Create(
                "Choose the output format for 'Annual_Report_Q4_2025'.\n\n" +
                "47 pages  ·  3.7 MB source  ·  Destination: Downloads folder\n" +
                "The selected format determines whether charts remain editable after export.")
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
                "Estimated time remaining: 4 minutes  ·  Speed: 6.2 MB/s\n" +
                "You can safely close this dialog; the backup continues in the background.")
            .Title("OneDrive Backup in Progress")
            .Icon(MessageBoxIcon.Information)
            .Progress(75, 100)
            .Buttons("Cancel Backup")
            .Show();

    private static void Demo_RTL()
        => GlassMessage.Create(
                "فشل حفظ الملف: تقرير_الربع_الرابع.docx\n\n" +
                "القرص الصلب ممتلئ. المساحة المتاحة: ٠ بايت.\n" +
                "يُرجى تحرير مساحة على القرص ثم إعادة المحاولة. يمكنك حذف الملفات المؤقتة من إعدادات " +
                "التخزين لاستعادة عدة جيجابايت بسرعة.")
            .Title("فشل الحفظ — القرص ممتلئ")
            .Icon(MessageBoxIcon.Error)
            .Buttons(MessageBoxButtons.RetryCancel)
            .RightToLeft()
            .Show();

    private static void Demo_SecurityAlert()
    {
        var r = GlassMessage.Create(
                "We noticed a new sign-in to your Contoso account and want to make sure it was you.\n\n" +
                "When:      Today at 09:48 (UTC+05:30)\n" +
                "Device:    Windows 11 · Microsoft Edge 124\n" +
                "Location:  Colombo, Sri Lanka  (IP 203.0.113.47)\n" +
                "App:       Outlook (Modern Authentication)\n\n" +
                "If this was you, no action is needed. If you don't recognise this activity, secure " +
                "your account now — we'll sign out all other sessions and prompt for a password reset.")
            .Title("New Sign-in to Your Account")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("This was me", "Secure account", "Review activity")
            .Default(MessageBoxDefaultButton.Button1)
            .ShowEx();

        if (r.Button == DialogResult.No)   // "Secure account"
            _ = GlassMessage.Show(
                "All other sessions have been signed out and a password reset link has been emailed " +
                "to your recovery address.",
                "Account Secured", MessageBoxIcon.Information);
    }

    // ── LONG (6 + lines, rich context) ────────────────────────────────────

    private static void Demo_Countdown()
        => GlassMessage.Create(
                "Your Contoso Portal session is about to expire.\n\n" +
                "Security policy POL-2024-AUTH-07 requires automatic sign-out after 15 minutes of " +
                "inactivity. Any unsaved form data will be lost.\n\n" +
                "Active connections that will be interrupted:\n" +
                "  • Contoso ERP — 3 unsaved purchase-order records\n" +
                "  • SharePoint  — 'Q4 Budget.xlsx' is checked out to you\n" +
                "  • Teams call  — 1 participant waiting in the lobby\n\n" +
                "Choose 'Stay Signed In' to extend your session by 30 minutes. If you do nothing, " +
                "you will be signed out automatically when the timer reaches zero.")
            .Title("Session Expiring — Contoso Portal")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Stay Signed In", "Sign Out Now")
            .AutoClose(10_000)
            .Animation(GlassAnimation.SlideDown)
            .Show();

    private static void Demo_CheckBox()
    {
        var r = GlassMessage.Create(
                "Drive C: has only 4.2 GB of free space remaining (of 512 GB total).\n\n" +
                "Windows Update needs at least 8 GB free to install the KB5040442 security patch " +
                "(June 2026 Patch Tuesday, rated Critical).\n\n" +
                "Largest items consuming disk space:\n" +
                "  • C:\\Windows\\Temp                        18.4 GB\n" +
                "  • C:\\Users\\Gehan\\Downloads               11.2 GB\n" +
                "  • Hibernation file (hiberfil.sys)          8.0 GB\n" +
                "  • WinSxS component store                   6.3 GB\n" +
                "  • Delivery Optimization cache              2.1 GB\n\n" +
                "Open Disk Cleanup to recover space immediately, or schedule it for your next restart.")
            .Title("Low Disk Space — C:\\")
            .Icon(MessageBoxIcon.Warning)
            .Buttons("Open Disk Cleanup", "Dismiss")
            .CheckBox("Don't warn me again for drive C:\\")
            .ShowEx();

        if (r.CheckBoxChecked)
            _ = GlassMessage.Show(
                "Disk-space warnings for C:\\ have been suppressed.\n" +
                "Re-enable them in Settings → System → Storage → Notifications.",
                "Warning Suppressed", MessageBoxIcon.Information);
    }

    private static void Demo_Detail()
        => GlassMessage.Create(
                "OneDrive failed to sync 'Annual_Report_Q4_2025.xlsx'.\n\n" +
                "The file is locked by Microsoft Excel. Close every workbook that references this " +
                "file, then choose Retry. Expand the details below to see the full diagnostic the " +
                "support desk will ask for.")
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
                "Press Ctrl+C to copy this message to the clipboard for the support desk.")
            .Title("Database Connection Failed")
            .Icon(MessageBoxIcon.Error)
            .Buttons(MessageBoxButtons.RetryCancel)
            .Show();

    private static void Demo_ReleaseNotes()
        => GlassMessage.Create(
                "Windows 11 cumulative update 2026-05 (KB5040442) is ready to install.\n\n" +
                "This restart-required update rolls up the following changes:\n\n" +
                "Security\n" +
                "  • Hardens the Windows kernel against CVE-2026-21855 (elevation of privilege)\n" +
                "  • Updates Secure Boot revocation lists (DBX)\n\n" +
                "Reliability\n" +
                "  • Fixes a memory leak in explorer.exe when previewing HEIC images\n" +
                "  • Resolves intermittent Bluetooth audio stutter on Intel AX211 adapters\n\n" +
                "Enterprise\n" +
                "  • Group Policy: new 'Restrict clipboard history to managed apps' setting\n" +
                "  • Improves Windows Hello for Business enrolment on hybrid-joined devices\n\n" +
                "Download size: 1.1 GB  ·  Estimated install + restart time: 12 minutes.\n" +
                "Your device will restart outside active hours unless you install now.")
            .Title("Update Ready — Windows 11, version 24H2")
            .Icon(MessageBoxIcon.Information)
            .Buttons("Install and restart", "Schedule", "Remind me later")
            .Default(MessageBoxDefaultButton.Button2)
            .Show();

    private static void Demo_Eula()
        => GlassMessage.Create(
                "Please review and accept the licence terms to continue installing Contoso Suite 2026.\n\n" +
                "A summary is shown below; the full agreement is available under 'Show details'.")
            .Title("End-User Licence Agreement")
            .Icon(MessageBoxIcon.Information)
            .Detail(
                "CONTOSO SUITE 2026 — END-USER LICENCE AGREEMENT\n" +
                "Last updated: 1 May 2026\n\n" +
                "1. GRANT OF LICENCE\n" +
                "   Contoso Corporation grants you a personal, non-exclusive, non-transferable\n" +
                "   licence to install and use one (1) copy of the Software on devices you own or\n" +
                "   control, solely for your internal business or personal purposes.\n\n" +
                "2. RESTRICTIONS\n" +
                "   You may not (a) reverse engineer, decompile, or disassemble the Software except\n" +
                "   to the extent permitted by applicable law; (b) rent, lease, lend, sell, or\n" +
                "   sublicense the Software; or (c) remove any proprietary notices.\n\n" +
                "3. DATA COLLECTION\n" +
                "   The Software collects diagnostic and usage data in accordance with the Contoso\n" +
                "   Privacy Statement. You may opt out of optional telemetry at any time in Settings.\n\n" +
                "4. UPDATES\n" +
                "   Contoso may provide updates, upgrades, or fixes automatically. This Agreement\n" +
                "   governs any such updates unless they are accompanied by separate terms.\n\n" +
                "5. WARRANTY DISCLAIMER\n" +
                "   THE SOFTWARE IS PROVIDED \"AS IS\" WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS\n" +
                "   OR IMPLIED, INCLUDING WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR\n" +
                "   PURPOSE.\n\n" +
                "6. LIMITATION OF LIABILITY\n" +
                "   To the maximum extent permitted by law, Contoso shall not be liable for any\n" +
                "   indirect, incidental, special, or consequential damages arising out of the use\n" +
                "   of or inability to use the Software.\n\n" +
                "7. TERMINATION\n" +
                "   This licence terminates automatically if you breach its terms. On termination you\n" +
                "   must cease all use and destroy all copies of the Software.")
            .CheckBox("I have read and accept the licence terms")
            .Buttons("Accept and install", "Decline")
            .Show();

    private static void Demo_Migration()
    {
        var r = GlassMessage.Create(
                "The Storage Migration Wizard will move your user profile from the 256 GB system " +
                "SSD to the new 2 TB NVMe drive.\n\n" +
                "What will happen:\n" +
                "  1. 184 GB across 312,940 files will be copied to D:\\Users\\Gehan\n" +
                "  2. A junction point will redirect C:\\Users\\Gehan to the new location\n" +
                "  3. Original files are kept until you confirm the move succeeded\n\n" +
                "Estimated time: 22–28 minutes at the current transfer rate of 940 MB/s.\n" +
                "Close all applications before continuing. Your PC must stay powered on for the " +
                "entire operation.")
            .Title("Storage Migration Wizard")
            .Icon(MessageBoxIcon.Question)
            .InputDropdown(
                ["Move profile and verify (recommended)",
                 "Move profile without verification (faster)",
                 "Copy only — keep originals in place"],
                "Move profile and verify (recommended)")
            .CheckBox("Restart automatically when the migration finishes")
            .Buttons("Start migration", "Cancel")
            .ShowEx();

        if (r.Button == DialogResult.OK)
            _ = GlassMessage.Create(
                    $"Strategy: {r.InputText}\n" +
                    $"Auto-restart: {(r.CheckBoxChecked ? "Yes" : "No")}\n\n" +
                    "Copying 312,940 files…")
                .Title("Migration Started")
                .Icon(MessageBoxIcon.Information)
                .Progress(12, 100)
                .Buttons("Run in background")
                .Show();
    }

    private static async void Demo_Async()
    {
        var r = await GlassMessage.ShowAsync(
            "Your local workspace has uncommitted changes and is out of sync with the remote " +
            "repository.\n\n" +
            "Pending changes:\n" +
            "  • GlassDialog.cs    +247 / −83 lines  (modified)\n" +
            "  • GlassTheme.cs      +12 / −4 lines   (modified)\n" +
            "  • GlassToast.cs       +5 / −2 lines   (modified)\n" +
            "  • CHANGELOG.md        new file\n\n" +
            "Push now to back up to Contoso DevOps (origin/main).\n" +
            "Next scheduled auto-push: today at 6:00 PM.",
            "Sync Workspace — Contoso DevOps",
            MessageBoxIcon.Question,
            MessageBoxButtons.OKCancel);

        _ = GlassMessage.Show(
            r == DialogResult.OK
                ? "Push started.\nYour workspace will be up to date in a few seconds."
                : "Push skipped. The scheduled auto-push will run at 6:00 PM.",
            r == DialogResult.OK ? "Pushing to origin/main…" : "Sync Deferred",
            r == DialogResult.OK ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static void Demo_ShowEx()
    {
        var r = GlassMessage.Create(
                "Adobe Acrobat Reader DC (v24.3.21, 698 MB) will be permanently removed from this " +
                "computer.\n\n" +
                "The following will be deleted:\n" +
                "  • All program files under C:\\Program Files\\Adobe\n" +
                "  • Desktop and Start-menu shortcuts\n" +
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
        GlassToast.Show("50 / 50 tests passed · Coverage 91.4 %",
            "Test Run Complete", MessageBoxIcon.Information);
        GlassToast.Show("Deploying to staging-01.contoso.com…",
            "CI/CD Pipeline", MessageBoxIcon.Warning);
    }

    private static void Demo_ToastCorners()
    {
        GlassToast.Show(new GlassToastOptions
        {
            Message = "Top-left corner", Title = "Position", Icon = MessageBoxIcon.Information,
            Position = ToastPosition.TopLeft,
        });
        GlassToast.Show(new GlassToastOptions
        {
            Message = "Top-right corner", Title = "Position", Icon = MessageBoxIcon.Information,
            Position = ToastPosition.TopRight,
        });
        GlassToast.Show(new GlassToastOptions
        {
            Message = "Bottom-left corner", Title = "Position", Icon = MessageBoxIcon.Information,
            Position = ToastPosition.BottomLeft,
        });
        GlassToast.Show(new GlassToastOptions
        {
            Message = "Bottom-centre", Title = "Position", Icon = MessageBoxIcon.Information,
            Position = ToastPosition.BottomCenter,
        });
    }
}
