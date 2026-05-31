// -----------------------------------------------------------------------------
//  Glass.Message — the heart of the library: the owner-drawn dialog window.
//  It measures and lays out its own content, paints the themed chrome, hosts the
//  optional input/checkbox/progress/detail controls, runs the open/close and
//  countdown animations, and applies Windows 11 Mica/Acrylic backdrops and
//  rounded corners where the OS supports them.
//
//  File        : GlassDialog.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Glass;

/// <summary>
/// The borderless, custom-painted form behind every Glass dialog. Internal by
/// design — callers reach it through <see cref="GlassMessage"/> or
/// <see cref="GlassBuilder"/> rather than constructing it directly.
/// </summary>
internal sealed class GlassDialog : Form
{
    // --- Base metrics, in 96-DPI pixels --------------------------------------
    // Every layout dimension starts from one of these and is run through Scale()
    // so the dialog stays crisp at any DPI.
    private const int _titleHBase = 40;
    private const int _btnPanelHBase = 60;
    private const int _iconSizeBase = 36;
    private const int _padBase = 16;
    private const int _btnWBase = 96;
    private const int _btnHBase = 32;
    private const int _btnGapBase = 8;
    private const int _minFormWBase = 360;
    private const int _minFormHBase = 164;
    private const int _progressHBase = 10;
    private const int _inputHBase = 40;
    private const int _inputMLHBase = 80;
    private const int _checkHBase = 24;
    private const int _linkHBase = 22;
    private const int _detailHBase = 100;
    private const int _closeBtnBase = 20;

    // Current DPI scale (1.0 at 96 DPI). Recomputed on WM_DPICHANGED so a dialog
    // dragged between monitors re-lays-out correctly.
    private float _scale = 1.0f;
    private int Scale(int v) => Math.Max(1, (int)(v * _scale));

    // DPI-scaled accessors used everywhere instead of the raw _*Base constants.
    private int TitleH => Scale(_titleHBase);
    private int BtnPanelH => Scale(_btnPanelHBase);
    private int IconSize => Scale(_iconSizeBase);
    private int Pad => Scale(_padBase);
    private int BtnW => Scale(_btnWBase);
    private int BtnH => Scale(_btnHBase);
    private int BtnGap => Scale(_btnGapBase);
    private int MinFormW => Scale(_minFormWBase);
    private int MinFormH => Scale(_minFormHBase);
    private int ProgressH => Scale(_progressHBase);
    private int InputH => Scale(_inputHBase);
    private int InputMLH => Scale(_inputMLHBase);
    private int CheckH => Scale(_checkHBase);
    private int LinkH => Scale(_linkHBase);
    private int DetailH => Scale(_detailHBase);
    private int CloseBtnSize => Scale(_closeBtnBase);

    // Button width chosen during measurement so every button fits its widest label.
    private int _computedBtnW;
    private const int _wmDpiChanged = 0x02E0;   // WM_DPICHANGED

    private readonly GlassDialogConfig _cfg;
    private readonly GlassTheme _theme;
    // Effective radii after honouring the rounded-corners setting (0 when square).
    private readonly int _effectiveRadius;
    private readonly int _effectiveButtonRadius;

    private Bitmap _iconBitmap;     // from the shared icon cache or the caller; not owned here
    private Point _dragOrigin;     // mouse offset captured when a title-bar drag begins
    private bool _dragging;
    private bool _isExpanded;     // whether the detail panel is currently open

    // Message-text rectangle worked out during measurement and reused at layout.
    private int _msgLeft, _msgW, _contentH;

    // The custom close ("×") button, drawn in the title bar.
    private Rectangle _closeBtnBounds;
    private bool _closeHover;

    // Optional content controls — any of these may be null depending on config.
    private CheckBox _checkBoxCtrl;
    private PlaceholderTextBox _inputTextBox;   // always a PlaceholderTextBox (concrete type — CA1859)
    private Rectangle _inputBandRect;     // bounds we paint the input border into
    private ComboBox _inputCombo;
    private LinkLabel _detailToggle;
    private CapsLockBadge _capsBadge;
    private GlassButton _countdownBtn;            // the button whose label shows the countdown
    private string _countdownBaseLabel = string.Empty;  // its caption without the " (Ns)" suffix
    private Font _detailFont;

    // Surfaced to GlassMessage.CoreEx() so the builder result carries them back.
    internal bool CheckBoxChecked => _checkBoxCtrl?.Checked ?? false;
    internal string InputText => _inputTextBox?.Text ?? _inputCombo?.Text ?? string.Empty;

    // --- Open/close animation state ------------------------------------------
    private System.Windows.Forms.Timer _fadeTimer;
    private double _targetOpacity;          // opacity at full visibility (backdrop may lower it)
    private bool _fadingOut;
    private DialogResult _pendingResult;           // result to apply once the close animation ends

    // Animations are driven by wall-clock time so their duration is constant even
    // when the UI thread is busy and timer ticks are uneven.
    private readonly System.Diagnostics.Stopwatch _animClock = new();
    private const int _animDurationMs = 170;
    private double _closeFromAppear = 1.0;       // opacity we were at when the close began

    private Point _slideFinal, _slideOrigin;
    private bool _slideActive;

    private Size _scaleFinalSize;
    private Point _scaleFinalLoc;
    private bool _scaleActive;

    // Smoothstep easing curve shared by all the animations.
    private static double Ease(double t) => t * t * (3.0 - (2.0 * t));

    // --- Auto-close countdown ------------------------------------------------
    private System.Windows.Forms.Timer _countTimer;
    private int _countRemaining;

    // --- Cached paint resources (rebuilt on resize/rebuild via InvalidateCache) -
    private GraphicsPath _bgPath;
    private LinearGradientBrush _bgBrush;
    private LinearGradientBrush _titleBrush;
    private GraphicsPath _borderPath;

    // Pens whose colours are fixed for the dialog's lifetime, so they live as
    // readonly fields and are disposed once in Dispose().
    private readonly Pen _glossPen;
    private readonly Pen _sepPen;
    private readonly Pen _glowPen;
    private readonly Pen _edgePen;
    private readonly Pen _panelSepPen;

