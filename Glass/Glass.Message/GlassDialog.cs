using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassDialog — the actual borderless form. Internal; not for direct use.
// ─────────────────────────────────────────────────────────────────────────
internal sealed class GlassDialog : Form
{
    // ── Base layout constants (unscaled pixels at 96 DPI) ─────────────────
    private const int _titleHBase = 36;
    private const int _btnPanelHBase = 58;
    private const int _iconSizeBase = 36;
    private const int _padBase = 16;
    private const int _btnWBase = 90;
    private const int _btnHBase = 30;
    private const int _btnGapBase = 8;
    private const int _minFormWBase = 330;
    private const int _minFormHBase = 155;

    // ── DPI ───────────────────────────────────────────────────────────────
    private float _scale = 1.0f;
    private int Scale(int v) => Math.Max(1, (int)(v * _scale));

    private int TitleH => Scale(_titleHBase);
    private int BtnPanelH => Scale(_btnPanelHBase);
    private int IconSize => Scale(_iconSizeBase);
    private int Pad => Scale(_padBase);
    private int BtnW => Scale(_btnWBase);
    private int BtnH => Scale(_btnHBase);
    private int BtnGap => Scale(_btnGapBase);
    private int MinFormW => Scale(_minFormWBase);
    private int MinFormH => Scale(_minFormHBase);

    private const int _wM_DPICHANGED = 0x02E0;

    // ── Data ──────────────────────────────────────────────────────────────
    private readonly GlassTheme _theme;
    private readonly string _message;
    private readonly string _title;
    private readonly MessageBoxIcon _icon;
    private readonly MessageBoxButtons _buttons;
    private readonly MessageBoxDefaultButton _defaultButton;
    private readonly string[] _customLabels;

    private Bitmap _iconBitmap;
    private Point _dragOrigin;
    private bool _dragging;

    // ── Animation (fade-in / fade-out) ────────────────────────────────────
    private System.Windows.Forms.Timer _fadeTimer;
    private double _targetOpacity;
    private bool _fadingOut;
    private DialogResult _pendingResult;
    private int _fadeStep;
    private const int _fadeTicks = 8; // 8 × 16 ms ≈ 128 ms

    // ── GDI+ resource cache ───────────────────────────────────────────────
    private GraphicsPath _cachedBgPath;
    private LinearGradientBrush _cachedBgBrush;
    private LinearGradientBrush _cachedTitleBrush;
    private GraphicsPath _cachedBorderPath;

    // Fixed pens — created once, do not depend on form size
    private readonly Pen _glossPen;
    private readonly Pen _sepPen;
    private readonly Pen _glowPen;
    private readonly Pen _edgePen;
    private readonly Pen _panelSepPen;

    // ── Acrylic backdrop ─────────────────────────────────────────────────
    private bool _acrylicEnabled;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor; // packed AABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttribData data);

    // ── Constructor ───────────────────────────────────────────────────────
    public GlassDialog(string message, string title, MessageBoxIcon icon,
                       MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton,
                       GlassTheme theme, string[] customLabels = null)
    {
        _message = message;
        _title = title;
        _icon = icon;
        _buttons = buttons;
        _defaultButton = defaultButton;
        _theme = theme;
        _customLabels = customLabels;
        _targetOpacity = theme.Opacity;

        // System DPI — safe to query without a window handle
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        {
            _scale = g.DpiX / 96f;
        }

        SetStyle(ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);

        // Create fixed pens once for the lifetime of the dialog
        _glossPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);
        _sepPen = new Pen(Color.FromArgb(90, _theme.BorderColor), 1f);
        _glowPen = new Pen(Color.FromArgb(55, _theme.BorderColor), 3f);
        _edgePen = new Pen(Color.FromArgb(180, _theme.BorderColor), 1f);
        _panelSepPen = new Pen(Color.FromArgb(40, _theme.BorderColor), 1f);

        Build();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    // ── Build / Rebuild ───────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterParent;
        Opacity = _targetOpacity;
        Font = _theme.MessageFont;
        BackColor = _theme.BackgroundBottom;
        KeyPreview = true;

        _iconBitmap = ResolveIcon(_icon);

        var (formW, formH) = MeasureForm();
        ClientSize = new Size(formW, formH);

