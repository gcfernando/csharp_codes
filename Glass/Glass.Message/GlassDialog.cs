using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Glass;

// ─────────────────────────────────────────────────────────────────────────
// GlassDialog — the actual borderless form.  Internal; not for direct use.
// ─────────────────────────────────────────────────────────────────────────
internal sealed class GlassDialog : Form
{
    // ═══════════════════════════════════════════════════════════════════════
    // Base layout constants (unscaled pixels at 96 DPI)
    // ═══════════════════════════════════════════════════════════════════════
    private const int _titleHBase      = 36;
    private const int _btnPanelHBase   = 58;
    private const int _iconSizeBase    = 36;
    private const int _padBase         = 16;
    private const int _btnWBase        = 92;
    private const int _btnHBase        = 30;
    private const int _btnGapBase      = 8;
    private const int _minFormWBase    = 360;
    private const int _minFormHBase    = 160;
    private const int _progressHBase   = 10;
    private const int _inputHBase      = 34;
    private const int _inputMLHBase    = 80;
    private const int _checkHBase      = 24;
    private const int _linkHBase       = 22;
    private const int _detailHBase     = 100;

    // ═══════════════════════════════════════════════════════════════════════
    // DPI scaling
    // ═══════════════════════════════════════════════════════════════════════
    private float _scale = 1.0f;
    private int   Scale(int v) => Math.Max(1, (int)(v * _scale));

    private int TitleH      => Scale(_titleHBase);
    private int BtnPanelH   => Scale(_btnPanelHBase);
    private int IconSize    => Scale(_iconSizeBase);
    private int Pad         => Scale(_padBase);
    private int BtnW        => Scale(_btnWBase);   // minimum; actual width set by MeasureForm
    private int BtnH        => Scale(_btnHBase);
    private int _computedBtnW; // measured from label text; set by MeasureForm, used by AddButtons
    private int BtnGap      => Scale(_btnGapBase);
    private int MinFormW    => Scale(_minFormWBase);
    private int MinFormH    => Scale(_minFormHBase);
    private int ProgressH   => Scale(_progressHBase);
    private int InputH      => Scale(_inputHBase);
    private int InputMLH    => Scale(_inputMLHBase);
    private int CheckH      => Scale(_checkHBase);
    private int LinkH       => Scale(_linkHBase);
    private int DetailH     => Scale(_detailHBase);

    private const int _wmDpiChanged = 0x02E0;

    // ═══════════════════════════════════════════════════════════════════════
    // Config & data
    // ═══════════════════════════════════════════════════════════════════════
    private readonly GlassDialogConfig _cfg;
    private readonly GlassTheme        _theme;

    private Bitmap _iconBitmap;
    private Point  _dragOrigin;
    private bool   _dragging;
    private bool   _isExpanded;     // detail section toggle state

    // Cached layout measurements set by MeasureForm()
    private int _msgLeft, _msgW, _contentH;

    // ═══════════════════════════════════════════════════════════════════════
    // Live control references (for state read-back)
    // ═══════════════════════════════════════════════════════════════════════
    private CheckBox      _checkBoxCtrl;
    private TextBox       _inputTextBox;
    private ComboBox      _inputCombo;
    private LinkLabel     _detailToggle;
    private GlassButton   _countdownBtn;

    // ── Results ───────────────────────────────────────────────────────────
    internal bool   CheckBoxChecked => _checkBoxCtrl?.Checked ?? false;
    internal string InputText       => _inputTextBox?.Text ?? _inputCombo?.Text ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // Fade / slide animation
    // ═══════════════════════════════════════════════════════════════════════
    private System.Windows.Forms.Timer _fadeTimer;
    private double       _targetOpacity;
    private bool         _fadingOut;
    private DialogResult _pendingResult;
    private int          _fadeStep;
    private const int    _fadeTicks = 8; // 8 × 16 ms ≈ 128 ms

    private Point _slideFinal;
    private Point _slideOrigin;
    private bool  _slideActive;

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown timer
    // ═══════════════════════════════════════════════════════════════════════
    private System.Windows.Forms.Timer _countTimer;
    private int _countRemaining;   // milliseconds remaining

    // ═══════════════════════════════════════════════════════════════════════
    // GDI+ resource cache (re-created on resize / DPI change)
    // ═══════════════════════════════════════════════════════════════════════
    private GraphicsPath          _bgPath;
    private LinearGradientBrush   _bgBrush;
    private LinearGradientBrush   _titleBrush;
    private GraphicsPath          _borderPath;

    // Fixed pens (created once, never depend on form size)
    private readonly Pen _glossPen;
    private readonly Pen _sepPen;
    private readonly Pen _glowPen;
    private readonly Pen _edgePen;
    private readonly Pen _panelSepPen;

