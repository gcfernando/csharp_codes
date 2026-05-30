using System;
using System.Collections.Generic;
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
    // Layout constants (unscaled pixels at 96 DPI)
    // ═══════════════════════════════════════════════════════════════════════
    private const int _titleHBase    = 40;
    private const int _btnPanelHBase = 60;
    private const int _iconSizeBase  = 36;
    private const int _padBase       = 16;
    private const int _btnWBase      = 96;
    private const int _btnHBase      = 32;
    private const int _btnGapBase    = 8;
    private const int _minFormWBase  = 360;
    private const int _minFormHBase  = 164;
    private const int _progressHBase = 10;
    private const int _inputHBase    = 34;
    private const int _inputMLHBase  = 80;
    private const int _checkHBase    = 24;
    private const int _linkHBase     = 22;
    private const int _detailHBase   = 100;
    private const int _closeBtnBase  = 20;   // close-button size

    // ═══════════════════════════════════════════════════════════════════════
    // DPI scaling
    // ═══════════════════════════════════════════════════════════════════════
    private float _scale = 1.0f;
    private int Scale(int v) => Math.Max(1, (int)(v * _scale));

    private int TitleH    => Scale(_titleHBase);
    private int BtnPanelH => Scale(_btnPanelHBase);
    private int IconSize  => Scale(_iconSizeBase);
    private int Pad       => Scale(_padBase);
    private int BtnW      => Scale(_btnWBase);
    private int BtnH      => Scale(_btnHBase);
    private int BtnGap    => Scale(_btnGapBase);
    private int MinFormW  => Scale(_minFormWBase);
    private int MinFormH  => Scale(_minFormHBase);
    private int ProgressH => Scale(_progressHBase);
    private int InputH    => Scale(_inputHBase);
    private int InputMLH  => Scale(_inputMLHBase);
    private int CheckH    => Scale(_checkHBase);
    private int LinkH     => Scale(_linkHBase);
    private int DetailH   => Scale(_detailHBase);
    private int CloseBtnSize => Scale(_closeBtnBase);

    private int _computedBtnW;
    private const int _wmDpiChanged = 0x02E0;

    // ═══════════════════════════════════════════════════════════════════════
    // Config & theme
    // ═══════════════════════════════════════════════════════════════════════
    private readonly GlassDialogConfig _cfg;
    private readonly GlassTheme        _theme;
    private readonly int _effectiveRadius;
    private readonly int _effectiveButtonRadius;

    private Bitmap _iconBitmap;  // from shared cache or caller-owned; never disposed here
    private Point  _dragOrigin;
    private bool   _dragging;
    private bool   _isExpanded;

    private int _msgLeft, _msgW, _contentH;

    // ── Close button ──────────────────────────────────────────────────────
    private Rectangle _closeBtnBounds;
    private bool      _closeHover;

    // ── Controls ──────────────────────────────────────────────────────────
    private CheckBox    _checkBoxCtrl;
    private TextBox     _inputTextBox;
    private ComboBox    _inputCombo;
    private LinkLabel   _detailToggle;
    private GlassButton _countdownBtn;
    private string      _countdownBaseLabel = string.Empty;   // cached: avoids recomputing per tick
    private Font        _detailFont;

    internal bool   CheckBoxChecked => _checkBoxCtrl?.Checked ?? false;
    internal string InputText       => _inputTextBox?.Text ?? _inputCombo?.Text ?? string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // Fade / slide / scale animation
    // ═══════════════════════════════════════════════════════════════════════
    private System.Windows.Forms.Timer _fadeTimer;
    private double       _targetOpacity;
    private bool         _fadingOut;
    private DialogResult _pendingResult;

    // Time-based animation: progress is derived from elapsed wall-clock time, so the duration
    // stays constant and the motion stays smooth even when the WinForms timer ticks unevenly.
    private readonly System.Diagnostics.Stopwatch _animClock = new();
    private const int _animDurationMs = 170;
    private double    _closeFromAppear = 1.0;   // visibility at the moment a fade-out begins

    private Point _slideFinal, _slideOrigin;
    private bool  _slideActive;

    private Size  _scaleFinalSize;
    private Point _scaleFinalLoc;
    private bool  _scaleActive;

    // ── Smoothstep easing (#6) ────────────────────────────────────────────
    private static double Ease(double t) => t * t * (3.0 - 2.0 * t);

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown
    // ═══════════════════════════════════════════════════════════════════════
    private System.Windows.Forms.Timer _countTimer;
    private int _countRemaining;

    // ═══════════════════════════════════════════════════════════════════════
    // GDI+ resource cache
    // ═══════════════════════════════════════════════════════════════════════
    private GraphicsPath        _bgPath;
    private LinearGradientBrush _bgBrush;
    private LinearGradientBrush _titleBrush;
    private GraphicsPath        _borderPath;

    private readonly Pen _glossPen;
    private readonly Pen _sepPen;
    private readonly Pen _glowPen;
    private readonly Pen _edgePen;
    private readonly Pen _panelSepPen;

    // ═══════════════════════════════════════════════════════════════════════
    // Win32 P-Invokes (only what is actually used)
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
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, uint wParam, string lParam);

    private bool _acrylicEnabled;
    private bool _micaEnabled;
    private bool _dwmRounded;   // true → DWM renders smooth native corners; skip the GDI region clip

    // DWM constants for the modern Windows 11 window corner preference.
    private const int _dwmwaWindowCornerPreference = 33;
    private const int _dwmwcpRound                 = 2;

    /// <summary>
    /// Asks DWM to render smooth, native rounded corners (+ shadow) for the given borderless window.
    /// Returns <c>true</c> on Windows 11+ where the attribute is honoured; the caller should then
    /// drop any manual GDI <see cref="Region"/> so the compositor owns the (anti-aliased) corners.
    /// </summary>
    internal static bool EnableModernCorners(IntPtr handle)
    {
        var v = Environment.OSVersion.Version;
        if (!(v.Major == 10 && v.Build >= 22000)) return false;   // Windows 11+ only
        try
        {
            int pref = _dwmwcpRound;
            return DwmSetWindowAttribute(handle, _dwmwaWindowCornerPreference, ref pref, sizeof(int)) == 0;
        }
        catch { return false; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Thread-safe cached system icons (#12) — Lazy ensures one-time init
    // ═══════════════════════════════════════════════════════════════════════
    private static readonly Lazy<Bitmap> _lazyInfo     = new(() => SystemIcons.Information.ToBitmap());
    private static readonly Lazy<Bitmap> _lazyQuestion = new(() => SystemIcons.Question.ToBitmap());
    private static readonly Lazy<Bitmap> _lazyWarning  = new(() => SystemIcons.Warning.ToBitmap());
    private static readonly Lazy<Bitmap> _lazyError    = new(() => SystemIcons.Error.ToBitmap());

    internal static Bitmap GetCachedSystemIcon(MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Information => _lazyInfo.Value,
        MessageBoxIcon.Question    => _lazyQuestion.Value,
        MessageBoxIcon.Warning     => _lazyWarning.Value,
        MessageBoxIcon.Error       => _lazyError.Value,
        _                          => null,
    };

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

        // #16: resolve UseRoundedCorners — null means inherit from global
        bool useRounded        = cfg.UseRoundedCorners ?? GlassMessage.UseRoundedCorners;
        _effectiveRadius       = useRounded ? _theme.CornerRadius       : 0;
        _effectiveButtonRadius = useRounded ? _theme.ButtonCornerRadius : 0;

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        _glossPen    = new Pen(Color.FromArgb(55,  255, 255, 255), 1f);
        _sepPen      = new Pen(Color.FromArgb(100, _theme.BorderColor), 1f);
        _glowPen     = new Pen(Color.FromArgb(55,  _theme.BorderColor), 3f);
        _edgePen     = new Pen(Color.FromArgb(190, _theme.BorderColor), 1f);
        _panelSepPen = new Pen(Color.FromArgb(45,  _theme.BorderColor), 1f);

        Build();
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; } // CS_DROPSHADOW
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
        StartPosition   = FormStartPosition.Manual;
        Opacity         = _cfg.Animation == GlassAnimation.None ? _targetOpacity : 0.0;
        Font            = _theme.MessageFont;
        BackColor       = _theme.BackgroundBottom;
        KeyPreview      = true;
        RightToLeft     = _cfg.RightToLeft ? RightToLeft.Yes : RightToLeft.No;
        AccessibleName  = string.IsNullOrEmpty(_cfg.Title) ? "Dialog" : _cfg.Title;
        AccessibleRole  = AccessibleRole.Alert;

        _iconBitmap = _cfg.CustomIcon ?? GetCachedSystemIcon(_cfg.Icon);

        var (fw, fh) = MeasureForm();
        ClientSize       = new Size(fw, fh);
        _closeBtnBounds  = ComputeCloseBtnBounds(fw);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        var wa = PrimaryWorkingArea;
        Location = new Point(wa.Left + (wa.Width - fw) / 2, wa.Top + (wa.Height - fh) / 2);
        ResumeLayout(false);
    }

    // Null-safe primary work area — Screen.PrimaryScreen can be null in headless/session-0 contexts.
    private static Rectangle PrimaryWorkingArea =>
        Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

    private void Rebuild(Screen recenterOn = null)
    {
        SuspendLayout();

        // Preserve live user state across the teardown — expanding/collapsing the detail
        // panel (or a DPI change) must not discard typed input, the combo selection, or the checkbox.
        var savedInputText = _inputTextBox != null && !_inputTextBox.IsDisposed ? _inputTextBox.Text : null;
        var savedComboText = _inputCombo   != null && !_inputCombo.IsDisposed   ? _inputCombo.Text   : null;
        var savedChecked   = _checkBoxCtrl != null && !_checkBoxCtrl.IsDisposed ? (bool?)_checkBoxCtrl.Checked : null;

        _detailFont?.Dispose();
        _detailFont = null;

        foreach (Control c in Controls)
        {
            if (c is PictureBox pb) pb.Image = null;
            c.Dispose();
        }
        Controls.Clear();
        _inputTextBox = null;
        _inputCombo   = null;
        _checkBoxCtrl = null;
        _detailToggle = null;
        _countdownBtn = null;
        _countdownBaseLabel = string.Empty;
        InvalidateCache();

        _iconBitmap = _cfg.CustomIcon ?? GetCachedSystemIcon(_cfg.Icon);

        var (fw, fh) = MeasureForm();
        ClientSize      = new Size(fw, fh);
        _closeBtnBounds = ComputeCloseBtnBounds(fw);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        // Restore captured state onto the freshly built controls.
        if (savedInputText != null && _inputTextBox != null) _inputTextBox.Text = savedInputText;
        if (savedComboText != null && _inputCombo   != null) _inputCombo.Text   = savedComboText;
        if (savedChecked.HasValue && _checkBoxCtrl != null)  _checkBoxCtrl.Checked = savedChecked.Value;

        // A DPI move re-centres on the destination monitor; a detail toggle keeps the user's
        // chosen position and only nudges the (now taller) dialog back on-screen if it overflows.
        if (recenterOn != null) CenterOn(recenterOn);
        else                    ClampToScreen(Screen.FromRectangle(Bounds));

        ResumeLayout(false);
        Invalidate();
    }

    private void CenterOn(Screen screen)
    {
        var wa = screen.WorkingArea;
        Location = new Point(
            wa.Left + (wa.Width  - Width)  / 2,
            wa.Top  + (wa.Height - Height) / 2);
    }

    private void ClampToScreen(Screen screen)
    {
        var wa = screen.WorkingArea;
        var x  = Math.Min(Math.Max(Location.X, wa.Left), Math.Max(wa.Left, wa.Right  - Width));
        var y  = Math.Min(Math.Max(Location.Y, wa.Top),  Math.Max(wa.Top,  wa.Bottom - Height));
        Location = new Point(x, y);
    }

    private Rectangle ComputeCloseBtnBounds(int fw)
    {
        var size = CloseBtnSize;
        var x    = _cfg.RightToLeft ? Pad : fw - Pad - size;
        var y    = (TitleH - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    private void ApplyRegion(int w, int h)
    {
        // When DWM owns the corners (Win11) we must NOT clip with a region — the compositor
        // already rounds the window and a 1-bit region would re-introduce aliased edges.
        if (_effectiveRadius <= 0 || _dwmRounded) { Region = null; return; }
        using var path = RoundRect(new Rectangle(0, 0, w, h), _effectiveRadius);
        Region = new Region(path);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Layout measurement (#8: single message measurement pass)
    // ═══════════════════════════════════════════════════════════════════════
    private (int w, int h) MeasureForm()
    {
        var maxW     = Math.Min((int)(PrimaryWorkingArea.Width * 0.80), Scale(720));
        var iconColW = _iconBitmap != null ? IconSize + Pad : 0;
        var textMaxW = maxW - Pad * 2 - iconColW;

        // Button widths
        var defs       = ButtonDefs(_cfg.Buttons);
        var defIdx     = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        var maxLabelPx = 0;
        for (var i = 0; i < defs.Length; i++)
        {
            var lbl = (_cfg.CustomLabels != null && i < _cfg.CustomLabels.Length)
                ? _cfg.CustomLabels[i] : defs[i].label;
            if (_cfg.AutoCloseMs > 0 && i == defIdx) lbl += $" ({_cfg.AutoCloseMs / 1000}s)";
            maxLabelPx = Math.Max(maxLabelPx, TextRenderer.MeasureText(lbl, _theme.ButtonFont).Width);
        }
        _computedBtnW = Math.Max(BtnW, maxLabelPx + Scale(28));
        var btnMinW   = defs.Length * _computedBtnW + (defs.Length - 1) * BtnGap + Pad * 2;

        // Title needs: text width + padding + close-button margin
        int titleNeedW = 0;
        if (_cfg.Title.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Title, _theme.TitleFont);
            titleNeedW = sz.Width + Pad * 2 + Scale(24);   // #11: no iconColW here
        }

        // Message — single pass (#8): sz.Height is reused directly
        int msgNeedW = 0, msgH = 0;
        if (_cfg.Message.Length > 0)
        {
            var sz   = TextRenderer.MeasureText(_cfg.Message, _theme.MessageFont,
                           new Size(textMaxW, int.MaxValue),
                           TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgNeedW = sz.Width + iconColW + Pad * 2;
            msgH     = sz.Height;
        }

        var w = Math.Max(Math.Max(Math.Max(titleNeedW, msgNeedW), MinFormW), btnMinW);
        if (_cfg.HasInput || _cfg.HasDetail) w = Math.Max(w, Scale(380));

        _msgLeft  = _cfg.RightToLeft ? Pad : Pad + iconColW;
        _msgW     = w - Pad * 2 - iconColW;
        _contentH = Math.Max(msgH, _iconBitmap != null ? IconSize : 0);

        var h = TitleH + Pad + _contentH;
        if (_cfg.HasProgress)   h += Pad + ProgressH;
        if (_cfg.HasInput)      h += Pad + (_cfg.InputMode == GlassInputMode.Multiline ? InputMLH : InputH);
        if (_cfg.HasCheckBox)   h += Scale(8) + CheckH;
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

        if (_iconBitmap != null)
        {
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

        if (_cfg.Message.Length > 0)
        {
            Controls.Add(new Label
            {
                Text                       = _cfg.Message,
                Font                       = _theme.MessageFont,
                ForeColor                  = _theme.MessageColor,
                BackColor                  = Color.Transparent,
                AutoSize                   = false,
                UseMnemonic                = false,
                UseCompatibleTextRendering = false,
                Bounds                     = new Rectangle(_msgLeft, y, _msgW, _contentH),
                TextAlign                  = _cfg.RightToLeft ? ContentAlignment.TopRight : ContentAlignment.TopLeft,
                AccessibleRole             = AccessibleRole.StaticText,
            });
        }

        y += _contentH;

        if (_cfg.HasProgress)
        {
            y += Pad;
            Controls.Add(new GlassProgressPanel(_theme, _cfg.ProgressValue, _cfg.ProgressMax)
            {
                Bounds         = new Rectangle(Pad, y, fw - Pad * 2, ProgressH),
                AccessibleName = "Progress",
                AccessibleRole = AccessibleRole.ProgressBar,
            });
            y += ProgressH;
        }

        if (_cfg.HasInput)
        {
            y += Pad;
            if (_cfg.InputMode == GlassInputMode.Dropdown)
            {
                _inputCombo = new ComboBox
                {
                    Bounds         = new Rectangle(Pad, y, fw - Pad * 2, InputH),
                    DropDownStyle  = ComboBoxStyle.DropDownList,
                    Font           = _theme.MessageFont,
                    BackColor      = _theme.InputBackColor,
                    ForeColor      = _theme.InputForeColor,
                    FlatStyle      = FlatStyle.Flat,
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
                var multiline = _cfg.InputMode == GlassInputMode.Multiline;
                var inputH2   = multiline ? InputMLH : InputH;
                var boxW      = fw - Pad * 2 - 6;
                _inputTextBox = new PlaceholderTextBox(_cfg.InputPlaceholder ?? string.Empty)
                {
                    Font           = _theme.MessageFont,
                    BackColor      = _theme.InputBackColor,
                    ForeColor      = _theme.InputForeColor,
                    BorderStyle    = BorderStyle.None,
                    Multiline      = multiline,
                    ScrollBars     = multiline ? ScrollBars.Vertical : ScrollBars.None,
                    PasswordChar   = _cfg.InputMode == GlassInputMode.Password ? '●' : '\0',
                    Text           = _cfg.InputDefault ?? string.Empty,
                    TextAlign      = _cfg.RightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    AccessibleName = "Input",
                    AccessibleRole = AccessibleRole.Text,
                };
                if (multiline)
                {
                    _inputTextBox.SetBounds(Pad + 3, y + 3, boxW, inputH2 - 6);
                }
                else
                {
                    // A single-line TextBox sizes its own height to the font, so explicitly centre
                    // it within the input band — left-aligned text sitting in the vertical middle.
                    var th = _inputTextBox.PreferredHeight;
                    _inputTextBox.SetBounds(Pad + 3, y + (inputH2 - th) / 2, boxW, th);
                }
                Controls.Add(_inputTextBox);
                y += inputH2;
            }
        }

        if (_cfg.HasCheckBox)
        {
            y += Scale(8);
            _checkBoxCtrl = new GlassCheckBox(_theme, _scale, _cfg.RightToLeft)
            {
                Text           = _cfg.CheckBoxLabel,
                Font           = _theme.MessageFont,
                Checked        = _cfg.CheckBoxDefault,
                Location       = new Point(_msgLeft, y),
                AccessibleRole = AccessibleRole.CheckButton,
            };
            Controls.Add(_checkBoxCtrl);
            y += CheckH;
        }

        if (_cfg.HasDetail)
        {
            y += Scale(8);
            _detailToggle = new LinkLabel
            {
                Text            = _isExpanded ? "Hide details ▲" : "Show details ▼",
                Font            = _theme.ButtonFont,
                ForeColor       = _theme.AccentColor,
                LinkColor       = _theme.AccentColor,
                ActiveLinkColor = _theme.BorderColor,
                BackColor       = Color.Transparent,
                AutoSize        = true,
                Location        = new Point(_msgLeft, y),
                AccessibleName  = "Toggle detail panel",
            };
            _detailToggle.LinkClicked += OnDetailToggleClick;
            Controls.Add(_detailToggle);
            y += LinkH;

            if (_isExpanded)
            {
                y += Scale(6);
                _detailFont = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
                Controls.Add(new TextBox
                {
                    Text           = _cfg.DetailText,
                    Font           = _detailFont,
                    BackColor      = Color.FromArgb(8, 15, 28),
                    ForeColor      = Color.FromArgb(160, 175, 200),
                    ReadOnly       = true,
                    Multiline      = true,
                    ScrollBars     = ScrollBars.Vertical,
                    WordWrap       = true,
                    BorderStyle    = BorderStyle.None,
                    Bounds         = new Rectangle(Pad, y, fw - Pad * 2, DetailH),
                    AccessibleName = "Detail",
                    AccessibleRole = AccessibleRole.Text,
                });
                y += DetailH;
            }
        }

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

            var btn = new GlassButton(_theme, _effectiveButtonRadius)
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
                AcceptButton  = btn;   // #1: Enter key activates default button
                if (_cfg.AutoCloseMs > 0)
                {
                    _countdownBtn       = btn;
                    _countdownBaseLabel = label;   // the default button's resolved label
                }
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
        if (e.Control && e.KeyCode == Keys.C)
        {
            var text = string.IsNullOrEmpty(_cfg.Title) ? _cfg.Message : $"{_cfg.Title}\n{_cfg.Message}";
            // Clipboard.SetText throws on empty text and can throw ExternalException when the
            // clipboard is momentarily locked by another process — both are non-fatal here.
            if (!string.IsNullOrEmpty(text))
            {
                try { Clipboard.SetText(text); }
                catch (System.Runtime.InteropServices.ExternalException) { }
                catch (System.Threading.ThreadStateException) { }
            }
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
        if (e.Button != MouseButtons.Left) return;

        // #15: close button takes priority over drag
        if (_closeBtnBounds.Contains(e.Location))
        {
            BeginClose(EscapeResult(_cfg.Buttons));
            return;
        }
        if (e.Y < TitleH)
        {
            _dragging   = true;
            _dragOrigin = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var wasHover = _closeHover;
        _closeHover  = _closeBtnBounds.Contains(e.Location);
        if (wasHover != _closeHover)
            Invalidate(Rectangle.Inflate(_closeBtnBounds, 2, 2));

        if (_dragging)
            Location = new Point(
                Location.X + e.X - _dragOrigin.X,
                Location.Y + e.Y - _dragOrigin.Y);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_closeHover)
        {
            _closeHover = false;
            Invalidate(Rectangle.Inflate(_closeBtnBounds, 2, 2));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _dragging = false; }

    private void OnDetailToggleClick(object sender, LinkLabelLinkClickedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        Rebuild();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Countdown (#9: DRY helper, #7: region-only repaint)
    // ═══════════════════════════════════════════════════════════════════════
    private void StartCountdown()
    {
        _countRemaining = _cfg.AutoCloseMs;
        UpdateCountdownLabel();
        _countTimer       = new System.Windows.Forms.Timer { Interval = 1000 };
        _countTimer.Tick += OnCountdownTick;
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
            InvalidateCountdownArc();  // #7: repaint only the arc, not the whole form
        }
    }

    // #7: invalidate only the countdown arc region
    private void InvalidateCountdownArc()
    {
        var w    = ClientSize.Width;
        var h    = ClientSize.Height;
        var arcD = BtnH - Scale(6);
        var arcX = w - Pad - arcD;
        var arcY = h - BtnPanelH + (BtnPanelH - arcD) / 2;
        var slop = Scale(3);
        Invalidate(new Rectangle(arcX - slop, arcY - slop, arcD + slop * 2, arcD + slop * 2));
        _countdownBtn?.Invalidate();
    }

    private void UpdateCountdownLabel()
    {
        if (_countdownBtn == null) return;
        var seconds = Math.Max(0, _countRemaining / 1000);
        _countdownBtn.Text = seconds > 0
            ? $"{_countdownBaseLabel} ({seconds}s)"
            : _countdownBaseLabel;
    }

    private void StopCountdown()
    {
        if (_countdownBtn != null)
            _countdownBtn.Text = _countdownBaseLabel;

        if (_countTimer == null) return;
        _countTimer.Stop();
        _countTimer.Dispose();
        _countTimer = null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Fade / slide / scale animation (#3 scale, #6 smoothstep)
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_cfg.Animation == GlassAnimation.None)
        {
            Opacity = _targetOpacity;
            return;
        }

        Opacity    = 0.0;
        _fadingOut = false;
        // Position/size the window at its entrance state BEFORE the first paint. Doing this here
        // (rather than in OnShown, after the window is already visible) avoids a one-frame white
        // flash on the Scale animation where the window was briefly shown at full size.
        SetupEntranceAnimation();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_cfg.Animation != GlassAnimation.None)
            StartFadeTimer();

        if (_cfg.AutoCloseMs > 0)
            StartCountdown();
    }

    private void SetupEntranceAnimation()
    {
        var screen  = Owner != null ? Screen.FromHandle(Owner.Handle) : Screen.FromPoint(Cursor.Position);
        var wa      = screen.WorkingArea;
        var centerX = wa.Left + (wa.Width  - Width)  / 2;
        var centerY = wa.Top  + (wa.Height - Height) / 2;
        _slideFinal = new Point(centerX, centerY);

        switch (_cfg.Animation)
        {
            case GlassAnimation.SlideDown:
                _slideOrigin = new Point(centerX, centerY - Scale(28));
                Location     = _slideOrigin;
                _slideActive = true;
                break;

            case GlassAnimation.Scale:   // #3: actual scale implementation
                _scaleFinalSize = new Size(Width, Height);
                _scaleFinalLoc  = _slideFinal;
                _scaleActive    = true;
                var sw0 = (int)(_scaleFinalSize.Width  * 0.90f);
                var sh0 = (int)(_scaleFinalSize.Height * 0.90f);
                SuspendLayout();
                SetBounds(
                    centerX + (_scaleFinalSize.Width  - sw0) / 2,
                    centerY + (_scaleFinalSize.Height - sh0) / 2,
                    sw0, sh0);
                ResumeLayout(false);
                break;

            default:
                Location = _slideFinal;
                break;
        }
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

        // Continue the fade-out from however visible the dialog currently is (handles a close
        // requested mid-entrance) so there is no opacity pop.
        _closeFromAppear = _targetOpacity > 0 ? Math.Min(1.0, Opacity / _targetOpacity) : 0.0;

        if (_cfg.Animation == GlassAnimation.SlideDown)
        {
            _slideOrigin = Location;
            _slideFinal  = new Point(Location.X, Location.Y + Scale(15));
            _slideActive = true;
        }
        else if (_cfg.Animation == GlassAnimation.Scale)
        {
            _scaleFinalSize = new Size(Width, Height);
            _scaleFinalLoc  = Location;
            _scaleActive    = true;
        }

        DisposeFadeTimer();
        StartFadeTimer();
    }

    private void StartFadeTimer()
    {
        _animClock.Restart();
        _fadeTimer      = new System.Windows.Forms.Timer { Interval = 15 };
        _fadeTimer.Tick += OnFadeTick;
        _fadeTimer.Start();
    }

    private void DisposeFadeTimer()
    {
        if (_fadeTimer == null) return;
        _fadeTimer.Stop();
        _fadeTimer.Dispose();
        _fadeTimer = null;
        _animClock.Stop();
    }

    private void OnFadeTick(object sender, EventArgs e)
    {
        var t    = Math.Min(1.0, _animClock.Elapsed.TotalMilliseconds / _animDurationMs);
        var done = t >= 1.0;

        // appear   = how visible the dialog is (drives opacity + scale)
        // slideDisp = 0→1 progress along the slide path, regardless of direction
        double appear, slideDisp;
        if (_fadingOut)
        {
            var eased = Ease(1.0 - t);
            appear    = _closeFromAppear * eased;
            slideDisp = 1.0 - eased;
        }
        else
        {
            var eased = Ease(t);
            appear    = eased;
            slideDisp = eased;
        }

        ApplyAnimationFrame(appear, slideDisp);

        if (!done) return;

        DisposeFadeTimer();
        if (_fadingOut)
        {
            _scaleActive = false;
            DialogResult = _pendingResult;
        }
        else
        {
            Opacity = _targetOpacity;
            if (_slideActive) { Location = _slideFinal; _slideActive = false; }
            if (_scaleActive)
            {
                SuspendLayout();
                SetBounds(_scaleFinalLoc.X, _scaleFinalLoc.Y, _scaleFinalSize.Width, _scaleFinalSize.Height);
                ResumeLayout(false);
                _scaleActive = false;
            }
        }
    }

    /// <summary>Applies a single animation frame shared by fade-in and fade-out.</summary>
    private void ApplyAnimationFrame(double appear, double slideDisp)
    {
        Opacity = _targetOpacity * appear;

        if (_slideActive && _cfg.Animation == GlassAnimation.SlideDown)
            Location = new Point(_slideFinal.X,
                _slideOrigin.Y + (int)(slideDisp * (_slideFinal.Y - _slideOrigin.Y)));

        if (_scaleActive && _cfg.Animation == GlassAnimation.Scale)
        {
            var sf  = 0.90f + 0.10f * (float)appear;
            var nsw = (int)(_scaleFinalSize.Width  * sf);
            var nsh = (int)(_scaleFinalSize.Height * sf);
            SuspendLayout();
            SetBounds(_scaleFinalLoc.X + (_scaleFinalSize.Width  - nsw) / 2,
                      _scaleFinalLoc.Y + (_scaleFinalSize.Height - nsh) / 2, nsw, nsh);
            ResumeLayout(false);
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

            // lParam carries the suggested window rectangle on the new monitor; centre the
            // re-measured dialog on that monitor so a cross-DPI move lands on the right screen.
            Screen target = null;
            if (m.LParam != IntPtr.Zero)
            {
                var r = Marshal.PtrToStructure<RECT>(m.LParam);
                target = Screen.FromRectangle(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            }
            Rebuild(target);
        }
        base.WndProc(ref m);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Mica / Acrylic
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!TryApplyMica()) TryApplyAcrylic();

        // Prefer native DWM corner rounding over the aliased 1-bit GDI region.
        if (_effectiveRadius > 0 && EnableModernCorners(Handle))
        {
            _dwmRounded = true;
            Region = null;     // DWM now masks the corners smoothly
            Invalidate();
        }
    }

    private bool TryApplyMica()
    {
        if (Environment.OSVersion.Version is var v && !(v.Major == 10 && v.Build >= 22000))
            return false;
        try
        {
            int val = 2;
            if (DwmSetWindowAttribute(Handle, 38, ref val, sizeof(int)) == 0)
                { _micaEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.90); return true; }
            val = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref val, sizeof(int)) == 0)
                { _micaEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.90); return true; }
        }
        catch { }
        return false;
    }

    private void TryApplyAcrylic()
    {
        var v = Environment.OSVersion.Version;
        if (!(v.Major == 10 && v.Build >= 17134)) return;
        try
        {
            var c    = _theme.BackgroundTop;
            var tint = ((uint)0xC0 << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
            var acc  = new AccentPolicy { AccentState = 4, GradientColor = tint };
            var sz   = Marshal.SizeOf(typeof(AccentPolicy));
            var ptr  = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(acc, ptr, false);
                var data = new WindowCompositionAttribData { Attribute = 19, Data = ptr, SizeOfData = sz };
                if (SetWindowCompositionAttribute(Handle, ref data))
                    { _acrylicEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.85); }
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GDI+ cache
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
    // Painting (#15 close button)
    // ═══════════════════════════════════════════════════════════════════════
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        SetQuality(g);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        // When DWM rounds the window, paint square shapes and let the compositor clip the
        // corners — this yields perfectly anti-aliased edges with no radius mismatch.
        var r = _dwmRounded ? 0 : _effectiveRadius;

        // Background
        if (!_acrylicEnabled && !_micaEnabled)
        {
            _bgPath  ??= RoundRect(new Rectangle(0, 0, w, h), r);
            _bgBrush ??= new LinearGradientBrush(new Rectangle(0, 0, w, h),
                             _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical);
            g.FillPath(_bgBrush, _bgPath);
        }

        // Title bar
        var titleRect = new Rectangle(0, 0, w, TitleH);
        _titleBrush ??= new LinearGradientBrush(titleRect,
                            _theme.TitleBarTop, _theme.TitleBarBottom, LinearGradientMode.Vertical);
        g.FillRectangle(_titleBrush, titleRect);

        // Top-edge gloss
        g.DrawLine(_glossPen, r + 1, 1, w - r - 2, 1);

        // Title / body separator
        g.DrawLine(_sepPen, 0, TitleH - 1, w, TitleH - 1);

        // Button panel separator
        g.DrawLine(_panelSepPen, Pad, h - BtnPanelH, w - Pad, h - BtnPanelH);

        // Border glow + edge
        _borderPath ??= RoundRect(new Rectangle(0, 0, w - 1, h - 1), r);
        g.DrawPath(_glowPen, _borderPath);
        g.DrawPath(_edgePen, _borderPath);

        // Title text — leave room for close button on correct side
        if (_cfg.Title.Length > 0)
        {
            var cb        = _closeBtnBounds;
            var closeSz   = cb.Width + Scale(4);
            var textLeft  = _cfg.RightToLeft ? (Pad + closeSz)   : Pad;
            var textRight = _cfg.RightToLeft ? (w - Pad)         : (w - Pad - closeSz);
            var flags     = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                          | TextFormatFlags.EndEllipsis;
            flags |= _cfg.RightToLeft
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                : TextFormatFlags.Left;
            TextRenderer.DrawText(g, _cfg.Title, _theme.TitleFont,
                new Rectangle(textLeft, 0, Math.Max(0, textRight - textLeft), TitleH),
                _theme.TitleColor, flags);
        }

        // #15: Close button
        {
            var cb = _closeBtnBounds;
            if (_closeHover)
            {
                using var hoverFill = new SolidBrush(Color.FromArgb(55, 220, 50, 50));
                g.FillEllipse(hoverFill, cb);
            }
            var margin = Scale(5);
            using var xPen = new Pen(
                Color.FromArgb(_closeHover ? 220 : 130, _theme.TitleColor),
                Math.Max(1f, _scale * 1.2f));
            g.DrawLine(xPen, cb.X + margin, cb.Y + margin, cb.Right - margin - 1, cb.Bottom - margin - 1);
            g.DrawLine(xPen, cb.Right - margin - 1, cb.Y + margin, cb.X + margin, cb.Bottom - margin - 1);
        }

        // Countdown arc
        if (_cfg.AutoCloseMs > 0 && _countTimer != null)
        {
            var ratio = (float)_countRemaining / _cfg.AutoCloseMs;
            var arcD  = BtnH - Scale(6);
            var arcX  = w - Pad - arcD;
            var arcY  = h - BtnPanelH + (BtnPanelH - arcD) / 2;
            using var trackPen = new Pen(Color.FromArgb(40, _theme.BorderColor), Scale(2));
            using var fillPen  = new Pen(_theme.AccentColor, Scale(2));
            g.DrawArc(trackPen, arcX, arcY, arcD, arcD, 0, 360);
            if (ratio > 0f)
                g.DrawArc(fillPen, arcX, arcY, arcD, arcD, -90, -(int)(360 * ratio));
        }

        // Input borders
        PaintInputBorders(g);
    }

    private void PaintInputBorders(Graphics g)
    {
        using var borderPen = new Pen(Color.FromArgb(70, _theme.BorderColor), 1f);

        if (_inputTextBox != null && !_inputTextBox.IsDisposed)
        {
            var b = new Rectangle(Pad, _inputTextBox.Top - 3,
                                   ClientSize.Width - Pad * 2, _inputTextBox.Height + 6);
            if (_effectiveRadius > 0) { using var p = RoundRect(b, 3); g.DrawPath(borderPen, p); }
            else g.DrawRectangle(borderPen, b);
        }

        if (_inputCombo != null && !_inputCombo.IsDisposed)
        {
            var b = _inputCombo.Bounds; b.Inflate(1, 1);
            if (_effectiveRadius > 0) { using var p = RoundRect(b, 3); g.DrawPath(borderPen, p); }
            else g.DrawRectangle(borderPen, b);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Static helpers
    // ═══════════════════════════════════════════════════════════════════════
    private static (string label, DialogResult result)[] ButtonDefs(MessageBoxButtons btns)
    {
        return btns switch
        {
            MessageBoxButtons.OK               => [("&OK", DialogResult.OK)],
            MessageBoxButtons.OKCancel         => [("&OK", DialogResult.OK), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.YesNo            => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No)],
            MessageBoxButtons.YesNoCancel      => [("&Yes", DialogResult.Yes), ("&No", DialogResult.No), ("&Cancel", DialogResult.Cancel)],
            MessageBoxButtons.RetryCancel      => [("&Retry", DialogResult.Retry), ("&Cancel", DialogResult.Cancel)],
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
        return defs[Math.Min(DefaultIndex(btns, def), defs.Length - 1)].result;
    }

    private static DialogResult EscapeResult(MessageBoxButtons btns) => btns switch
    {
        MessageBoxButtons.OK               => DialogResult.OK,
        MessageBoxButtons.OKCancel         => DialogResult.Cancel,
        MessageBoxButtons.YesNo            => DialogResult.No,
        MessageBoxButtons.YesNoCancel      => DialogResult.Cancel,
        MessageBoxButtons.RetryCancel      => DialogResult.Cancel,
        MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
        _                                  => DialogResult.Cancel,
    };

    /// <summary>
    /// Fills <paramref name="c"/>'s background with the slice of the dialog's vertical theme
    /// gradient that sits directly behind it. UserPaint child controls (buttons, the checkbox)
    /// must call this from <c>OnPaintBackground</c>: WinForms' default transparent-background
    /// handling smears a custom-painted parent, which is what produced the ghosting / white
    /// corner artefacts. Painting the exact gradient slice is seamless and clears every frame.
    /// </summary>
    internal static void PaintThemedBackground(Graphics g, Control c, GlassTheme theme)
    {
        var ph = c.Parent?.Height ?? c.Height;
        if (ph < 1) ph = 1;
        using var brush = new LinearGradientBrush(
            new Rectangle(0, -c.Top, Math.Max(1, c.Width), ph),
            theme.BackgroundTop, theme.BackgroundBottom, LinearGradientMode.Vertical);
        g.FillRectangle(brush, c.ClientRectangle);
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

    /// <summary>
    /// Rectangle path with corner radius. radius ≤ 0 returns a plain rectangle path
    /// (pixel-perfect sharp corners, no arc artefacts).
    /// </summary>
    internal static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(rect); return path; }
        var d = radius * 2;
        path.AddArc(rect.Left,      rect.Top,          d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top,          d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d,   d, d,   0, 90);
        path.AddArc(rect.Left,      rect.Bottom - d,   d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Nested: animated progress bar
    // ═══════════════════════════════════════════════════════════════════════
    private sealed class GlassProgressPanel : Control
    {
        private readonly GlassTheme _theme;
        private readonly int _value, _max;
        private float _phase;
        private System.Windows.Forms.Timer _ticker;
        private GraphicsPath _trackPath;
        private Size         _trackSize;

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
                _ticker = new System.Windows.Forms.Timer { Interval = 16 };
                _ticker.Tick += (s, e) => { _phase = (_phase + 0.045f) % (float)(Math.PI * 2.0); Invalidate(); };
                _ticker.Start();
            }
        }

        protected override void OnResize(EventArgs e) { _trackPath?.Dispose(); _trackPath = null; base.OnResize(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g    = e.Graphics;
            SetQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var r    = Height / 2;

            if (_trackPath == null || _trackSize != Size)
            {
                _trackPath?.Dispose();
                _trackSize = Size;
                _trackPath = RoundRect(rect, r);
            }

            using (var bgBrush = new SolidBrush(Color.FromArgb(30, _theme.AccentColor)))
                g.FillPath(bgBrush, _trackPath);

            if (_value == -1)
            {
                var t     = (float)((1.0 + Math.Sin(_phase - Math.PI / 2.0)) / 2.0);
                var fw    = Math.Max(Height * 2, Width / 3);
                var fx    = (int)(t * (Width - fw));
                var fRect = new Rectangle(fx, 0, fw, Height - 1);
                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(
                        new Rectangle(fRect.X, fRect.Y, Math.Max(1, fRect.Width), Math.Max(1, fRect.Height)),
                        Color.FromArgb(80, _theme.AccentColor), _theme.AccentColor, LinearGradientMode.Horizontal);
                    fb.SetBlendTriangularShape(0.5f, 1.0f);
                    g.SetClip(_trackPath); g.FillPath(fb, fp); g.ResetClip();
                }
            }
            else
            {
                var fw    = Math.Max(r * 2, (int)((float)_value / _max * (Width - 1)));
                var fRect = new Rectangle(0, 0, fw, Height - 1);
                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(fRect, _theme.AccentColor, _theme.BorderColor, 0f);
                    g.SetClip(_trackPath); g.FillPath(fb, fp);
                    if (fw > 4)
                    {
                        var sh = Math.Max(1, (Height - 1) / 2);
                        using var shine = new LinearGradientBrush(
                            new Rectangle(0, 0, Math.Max(1, fw), Math.Max(1, sh)),
                            Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
                            LinearGradientMode.Vertical);
                        g.FillRectangle(shine, 0, 0, fw, sh);
                    }
                    g.ResetClip();
                }
            }

            using var pen = new Pen(Color.FromArgb(70, _theme.BorderColor), 1f);
            g.DrawPath(pen, _trackPath);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _ticker?.Stop(); _ticker?.Dispose(); _trackPath?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Nested: owner-drawn checkbox that honours GlassTheme.CheckBoxColor so the
    // glyph blends with the glass palette instead of the stock white Win32 box.
    // ═══════════════════════════════════════════════════════════════════════
    private sealed class GlassCheckBox : CheckBox
    {
        private readonly GlassTheme _theme;
        private readonly float      _scale;
        private readonly bool       _rtl;
        private bool _hover;

        public GlassCheckBox(GlassTheme theme, float scale, bool rtl)
        {
            _theme = theme;
            _scale = scale;
            _rtl   = rtl;
            AutoSize  = true;
            ForeColor = theme.MessageColor;
            Cursor    = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint             |
                     ControlStyles.Opaque                |   // we paint every pixel ourselves
                     ControlStyles.ResizeRedraw, true);
        }

        private int Box => Math.Max(14, (int)(15 * _scale));
        private int Gap => Math.Max(6,  (int)(8  * _scale));

        public override Size GetPreferredSize(Size proposedSize)
        {
            var ts = TextRenderer.MeasureText(Text, Font);
            return new Size(Box + Gap + ts.Width + 2, Math.Max(Box, ts.Height) + 2);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e)   { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e)  { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var g = pe.Graphics;
            SetQuality(g);

            // Opaque control: paint our own backdrop (the dialog's gradient slice) so nothing
            // ghosts and WinForms never replays the parent's content into our region.
            PaintThemedBackground(g, this, _theme);

            var box  = Box;
            var rect = new Rectangle(_rtl ? Width - box : 0, (Height - box) / 2, box - 1, box - 1);

            using (var path = RoundRect(rect, Math.Max(2, (int)(3 * _scale))))
            {
                using (var fill = new SolidBrush(Checked
                           ? _theme.CheckBoxColor
                           : Color.FromArgb(40, _theme.InputBackColor)))
                    g.FillPath(fill, path);

                using (var border = new Pen(
                           Color.FromArgb(_hover || Focused ? 230 : 150, _theme.CheckBoxColor), 1f))
                    g.DrawPath(border, path);

                if (Checked)
                {
                    using var tick = new Pen(Color.White, Math.Max(1.6f, _scale * 1.8f))
                    {
                        StartCap = LineCap.Round,
                        EndCap   = LineCap.Round,
                        LineJoin = LineJoin.Round,
                    };
                    float l = rect.Left, t = rect.Top, w = rect.Width, h = rect.Height;
                    g.DrawLines(tick, new[]
                    {
                        new PointF(l + w * 0.22f, t + h * 0.52f),
                        new PointF(l + w * 0.42f, t + h * 0.72f),
                        new PointF(l + w * 0.78f, t + h * 0.26f),
                    });
                }
            }

            var textX = _rtl ? 0 : box + Gap;
            var textW = Width - box - Gap;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                        (_rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, textW, Height), ForeColor, flags);

            if (Focused)
            {
                var tw = Math.Min(textW, TextRenderer.MeasureText(Text, Font).Width + 2);
                using var fp = new Pen(Color.FromArgb(130, _theme.AccentColor), 1f) { DashStyle = DashStyle.Dot };
                g.DrawRectangle(fp, new Rectangle(_rtl ? Width - box - Gap - tw : textX, 1, Math.Max(1, tw), Height - 3));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Nested: TextBox with EM_SETCUEBANNER placeholder
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
            _detailFont?.Dispose();
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