        ApplyRegion(formW, formH);
        AddMessageControls(formW, formH);
        AddButtons(formW, formH);

        ResumeLayout(false);
    }

    private void Rebuild()
    {
        SuspendLayout();
        foreach (Control c in Controls)
        {
            c.Dispose();
        }

        Controls.Clear();
        InvalidateCache();

        _iconBitmap?.Dispose();
        _iconBitmap = ResolveIcon(_icon);

        var (formW, formH) = MeasureForm();
        ClientSize = new Size(formW, formH);
        ApplyRegion(formW, formH);
        AddMessageControls(formW, formH);
        AddButtons(formW, formH);

        ResumeLayout(false);
        Invalidate();
    }

    private void ApplyRegion(int w, int h)
    {
        using var path = RoundRect(new Rectangle(0, 0, w, h), _theme.CornerRadius);
        Region = new Region(path);
    }

    private (int w, int h) MeasureForm()
    {
        var maxW = (int)(SystemInformation.WorkingArea.Width * 0.8)
                   - (Pad * 3) - IconSize;

        int titleW = 0, msgW = 0, msgH = 0;

        if (_title.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_title, _theme.TitleFont);
            titleW = sz.Width + (Pad * 3) + IconSize;
        }

        if (_message.Length > 0)
        {
            var szW = TextRenderer.MeasureText(_message, _theme.MessageFont,
                          new Size(maxW, int.MaxValue),
                          TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgW = szW.Width + (_iconBitmap != null ? IconSize + Pad : 0) + (Pad * 2);
        }

        var w = Math.Max(Math.Max(titleW, msgW), MinFormW);

        if (_message.Length > 0)
        {
            var msgLeft = _iconBitmap != null ? Pad + IconSize + Pad : Pad;
            var availW = w - msgLeft - Pad;
            var szH = TextRenderer.MeasureText(_message, _theme.MessageFont,
                          new Size(availW, int.MaxValue),
                          TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgH = szH.Height;
        }

        var h = Math.Max(TitleH + Pad + msgH + Pad + BtnPanelH, MinFormH);
        return (w, h);
    }

    private void AddMessageControls(int formW, int formH)
    {
        var contentTop = TitleH + Pad;
        var contentH = formH - TitleH - Pad - BtnPanelH - Pad;

        if (_iconBitmap != null)
        {
            Controls.Add(new PictureBox
            {
                Bounds = new Rectangle(Pad, contentTop, IconSize, IconSize),
                Image = _iconBitmap,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
            });
        }

        var msgLeft = _iconBitmap != null ? Pad + IconSize + Pad : Pad;
        Controls.Add(new Label
        {
            Text = _message,
            Font = _theme.MessageFont,
            ForeColor = _theme.MessageColor,
            BackColor = Color.Transparent,
            AutoSize = false,
            UseMnemonic = false,
            Bounds = new Rectangle(msgLeft, contentTop, formW - msgLeft - Pad, contentH),
        });
    }

    private void AddButtons(int formW, int formH)
    {
        var defs = ButtonDefs(_buttons);
        var totalW = (defs.Length * BtnW) + ((defs.Length - 1) * BtnGap);
        var startX = (formW - totalW) / 2;
        var btnY = formH - BtnPanelH + ((BtnPanelH - BtnH) / 2);
        var focusIdx = DefaultIndex(_buttons, _defaultButton);

        for (var i = 0; i < defs.Length; i++)
        {
            var (label, result) = defs[i];
            if (_customLabels != null && i < _customLabels.Length)
            {
                label = _customLabels[i];
            }

            var btn = new GlassButton(_theme)
            {
                Text = label,
                Bounds = new Rectangle(startX + (i * (BtnW + BtnGap)), btnY, BtnW, BtnH),
                Tag = result,
            };
            btn.Click += OnButtonClick;
            Controls.Add(btn);

            if (i == focusIdx)
            {
                ActiveControl = btn;
            }
        }
    }

    // ── Events ────────────────────────────────────────────────────────────

    private void OnButtonClick(object sender, EventArgs e)
    {
        if (sender is Button b && b.Tag is DialogResult r)
        {
            BeginClose(r);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            BeginClose(EscapeResult(_buttons));
            e.Handled = true;
        }
    }

    // Intercept Alt+F4 / system close and route through the fade.
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_fadingOut)
        {
            e.Cancel = true;
            BeginClose(EscapeResult(_buttons));
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Y < TitleH)
        {
            _dragging = true;
            _dragOrigin = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            Location = new Point(
                Location.X + e.X - _dragOrigin.X,
                Location.Y + e.Y - _dragOrigin.Y);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    // ── Fade-in / fade-out animation ──────────────────────────────────────

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Opacity = 0.0;
        _fadeStep = 0;
        _fadingOut = false;
        StartFadeTimer();
    }

    private void BeginClose(DialogResult result)
    {
        if (_fadingOut)
        {
            return;
        }

        _pendingResult = result;
        _fadingOut = true;
        // Compute the step that matches the current opacity so fade-out
        // starts smoothly even if the button was clicked mid-fade-in.
        var ratio = _targetOpacity > 0 ? Opacity / _targetOpacity : 0.0;
        _fadeStep = (int)((1.0 - ratio) * _fadeTicks);
        DisposeFadeTimer();
        StartFadeTimer();
    }

    private void StartFadeTimer()
    {
        _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += OnFadeTick;
        _fadeTimer.Start();
    }

    private void DisposeFadeTimer()
    {
        if (_fadeTimer == null)
        {
            return;
        }

        _fadeTimer.Stop();
        _fadeTimer.Dispose();
        _fadeTimer = null;
    }

    private void OnFadeTick(object sender, EventArgs e)
    {
        _fadeStep++;
        if (_fadingOut)
        {
            Opacity = _targetOpacity * Math.Max(0.0, 1.0 - ((double)_fadeStep / _fadeTicks));
            if (_fadeStep >= _fadeTicks)
            {
                DisposeFadeTimer();
                DialogResult = _pendingResult; // exits the ShowDialog modal loop
            }
        }
        else
        {
            Opacity = _targetOpacity * Math.Min(1.0, (double)_fadeStep / _fadeTicks);
            if (_fadeStep >= _fadeTicks)
            {
                Opacity = _targetOpacity;
                DisposeFadeTimer();
            }
        }
    }

    // ── DPI change ────────────────────────────────────────────────────────

    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == _wM_DPICHANGED)
        {
            _scale = (m.WParam.ToInt32() & 0xFFFF) / 96f;
            Rebuild();
        }
        base.WndProc(ref m);
    }

    // ── Windows 10/11 acrylic backdrop ───────────────────────────────────

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryApplyAcrylic();
    }

    private void TryApplyAcrylic()
    {
        if (!IsAcrylicSupported())
        {
            return;
        }

        try
        {
            // Dark tinted acrylic — 75% opacity tint matching theme background
            var c = _theme.BackgroundTop;
            var tint = ((uint)0xC0 << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;

            var accent = new AccentPolicy
            {
                AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                AccentFlags = 0,
                GradientColor = tint,
            };
            var sz = Marshal.SizeOf(typeof(AccentPolicy));
            var ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttribData
                {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    Data = ptr,
                    SizeOfData = sz,
                };
                if (SetWindowCompositionAttribute(Handle, ref data))
                {
                    _acrylicEnabled = true;
                    // Reduce opacity so the blurred desktop shows through our gradient
                    _targetOpacity = Math.Min(_theme.Opacity, 0.85);
                }
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch { /* silently fall back to solid gradient */ }
    }

    // Windows 10 1803 (build 17134) and Windows 11 support ACCENT_ENABLE_ACRYLICBLURBEHIND.
    // Windows 11 reports as build 22000+ but still returns Major == 10.
    private static bool IsAcrylicSupported()
    {
        var v = Environment.OSVersion.Version;
        return v.Major == 10 && v.Build >= 17134;
    }

    // ── GDI+ resource cache ───────────────────────────────────────────────

    private void InvalidateCache()
    {
        _cachedBgPath?.Dispose();
        _cachedBgPath = null;
        _cachedBgBrush?.Dispose();
        _cachedBgBrush = null;
        _cachedTitleBrush?.Dispose();
        _cachedTitleBrush = null;
        _cachedBorderPath?.Dispose();
        _cachedBorderPath = null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateCache();
        Invalidate();
    }

    // ── Painting ──────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        SetQuality(g);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var r = _theme.CornerRadius;

        // Background — skip when acrylic provides it
        if (!_acrylicEnabled)
        {
            _cachedBgPath ??= RoundRect(new Rectangle(0, 0, w, h), r);

            _cachedBgBrush ??= new LinearGradientBrush(
                    new Rectangle(0, 0, w, h),
                    _theme.BackgroundTop, _theme.BackgroundBottom,
                    LinearGradientMode.Vertical);

            g.FillPath(_cachedBgBrush, _cachedBgPath);
        }

        // Title bar gradient (always painted)
        var titleRect = new Rectangle(0, 0, w, TitleH);
        _cachedTitleBrush ??= new LinearGradientBrush(
                titleRect, _theme.TitleBarTop, _theme.TitleBarBottom,
                LinearGradientMode.Vertical);

        g.FillRectangle(_cachedTitleBrush, titleRect);

        // Top-edge gloss line
        g.DrawLine(_glossPen, r + 1, 1, w - r - 2, 1);

        // Title / body separator
        g.DrawLine(_sepPen, 0, TitleH - 1, w, TitleH - 1);

        // Border (glow + crisp edge)
        var borderRect = new Rectangle(0, 0, w - 1, h - 1);
        _cachedBorderPath ??= RoundRect(borderRect, r);

        g.DrawPath(_glowPen, _cachedBorderPath);
        g.DrawPath(_edgePen, _cachedBorderPath);

        // Button panel separator
        var sepY = h - BtnPanelH;
        g.DrawLine(_panelSepPen, Pad, sepY, w - Pad, sepY);

        // Title text
        if (_title.Length > 0)
        {
            TextRenderer.DrawText(g, _title, _theme.TitleFont,
                new Rectangle(Pad, 0, w - (Pad * 2), TitleH),
                _theme.TitleColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        }
    }

    internal static void SetQuality(Graphics g)
    {
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    internal static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1, radius * 2);
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Bitmap ResolveIcon(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Information => SystemIcons.Information.ToBitmap(),
            MessageBoxIcon.Question => SystemIcons.Question.ToBitmap(),
            MessageBoxIcon.Warning => SystemIcons.Warning.ToBitmap(),
            MessageBoxIcon.Error => SystemIcons.Error.ToBitmap(),
            _ => null,
        };
    }

    private static (string label, DialogResult result)[] ButtonDefs(MessageBoxButtons btns)
    {
        return btns switch
        {
            MessageBoxButtons.OK => [("&OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel => [("&OK", DialogResult.OK), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.YesNo => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No)],
            MessageBoxButtons.YesNoCancel => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.RetryCancel => [("&Retry", DialogResult.Retry), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.AbortRetryIgnore => [("&Abort", DialogResult.Abort), ("&Retry", DialogResult.Retry), ("&Ignore", DialogResult.Ignore)],
            _ => [("&OK", DialogResult.OK)],
        };
    }

    private static int DefaultIndex(MessageBoxButtons btns, MessageBoxDefaultButton def)
    {
        var max = ButtonDefs(btns).Length - 1;
        return def switch
        {
            MessageBoxDefaultButton.Button1 => 0,
            MessageBoxDefaultButton.Button2 => Math.Min(1, max),
            MessageBoxDefaultButton.Button3 => Math.Min(2, max),
            _ => 0,
        };
    }

    private static DialogResult EscapeResult(MessageBoxButtons btns)
    {
        return btns switch
        {
            MessageBoxButtons.OK => DialogResult.OK,
            MessageBoxButtons.OKCancel => DialogResult.Cancel,
            MessageBoxButtons.YesNo => DialogResult.No,
            MessageBoxButtons.YesNoCancel => DialogResult.Cancel,
            MessageBoxButtons.RetryCancel => DialogResult.Cancel,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
            _ => DialogResult.Cancel,
        };
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeFadeTimer();
            _iconBitmap?.Dispose();
            InvalidateCache();
            _glossPen?.Dispose();
            _sepPen?.Dispose();
            _glowPen?.Dispose();
            _edgePen?.Dispose();
            _panelSepPen?.Dispose();
        }
        base.Dispose(disposing);
    }
}