    // ═══════════════════════════════════════════════════════════════════════
    // Win32 / DWM P-Invokes
    // ═══════════════════════════════════════════════════════════════════════
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState, AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttribData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int Left, Right, Top, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, uint wParam, string lParam);

    private bool _acrylicEnabled;
    private bool _micaEnabled;

    // ═══════════════════════════════════════════════════════════════════════
    // Constructor
    // ═══════════════════════════════════════════════════════════════════════
    public GlassDialog(GlassDialogConfig cfg)
    {
        _cfg   = cfg;
        _theme = cfg.Theme ?? GlassTheme.Default;
        _targetOpacity = _theme.Opacity;

        using (var g = Graphics.FromHwnd(IntPtr.Zero))
            _scale = g.DpiX / 96f;

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        _glossPen    = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);
        _sepPen      = new Pen(Color.FromArgb(90, _theme.BorderColor), 1f);
        _glowPen     = new Pen(Color.FromArgb(55, _theme.BorderColor), 3f);
        _edgePen     = new Pen(Color.FromArgb(180, _theme.BorderColor), 1f);
        _panelSepPen = new Pen(Color.FromArgb(40, _theme.BorderColor), 1f);

        Build();
    }

    // ─────────────────────────────────────────────────────────────────────
    // CreateParams — drop shadow
    // ─────────────────────────────────────────────────────────────────────
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return cp;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Build / Rebuild
    // ═══════════════════════════════════════════════════════════════════════
    private void Build()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        ShowIcon        = false;
        StartPosition   = FormStartPosition.Manual;   // we position in OnShown
        Opacity         = _cfg.Animation == GlassAnimation.None ? _targetOpacity : 0.0;
        Font            = _theme.MessageFont;
        BackColor       = _theme.BackgroundBottom;
        KeyPreview      = true;
        // RightToLeftLayout is intentionally NOT set — WS_EX_LAYOUTRTL mirrors the
        // entire window DC and breaks GDI+ custom painting (black/white artefact).
        // RTL is implemented manually: icon on the right, text right-aligned.
        RightToLeft = _cfg.RightToLeft ? RightToLeft.Yes : RightToLeft.No;

        // Accessibility
        AccessibleName = string.IsNullOrEmpty(_cfg.Title) ? "Dialog" : _cfg.Title;
        AccessibleRole = AccessibleRole.Alert;

        _iconBitmap = _cfg.CustomIcon ?? ResolveSystemIcon(_cfg.Icon);

        var (fw, fh) = MeasureForm();
        ClientSize = new Size(fw, fh);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        // Temporary off-screen position — refined in OnShown
        var wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Left + (wa.Width - fw) / 2, wa.Top + (wa.Height - fh) / 2);

        ResumeLayout(false);
    }

    private void Rebuild()
    {
        SuspendLayout();
        foreach (Control c in Controls) c.Dispose();
        Controls.Clear();
        InvalidateCache();

        if (_cfg.CustomIcon == null)
        {
            _iconBitmap?.Dispose();
            _iconBitmap = ResolveSystemIcon(_cfg.Icon);
        }

        var (fw, fh) = MeasureForm();
        ClientSize = new Size(fw, fh);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        ResumeLayout(false);
        Invalidate();
    }

    private void ApplyRegion(int w, int h)
    {
        if (_theme.CornerRadius <= 0) { Region = null; return; }
        using var path = RoundRect(new Rectangle(0, 0, w, h), _theme.CornerRadius);
        Region = new Region(path);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Layout measurement
    // ═══════════════════════════════════════════════════════════════════════
    private (int w, int h) MeasureForm()
    {
        var maxW = Math.Min(
            (int)(Screen.PrimaryScreen.WorkingArea.Width * 0.80),
            Scale(720));

        var iconColW = _iconBitmap != null ? IconSize + Pad : 0;
        var textMaxW = maxW - Pad - iconColW - Pad;

        // Button width: measure every label so text never clips.
        // Custom labels and the countdown suffix " (NNs)" are included.
        var defs       = ButtonDefs(_cfg.Buttons);
        var defIdx     = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        var maxLabelPx = 0;
        for (var i = 0; i < defs.Length; i++)
        {
            var lbl = (_cfg.CustomLabels != null && i < _cfg.CustomLabels.Length)
                ? _cfg.CustomLabels[i]
                : defs[i].label;
            // Reserve room for the widest possible countdown suffix " (NNs)"
            if (_cfg.AutoCloseMs > 0 && i == defIdx)
                lbl += $" ({_cfg.AutoCloseMs / 1000}s)";
            maxLabelPx = Math.Max(maxLabelPx,
                TextRenderer.MeasureText(lbl, _theme.ButtonFont).Width);
        }
        _computedBtnW = Math.Max(BtnW, maxLabelPx + Scale(24)); // 12 px padding each side
        var btnMinW   = defs.Length * _computedBtnW + (defs.Length - 1) * BtnGap + Pad * 2;

        // Title
        int titleNeedW = 0;
        if (_cfg.Title.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Title, _theme.TitleFont);
            titleNeedW = sz.Width + Pad * 2 + iconColW + Scale(24); // close-btn margin
        }

        // Message
        int msgNeedW = 0;
        if (_cfg.Message.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Message, _theme.MessageFont,
                new Size(textMaxW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgNeedW = sz.Width + iconColW + Pad * 2;
        }

        var w = Math.Max(Math.Max(Math.Max(titleNeedW, msgNeedW), MinFormW), btnMinW);
        if (_cfg.HasInput || _cfg.HasDetail) w = Math.Max(w, Scale(380));

        // Message height at chosen width.
        // RTL: icon sits on the right, so message starts at the left edge.
        _msgLeft = _cfg.RightToLeft ? Pad : (Pad + iconColW);
        _msgW    = w - Pad * 2 - iconColW;

        int msgH = 0;
        if (_cfg.Message.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Message, _theme.MessageFont,
                new Size(_msgW, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgH = sz.Height;
        }

        _contentH = Math.Max(msgH, _iconBitmap != null ? IconSize : 0);

        // Accumulate total height
        var h = TitleH + Pad + _contentH;
        if (_cfg.HasProgress)        h += Pad + ProgressH;
        if (_cfg.HasInput)           h += Pad + (_cfg.InputMode == GlassInputMode.Multiline ? InputMLH : InputH);
        if (_cfg.HasCheckBox)        h += Scale(8) + CheckH;
        if (_cfg.HasDetail)
        {
            h += Scale(8) + LinkH;
            if (_isExpanded) h += Scale(6) + DetailH;
        }
        h += Pad + BtnPanelH;
        h  = Math.Max(h, MinFormH);

        return (w, h);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Control construction
    // ═══════════════════════════════════════════════════════════════════════
    private void AddControls(int fw, int fh)
    {
        var y = TitleH + Pad;

        // ── Icon ──────────────────────────────────────────────────────────
        if (_iconBitmap != null)
        {
            // RTL: icon on the right side
            var iconX = _cfg.RightToLeft ? fw - Pad - IconSize : Pad;
            Controls.Add(new PictureBox
            {
                Bounds         = new Rectangle(iconX, y, IconSize, IconSize),
                Image          = _iconBitmap,
                SizeMode       = PictureBoxSizeMode.StretchImage,
                BackColor      = Color.Transparent,
                AccessibleName = _cfg.Icon.ToString(),
                AccessibleRole = AccessibleRole.Graphic,
            });
        }

        // ── Message ───────────────────────────────────────────────────────
        if (_cfg.Message.Length > 0)
        {
            Controls.Add(new Label
            {
                Text           = _cfg.Message,
                Font           = _theme.MessageFont,
                ForeColor      = _theme.MessageColor,
                BackColor      = Color.Transparent,
                AutoSize       = false,
                UseMnemonic    = false,
                Bounds         = new Rectangle(_msgLeft, y, _msgW, _contentH),
                TextAlign      = _cfg.RightToLeft ? ContentAlignment.TopRight : ContentAlignment.TopLeft,
                AccessibleRole = AccessibleRole.StaticText,
            });
        }

        y += _contentH;

        // ── Progress bar ─────────────────────────────────────────────────
        if (_cfg.HasProgress)
        {
            y += Pad;
            var prog = new GlassProgressPanel(_theme, _cfg.ProgressValue, _cfg.ProgressMax)
            {
                Bounds = new Rectangle(Pad, y, fw - Pad * 2, ProgressH),
                AccessibleName = "Progress",
                AccessibleRole = AccessibleRole.ProgressBar,
            };
            Controls.Add(prog);
            y += ProgressH;
        }

        // ── Input control ─────────────────────────────────────────────────
        if (_cfg.HasInput)
        {
            y += Pad;

            if (_cfg.InputMode == GlassInputMode.Dropdown)
            {
                _inputCombo = new ComboBox
                {
                    Bounds        = new Rectangle(Pad, y, fw - Pad * 2, InputH),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font          = _theme.MessageFont,
                    BackColor     = _theme.InputBackColor,
                    ForeColor     = _theme.InputForeColor,
                    FlatStyle     = FlatStyle.Flat,
                    AccessibleName = "Input",
                };
                if (_cfg.InputDropdownItems != null)
                    _inputCombo.Items.AddRange(_cfg.InputDropdownItems);
                if (!string.IsNullOrEmpty(_cfg.InputDefault))
                {
                    var idx = _inputCombo.Items.IndexOf(_cfg.InputDefault);
                    _inputCombo.SelectedIndex = idx >= 0 ? idx : (_inputCombo.Items.Count > 0 ? 0 : -1);
                }
                else if (_inputCombo.Items.Count > 0)
                    _inputCombo.SelectedIndex = 0;

                Controls.Add(_inputCombo);
                y += InputH;
            }
            else
            {
                var inputH2 = _cfg.InputMode == GlassInputMode.Multiline ? InputMLH : InputH;
                _inputTextBox = new PlaceholderTextBox(_cfg.InputPlaceholder ?? string.Empty)
                {
                    Bounds        = new Rectangle(Pad, y, fw - Pad * 2, inputH2),
                    Font          = _theme.MessageFont,
                    BackColor     = _theme.InputBackColor,
                    ForeColor     = _theme.InputForeColor,
                    BorderStyle   = BorderStyle.None,
                    Multiline     = _cfg.InputMode == GlassInputMode.Multiline,
                    ScrollBars    = _cfg.InputMode == GlassInputMode.Multiline ? ScrollBars.Vertical : ScrollBars.None,
                    PasswordChar  = _cfg.InputMode == GlassInputMode.Password ? '●' : '\0',
                    Text          = _cfg.InputDefault ?? string.Empty,
                    AccessibleName = "Input",
                    AccessibleRole = AccessibleRole.Text,
                };
                Controls.Add(_inputTextBox);
                y += inputH2;
            }
        }

        // ── "Don't show again" checkbox ───────────────────────────────────
        if (_cfg.HasCheckBox)
        {
            y += Scale(8);
            _checkBoxCtrl = new CheckBox
            {
                Text          = _cfg.CheckBoxLabel,
                Font          = _theme.MessageFont,
                ForeColor     = _theme.MessageColor,
                BackColor     = Color.Transparent,
                Checked       = _cfg.CheckBoxDefault,
                AutoSize      = true,
                Location      = new Point(_msgLeft, y),
                AccessibleRole = AccessibleRole.CheckButton,
            };
            Controls.Add(_checkBoxCtrl);
            y += CheckH;
        }

        // ── Expandable detail toggle ───────────────────────────────────────
        if (_cfg.HasDetail)
        {
            y += Scale(8);
            _detailToggle = new LinkLabel
            {
                Text          = _isExpanded ? "Hide details ▲" : "Show details ▼",
                Font          = _theme.ButtonFont,
                ForeColor     = _theme.AccentColor,
                LinkColor     = _theme.AccentColor,
                ActiveLinkColor = _theme.BorderColor,
                BackColor     = Color.Transparent,
                AutoSize      = true,
                Location      = new Point(_msgLeft, y),
                AccessibleName = "Toggle detail panel",
            };
            _detailToggle.LinkClicked += OnDetailToggleClick;
            Controls.Add(_detailToggle);
            y += LinkH;

            if (_isExpanded)
            {
                y += Scale(6);
                Controls.Add(new TextBox
                {
                    Text          = _cfg.DetailText,
                    Font          = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                    BackColor     = Color.FromArgb(8, 15, 28),
                    ForeColor     = Color.FromArgb(160, 175, 200),
                    ReadOnly      = true,
                    Multiline     = true,
                    ScrollBars    = ScrollBars.Vertical,
                    WordWrap      = true,
                    BorderStyle   = BorderStyle.None,
                    Bounds        = new Rectangle(Pad, y, fw - Pad * 2, DetailH),
                    AccessibleName = "Detail",
                    AccessibleRole = AccessibleRole.Text,
                });
                y += DetailH;
            }
        }

        // ── Buttons ───────────────────────────────────────────────────────
        AddButtons(fw, fh);
    }

    private void AddButtons(int fw, int fh)
    {
        var defs = ButtonDefs(_cfg.Buttons);
        if (_cfg.RightToLeft) Array.Reverse(defs);

        var totalW = defs.Length * _computedBtnW + (defs.Length - 1) * BtnGap;
        var startX = (fw - totalW) / 2;
        var btnY   = fh - BtnPanelH + (BtnPanelH - BtnH) / 2;

        var focusIdx = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        if (_cfg.RightToLeft) focusIdx = defs.Length - 1 - focusIdx;

        for (var i = 0; i < defs.Length; i++)
        {
            var (label, result) = defs[i];
            if (_cfg.CustomLabels != null && i < _cfg.CustomLabels.Length)
                label = _cfg.CustomLabels[i];

            var btn = new GlassButton(_theme)
            {
                Text           = label,
                Bounds         = new Rectangle(startX + i * (_computedBtnW + BtnGap), btnY, _computedBtnW, BtnH),
                Tag            = result,
                AccessibleName = label.Replace("&", string.Empty),
            };
            btn.Click += OnButtonClick;
            Controls.Add(btn);

            if (i == focusIdx)
            {
                ActiveControl = btn;
                if (_cfg.AutoCloseMs > 0) _countdownBtn = btn;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Events
    // ═══════════════════════════════════════════════════════════════════════
    private void OnButtonClick(object sender, EventArgs e)
    {
        if (sender is Button b && b.Tag is DialogResult r)
            BeginClose(r);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // Ctrl+C — copy title + message to clipboard
        if (e.Control && e.KeyCode == Keys.C)
        {
            var text = string.IsNullOrEmpty(_cfg.Title)
                ? _cfg.Message
                : $"{_cfg.Title}\n{_cfg.Message}";
            Clipboard.SetText(text);
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            BeginClose(EscapeResult(_cfg.Buttons));
            e.Handled = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_fadingOut)
        {
            e.Cancel = true;
            BeginClose(EscapeResult(_cfg.Buttons));
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && e.Y < TitleH)
        {
            _dragging   = true;
            _dragOrigin = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
            Location = new Point(
                Location.X + e.X - _dragOrigin.X,
                Location.Y + e.Y - _dragOrigin.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
    }

    private void OnDetailToggleClick(object sender, LinkLabelLinkClickedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        Rebuild();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown timer
    // ═══════════════════════════════════════════════════════════════════════
    private void StartCountdown()
    {
        _countRemaining = _cfg.AutoCloseMs;
        UpdateCountdownLabel();
        _countTimer          = new System.Windows.Forms.Timer { Interval = 1000 };
        _countTimer.Tick    += OnCountdownTick;
        _countTimer.Start();
    }

    private void OnCountdownTick(object sender, EventArgs e)
    {
        _countRemaining -= 1000;
        if (_countRemaining <= 0)
        {
            StopCountdown();
            BeginClose(DefaultResult(_cfg.Buttons, _cfg.DefaultButton));
        }
        else
        {
            UpdateCountdownLabel();
        }
    }

    private void UpdateCountdownLabel()
    {
        if (_countdownBtn == null) return;
        var seconds = Math.Max(0, _countRemaining / 1000);
        var defs    = ButtonDefs(_cfg.Buttons);
        if (_cfg.RightToLeft) Array.Reverse(defs);

        var focusIdx = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        if (_cfg.RightToLeft) focusIdx = defs.Length - 1 - focusIdx;

        var baseLabel = defs.Length > focusIdx
            ? defs[focusIdx].label
            : _countdownBtn.Text.Split('(')[0].TrimEnd();

        if (_cfg.CustomLabels != null && focusIdx < _cfg.CustomLabels.Length)
            baseLabel = _cfg.CustomLabels[focusIdx];

        _countdownBtn.Text = seconds > 0
            ? $"{baseLabel} ({seconds}s)"
            : baseLabel;
    }

    private void StopCountdown()
    {
        if (_countdownBtn != null)
        {
            // restore clean label
            var defs     = ButtonDefs(_cfg.Buttons);
            if (_cfg.RightToLeft) Array.Reverse(defs);
            var focusIdx = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
            if (_cfg.RightToLeft) focusIdx = defs.Length - 1 - focusIdx;
            if (focusIdx < defs.Length)
                _countdownBtn.Text = (_cfg.CustomLabels != null && focusIdx < _cfg.CustomLabels.Length)
                    ? _cfg.CustomLabels[focusIdx]
                    : defs[focusIdx].label;
        }

        if (_countTimer == null) return;
        _countTimer.Stop();
        _countTimer.Dispose();
        _countTimer = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Fade / slide animation
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_cfg.Animation == GlassAnimation.None)
            Opacity = _targetOpacity;
        else
        {
            Opacity    = 0.0;
            _fadeStep  = 0;
            _fadingOut = false;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Multi-monitor centering — target the screen that contains the owner
        var screen = Owner != null
            ? Screen.FromHandle(Owner.Handle)
            : Screen.FromPoint(Cursor.Position);

        var wa      = screen.WorkingArea;
        var centerX = wa.Left + (wa.Width  - Width)  / 2;
        var centerY = wa.Top  + (wa.Height - Height) / 2;

        _slideFinal = new Point(centerX, centerY);

        if (_cfg.Animation == GlassAnimation.SlideDown)
        {
            _slideOrigin = new Point(centerX, centerY - Scale(28));
            Location     = _slideOrigin;
            _slideActive = true;
        }
        else
        {
            Location = _slideFinal;
        }

        if (_cfg.Animation != GlassAnimation.None)
            StartFadeTimer();

        if (_cfg.AutoCloseMs > 0)
            StartCountdown();
    }

    private void BeginClose(DialogResult result)
    {
        if (_fadingOut) return;

        StopCountdown();
        _pendingResult = result;
        _fadingOut     = true;

        if (_cfg.Animation == GlassAnimation.None)
        {
            DialogResult = _pendingResult;
            return;
        }

        var ratio   = _targetOpacity > 0 ? Opacity / _targetOpacity : 0.0;
        _fadeStep   = (int)((1.0 - ratio) * _fadeTicks);

        if (_cfg.Animation == GlassAnimation.SlideDown)
        {
            _slideOrigin = Location;
            _slideFinal  = new Point(Location.X, Location.Y + Scale(15));
            _slideActive = true;
        }

        DisposeFadeTimer();
        StartFadeTimer();
    }

    private void StartFadeTimer()
    {
        _fadeTimer      = new System.Windows.Forms.Timer { Interval = 16 };
        _fadeTimer.Tick += OnFadeTick;
        _fadeTimer.Start();
    }

    private void DisposeFadeTimer()
    {
        if (_fadeTimer == null) return;
        _fadeTimer.Stop();
        _fadeTimer.Dispose();
        _fadeTimer = null;
    }

    private void OnFadeTick(object sender, EventArgs e)
    {
        _fadeStep++;
        double ratio;

        if (_fadingOut)
        {
            ratio   = Math.Max(0.0, 1.0 - (double)_fadeStep / _fadeTicks);
            Opacity = _targetOpacity * ratio;

            if (_slideActive && _cfg.Animation == GlassAnimation.SlideDown)
                Location = new Point(
                    _slideFinal.X,
                    _slideOrigin.Y + (int)((1.0 - ratio) * (_slideFinal.Y - _slideOrigin.Y)));

            if (_fadeStep >= _fadeTicks)
            {
                DisposeFadeTimer();
                DialogResult = _pendingResult;
            }
        }
        else
        {
            ratio   = Math.Min(1.0, (double)_fadeStep / _fadeTicks);
            Opacity = _targetOpacity * ratio;

            if (_slideActive && _cfg.Animation == GlassAnimation.SlideDown)
                Location = new Point(
                    _slideFinal.X,
                    _slideOrigin.Y + (int)(ratio * (_slideFinal.Y - _slideOrigin.Y)));

            if (_fadeStep >= _fadeTicks)
            {
                Opacity = _targetOpacity;
                DisposeFadeTimer();
                if (_slideActive) { Location = _slideFinal; _slideActive = false; }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DPI change
    // ═══════════════════════════════════════════════════════════════════════
    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == _wmDpiChanged)
        {
            _scale = (m.WParam.ToInt32() & 0xFFFF) / 96f;
            Rebuild();
        }
        base.WndProc(ref m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Mica / Acrylic backdrop
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (!TryApplyMica())
            TryApplyAcrylic();
    }

    private bool TryApplyMica()
    {
        if (!IsMicaSupported()) return false;
        try
        {
            // Try DWMWA_SYSTEMBACKDROP_TYPE (build 22523+)
            int v = 2; // DWMSBT_MAINWINDOW
            if (DwmSetWindowAttribute(Handle, 38, ref v, sizeof(int)) == 0)
            {
                _micaEnabled   = true;
                _targetOpacity = Math.Min(_theme.Opacity, 0.90);
                return true;
            }
            // Fallback: DWMWA_MICA_EFFECT (early Win11)
            v = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref v, sizeof(int)) == 0)
            {
                _micaEnabled   = true;
                _targetOpacity = Math.Min(_theme.Opacity, 0.90);
                return true;
            }
        }
        catch { /* fall through to acrylic */ }
        return false;
    }

    private void TryApplyAcrylic()
    {
        if (!IsAcrylicSupported()) return;
        try
        {
            var c    = _theme.BackgroundTop;
            var tint = ((uint)0xC0 << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;

            var accent = new AccentPolicy { AccentState = 4, GradientColor = tint };
            var sz     = Marshal.SizeOf(typeof(AccentPolicy));
            var ptr    = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttribData
                    { Attribute = 19, Data = ptr, SizeOfData = sz };
                if (SetWindowCompositionAttribute(Handle, ref data))
                {
                    _acrylicEnabled = true;
                    _targetOpacity  = Math.Min(_theme.Opacity, 0.85);
                }
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch { /* silent fallback to solid gradient */ }
    }

    private static bool IsMicaSupported()
    {
        var v = Environment.OSVersion.Version;
        return v.Major == 10 && v.Build >= 22000;
    }

    private static bool IsAcrylicSupported()
    {
        var v = Environment.OSVersion.Version;
        return v.Major == 10 && v.Build >= 17134;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GDI+ resource cache
    // ═══════════════════════════════════════════════════════════════════════
    private void InvalidateCache()
    {
        _bgPath?.Dispose();     _bgPath     = null;
        _bgBrush?.Dispose();    _bgBrush    = null;
        _titleBrush?.Dispose(); _titleBrush = null;
        _borderPath?.Dispose(); _borderPath = null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateCache();
        Invalidate();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Painting
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        SetQuality(g);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var r = _theme.CornerRadius;

        // ── Background ────────────────────────────────────────────────────
        if (!_acrylicEnabled && !_micaEnabled)
        {
            _bgPath  ??= RoundRect(new Rectangle(0, 0, w, h), r);
            _bgBrush ??= new LinearGradientBrush(new Rectangle(0, 0, w, h),
                _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical);
            g.FillPath(_bgBrush, _bgPath);
        }

        // ── Title bar gradient ────────────────────────────────────────────
        var titleRect = new Rectangle(0, 0, w, TitleH);
        _titleBrush ??= new LinearGradientBrush(
            titleRect, _theme.TitleBarTop, _theme.TitleBarBottom, LinearGradientMode.Vertical);
        g.FillRectangle(_titleBrush, titleRect);

        // ── Top-edge gloss ────────────────────────────────────────────────
        g.DrawLine(_glossPen, r + 1, 1, w - r - 2, 1);

        // ── Title / body separator ────────────────────────────────────────
        g.DrawLine(_sepPen, 0, TitleH - 1, w, TitleH - 1);

        // ── Border glow + crisp edge ──────────────────────────────────────
        var borderRect = new Rectangle(0, 0, w - 1, h - 1);
        _borderPath ??= RoundRect(borderRect, r);
        g.DrawPath(_glowPen, _borderPath);
        g.DrawPath(_edgePen, _borderPath);

        // ── Button panel separator ────────────────────────────────────────
        var sepY = h - BtnPanelH;
        g.DrawLine(_panelSepPen, Pad, sepY, w - Pad, sepY);

        // ── Title text ────────────────────────────────────────────────────
        if (_cfg.Title.Length > 0)
        {
            var titleFlags = TextFormatFlags.VerticalCenter |
                             TextFormatFlags.SingleLine;
            titleFlags |= _cfg.RightToLeft
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                : TextFormatFlags.Left;

            TextRenderer.DrawText(g, _cfg.Title, _theme.TitleFont,
                new Rectangle(Pad, 0, w - Pad * 2, TitleH),
                _theme.TitleColor, titleFlags);
        }

        // ── Countdown ring (arc in button panel) ──────────────────────────
        if (_cfg.AutoCloseMs > 0 && _countTimer != null)
        {
            var ratio   = (float)_countRemaining / _cfg.AutoCloseMs;
            var arcD    = BtnH - Scale(6);
            var arcX    = w - Pad - arcD;
            var arcY    = h - BtnPanelH + (BtnPanelH - arcD) / 2;
            using var trackPen = new Pen(Color.FromArgb(40, _theme.BorderColor), Scale(2));
            using var fillPen  = new Pen(_theme.AccentColor, Scale(2));
            g.DrawArc(trackPen, arcX, arcY, arcD, arcD, 0, 360);
            if (ratio > 0f)
                g.DrawArc(fillPen, arcX, arcY, arcD, arcD, -90, -(int)(360 * ratio));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════
    private static Bitmap ResolveSystemIcon(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Information => SystemIcons.Information.ToBitmap(),
            MessageBoxIcon.Question    => SystemIcons.Question.ToBitmap(),
            MessageBoxIcon.Warning     => SystemIcons.Warning.ToBitmap(),
            MessageBoxIcon.Error       => SystemIcons.Error.ToBitmap(),
            _                          => null,
        };
    }

    private static (string label, DialogResult result)[] ButtonDefs(MessageBoxButtons btns)
    {
        return btns switch
        {
            MessageBoxButtons.OK              => [("&OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel        => [("&OK", DialogResult.OK), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.YesNo           => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No)],
            MessageBoxButtons.YesNoCancel     => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.RetryCancel     => [("&Retry", DialogResult.Retry), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.AbortRetryIgnore => [("&Abort", DialogResult.Abort), ("&Retry", DialogResult.Retry), ("&Ignore", DialogResult.Ignore)],
            _                                  => [("&OK", DialogResult.OK)],
        };
    }

    private static int DefaultIndex(MessageBoxButtons btns, MessageBoxDefaultButton def)
    {
        var max = ButtonDefs(btns).Length - 1;
        return def switch
        {
            MessageBoxDefaultButton.Button2 => Math.Min(1, max),
            MessageBoxDefaultButton.Button3 => Math.Min(2, max),
            _                               => 0,
        };
    }

    private static DialogResult DefaultResult(MessageBoxButtons btns, MessageBoxDefaultButton def)
    {
        var defs = ButtonDefs(btns);
        var idx  = Math.Min(DefaultIndex(btns, def), defs.Length - 1);
        return defs[idx].result;
    }

    private static DialogResult EscapeResult(MessageBoxButtons btns)
    {
        return btns switch
        {
            MessageBoxButtons.OK               => DialogResult.OK,
            MessageBoxButtons.OKCancel         => DialogResult.Cancel,
            MessageBoxButtons.YesNo            => DialogResult.No,
            MessageBoxButtons.YesNoCancel      => DialogResult.Cancel,
            MessageBoxButtons.RetryCancel      => DialogResult.Cancel,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
            _                                  => DialogResult.Cancel,
        };
    }

    internal static void SetQuality(Graphics g)
    {
        g.CompositingMode    = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.TextRenderingHint  = TextRenderingHint.ClearTypeGridFit;
    }

    internal static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d    = Math.Max(1, radius * 2);
        path.AddArc(rect.Left,          rect.Top,          d, d, 180, 90);
        path.AddArc(rect.Right - d,     rect.Top,          d, d, 270, 90);
        path.AddArc(rect.Right - d,     rect.Bottom - d,   d, d,   0, 90);
        path.AddArc(rect.Left,          rect.Bottom - d,   d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Nested: themed progress bar panel
    // ═══════════════════════════════════════════════════════════════════════
    private sealed class GlassProgressPanel : Control
    {
        private readonly GlassTheme _theme;
        private readonly int  _value;  // -1 = indeterminate, 0..max = determinate
        private readonly int  _max;
        private float         _phase; // continuously increasing angle (radians)
        private System.Windows.Forms.Timer _ticker;
        private GraphicsPath  _trackPath;
        private Size          _trackSize;

        public GlassProgressPanel(GlassTheme theme, int value, int max)
        {
            _theme = theme;
            _value = value;
            _max   = Math.Max(1, max);

            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint, true);

            if (_value == -1)
            {
                // 16 ms ≈ 60 fps; 0.045 rad/tick → full back-and-forth in ~2.2 s
                _ticker = new System.Windows.Forms.Timer { Interval = 16 };
                _ticker.Tick += (s, e) =>
                {
                    _phase = (_phase + 0.045f) % (float)(Math.PI * 2.0);
                    Invalidate();
                };
                _ticker.Start();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            _trackPath?.Dispose();
            _trackPath = null;
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g    = e.Graphics;
            SetQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var r    = Height / 2;

            // Track (cached — no GDI+ alloc per frame)
            if (_trackPath == null || _trackSize != Size)
            {
                _trackPath?.Dispose();
                _trackSize = Size;
                _trackPath = RoundRect(rect, r);
            }

            using (var bgBrush = new SolidBrush(Color.FromArgb(30, _theme.AccentColor)))
                g.FillPath(bgBrush, _trackPath);

            if (_value == -1) // ── Indeterminate ────────────────────────────
            {
                var t     = (float)((1.0 + Math.Sin(_phase - Math.PI / 2.0)) / 2.0);
                var fw    = Math.Max(Height * 2, Width / 3);
                var fx    = (int)(t * (Width - fw));
                var fRect = new Rectangle(fx, 0, fw, Height - 1);

                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(
                        new Rectangle(fRect.X, fRect.Y,
                                      Math.Max(1, fRect.Width),
                                      Math.Max(1, fRect.Height)),
                        Color.FromArgb(80, _theme.AccentColor),
                        _theme.AccentColor,
                        LinearGradientMode.Horizontal);
                    fb.SetBlendTriangularShape(0.5f, 1.0f);
                    g.SetClip(_trackPath);
                    g.FillPath(fb, fp);
                    g.ResetClip();
                }
            }
            else // ── Determinate ───────────────────────────────────────────
            {
                var fw    = Math.Max(r * 2, (int)((float)_value / _max * (Width - 1)));
                var fRect = new Rectangle(0, 0, fw, Height - 1);
                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(fRect,
                        _theme.AccentColor, _theme.BorderColor, 0f);
                    g.SetClip(_trackPath);
                    g.FillPath(fb, fp);

                    // Top-half shine highlight
                    if (fw > 4)
                    {
                        var shineH    = Math.Max(1, (Height - 1) / 2);
                        var shineRect = new Rectangle(0, 0, fw, shineH);
                        using var shine = new LinearGradientBrush(
                            new Rectangle(0, 0, Math.Max(1, fw), Math.Max(1, shineH)),
                            Color.FromArgb(70, 255, 255, 255),
                            Color.FromArgb(0,  255, 255, 255),
                            LinearGradientMode.Vertical);
                        g.FillRectangle(shine, shineRect);
                    }
                    g.ResetClip();
                }
            }

            // Border
            using var pen = new Pen(Color.FromArgb(70, _theme.BorderColor), 1f);
            g.DrawPath(pen, _trackPath);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ticker?.Stop();
                _ticker?.Dispose();
                _trackPath?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Nested: TextBox with placeholder via Win32 EM_SETCUEBANNER
    // (works on .NET Framework 4.8.1 and .NET 8 identically)
    // ═══════════════════════════════════════════════════════════════════════
    private sealed class PlaceholderTextBox : TextBox
    {
        private readonly string _placeholder;
        public PlaceholderTextBox(string placeholder) => _placeholder = placeholder;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!string.IsNullOrEmpty(_placeholder))
                SendMessage(Handle, 0x1501 /*EM_SETCUEBANNER*/, 0, _placeholder);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════════════
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeFadeTimer();
            StopCountdown();

            if (_cfg.CustomIcon == null) _iconBitmap?.Dispose();
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