    // --- Win32 / DWM interop for backdrops and rounded corners ----------------
    // Used to request the (undocumented) Acrylic blur-behind on Windows 10.
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
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, uint wParam, string lParam);

    // Which backdrop/corner treatments actually took effect on this machine.
    private bool _acrylicEnabled;
    private bool _micaEnabled;
    private bool _dwmRounded;

    private const int _dwmwaWindowCornerPreference = 33;   // DWMWA_WINDOW_CORNER_PREFERENCE
    private const int _dwmwcpRound = 2;    // DWMWCP_ROUND

    /// <summary>
    /// Asks DWM to round the window's corners. Only attempted on Windows 11
    /// (build 22000+); returns whether the request succeeded. Shared with
    /// <see cref="GlassToast"/>.
    /// </summary>
    internal static bool EnableModernCorners(IntPtr handle)
    {
        var v = Environment.OSVersion.Version;
        if (!(v.Major == 10 && v.Build >= 22000))
        {
            return false;
        }

        try
        {
            var pref = _dwmwcpRound;
            return DwmSetWindowAttribute(handle, _dwmwaWindowCornerPreference, ref pref, sizeof(int)) == 0;
        }
        catch { return false; }
    }

    // System icons are turned into bitmaps lazily and cached for the process, so a
    // dialog that uses Information/Warning/etc. never re-rasterises them.
    private static readonly Lazy<Bitmap> _lazyInfo = new(SystemIcons.Information.ToBitmap);
    private static readonly Lazy<Bitmap> _lazyQuestion = new(SystemIcons.Question.ToBitmap);
    private static readonly Lazy<Bitmap> _lazyWarning = new(SystemIcons.Warning.ToBitmap);
    private static readonly Lazy<Bitmap> _lazyError = new(SystemIcons.Error.ToBitmap);

    /// <summary>Returns the shared bitmap for a standard icon, or null for <see cref="MessageBoxIcon.None"/>.</summary>
    internal static Bitmap GetCachedSystemIcon(MessageBoxIcon icon) => icon switch
    {
        MessageBoxIcon.Information => _lazyInfo.Value,
        MessageBoxIcon.Question => _lazyQuestion.Value,
        MessageBoxIcon.Warning => _lazyWarning.Value,
        MessageBoxIcon.Error => _lazyError.Value,
        _ => null,
    };

    public GlassDialog(GlassDialogConfig cfg)
    {
        _cfg = cfg;
        _theme = cfg.Theme ?? GlassTheme.Default;
        _targetOpacity = _theme.Opacity;

        // Capture the desktop DPI up front; WM_DPICHANGED will refresh it later.
        using (var g = Graphics.FromHwnd(IntPtr.Zero))
        {
            _scale = g.DpiX / 96f;
        }

        var useRounded = cfg.UseRoundedCorners ?? GlassMessage.UseRoundedCorners;
        _effectiveRadius = useRounded ? _theme.CornerRadius : 0;
        _effectiveButtonRadius = useRounded ? _theme.ButtonCornerRadius : 0;

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        // Fixed-colour pens built once for the lifetime of the dialog.
        _glossPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f);  // top sheen line
        _sepPen = new Pen(Color.FromArgb(100, _theme.BorderColor), 1f);  // under the title bar
        _glowPen = new Pen(Color.FromArgb(55, _theme.BorderColor), 3f);  // outer border glow
        _edgePen = new Pen(Color.FromArgb(190, _theme.BorderColor), 1f);  // crisp border edge
        _panelSepPen = new Pen(Color.FromArgb(45, _theme.BorderColor), 1f);  // above the button strip

        Build();
    }

    // Add the CS_DROPSHADOW class style so the borderless window still casts the
    // small system drop shadow.
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; }
    }

    // First-time construction: configure the form, measure it, create the
    // child controls, and centre it on the primary work area.
    private void Build()
    {
        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.Manual;
        Opacity = _cfg.Animation == GlassAnimation.None ? _targetOpacity : 0.0;
        Font = _theme.MessageFont;
        BackColor = _theme.BackgroundBottom;
        KeyPreview = true;
        RightToLeft = _cfg.RightToLeft ? RightToLeft.Yes : RightToLeft.No;
        AccessibleName = string.IsNullOrEmpty(_cfg.Title) ? "Dialog" : _cfg.Title;
        AccessibleRole = AccessibleRole.Alert;

        _iconBitmap = _cfg.CustomIcon ?? GetCachedSystemIcon(_cfg.Icon);

        var (fw, fh) = MeasureForm();
        ClientSize = new Size(fw, fh);
        _closeBtnBounds = ComputeCloseBtnBounds(fw);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        var wa = PrimaryWorkingArea;
        Location = new Point(wa.Left + ((wa.Width - fw) / 2), wa.Top + ((wa.Height - fh) / 2));
        ResumeLayout(false);
    }

    private static Rectangle PrimaryWorkingArea =>
        Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

    // Tears down and recreates all child controls — used when the layout changes
    // at runtime (detail panel toggled, or DPI changed). User-entered values are
    // preserved across the rebuild so the user never loses what they typed.
    private void Rebuild(Screen recenterOn = null)
    {
        SuspendLayout();

        var savedInputText = _inputTextBox != null && !_inputTextBox.IsDisposed ? _inputTextBox.Text : null;
        var savedComboText = _inputCombo != null && !_inputCombo.IsDisposed ? _inputCombo.Text : null;
        var savedChecked = _checkBoxCtrl != null && !_checkBoxCtrl.IsDisposed ? (bool?)_checkBoxCtrl.Checked : null;

        _detailFont?.Dispose();
        _detailFont = null;

        foreach (Control c in Controls)
        {
            if (c is PictureBox pb)
            {
                pb.Image = null;
            }

            c.Dispose();
        }
        Controls.Clear();
        _capsBadge = null;
        _inputTextBox = null;
        _inputBandRect = Rectangle.Empty;
        _inputCombo = null;
        _checkBoxCtrl = null;
        _detailToggle = null;
        _countdownBtn = null;
        _countdownBaseLabel = string.Empty;
        InvalidateCache();

        _iconBitmap = _cfg.CustomIcon ?? GetCachedSystemIcon(_cfg.Icon);

        var (fw, fh) = MeasureForm();
        ClientSize = new Size(fw, fh);
        _closeBtnBounds = ComputeCloseBtnBounds(fw);
        ApplyRegion(fw, fh);
        AddControls(fw, fh);

        if (savedInputText != null && _inputTextBox != null)
        {
            _inputTextBox.Text = savedInputText;
        }

        if (savedComboText != null && _inputCombo != null)
        {
            _inputCombo.Text = savedComboText;
        }

        if (savedChecked.HasValue && _checkBoxCtrl != null)
        {
            _checkBoxCtrl.Checked = savedChecked.Value;
        }

        if (recenterOn != null)
        {
            CenterOn(recenterOn);
        }
        else
        {
            ClampToScreen(Screen.FromRectangle(Bounds));
        }

        ResumeLayout(false);
        Invalidate();
    }

    private void CenterOn(Screen screen)
    {
        var wa = screen.WorkingArea;
        Location = new Point(
            wa.Left + ((wa.Width - Width) / 2),
            wa.Top + ((wa.Height - Height) / 2));
    }

    private void ClampToScreen(Screen screen)
    {
        var wa = screen.WorkingArea;
        var x = Math.Min(Math.Max(Location.X, wa.Left), Math.Max(wa.Left, wa.Right - Width));
        var y = Math.Min(Math.Max(Location.Y, wa.Top), Math.Max(wa.Top, wa.Bottom - Height));
        Location = new Point(x, y);
    }

    // Close button sits at the trailing edge of the title bar (left in RTL).
    private Rectangle ComputeCloseBtnBounds(int fw)
    {
        var size = CloseBtnSize;
        var x = _cfg.RightToLeft ? Pad : fw - Pad - size;
        var y = (TitleH - size) / 2;
        return new Rectangle(x, y, size, size);
    }

    // Clip the window to a rounded rectangle in software, unless DWM is already
    // rounding it (in which case a region would only add aliasing).
    private void ApplyRegion(int w, int h)
    {
        if (_effectiveRadius <= 0 || _dwmRounded)
        { Region = null; return; }
        using var path = RoundRect(new Rectangle(0, 0, w, h), _effectiveRadius);
        Region = new Region(path);
    }

    // Works out the window size from its content and, as a side effect, records
    // the message rectangle and per-button width used later during layout. Width
    // is the max of what the title, message, and button row each need (bounded),
    // and height is the sum of whichever sections are present.
    private (int w, int h) MeasureForm()
    {
        var maxW = Math.Min((int)(PrimaryWorkingArea.Width * 0.80), Scale(720));
        var iconColW = _iconBitmap != null ? IconSize + Pad : 0;
        var textMaxW = maxW - (Pad * 2) - iconColW;

        var defs = ButtonDefs(_cfg.Buttons);
        var defIdx = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        var maxLabelPx = 0;
        for (var i = 0; i < defs.Length; i++)
        {
            var lbl = (_cfg.CustomLabels != null && i < _cfg.CustomLabels.Length)
                ? _cfg.CustomLabels[i] : defs[i].label;
            if (_cfg.AutoCloseMs > 0 && i == defIdx)
            {
                lbl += $" ({_cfg.AutoCloseMs / 1000}s)";
            }

            maxLabelPx = Math.Max(maxLabelPx, TextRenderer.MeasureText(lbl, _theme.ButtonFont).Width);
        }
        _computedBtnW = Math.Max(BtnW, maxLabelPx + Scale(28));
        var btnMinW = (defs.Length * _computedBtnW) + ((defs.Length - 1) * BtnGap) + (Pad * 2);

        var titleNeedW = 0;
        if (_cfg.Title.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Title, _theme.TitleFont);
            titleNeedW = sz.Width + (Pad * 2) + Scale(24);
        }

        int msgNeedW = 0, msgH = 0;
        if (_cfg.Message.Length > 0)
        {
            var sz = TextRenderer.MeasureText(_cfg.Message, _theme.MessageFont,
                           new Size(textMaxW, int.MaxValue),
                           TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            msgNeedW = sz.Width + iconColW + (Pad * 2);
            msgH = sz.Height;
        }

        var w = Math.Max(Math.Max(Math.Max(titleNeedW, msgNeedW), MinFormW), btnMinW);
        if (_cfg.HasInput || _cfg.HasDetail)
        {
            w = Math.Max(w, Scale(380));
        }

        // Cache the message rectangle; the row is as tall as the taller of the
        // text and the icon.
        _msgLeft = _cfg.RightToLeft ? Pad : Pad + iconColW;
        _msgW = w - (Pad * 2) - iconColW;
        _contentH = Math.Max(msgH, _iconBitmap != null ? IconSize : 0);

        // Accumulate height: title + content, then each optional section, then the
        // button strip; finally clamp to the minimum dialog height.
        var h = TitleH + Pad + _contentH;
        if (_cfg.HasProgress)
        {
            h += Pad + ProgressH;
        }

        if (_cfg.HasInput)
        {
            h += Pad + (_cfg.InputMode == GlassInputMode.Multiline ? InputMLH : InputH);
        }

        if (_cfg.HasCheckBox)
        {
            h += Scale(8) + CheckH;
        }

        if (_cfg.HasDetail)
        {
            h += Scale(8) + LinkH;
            if (_isExpanded)
            {
                h += Scale(6) + DetailH;
            }
        }
        h += Pad + BtnPanelH;
        h = Math.Max(h, MinFormH);

        return (w, h);
    }

    // Creates the child controls top-to-bottom, advancing 'y' as each section is
    // placed. Only the sections enabled in the config are added.
    private void AddControls(int fw, int fh)
    {
        var y = TitleH + Pad;

        if (_iconBitmap != null)
        {
            var iconX = _cfg.RightToLeft ? fw - Pad - IconSize : Pad;
            Controls.Add(new PictureBox
            {
                Bounds = new Rectangle(iconX, y, IconSize, IconSize),
                Image = _iconBitmap,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                AccessibleName = _cfg.Icon.ToString(),
                AccessibleRole = AccessibleRole.Graphic,
            });
        }

        if (_cfg.Message.Length > 0)
        {
            Controls.Add(new Label
            {
                Text = _cfg.Message,
                Font = _theme.MessageFont,
                ForeColor = _theme.MessageColor,
                BackColor = Color.Transparent,
                AutoSize = false,
                UseMnemonic = false,
                UseCompatibleTextRendering = false,
                Bounds = new Rectangle(_msgLeft, y, _msgW, _contentH),
                TextAlign = ContentAlignment.TopLeft,
                AccessibleRole = AccessibleRole.StaticText,
            });
        }

        y += _contentH;

        if (_cfg.HasProgress)
        {
            y += Pad;
            Controls.Add(new GlassProgressPanel(_theme, _cfg.ProgressValue, _cfg.ProgressMax)
            {
                Bounds = new Rectangle(Pad, y, fw - (Pad * 2), ProgressH),
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
                    Bounds = new Rectangle(Pad, y, fw - (Pad * 2), InputH),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = _theme.MessageFont,
                    BackColor = _theme.InputBackColor,
                    ForeColor = _theme.InputForeColor,
                    FlatStyle = FlatStyle.Flat,
                    AccessibleName = "Input",
                };
                if (_cfg.InputDropdownItems != null)
                {
                    _inputCombo.Items.AddRange(_cfg.InputDropdownItems);
                }

                if (!string.IsNullOrEmpty(_cfg.InputDefault))
                {
                    // Select the requested item; otherwise fall back to the first
                    // entry, or -1 (no selection) when the list is empty.
                    var idx = _inputCombo.Items.IndexOf(_cfg.InputDefault);
                    if (idx < 0)
                    {
                        idx = _inputCombo.Items.Count > 0 ? 0 : -1;
                    }
                    _inputCombo.SelectedIndex = idx;
                }
                else if (_inputCombo.Items.Count > 0)
                {
                    _inputCombo.SelectedIndex = 0;
                }

                Controls.Add(_inputCombo);
                y += InputH;
            }
            else
            {
                var multiline = _cfg.InputMode == GlassInputMode.Multiline;
                var password = _cfg.InputMode == GlassInputMode.Password;
                var inputH2 = multiline ? InputMLH : InputH;
                _inputBandRect = new Rectangle(Pad, y, fw - (Pad * 2), inputH2);
                // Password fields reserve a square column on the trailing edge for
                // the reveal "eye" toggle.
                var eyeSize = password ? inputH2 : 0;

                _inputTextBox = new PlaceholderTextBox(_cfg.InputPlaceholder ?? string.Empty)
                {
                    Font = _theme.MessageFont,
                    BackColor = _theme.InputBackColor,
                    ForeColor = _theme.InputForeColor,
                    BorderStyle = BorderStyle.None,
                    Multiline = multiline,
                    ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
                    PasswordChar = password ? '●' : '\0',
                    Text = _cfg.InputDefault ?? string.Empty,
                    TextAlign = _cfg.RightToLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    AccessibleName = "Input",
                    AccessibleRole = AccessibleRole.Text,
                };

                if (multiline)
                {
                    _inputTextBox.SetBounds(Pad + 3, y + 3, fw - (Pad * 2) - 6, inputH2 - 6);
                }
                else
                {
                    // Single-line: vertically centre the text within the band.
                    var th = _inputTextBox.PreferredHeight;
                    var tbX = Pad + 3 + (_cfg.RightToLeft ? eyeSize : 0);
                    var tbW = fw - (Pad * 2) - 6 - eyeSize;
                    _inputTextBox.SetBounds(tbX, y + ((inputH2 - th) / 2), tbW, th);
                }
                Controls.Add(_inputTextBox);

                if (password)
                {
                    var eyeX = _cfg.RightToLeft ? _inputBandRect.Left : _inputBandRect.Right - eyeSize;
                    var eye = new RevealToggle(_theme, _scale)
                    {
                        Bounds = new Rectangle(eyeX, y, eyeSize, inputH2),
                    };
                    eye.RevealedChanged += (s, e) => _inputTextBox.PasswordChar = eye.Revealed ? '\0' : '●';
                    Controls.Add(eye);
                    eye.BringToFront();

                    _capsBadge = new CapsLockBadge(_theme, _scale)
                    {
                        Location = new Point(_inputBandRect.Left, _inputBandRect.Bottom + Scale(2)),
                        Visible = false,
                    };
                    Controls.Add(_capsBadge);
                    _capsBadge.BringToFront();
                    // Show the Caps Lock warning only while the field is focused
                    // and Caps Lock is actually on; keep it refreshed on key-up.
                    void UpdateCaps()
                    {
                        if (_inputTextBox == null || _inputTextBox.IsDisposed || _capsBadge == null || _capsBadge.IsDisposed)
                        {
                            return;
                        }

                        var on = IsKeyLocked(Keys.CapsLock) && _inputTextBox.Focused;
                        _capsBadge.Visible = on;
                        if (on)
                        {
                            _capsBadge.BringToFront();
                        }
                    }
                    _inputTextBox.Enter += (s, e) => UpdateCaps();
                    _inputTextBox.Leave += (s, e) => { if (_capsBadge != null && !_capsBadge.IsDisposed) { _capsBadge.Visible = false; } };
                    _inputTextBox.KeyUp += (s, e) => UpdateCaps();
                }
                y += inputH2;
            }
        }

        if (_cfg.HasCheckBox)
        {
            y += Scale(8);
            _checkBoxCtrl = new GlassCheckBox(_theme, _scale, _cfg.RightToLeft)
            {
                Text = _cfg.CheckBoxLabel,
                Font = _theme.MessageFont,
                Checked = _cfg.CheckBoxDefault,
                Location = new Point(_msgLeft, y),
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
                Text = _isExpanded ? "Hide details ▲" : "Show details ▼",
                Font = _theme.ButtonFont,
                ForeColor = _theme.AccentColor,
                LinkColor = _theme.AccentColor,
                ActiveLinkColor = _theme.BorderColor,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(_msgLeft, y),
                AccessibleName = "Toggle detail panel",
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
                    Text = _cfg.DetailText,
                    Font = _detailFont,
                    BackColor = Color.FromArgb(8, 15, 28),
                    ForeColor = Color.FromArgb(160, 175, 200),
                    ReadOnly = true,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    WordWrap = true,
                    BorderStyle = BorderStyle.None,
                    Bounds = new Rectangle(Pad, y, fw - (Pad * 2), DetailH),
                    AccessibleName = "Detail",
                    AccessibleRole = AccessibleRole.Text,
                });
                y += DetailH;
            }
        }

        AddButtons(fw, fh);
    }

    // Lays out the bottom button row, centred horizontally. The focused/default
    // button also becomes the AcceptButton and, if configured, the countdown host.
    private void AddButtons(int fw, int fh)
    {
        var defs = ButtonDefs(_cfg.Buttons);
        // In RTL the visual order is mirrored.
        if (_cfg.RightToLeft)
        {
            Array.Reverse(defs);
        }

        var totalW = (defs.Length * _computedBtnW) + ((defs.Length - 1) * BtnGap);
        var startX = (fw - totalW) / 2;
        var btnY = fh - BtnPanelH + ((BtnPanelH - BtnH) / 2);

        var focusIdx = DefaultIndex(_cfg.Buttons, _cfg.DefaultButton);
        if (_cfg.RightToLeft)
        {
            focusIdx = defs.Length - 1 - focusIdx;
        }

        for (var i = 0; i < defs.Length; i++)
        {
            var (label, result) = defs[i];
            if (_cfg.CustomLabels != null && i < _cfg.CustomLabels.Length)
            {
                label = _cfg.CustomLabels[i];
            }

            var btn = new GlassButton(_theme, _effectiveButtonRadius)
            {
                Text = label,
                Bounds = new Rectangle(startX + (i * (_computedBtnW + BtnGap)), btnY, _computedBtnW, BtnH),
                Tag = result,
                AccessibleName = label.Replace("&", string.Empty),
            };
            btn.Click += OnButtonClick;
            Controls.Add(btn);

            if (i == focusIdx)
            {
                ActiveControl = btn;
                AcceptButton = btn;
                if (_cfg.AutoCloseMs > 0)
                {
                    _countdownBtn = btn;
                    _countdownBaseLabel = label;
                }
            }
        }
    }

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
        // Ctrl+C copies the title and message, matching the classic message box.
        // Clipboard access can transiently fail, so the known exceptions are swallowed.
        if (e.Control && e.KeyCode == Keys.C)
        {
            var text = string.IsNullOrEmpty(_cfg.Title) ? _cfg.Message : $"{_cfg.Title}\n{_cfg.Message}";
            if (!string.IsNullOrEmpty(text))
            {
                try
                { Clipboard.SetText(text); }
                catch (System.Runtime.InteropServices.ExternalException) { /* clipboard busy/locked by another app — ignore */ }
                catch (System.Threading.ThreadStateException) { /* not on an STA thread — ignore */ }
            }
            e.Handled = true;
            return;
        }
        // Escape maps to the most sensible cancel-ish result for the button set.
        if (e.KeyCode == Keys.Escape)
        {
            BeginClose(EscapeResult(_cfg.Buttons));
            e.Handled = true;
        }
    }

    // Intercept the user closing the window (e.g. Alt+F4) so it runs through the
    // close animation and yields the escape result rather than vanishing instantly.
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
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        // Clicking the custom × closes; clicking elsewhere in the title bar starts
        // a window drag (the form is borderless, so we move it ourselves).
        if (_closeBtnBounds.Contains(e.Location))
        {
            BeginClose(EscapeResult(_cfg.Buttons));
            return;
        }
        if (e.Y < TitleH)
        {
            _dragging = true;
            _dragOrigin = e.Location;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var wasHover = _closeHover;
        _closeHover = _closeBtnBounds.Contains(e.Location);
        if (wasHover != _closeHover)
        {
            Invalidate(Rectangle.Inflate(_closeBtnBounds, 2, 2));
        }

        if (_dragging)
        {
            Location = new Point(
                Location.X + e.X - _dragOrigin.X,
                Location.Y + e.Y - _dragOrigin.Y);
        }
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

    // Expanding/collapsing the detail panel changes the dialog height, so the
    // simplest correct path is a full rebuild at the new size.
    private void OnDetailToggleClick(object sender, LinkLabelLinkClickedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        Rebuild();
    }

    // --- Auto-close countdown -------------------------------------------------
    private void StartCountdown()
    {
        _countRemaining = _cfg.AutoCloseMs;
        UpdateCountdownLabel();
        _countTimer = new System.Windows.Forms.Timer { Interval = 1000 };
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
            InvalidateCountdownArc();
        }
    }

    // Repaint just the circular countdown arc (plus the host button) each second,
    // instead of invalidating the whole window.
    private void InvalidateCountdownArc()
    {
        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var arcD = BtnH - Scale(6);
        var arcX = w - Pad - arcD;
        var arcY = h - BtnPanelH + ((BtnPanelH - arcD) / 2);
        var slop = Scale(3);
        Invalidate(new Rectangle(arcX - slop, arcY - slop, arcD + (slop * 2), arcD + (slop * 2)));
        _countdownBtn?.Invalidate();
    }

    private void UpdateCountdownLabel()
    {
        if (_countdownBtn == null)
        {
            return;
        }

        var seconds = Math.Max(0, _countRemaining / 1000);
        _countdownBtn.Text = seconds > 0
            ? $"{_countdownBaseLabel} ({seconds}s)"
            : _countdownBaseLabel;
    }

    private void StopCountdown()
    {
        _ = (_countdownBtn?.Text = _countdownBaseLabel);

        if (_countTimer == null)
        {
            return;
        }

        _countTimer.Stop();
        _countTimer.Dispose();
        _countTimer = null;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // With no animation, just snap to full opacity. Otherwise start hidden and
        // arrange the entrance (the timer is started in OnShown).
        if (_cfg.Animation == GlassAnimation.None)
        {
            Opacity = _targetOpacity;
            return;
        }

        Opacity = 0.0;
        _fadingOut = false;
        SetupEntranceAnimation();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Begin the entrance animation and the countdown only once the window is
        // actually on screen.
        if (_cfg.Animation != GlassAnimation.None)
        {
            StartFadeTimer();
        }

        if (_cfg.AutoCloseMs > 0)
        {
            StartCountdown();
        }
    }

    // Positions the window for the start of the chosen entrance animation: slightly
    // higher for SlideDown, slightly smaller for Scale, or just centred otherwise.
    private void SetupEntranceAnimation()
    {
        var screen = Owner != null ? Screen.FromHandle(Owner.Handle) : Screen.FromPoint(Cursor.Position);
        var wa = screen.WorkingArea;
        var centerX = wa.Left + ((wa.Width - Width) / 2);
        var centerY = wa.Top + ((wa.Height - Height) / 2);
        _slideFinal = new Point(centerX, centerY);

        switch (_cfg.Animation)
        {
            case GlassAnimation.SlideDown:
                _slideOrigin = new Point(centerX, centerY - Scale(28));
                Location = _slideOrigin;
                _slideActive = true;
                break;

            case GlassAnimation.Scale:
                _scaleFinalSize = new Size(Width, Height);
                _scaleFinalLoc = _slideFinal;
                _scaleActive = true;
                var sw0 = (int)(_scaleFinalSize.Width * 0.90f);
                var sh0 = (int)(_scaleFinalSize.Height * 0.90f);
                SuspendLayout();
                SetBounds(
                    centerX + ((_scaleFinalSize.Width - sw0) / 2),
                    centerY + ((_scaleFinalSize.Height - sh0) / 2),
                    sw0, sh0);
                ResumeLayout(false);
                break;

            default:
                Location = _slideFinal;
                break;
        }
    }

    // Begins closing with the given result. Guard against re-entry, stop the
    // countdown, then either set the result immediately (no animation) or kick off
    // the reverse animation; OnFadeTick applies the result when it finishes.
    private void BeginClose(DialogResult result)
    {
        if (_fadingOut)
        {
            return;
        }

        StopCountdown();
        _pendingResult = result;
        _fadingOut = true;

        if (_cfg.Animation == GlassAnimation.None)
        {
            DialogResult = _pendingResult;
            return;
        }

        // Remember how visible we are right now so an interrupted entrance fades
        // out smoothly from its current opacity rather than snapping to full.
        _closeFromAppear = _targetOpacity > 0 ? Math.Min(1.0, Opacity / _targetOpacity) : 0.0;

        if (_cfg.Animation == GlassAnimation.SlideDown)
        {
            _slideOrigin = Location;
            _slideFinal = new Point(Location.X, Location.Y + Scale(15));
            _slideActive = true;
        }
        else if (_cfg.Animation == GlassAnimation.Scale)
        {
            _scaleFinalSize = new Size(Width, Height);
            _scaleFinalLoc = Location;
            _scaleActive = true;
        }

        DisposeFadeTimer();
        StartFadeTimer();
    }

    private void StartFadeTimer()
    {
        _animClock.Restart();
        _fadeTimer = new System.Windows.Forms.Timer { Interval = 15 };
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
        _animClock.Stop();
    }

    // One animation frame. Progress is read from the wall clock, eased, and split
    // into an "appear" amount (opacity/scale) and a "slide displacement"; the
    // direction is reversed while fading out.
    private void OnFadeTick(object sender, EventArgs e)
    {
        var t = Math.Min(1.0, _animClock.Elapsed.TotalMilliseconds / _animDurationMs);
        var done = t >= 1.0;

        double appear, slideDisp;
        if (_fadingOut)
        {
            var eased = Ease(1.0 - t);
            appear = _closeFromAppear * eased;
            slideDisp = 1.0 - eased;
        }
        else
        {
            var eased = Ease(t);
            appear = eased;
            slideDisp = eased;
        }

        ApplyAnimationFrame(appear, slideDisp);

        if (!done)
        {
            return;
        }

        // Final frame: stop the timer and snap to the exact end state — either
        // commit the dialog result (closing) or settle at full size/opacity (opening).
        DisposeFadeTimer();
        if (_fadingOut)
        {
            _scaleActive = false;
            DialogResult = _pendingResult;
        }
        else
        {
            Opacity = _targetOpacity;
            if (_slideActive)
            { Location = _slideFinal; _slideActive = false; }
            if (_scaleActive)
            {
                SuspendLayout();
                SetBounds(_scaleFinalLoc.X, _scaleFinalLoc.Y, _scaleFinalSize.Width, _scaleFinalSize.Height);
                ResumeLayout(false);
                _scaleActive = false;
            }
        }
    }

    // Applies one interpolated frame: opacity always, plus position (SlideDown) or
    // size+position (Scale, growing 90%→100% with 'appear').
    private void ApplyAnimationFrame(double appear, double slideDisp)
    {
        Opacity = _targetOpacity * appear;

        if (_slideActive && _cfg.Animation == GlassAnimation.SlideDown)
        {
            Location = new Point(_slideFinal.X,
                _slideOrigin.Y + (int)(slideDisp * (_slideFinal.Y - _slideOrigin.Y)));
        }

        if (_scaleActive && _cfg.Animation == GlassAnimation.Scale)
        {
            var sf = 0.90f + (0.10f * (float)appear);
            var nsw = (int)(_scaleFinalSize.Width * sf);
            var nsh = (int)(_scaleFinalSize.Height * sf);
            SuspendLayout();
            SetBounds(_scaleFinalLoc.X + ((_scaleFinalSize.Width - nsw) / 2),
                      _scaleFinalLoc.Y + ((_scaleFinalSize.Height - nsh) / 2), nsw, nsh);
            ResumeLayout(false);
        }
    }

    // Respond to per-monitor DPI changes: update the scale from WPARAM, find the
    // target monitor from LPARAM's suggested rect, and rebuild at the new scale.
    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == _wmDpiChanged)
        {
            _scale = (m.WParam.ToInt32() & 0xFFFF) / 96f;

            Screen target = null;
            if (m.LParam != IntPtr.Zero)
            {
                var r = Marshal.PtrToStructure<Rect>(m.LParam);
                target = Screen.FromRectangle(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            }
            Rebuild(target);
        }
        base.WndProc(ref m);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Prefer a Mica backdrop (Win11); fall back to Acrylic blur (Win10). Then,
        // if rounding is requested and DWM obliges, drop the software region.
        if (!TryApplyMica())
        {
            TryApplyAcrylic();
        }

        if (_effectiveRadius > 0 && EnableModernCorners(Handle))
        {
            _dwmRounded = true;
            Region = null;
            Invalidate();
        }
    }

    // Tries the Windows 11 Mica system backdrop. First the modern
    // DWMWA_SYSTEMBACKDROP_TYPE (38), then the older DWMWA_MICA_EFFECT (20) for
    // early builds. When applied, opacity is capped so the backdrop shows through.
    private bool TryApplyMica()
    {
        if (Environment.OSVersion.Version is var v && !(v.Major == 10 && v.Build >= 22000))
        {
            return false;
        }

        try
        {
            var val = 2;   // DWMSBT_MAINWINDOW
            if (DwmSetWindowAttribute(Handle, 38, ref val, sizeof(int)) == 0)
            { _micaEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.90); return true; }
            val = 1;       // legacy mica toggle
            if (DwmSetWindowAttribute(Handle, 20, ref val, sizeof(int)) == 0)
            { _micaEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.90); return true; }
        }
        catch { /* Mica is a best-effort enhancement; fall back to the painted background */ }
        return false;
    }

    // Tries the Windows 10 Acrylic blur-behind via the undocumented
    // SetWindowCompositionAttribute API, tinting it with the theme background.
    private void TryApplyAcrylic()
    {
        var v = Environment.OSVersion.Version;
        if (!(v.Major == 10 && v.Build >= 17134))
        {
            return;
        }

        try
        {
            // Pack the tint as 0xAABBGGRR with ~75% alpha, as the API expects.
            var c = _theme.BackgroundTop;
            var tint = ((uint)0xC0 << 24) | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;
            var acc = new AccentPolicy { AccentState = 4, GradientColor = tint };   // ACCENT_ENABLE_ACRYLICBLURBEHIND
            var sz = Marshal.SizeOf<AccentPolicy>();
            var ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(acc, ptr, false);
                var data = new WindowCompositionAttribData { Attribute = 19, Data = ptr, SizeOfData = sz };
                if (SetWindowCompositionAttribute(Handle, ref data))
                { _acrylicEnabled = true; _targetOpacity = Math.Min(_theme.Opacity, 0.85); }
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch { /* Acrylic is a best-effort enhancement; fall back to the painted background */ }
    }

    // Drops the cached paths/brushes so the next paint rebuilds them at the new
    // size or theme.
    private void InvalidateCache()
    {
        _bgPath?.Dispose();
        _bgPath = null;
        _bgBrush?.Dispose();
        _bgBrush = null;
        _titleBrush?.Dispose();
        _titleBrush = null;
        _borderPath?.Dispose();
        _borderPath = null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateCache();
        Invalidate();
    }

    // Paints the entire window: background gradient, title bar, separators, border
    // glow, title text, close button, countdown arc, and input borders. Cached
    // resources are created lazily with ??= and reused across repaints.
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        SetQuality(g);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var r = _dwmRounded ? 0 : _effectiveRadius;

        // Skip the opaque background fill when a Mica/Acrylic backdrop is showing
        // through — otherwise we'd paint over it.
        if (!_acrylicEnabled && !_micaEnabled)
        {
            _bgPath ??= RoundRect(new Rectangle(0, 0, w, h), r);
            _bgBrush ??= new LinearGradientBrush(new Rectangle(0, 0, w, h),
                             _theme.BackgroundTop, _theme.BackgroundBottom, LinearGradientMode.Vertical);
            g.FillPath(_bgBrush, _bgPath);
        }

        var titleRect = new Rectangle(0, 0, w, TitleH);
        _titleBrush ??= new LinearGradientBrush(titleRect,
                            _theme.TitleBarTop, _theme.TitleBarBottom, LinearGradientMode.Vertical);
        g.FillRectangle(_titleBrush, titleRect);

        g.DrawLine(_glossPen, r + 1, 1, w - r - 2, 1);

        g.DrawLine(_sepPen, 0, TitleH - 1, w, TitleH - 1);

        g.DrawLine(_panelSepPen, Pad, h - BtnPanelH, w - Pad, h - BtnPanelH);

        _borderPath ??= RoundRect(new Rectangle(0, 0, w - 1, h - 1), r);
        g.DrawPath(_glowPen, _borderPath);
        g.DrawPath(_edgePen, _borderPath);

        if (_cfg.Title.Length > 0)
        {
            var cb = _closeBtnBounds;
            var closeSz = cb.Width + Scale(4);
            var textLeft = _cfg.RightToLeft ? (Pad + closeSz) : Pad;
            var textRight = _cfg.RightToLeft ? (w - Pad) : (w - Pad - closeSz);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                          | TextFormatFlags.EndEllipsis;
            flags |= _cfg.RightToLeft
                ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                : TextFormatFlags.Left;
            TextRenderer.DrawText(g, _cfg.Title, _theme.TitleFont,
                new Rectangle(textLeft, 0, Math.Max(0, textRight - textLeft), TitleH),
                _theme.TitleColor, flags);
        }

        // Close button: a red hover halo plus the two strokes of the "×".
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

        // Countdown ring around the auto-close button: a faint full track plus an
        // accent arc that sweeps down as the remaining time shrinks.
        if (_cfg.AutoCloseMs > 0 && _countTimer != null)
        {
            var ratio = (float)_countRemaining / _cfg.AutoCloseMs;
            var arcD = BtnH - Scale(6);
            var arcX = w - Pad - arcD;
            var arcY = h - BtnPanelH + ((BtnPanelH - arcD) / 2);
            using var trackPen = new Pen(Color.FromArgb(40, _theme.BorderColor), Scale(2));
            using var fillPen = new Pen(_theme.AccentColor, Scale(2));
            g.DrawArc(trackPen, arcX, arcY, arcD, arcD, 0, 360);
            if (ratio > 0f)
            {
                g.DrawArc(fillPen, arcX, arcY, arcD, arcD, -90, -(int)(360 * ratio));
            }
        }

        PaintInputBorders(g);
    }

    // Draws the themed border (and background fill) around the input controls,
    // since the borderless text box / combo can't draw their own.
    private void PaintInputBorders(Graphics g)
    {
        using var borderPen = new Pen(Color.FromArgb(70, _theme.BorderColor), 1f);

        if (_inputTextBox != null && !_inputTextBox.IsDisposed)
        {
            var b = _inputBandRect;
            using (var fill = new SolidBrush(_theme.InputBackColor))
            {
                if (_effectiveRadius > 0)
                { using var fp = RoundRect(b, 4); g.FillPath(fill, fp); }
                else
                {
                    g.FillRectangle(fill, b);
                }
            }
            if (_effectiveRadius > 0)
            { using var p = RoundRect(b, 4); g.DrawPath(borderPen, p); }
            else
            {
                g.DrawRectangle(borderPen, b);
            }
        }

        if (_inputCombo != null && !_inputCombo.IsDisposed)
        {
            var b = _inputCombo.Bounds;
            b.Inflate(1, 1);
            if (_effectiveRadius > 0)
            { using var p = RoundRect(b, 3); g.DrawPath(borderPen, p); }
            else
            {
                g.DrawRectangle(borderPen, b);
            }
        }
    }

    // Maps a standard button set onto its ordered (caption, result) pairs. The
    // "&" marks the access-key letter; custom labels (if any) replace the captions.
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

    // Index of the default button, clamped to the number of buttons present.
    private static int DefaultIndex(MessageBoxButtons btns, MessageBoxDefaultButton def)
    {
        var max = ButtonDefs(btns).Length - 1;
        return def switch
        {
            MessageBoxDefaultButton.Button2 => Math.Min(1, max),
            MessageBoxDefaultButton.Button3 => Math.Min(2, max),
            _ => 0,
        };
    }

    private static DialogResult DefaultResult(MessageBoxButtons btns, MessageBoxDefaultButton def)
    {
        var defs = ButtonDefs(btns);
        return defs[Math.Min(DefaultIndex(btns, def), defs.Length - 1)].result;
    }

    // The result produced when the dialog is dismissed via Escape or the × — the
    // least destructive option for each button set.
    private static DialogResult EscapeResult(MessageBoxButtons btns) => btns switch
    {
        MessageBoxButtons.OK => DialogResult.OK,
        MessageBoxButtons.OKCancel => DialogResult.Cancel,
        MessageBoxButtons.YesNo => DialogResult.No,
        MessageBoxButtons.YesNoCancel => DialogResult.Cancel,
        MessageBoxButtons.RetryCancel => DialogResult.Cancel,
        MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
        _ => DialogResult.Cancel,
    };

    // Fills a child control with a slice of the dialog's full-height gradient,
    // offset by the control's top, so transparent controls blend seamlessly into
    // the window. Shared by the custom button/checkbox/badge.
    internal static void PaintThemedBackground(Graphics g, Control c, GlassTheme theme)
    {
        var ph = c.Parent?.Height ?? c.Height;
        if (ph < 1)
        {
            ph = 1;
        }

        using var brush = new LinearGradientBrush(
            new Rectangle(0, -c.Top, Math.Max(1, c.Width), ph),
            theme.BackgroundTop, theme.BackgroundBottom, LinearGradientMode.Vertical);
        g.FillRectangle(brush, c.ClientRectangle);
    }

    // Standard high-quality GDI+ settings applied at the top of every custom paint.
    internal static void SetQuality(Graphics g)
    {
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    /// <summary>
    /// Builds a rounded-rectangle path (four corner arcs joined by straight
    /// edges), or a plain rectangle when <paramref name="radius"/> is ≤ 0. The
    /// one geometry primitive the whole library draws its surfaces from.
    /// </summary>
    internal static GraphicsPath RoundRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        { path.AddRectangle(rect); return path; }
        var d = radius * 2;   // arc bounding-box side = diameter
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // Owner-drawn progress bar. With _value == -1 it animates a sweeping marquee
    // highlight; otherwise it draws a determinate fill with a glossy top sheen.
    private sealed class GlassProgressPanel : Control
    {
        private readonly GlassTheme _theme;
        private readonly int _value, _max;
        private float _phase;   // marquee animation phase, advanced each tick
        private readonly System.Windows.Forms.Timer _ticker;
        private GraphicsPath _trackPath;
        private Size _trackSize;

        public GlassProgressPanel(GlassTheme theme, int value, int max)
        {
            _theme = theme;
            _value = value;
            _max = Math.Max(1, max);
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
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
            var g = e.Graphics;
            SetQuality(g);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var r = Height / 2;

            if (_trackPath == null || _trackSize != Size)
            {
                _trackPath?.Dispose();
                _trackSize = Size;
                _trackPath = RoundRect(rect, r);
            }

            using (var bgBrush = new SolidBrush(Color.FromArgb(30, _theme.AccentColor)))
            {
                g.FillPath(bgBrush, _trackPath);
            }

            if (_value == -1)
            {
                // Indeterminate: a soft chunk eases left↔right via a sine of _phase.
                var t = (float)((1.0 + Math.Sin(_phase - (Math.PI / 2.0))) / 2.0);
                var fw = Math.Max(Height * 2, Width / 3);
                var fx = (int)(t * (Width - fw));
                var fRect = new Rectangle(fx, 0, fw, Height - 1);
                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(
                        new Rectangle(fRect.X, fRect.Y, Math.Max(1, fRect.Width), Math.Max(1, fRect.Height)),
                        Color.FromArgb(80, _theme.AccentColor), _theme.AccentColor, LinearGradientMode.Horizontal);
                    fb.SetBlendTriangularShape(0.5f, 1.0f);
                    g.SetClip(_trackPath);
                    g.FillPath(fb, fp);
                    g.ResetClip();
                }
            }
            else
            {
                // Determinate: fill proportional to value/max, never shorter than a
                // full pill cap so the rounded ends always render.
                var fw = Math.Max(r * 2, (int)((float)_value / _max * (Width - 1)));
                var fRect = new Rectangle(0, 0, fw, Height - 1);
                if (fRect.Width > 0)
                {
                    using var fp = RoundRect(fRect, r);
                    using var fb = new LinearGradientBrush(fRect, _theme.AccentColor, _theme.BorderColor, 0f);
                    g.SetClip(_trackPath);
                    g.FillPath(fb, fp);
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
            if (disposing)
            { _ticker?.Stop(); _ticker?.Dispose(); _trackPath?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Owner-drawn checkbox that matches the theme: a rounded box with an animated
    // accent fill and a hand-drawn tick, plus a dotted focus ring. RTL-aware.
    private sealed class GlassCheckBox : CheckBox
    {
        private readonly GlassTheme _theme;
        private readonly float _scale;
        private readonly bool _rtl;
        private bool _hover;

        public GlassCheckBox(GlassTheme theme, float scale, bool rtl)
        {
            _theme = theme;
            _scale = scale;
            _rtl = rtl;
            AutoSize = true;
            ForeColor = theme.MessageColor;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.Opaque |
                     ControlStyles.ResizeRedraw, true);
        }

        private int Box => Math.Max(14, (int)(15 * _scale));
        private int Gap => Math.Max(6, (int)(8 * _scale));

        public override Size GetPreferredSize(Size proposedSize)
        {
            var ts = TextRenderer.MeasureText(Text, Font);
            return new Size(Box + Gap + ts.Width + 2, Math.Max(Box, ts.Height) + 2);
        }

        protected override void OnMouseEnter(EventArgs eventargs) { _hover = true; Invalidate(); base.OnMouseEnter(eventargs); }
        protected override void OnMouseLeave(EventArgs eventargs) { _hover = false; Invalidate(); base.OnMouseLeave(eventargs); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            SetQuality(g);

            PaintThemedBackground(g, this, _theme);

            var box = Box;
            var rect = new Rectangle(_rtl ? Width - box : 0, (Height - box) / 2, box - 1, box - 1);

            using (var path = RoundRect(rect, Math.Max(2, (int)(3 * _scale))))
            {
                using (var fill = new SolidBrush(Checked
                           ? _theme.CheckBoxColor
                           : Color.FromArgb(40, _theme.InputBackColor)))
                {
                    g.FillPath(fill, path);
                }

                using (var border = new Pen(
                           Color.FromArgb(_hover || Focused ? 230 : 150, _theme.CheckBoxColor), 1f))
                {
                    g.DrawPath(border, path);
                }

                if (Checked)
                {
                    using var tick = new Pen(Color.White, Math.Max(1.6f, _scale * 1.8f))
                    {
                        StartCap = LineCap.Round,
                        EndCap = LineCap.Round,
                        LineJoin = LineJoin.Round,
                    };
                    // Tick drawn as a two-segment polyline at proportional points
                    // within the box so it scales cleanly with DPI.
                    float l = rect.Left, t = rect.Top, w = rect.Width, h = rect.Height;
                    g.DrawLines(tick,
                    [
                        new PointF(l + (w * 0.22f), t + (h * 0.52f)),
                        new PointF(l + (w * 0.42f), t + (h * 0.72f)),
                        new PointF(l + (w * 0.78f), t + (h * 0.26f)),
                    ]);
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

    // The password "show/hide" eye button. Draws a hand-built eye glyph and raises
    // RevealedChanged so the dialog can flip the text box's PasswordChar.
    private sealed class RevealToggle : Control
    {
        private readonly GlassTheme _theme;
        private readonly float _scale;
        private bool _hover;

        public event EventHandler RevealedChanged;
        public bool Revealed { get; private set; }

        public RevealToggle(GlassTheme theme, float scale)
        {
            _theme = theme;
            _scale = scale;
            BackColor = theme.InputBackColor;
            Cursor = Cursors.Hand;
            TabStop = false;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "Show password";
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.Opaque, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            Revealed = !Revealed;
            AccessibleName = Revealed ? "Hide password" : "Show password";
            Invalidate();
            RevealedChanged?.Invoke(this, e);
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            SetQuality(g);
            using (var bg = new SolidBrush(_theme.InputBackColor))
            {
                g.FillRectangle(bg, ClientRectangle);
            }

            var d = (int)Math.Round(Math.Min(Width, Height) * 0.72f);
            var hi = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
            if (_hover)
            {
                using var hb = new SolidBrush(Color.FromArgb(28, _theme.AccentColor));
                g.FillEllipse(hb, hi);
            }

            var col = Color.FromArgb(_hover ? 255 : 175, _theme.AccentColor);
            var lineW = Math.Max(1.3f, _scale * 1.5f);
            using var pen = new Pen(col, lineW) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

            float cx = Width / 2f, cy = Height / 2f;
            var ew = Width * 0.24f;
            var eh = Height * 0.17f;

            // Eye outline: two mirrored bezier curves forming the almond shape.
            using (var eye = new GraphicsPath())
            {
                eye.AddBezier(cx - ew, cy, cx - (ew * 0.45f), cy - eh, cx + (ew * 0.45f), cy - eh, cx + ew, cy);
                eye.AddBezier(cx + ew, cy, cx + (ew * 0.45f), cy + eh, cx - (ew * 0.45f), cy + eh, cx - ew, cy);
                eye.CloseFigure();
                g.DrawPath(pen, eye);
            }

            // Iris ring and filled pupil at the centre.
            var ir = eh * 0.95f;
            g.DrawEllipse(pen, cx - ir, cy - ir, ir * 2, ir * 2);
            using (var pupil = new SolidBrush(col))
            {
                var pr = ir * 0.45f;
                g.FillEllipse(pupil, cx - pr, cy - pr, pr * 2, pr * 2);
            }

            // When revealed, strike the eye through with a slash (drawn over a
            // thicker background-coloured stroke so it reads cleanly against the eye).
            if (Revealed)
            {
                using var slashBg = new Pen(_theme.InputBackColor, lineW + 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(slashBg, cx - (ew * 1.05f), cy + (eh * 1.7f), cx + (ew * 1.05f), cy - (eh * 1.7f));
                g.DrawLine(pen, cx - (ew * 1.05f), cy + (eh * 1.7f), cx + (ew * 1.05f), cy - (eh * 1.7f));
            }
        }
    }

    // Small warning pill shown beneath a password field when Caps Lock is on:
    // a themed rounded panel with a warning triangle and a short message.
    private sealed class CapsLockBadge : Control
    {
        private readonly GlassTheme _theme;
        private readonly float _scale;
        private readonly Font _font;
        private const string _text = "Caps Lock is on";
        private readonly int _pad, _icon, _gap, _radius;

        public CapsLockBadge(GlassTheme theme, float scale)
        {
            _theme = theme;
            _scale = scale;
            _font = theme.MessageFont;
            _pad = Sc(8);
            _icon = Sc(13);
            _gap = Sc(6);
            _radius = Sc(4);

            AccessibleRole = AccessibleRole.Alert;
            AccessibleName = _text;
            TabStop = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.Opaque, true);

            var ts = TextRenderer.MeasureText(_text, _font);
            Size = new Size(_pad + _icon + _gap + ts.Width + _pad,
                            Math.Max(_icon, ts.Height) + Sc(6));
        }

        private int Sc(int v) => Math.Max(1, (int)(v * _scale));

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            SetQuality(g);

            PaintThemedBackground(g, this, _theme);

            var w = Width;
            var h = Height;

            var panel = Color.FromArgb(
                Math.Min(255, _theme.InputBackColor.R + 8),
                Math.Min(255, _theme.InputBackColor.G + 10),
                Math.Min(255, _theme.InputBackColor.B + 16));
            using (var path = RoundRect(new Rectangle(0, 0, w - 1, h - 1), _radius))
            {
                using (var fill = new SolidBrush(panel))
                {
                    g.FillPath(fill, path);
                }

                using var edge = new Pen(Color.FromArgb(200, _theme.AccentColor), Math.Max(1f, _scale));
                g.DrawPath(edge, path);
            }

            var ix = _pad;
            var iy = (h - _icon) / 2;
            using (var tri = new GraphicsPath())
            {
                tri.AddPolygon(
                [
                    new PointF(ix + (_icon / 2f), iy),
                    new PointF(ix + _icon,      iy + _icon),
                    new PointF(ix,              iy + _icon),
                ]);
                tri.CloseFigure();
                using var fill = new SolidBrush(_theme.AccentColor);
                g.FillPath(fill, tri);
            }
            using (var exFont = new Font(_font.FontFamily, _icon * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, "!", exFont,
                    new Rectangle(ix, iy + (_icon / 5), _icon, _icon),
                    Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding);
            }

            var textX = ix + _icon + _gap;
            TextRenderer.DrawText(g, _text, _font,
                new Rectangle(textX, 0, w - textX - _pad, h),
                _theme.MessageColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // A TextBox that shows native cue-banner placeholder text. The text is set via
    // EM_SETCUEBANNER (0x1501) once the handle exists, so it works without us
    // having to paint the placeholder ourselves.
    private sealed class PlaceholderTextBox(string placeholder) : TextBox
    {
        private readonly string _placeholder = placeholder;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!string.IsNullOrEmpty(_placeholder))
            {
                _ = SendMessage(Handle, 0x1501, 0, _placeholder);   // EM_SETCUEBANNER
            }
        }
    }

    // Releases timers, the detail font, cached paint resources, and the fixed pens.
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
