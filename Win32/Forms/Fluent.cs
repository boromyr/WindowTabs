using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Bemo.Win32.Forms
{
    // Bemo has Win32 constant classes with the same names as these WinForms types
    using TextFormatFlags = System.Windows.Forms.TextFormatFlags;

    /// <summary>
    /// Windows 11 (Fluent) dark palette, fonts and window chrome helpers.
    /// </summary>
    public static class FluentTheme
    {
        public static readonly Color Background = Color.FromArgb(32, 32, 32);
        public static readonly Color Card = Color.FromArgb(43, 43, 43);
        public static readonly Color CardBorder = Color.FromArgb(29, 29, 29);

        public static readonly Color Control = Color.FromArgb(55, 55, 55);
        public static readonly Color ControlHover = Color.FromArgb(61, 61, 61);
        public static readonly Color ControlPressed = Color.FromArgb(48, 48, 48);
        public static readonly Color ControlBorder = Color.FromArgb(67, 67, 67);

        public static readonly Color Input = Color.FromArgb(45, 45, 45);
        public static readonly Color InputFocused = Color.FromArgb(31, 31, 31);

        public static readonly Color NavHover = Color.FromArgb(45, 45, 45);
        public static readonly Color NavSelected = Color.FromArgb(50, 50, 50);

        public static readonly Color Text = Color.FromArgb(255, 255, 255);
        public static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
        public static readonly Color TextDisabled = Color.FromArgb(120, 120, 120);
        public static readonly Color TextOnAccent = Color.FromArgb(0, 0, 0);

        public static readonly Color Accent = ReadAccent();
        public static readonly Color AccentHover = Blend(Accent, Background, 0.9f);
        public static readonly Color AccentPressed = Blend(Accent, Background, 0.8f);

        private static readonly string UiFamily = PickFamily("Segoe UI Variable Text", "Segoe UI");
        private static readonly string UiFamilySemibold = PickFamily("Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI");
        private static readonly string DisplayFamilySemibold = PickFamily("Segoe UI Variable Display Semibold", "Segoe UI Semibold", "Segoe UI");
        private static readonly string MonoFamily = PickFamily("Cascadia Mono", "Consolas");
        public static readonly string IconFamily = PickFamily("Segoe Fluent Icons", "Segoe MDL2 Assets");

        public static Font Body => new Font(UiFamily, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Caption => new Font(UiFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font BodyStrong => new Font(UiFamilySemibold, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Subtitle => new Font(UiFamilySemibold, 12f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Title => new Font(DisplayFamilySemibold, 21f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Mono => new Font(MonoFamily, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static Font Icon(float size) => new Font(IconFamily, size, FontStyle.Regular, GraphicsUnit.Point);

        public const float CornerRadius = 4f;
        public const float CardRadius = 8f;

        private static string PickFamily(params string[] names)
        {
            foreach (var name in names)
            {
                using (var font = new Font(name, 10f))
                {
                    if (string.Equals(font.Name, name, StringComparison.OrdinalIgnoreCase)) return name;
                }
            }
            return names[names.Length - 1];
        }

        private static Color Blend(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R * amount + b.R * (1 - amount)),
                (int)(a.G * amount + b.G * (1 - amount)),
                (int)(a.B * amount + b.B * (1 - amount)));
        }

        /// <summary>
        /// Windows 11 dark mode uses the "Light 2" shade of the user's accent colour for primary controls.
        /// </summary>
        private static Color ReadAccent()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 8)
                    {
                        return Color.FromArgb(palette[4], palette[5], palette[6]);
                    }
                }
            }
            catch
            {
                // Fall through to the default Windows 11 accent.
            }
            return Color.FromArgb(96, 205, 255);
        }

        private static readonly float SystemScale = GetSystemScale();

        private static float GetSystemScale()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX / 96f;
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// The process is system DPI aware without WinForms' high DPI mode, where Control.DeviceDpi
        /// always reports 96, so scale by the system DPI instead.
        /// </summary>
        public static float Scale(Control control)
        {
            return SystemScale;
        }

        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            if (d <= 0 || rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Paints the area behind a control with its parent's colour, so rounded corners blend in.
        /// </summary>
        public static void PaintParentBackground(Control control, Graphics g)
        {
            // skip transparent containers (e.g. a row of controls) up to the surface they sit on
            var parent = control.Parent;
            while (parent != null && parent.BackColor.A < 255) parent = parent.Parent;
            var parentColor = parent?.BackColor ?? Background;
            using (var brush = new SolidBrush(parentColor))
            {
                g.FillRectangle(brush, control.ClientRectangle);
            }
        }

        #region DWM

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static extern int DwmSetWindowAttributeInt(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Dark title bar, rounded corners and a caption that matches the window background (Windows 11).
        /// Silently ignored on older systems.
        /// </summary>
        public static void ApplyWindowChrome(Form form)
        {
            try
            {
                var handle = form.Handle;
                var dark = 1;
                DwmSetWindowAttributeInt(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                var corner = DWMWCP_ROUND;
                DwmSetWindowAttributeInt(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

                var caption = ColorTranslator.ToWin32(Background);
                DwmSetWindowAttributeInt(handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        /// <summary>
        /// Dark scrollbars / dropdowns for native child windows.
        /// </summary>
        public static void ApplyDarkControlTheme(IntPtr handle, string theme = "DarkMode_Explorer")
        {
            try
            {
                SetWindowTheme(handle, theme, null);
            }
            catch (DllNotFoundException)
            {
            }
        }

        /// <summary>
        /// Dark scrollbars for a control, applied whenever its handle is (re)created.
        /// </summary>
        public static void UseDarkScrollBars(Control control)
        {
            if (control.IsHandleCreated) ApplyDarkControlTheme(control.Handle);
            control.HandleCreated += (sender, e) => ApplyDarkControlTheme(control.Handle);
        }

        #endregion
    }

    /// <summary>
    /// Base form with the Windows 11 dark look: Segoe UI Variable, dark caption and rounded corners.
    /// </summary>
    public class FluentForm : Form
    {
        public FluentForm()
        {
            BackColor = FluentTheme.Background;
            ForeColor = FluentTheme.Text;
            Font = FluentTheme.Body;
            DoubleBuffered = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FluentTheme.ApplyWindowChrome(this);
        }

        /// <summary>
        /// Raises the window above the others and tries to give it focus. Windows may refuse the focus
        /// (it then flashes the taskbar button), but toggling TopMost still puts the window on top.
        /// </summary>
        public void BringToForeground()
        {
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            TopMost = true;
            TopMost = false;
            Activate();
        }
    }

    /// <summary>
    /// Rounded "settings card" surface.
    /// </summary>
    public class FluentCard : Panel
    {
        public FluentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = FluentTheme.Card;
            ForeColor = FluentTheme.Text;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CardRadius * s))
            using (var fill = new SolidBrush(BackColor))
            using (var border = new Pen(FluentTheme.CardBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
        }
    }

    /// <summary>
    /// Lays its children out top to bottom at full width and sizes itself to fit them.
    /// A much cheaper replacement for an auto-sized TableLayoutPanel.
    /// </summary>
    public class FluentStackPanel : Panel
    {
        public FluentStackPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = FluentTheme.Background;
        }

        /// <summary>Height of a child: its preferred height for the given width, or its current height.</summary>
        private static int ChildHeight(Control child, int width)
        {
            if (child is Label || child is FlowLayoutPanel)
                return child.GetPreferredSize(new Size(width, 0)).Height;
            return child.Height;
        }

        private bool _layingOut;

        protected override void OnLayout(LayoutEventArgs levent)
        {
            // moving the children re-enters layout once per child; one pass is enough
            if (_layingOut) return;
            _layingOut = true;
            try
            {
                var y = Padding.Top;
                var width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
                foreach (Control child in Controls)
                {
                    if (!child.Visible) continue;
                    y += child.Margin.Top;
                    var childWidth = Math.Max(0, width - child.Margin.Horizontal);
                    var height = ChildHeight(child, childWidth);
                    child.SetBounds(Padding.Left + child.Margin.Left, y, childWidth, height);
                    y += height + child.Margin.Bottom;
                }
                var total = y + Padding.Bottom;
                if (Height != total) Height = total;
            }
            finally
            {
                _layingOut = false;
            }
        }
    }

    /// <summary>
    /// A row of a settings page: optional icon, title and description painted on the left,
    /// one control on the right.
    /// </summary>
    public class FluentSettingCard : FluentCard
    {
        private Control _content;

        public FluentSettingCard()
        {
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public Image Icon { get; set; }

        public Control Content
        {
            get => _content;
            set
            {
                if (_content != null) Controls.Remove(_content);
                _content = value;
                if (_content != null) Controls.Add(_content);
                PerformLayout();
            }
        }

        /// <summary>Width a derived card paints on the right instead of hosting a control there.</summary>
        protected virtual int ReservedRight => 0;

        private int ContentLeft
        {
            get
            {
                if (_content == null) return Width - Padding.Right - ReservedRight;
                return _content.Left - (int)(12 * FluentTheme.Scale(this));
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_content == null) return;
            var size = _content.AutoSize ? _content.GetPreferredSize(Size.Empty) : _content.Size;
            _content.SetBounds(Width - Padding.Right - size.Width, (Height - size.Height) / 2, size.Width, size.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            var s = FluentTheme.Scale(this);
            var x = Padding.Left;
            if (Icon != null)
            {
                var iconSize = (int)(24 * s);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Icon, new Rectangle(x, (Height - iconSize) / 2, iconSize, iconSize));
                x += iconSize + (int)(14 * s);
            }

            var textWidth = Math.Max(0, ContentLeft - x);
            var flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            if (string.IsNullOrEmpty(Description))
            {
                TextRenderer.DrawText(g, Title, Font, new Rectangle(x, 0, textWidth, Height), FluentTheme.Text, flags | TextFormatFlags.VerticalCenter);
                return;
            }

            using (var caption = FluentTheme.Caption)
            {
                var titleHeight = TextRenderer.MeasureText(g, "Ag", Font, Size.Empty, flags).Height;
                var captionHeight = TextRenderer.MeasureText(g, "Ag", caption, Size.Empty, flags).Height;
                var gap = (int)(2 * s);
                var top = (Height - titleHeight - gap - captionHeight) / 2;
                TextRenderer.DrawText(g, Title, Font, new Rectangle(x, top, textWidth, titleHeight), FluentTheme.Text, flags);
                TextRenderer.DrawText(g, Description, caption, new Rectangle(x, top + titleHeight + gap, textWidth, captionHeight), FluentTheme.TextSecondary, flags);
            }
        }
    }

    /// <summary>
    /// Setting card with an on/off switch painted on its right. The switch is part of the card
    /// (no window of its own), which keeps pages with many switches quick to show.
    /// </summary>
    [DefaultEvent("CheckedChanged")]
    public class FluentToggleCard : FluentSettingCard
    {
        private bool _checked;
        private bool _hover;

        public FluentToggleCard()
        {
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public event EventHandler CheckedChanged;

        public string OnText { get; set; } = "On";
        public string OffText { get; set; } = "Off";

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private Size TrackSize => new Size((int)Math.Round(40 * FluentTheme.Scale(this)), (int)Math.Round(20 * FluentTheme.Scale(this)));

        private RectangleF TrackRect
        {
            get
            {
                var track = TrackSize;
                return new RectangleF(Width - Padding.Right - track.Width + 0.5f, (Height - track.Height) / 2f + 0.5f, track.Width - 1f, track.Height - 1f);
            }
        }

        /// <summary>The switch and its On/Off text: the part that reacts to the mouse.</summary>
        private Rectangle ActiveRect
        {
            get
            {
                var s = FluentTheme.Scale(this);
                var textWidth = Math.Max(TextRenderer.MeasureText(OnText, Font).Width, TextRenderer.MeasureText(OffText, Font).Width);
                var width = TrackSize.Width + (int)(12 * s) + textWidth;
                return new Rectangle(Width - Padding.Right - width, 0, width, Height);
            }
        }

        protected override int ReservedRight => ActiveRect.Width + (int)(12 * FluentTheme.Scale(this));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var hover = ActiveRect.Contains(e.Location);
            if (hover != _hover) { _hover = hover; Invalidate(); }
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && ActiveRect.Contains(e.Location))
            {
                Focus();
                Checked = !Checked;
            }
            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var s = FluentTheme.Scale(this);
            var rect = TrackRect;
            var radius = rect.Height / 2f;

            using (var path = FluentTheme.RoundedRect(rect, radius))
            {
                if (_checked)
                {
                    using (var fill = new SolidBrush(_hover ? FluentTheme.AccentHover : FluentTheme.Accent))
                        g.FillPath(fill, path);
                }
                else
                {
                    using (var fill = new SolidBrush(_hover ? FluentTheme.ControlHover : FluentTheme.Input))
                    using (var pen = new Pen(Color.FromArgb(160, 160, 160), Math.Max(1f, s)))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }
                }
            }

            var knob = (_hover ? 14f : 12f) * s;
            var knobX = _checked ? rect.Right - radius - knob / 2f : rect.X + radius - knob / 2f;
            using (var brush = new SolidBrush(_checked ? FluentTheme.TextOnAccent : FluentTheme.TextSecondary))
                g.FillEllipse(brush, knobX, rect.Y + (rect.Height - knob) / 2f, knob, knob);

            var textRect = new Rectangle(ActiveRect.X, 0, (int)rect.X - ActiveRect.X - (int)(12 * s), Height);
            TextRenderer.DrawText(g, _checked ? OnText : OffText, Font, textRect, FluentTheme.Text,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            if (Focused && ShowFocusCues)
            {
                var ring = rect;
                ring.Inflate(3 * s, 3 * s);
                using (var pen = new Pen(FluentTheme.Text, 1.5f * s))
                using (var path = FluentTheme.RoundedRect(ring, ring.Height / 2f))
                    g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Fluent button. <see cref="Accent"/> renders it as the primary (accent coloured) action.
    /// </summary>
    public class FluentButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private bool _accent;

        public FluentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ForeColor = FluentTheme.Text;
        }

        [DefaultValue(false)]
        public bool Accent
        {
            get => _accent;
            set { _accent = value; Invalidate(); }
        }

        /// <summary>Optional Segoe Fluent Icons glyph shown before the text.</summary>
        [DefaultValue(null)]
        public string Glyph { get; set; }

        [DefaultValue(96)]
        public int MinimumWidth96 { get; set; } = 96;

        private int GlyphWidth(float s) => string.IsNullOrEmpty(Glyph) ? 0 : (int)(24 * s);

        public override Size GetPreferredSize(Size proposedSize)
        {
            var s = FluentTheme.Scale(this);
            var text = TextRenderer.MeasureText(Text, Font);
            var width = Math.Max(text.Width + GlyphWidth(s) + (int)(28 * s), (int)(MinimumWidth96 * s));
            var height = Math.Max(text.Height + (int)(10 * s), (int)(32 * s));
            return new Size(width, height);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            Color fill, border, text;
            if (!Enabled)
            {
                fill = _accent ? Color.FromArgb(67, 67, 67) : FluentTheme.ControlPressed;
                border = _accent ? fill : FluentTheme.ControlBorder;
                text = FluentTheme.TextDisabled;
            }
            else if (_accent)
            {
                fill = _pressed ? FluentTheme.AccentPressed : _hover ? FluentTheme.AccentHover : FluentTheme.Accent;
                border = fill;
                text = FluentTheme.TextOnAccent;
            }
            else
            {
                fill = _pressed ? FluentTheme.ControlPressed : _hover ? FluentTheme.ControlHover : FluentTheme.Control;
                border = FluentTheme.ControlBorder;
                text = _pressed ? FluentTheme.TextSecondary : FluentTheme.Text;
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (Focused && ShowFocusCues)
            {
                using (var ring = FluentTheme.RoundedRect(rect, (FluentTheme.CornerRadius + 2) * s))
                using (var pen = new Pen(FluentTheme.Text, 2 * s))
                {
                    pen.Alignment = PenAlignment.Inset;
                    g.DrawPath(pen, ring);
                }
                rect.Inflate(-3 * s, -3 * s);
            }

            using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var glyphWidth = GlyphWidth(s);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (glyphWidth == 0)
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, text, flags | TextFormatFlags.HorizontalCenter);
                return;
            }

            // glyph + text, centred together
            var textSize = TextRenderer.MeasureText(Text, Font);
            var total = glyphWidth + textSize.Width;
            var x = Math.Max((int)(8 * s), (Width - total) / 2);
            using (var iconFont = FluentTheme.Icon(10.5f))
            {
                TextRenderer.DrawText(g, Glyph, iconFont, new Rectangle(x, 0, glyphWidth, Height), text, flags | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + glyphWidth, 0, Width - x - glyphWidth, Height), text, flags | TextFormatFlags.Left);
        }
    }

    /// <summary>
    /// Rounded text box with the Windows 11 accent underline when focused.
    /// </summary>
    [DefaultEvent("TextChanged")]
    public class FluentTextBox : Control
    {
        private readonly TextBox _inner = new TextBox();

        public FluentTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);

            _inner.BorderStyle = BorderStyle.None;
            _inner.BackColor = FluentTheme.Input;
            _inner.ForeColor = FluentTheme.Text;
            _inner.GotFocus += (sender, e) => UpdateFocusState();
            _inner.LostFocus += (sender, e) => UpdateFocusState();
            _inner.TextChanged += (sender, e) => OnTextChanged(e);
            _inner.SizeChanged += (sender, e) => LayoutInner();
            Controls.Add(_inner);

            BackColor = FluentTheme.Input;
            ForeColor = FluentTheme.Text;
            Cursor = Cursors.IBeam;
        }

        /// <summary>The hosted native text box, for key handling and validation.</summary>
        [Browsable(false)]
        public TextBox Inner => _inner;

        [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public override string Text
        {
            get => _inner.Text;
            set => _inner.Text = value;
        }

        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _inner.ReadOnly;
            set => _inner.ReadOnly = value;
        }

        protected override Size DefaultSize => new Size(160, 32);

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _inner.Font = Font;
            LayoutInner();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _inner.Focus();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _inner.Focus();
        }

        private void UpdateFocusState()
        {
            _inner.BackColor = _inner.Focused ? FluentTheme.InputFocused : FluentTheme.Input;
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutInner();
        }

        protected virtual int RightReserve => 0;

        private bool _layingOut;

        private void LayoutInner()
        {
            if (_layingOut) return;
            _layingOut = true;
            try
            {
                var s = FluentTheme.Scale(this);
                var padX = (int)(11 * s);
                _inner.Width = Math.Max(0, Width - padX * 2 - RightReserve);
                _inner.Location = new Point(padX, (Height - _inner.Height) / 2);
            }
            finally
            {
                _layingOut = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var focused = _inner.Focused;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            var radius = FluentTheme.CornerRadius * s;

            using (var path = FluentTheme.RoundedRect(rect, radius))
            using (var fill = new SolidBrush(focused ? FluentTheme.InputFocused : FluentTheme.Input))
            using (var border = new Pen(FluentTheme.ControlBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);

                // Bottom stroke: accent and thicker when focused, like WinUI's TextBox.
                var thickness = focused ? Math.Max(2, (int)Math.Round(2 * s)) : Math.Max(1, (int)Math.Round(s));
                var clip = g.Clip;
                g.SetClip(new RectangleF(0, Height - thickness, Width, thickness));
                using (var bottom = new SolidBrush(focused ? FluentTheme.Accent : Color.FromArgb(140, 140, 140)))
                {
                    g.FillPath(bottom, path);
                }
                g.Clip = clip;
            }
            PaintExtras(g, s);
        }

        protected virtual void PaintExtras(Graphics g, float s) { }
    }

    /// <summary>
    /// Text box for a hexadecimal colour with a clickable colour sample inside it, on the right.
    /// One window instead of a text box plus a separate swatch control.
    /// </summary>
    [DefaultEvent("ColorChanged")]
    public class FluentColorBox : FluentTextBox
    {
        private Color _color = Color.White;

        public event EventHandler ColorChanged;

        public Color Color
        {
            get => _color;
            set { _color = value; Invalidate(); }
        }

        private int SwatchSize => (int)Math.Round(22 * FluentTheme.Scale(this));
        protected override int RightReserve => SwatchSize + (int)(4 * FluentTheme.Scale(this));

        private Rectangle SwatchRect
        {
            get
            {
                var size = SwatchSize;
                return new Rectangle(Width - size - (int)(6 * FluentTheme.Scale(this)), (Height - size) / 2, size, size);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = SwatchRect.Contains(e.Location) ? Cursors.Hand : Cursors.IBeam;
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!SwatchRect.Contains(e.Location))
            {
                base.OnMouseDown(e);
                return;
            }
            using (var dialog = new ColorDialog { Color = _color, FullOpen = true })
            {
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    Color = dialog.Color;
                    ColorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void PaintExtras(Graphics g, float s)
        {
            var rect = SwatchRect;
            using (var path = FluentTheme.RoundedRect(new RectangleF(rect.X + 0.5f, rect.Y + 0.5f, rect.Width - 1f, rect.Height - 1f), FluentTheme.CornerRadius * s / 2))
            using (var fill = new SolidBrush(Color.FromArgb(255, _color)))
            using (var pen = new Pen(FluentTheme.ControlBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Several check boxes side by side, painted in a single window.
    /// </summary>
    public class FluentCheckGroup : Control
    {
        private readonly List<string> _texts = new List<string>();
        private readonly List<bool> _checked = new List<bool>();
        private int _hover = -1;

        public FluentCheckGroup()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            ForeColor = FluentTheme.Text;
            Cursor = Cursors.Hand;
        }

        /// <summary>Raised with the index of the item whose state changed.</summary>
        public event EventHandler<int> CheckedChanged;

        public void AddItem(string text, bool isChecked)
        {
            _texts.Add(text);
            _checked.Add(isChecked);
            Invalidate();
        }

        public bool IsChecked(int index) => _checked[index];

        private int BoxSize => (int)Math.Round(20 * FluentTheme.Scale(this));
        private int TextGap => (int)Math.Round(10 * FluentTheme.Scale(this));
        private int ItemGap => (int)Math.Round(24 * FluentTheme.Scale(this));

        private int ItemWidth(int index) => BoxSize + TextGap + TextRenderer.MeasureText(_texts[index], Font).Width;

        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = 0;
            for (var i = 0; i < _texts.Count; i++) width += ItemWidth(i) + (i > 0 ? ItemGap : 0);
            return new Size(width, Math.Max(BoxSize, Font.Height) + (int)(8 * FluentTheme.Scale(this)));
        }

        private int IndexAt(Point p)
        {
            var x = 0;
            for (var i = 0; i < _texts.Count; i++)
            {
                var w = ItemWidth(i);
                if (p.X >= x && p.X < x + w) return i;
                x += w + ItemGap;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var index = IndexAt(e.Location);
            if (index != _hover) { _hover = index; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var index = IndexAt(e.Location);
            if (e.Button == MouseButtons.Left && index >= 0)
            {
                _checked[index] = !_checked[index];
                Invalidate();
                CheckedChanged?.Invoke(this, index);
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            var s = FluentTheme.Scale(this);
            var box = BoxSize;
            var x = 0;
            for (var i = 0; i < _texts.Count; i++)
            {
                var boxRect = new RectangleF(x + 0.5f, (Height - box) / 2f + 0.5f, box - 1f, box - 1f);
                FluentCheckGlyph.Draw(g, boxRect, _checked[i], i == _hover, s);
                var textX = x + box + TextGap;
                TextRenderer.DrawText(g, _texts[i], Font, new Rectangle(textX, 0, Width - textX, Height), ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                x += ItemWidth(i) + ItemGap;
            }
        }
    }

    /// <summary>
    /// Integer box with WinUI NumberBox behaviour: arrow keys and the mouse wheel step the value.
    /// </summary>
    [DefaultEvent("ValueChanged")]
    public class FluentNumberBox : FluentTextBox
    {
        private int _value;
        private bool _updating;

        public FluentNumberBox()
        {
            Inner.KeyPress += (sender, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(e.KeyChar == '-' && Minimum < 0))
                    e.Handled = true;
            };
            Inner.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.Up) { Value = _value + 1; e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { Value = _value - 1; e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            Inner.MouseWheel += (sender, e) => Value = _value + Math.Sign(e.Delta);
            Inner.LostFocus += (sender, e) => Commit();
            Text = "0";
        }

        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 1000;

        public event EventHandler ValueChanged;

        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                var clamped = Math.Max(Minimum, Math.Min(Maximum, value));
                _updating = true;
                Text = clamped.ToString();
                _updating = false;
                if (clamped == _value) return;
                _value = clamped;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (!_updating && int.TryParse(Text, out var parsed) && parsed >= Minimum && parsed <= Maximum && parsed != _value)
            {
                _value = parsed;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Commit()
        {
            Value = int.TryParse(Text, out var parsed) ? parsed : _value;
        }
    }

    /// <summary>
    /// Windows 11 toggle switch.
    /// </summary>
    public class FluentToggle : CheckBox
    {
        private bool _hover;

        public FluentToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            AutoSize = false;
            ForeColor = FluentTheme.Text;
        }

        /// <summary>Text shown left of the switch for each state, as in the Settings app.</summary>
        public string OnText { get; set; } = "On";
        public string OffText { get; set; } = "Off";

        private Size TrackSize(float s) => new Size((int)Math.Round(40 * s), (int)Math.Round(20 * s));

        public override Size GetPreferredSize(Size proposedSize)
        {
            var s = FluentTheme.Scale(this);
            var track = TrackSize(s);
            var text = Math.Max(TextRenderer.MeasureText(OnText, Font).Width, TextRenderer.MeasureText(OffText, Font).Width);
            return new Size(track.Width + text + (int)(12 * s), Math.Max(track.Height, Font.Height) + (int)(8 * s));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var track = TrackSize(s);
            var rect = new RectangleF(Width - track.Width + 0.5f, (Height - track.Height) / 2f + 0.5f, track.Width - 1f, track.Height - 1f);
            var radius = rect.Height / 2f;

            using (var path = FluentTheme.RoundedRect(rect, radius))
            {
                if (Checked)
                {
                    using (var fill = new SolidBrush(_hover ? FluentTheme.AccentHover : FluentTheme.Accent))
                        g.FillPath(fill, path);
                }
                else
                {
                    using (var fill = new SolidBrush(_hover ? FluentTheme.ControlHover : FluentTheme.Input))
                    using (var pen = new Pen(Color.FromArgb(160, 160, 160), Math.Max(1f, s)))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }
                }
            }

            var knob = (_hover ? 14f : 12f) * s;
            var knobX = Checked ? rect.Right - radius - knob / 2f : rect.X + radius - knob / 2f;
            var knobY = rect.Y + (rect.Height - knob) / 2f;
            using (var brush = new SolidBrush(Checked ? FluentTheme.TextOnAccent : FluentTheme.TextSecondary))
                g.FillEllipse(brush, knobX, knobY, knob, knob);

            var textRect = new Rectangle(0, 0, Width - track.Width - (int)(12 * s), Height);
            TextRenderer.DrawText(g, Checked ? OnText : OffText, Font, textRect, Enabled ? ForeColor : FluentTheme.TextDisabled,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            if (Focused && ShowFocusCues)
            {
                var ring = rect;
                ring.Inflate(3 * s, 3 * s);
                using (var pen = new Pen(FluentTheme.Text, 1.5f * s))
                using (var path = FluentTheme.RoundedRect(ring, ring.Height / 2f))
                    g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Fluent check box with a rounded, accent-filled box.
    /// </summary>
    public class FluentCheckBox : CheckBox
    {
        private bool _hover;

        public FluentCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            ForeColor = FluentTheme.Text;
        }

        private int BoxSize => (int)Math.Round(20 * FluentTheme.Scale(this));
        private int Gap => string.IsNullOrEmpty(Text) ? 0 : (int)Math.Round(10 * FluentTheme.Scale(this));

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font);
            var s = FluentTheme.Scale(this);
            return new Size(BoxSize + Gap + text.Width + (int)(4 * s), Math.Max(BoxSize, text.Height) + (int)(8 * s));
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var box = BoxSize;
            var boxRect = new RectangleF(0.5f, (Height - box) / 2f + 0.5f, box - 1f, box - 1f);
            FluentCheckGlyph.Draw(g, boxRect, Checked, _hover, s);

            var textRect = new Rectangle(box + Gap, 0, Width - box - Gap, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : FluentTheme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }

    /// <summary>
    /// The check box square, shared with owner-drawn lists.
    /// </summary>
    public static class FluentCheckGlyph
    {
        public static void Draw(Graphics g, RectangleF boxRect, bool isChecked, bool hover, float s)
        {
            var oldMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = FluentTheme.RoundedRect(boxRect, FluentTheme.CornerRadius * s))
            {
                if (isChecked)
                {
                    using (var fill = new SolidBrush(hover ? FluentTheme.AccentHover : FluentTheme.Accent))
                        g.FillPath(fill, path);

                    using (var pen = new Pen(FluentTheme.TextOnAccent, 1.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        var x = boxRect.X;
                        var y = boxRect.Y;
                        var w = boxRect.Width;
                        g.DrawLines(pen, new[]
                        {
                            new PointF(x + w * 0.26f, y + w * 0.52f),
                            new PointF(x + w * 0.43f, y + w * 0.68f),
                            new PointF(x + w * 0.75f, y + w * 0.34f)
                        });
                    }
                }
                else
                {
                    using (var fill = new SolidBrush(hover ? FluentTheme.ControlHover : FluentTheme.Input))
                    using (var pen = new Pen(Color.FromArgb(160, 160, 160)))
                    {
                        g.FillPath(fill, path);
                        g.DrawPath(pen, path);
                    }
                }
            }
            g.SmoothingMode = oldMode;
        }
    }

    /// <summary>
    /// Drop-down list styled as a WinUI ComboBox. The closed state is painted entirely by us.
    /// </summary>
    public class FluentComboBox : ComboBox
    {
        private const int WM_PAINT = 0x000F;
        private bool _hover;

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo;
            public IntPtr hwndItem;
            public IntPtr hwndList;
        }

        [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr hWnd, ref COMBOBOXINFO pcbi);

        public FluentComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = FluentTheme.Card;
            ForeColor = FluentTheme.Text;
            Cursor = Cursors.Hand;
            // The closed height of an owner-drawn drop-down list follows ItemHeight; it has to be set
            // before the handle exists, changing it afterwards only affects the list.
            ItemHeight = (int)Math.Round(26 * FluentTheme.Scale(this));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO)) };
            if (GetComboBoxInfo(Handle, ref info) && info.hwndList != IntPtr.Zero)
            {
                FluentTheme.ApplyDarkControlTheme(info.hwndList);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            var s = FluentTheme.Scale(this);
            var selected = (e.State & DrawItemState.Selected) != 0;

            using (var bg = new SolidBrush(selected ? FluentTheme.ControlHover : FluentTheme.Card))
            {
                g.FillRectangle(bg, e.Bounds);
            }

            if (selected)
            {
                // Accent "pill" on the left of the highlighted item.
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var pillHeight = e.Bounds.Height * 0.5f;
                var pill = new RectangleF(e.Bounds.X + 2 * s, e.Bounds.Y + (e.Bounds.Height - pillHeight) / 2, 3 * s, pillHeight);
                using (var path = FluentTheme.RoundedRect(pill, 1.5f * s))
                using (var brush = new SolidBrush(FluentTheme.Accent))
                {
                    g.FillPath(brush, path);
                }
            }

            var textRect = new Rectangle(e.Bounds.X + (int)(12 * s), e.Bounds.Y, e.Bounds.Width - (int)(12 * s), e.Bounds.Height);
            TextRenderer.DrawText(g, GetItemText(Items[e.Index]), Font, textRect, FluentTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != WM_PAINT)
            {
                base.WndProc(ref m);
                return;
            }

            BeginPaint(Handle, out var ps);
            try
            {
                using (var buffer = new Bitmap(Math.Max(1, Width), Math.Max(1, Height)))
                {
                    using (var g = Graphics.FromImage(buffer))
                    {
                        PaintClosed(g);
                    }
                    using (var target = Graphics.FromHdc(ps.hdc))
                    {
                        // Explicit size: the bitmap and the window DC may report different DPIs.
                        target.DrawImage(buffer, 0, 0, buffer.Width, buffer.Height);
                    }
                }
            }
            finally
            {
                EndPaint(Handle, ref ps);
            }
        }

        private void PaintClosed(Graphics g)
        {
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var fill = DroppedDown ? FluentTheme.ControlPressed : _hover ? FluentTheme.ControlHover : FluentTheme.Control;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

            using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Focused && !DroppedDown ? FluentTheme.Accent : FluentTheme.ControlBorder))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var chevronWidth = (int)(32 * s);
            var textRect = new Rectangle((int)(11 * s), 0, Width - (int)(11 * s) - chevronWidth, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? FluentTheme.Text : FluentTheme.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // Chevron
            var cx = Width - chevronWidth / 2f - 2 * s;
            var cy = Height / 2f;
            var half = 4.5f * s;
            using (var pen = new Pen(FluentTheme.TextSecondary, 1.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - half, cy - half / 2),
                    new PointF(cx, cy + half / 2),
                    new PointF(cx + half, cy - half / 2)
                });
            }
        }
    }

    /// <summary>
    /// Rounded colour sample that opens the colour picker when clicked.
    /// </summary>
    [DefaultEvent("ColorChanged")]
    public class FluentColorSwatch : Control
    {
        private bool _hover;
        private Color _color = Color.White;

        public FluentColorSwatch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
        }

        public event EventHandler ColorChanged;

        public Color Color
        {
            get => _color;
            set { _color = value; Invalidate(); }
        }

        protected override Size DefaultSize => new Size(32, 32);

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            using (var dialog = new ColorDialog { Color = _color, FullOpen = true })
            {
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    Color = dialog.Color;
                    ColorChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
            using (var fill = new SolidBrush(Color.FromArgb(255, _color)))
            using (var pen = new Pen(_hover ? FluentTheme.TextSecondary : FluentTheme.ControlBorder, Math.Max(1f, s)))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Records a key combination. The value uses the HOTKEY control's format:
    /// virtual key in the low byte, HOTKEYF_* modifiers in the next one.
    /// </summary>
    [DefaultEvent("HotKeyChanged")]
    public class FluentHotKeyBox : Control
    {
        private const int HOTKEYF_SHIFT = 0x01;
        private const int HOTKEYF_CONTROL = 0x02;
        private const int HOTKEYF_ALT = 0x04;

        private int _hotKey;

        public FluentHotKeyBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = FluentTheme.Input;
            ForeColor = FluentTheme.Text;
            TabStop = true;
            Cursor = Cursors.Hand;
        }

        public event EventHandler HotKeyChanged;

        public string NoneText { get; set; } = "None";

        public int HotKey
        {
            get => _hotKey;
            set { _hotKey = value; Invalidate(); }
        }

        protected override Size DefaultSize => new Size(160, 32);

        protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData) => true;

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!Focused) return base.ProcessCmdKey(ref msg, keyData);
            Record(keyData);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Record(e.KeyData);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void Record(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
                return;

            int value;
            if ((key == Keys.Back || key == Keys.Delete) && (keyData & Keys.Modifiers) == Keys.None)
            {
                value = 0;
            }
            else
            {
                var modifiers = 0;
                if ((keyData & Keys.Shift) != 0) modifiers |= HOTKEYF_SHIFT;
                if ((keyData & Keys.Control) != 0) modifiers |= HOTKEYF_CONTROL;
                if ((keyData & Keys.Alt) != 0) modifiers |= HOTKEYF_ALT;
                value = (modifiers << 8) | ((int)key & 0xFF);
            }

            if (value == _hotKey) return;
            HotKey = value;
            HotKeyChanged?.Invoke(this, EventArgs.Empty);
        }

        public static string Describe(int hotKey, string noneText)
        {
            if (hotKey == 0) return noneText;
            var parts = new List<string>();
            var modifiers = (hotKey >> 8) & 0xFF;
            if ((modifiers & HOTKEYF_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & HOTKEYF_ALT) != 0) parts.Add("Alt");
            if ((modifiers & HOTKEYF_SHIFT) != 0) parts.Add("Shift");
            var key = (Keys)(hotKey & 0xFF);
            parts.Add(KeyName(key));
            return string.Join(" + ", parts);
        }

        private static string KeyName(Keys key)
        {
            switch (key)
            {
                case Keys.Next: return "Page Down";
                case Keys.Prior: return "Page Up";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "+";
                case Keys.Return: return "Enter";
                case Keys.Back: return "Backspace";
                default:
                    if (key >= Keys.D0 && key <= Keys.D9) return ((int)(key - Keys.D0)).ToString();
                    return key.ToString();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            FluentTheme.PaintParentBackground(this, g);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
            using (var fill = new SolidBrush(Focused ? FluentTheme.InputFocused : FluentTheme.Input))
            using (var border = new Pen(FluentTheme.ControlBorder))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);

                var thickness = Focused ? Math.Max(2, (int)Math.Round(2 * s)) : Math.Max(1, (int)Math.Round(s));
                var clip = g.Clip;
                g.SetClip(new RectangleF(0, Height - thickness, Width, thickness));
                using (var bottom = new SolidBrush(Focused ? FluentTheme.Accent : Color.FromArgb(140, 140, 140)))
                    g.FillPath(bottom, path);
                g.Clip = clip;
            }

            var textRect = new Rectangle((int)(11 * s), 0, Width - (int)(22 * s), Height);
            TextRenderer.DrawText(g, Describe(_hotKey, NoneText), Font, textRect,
                _hotKey == 0 ? FluentTheme.TextSecondary : FluentTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Left navigation pane as in the Windows 11 Settings app.
    /// </summary>
    public class FluentNavList : Control
    {
        private readonly List<KeyValuePair<string, string>> _items = new List<KeyValuePair<string, string>>();
        private int _selected = -1;
        private int _hover = -1;

        public FluentNavList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            BackColor = FluentTheme.Background;
            ForeColor = FluentTheme.Text;
            TabStop = true;
        }

        public event EventHandler SelectedIndexChanged;

        /// <summary>Adds an entry with a Segoe Fluent Icons glyph.</summary>
        public void AddItem(string glyph, string text)
        {
            _items.Add(new KeyValuePair<string, string>(glyph, text));
            Invalidate();
        }

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value == _selected || value < -1 || value >= _items.Count) return;
                _selected = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private int ItemHeight => (int)Math.Round(40 * FluentTheme.Scale(this));
        private int Gap => (int)Math.Round(4 * FluentTheme.Scale(this));

        private int IndexAt(Point p)
        {
            if (p.Y < 0) return -1;
            var index = p.Y / (ItemHeight + Gap);
            return index < _items.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var index = IndexAt(e.Location);
            if (index != _hover) { _hover = index; Invalidate(); }
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var index = IndexAt(e.Location);
            if (index >= 0) SelectedIndex = index;
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData) => keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up && _selected > 0) SelectedIndex = _selected - 1;
            else if (e.KeyCode == Keys.Down && _selected < _items.Count - 1) SelectedIndex = _selected + 1;
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var s = FluentTheme.Scale(this);
            using (var iconFont = FluentTheme.Icon(12f))
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var y = i * (ItemHeight + Gap);
                    var rect = new RectangleF(0.5f, y + 0.5f, Width - 1f, ItemHeight - 1f);
                    if (i == _selected || i == _hover)
                    {
                        using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
                        using (var brush = new SolidBrush(i == _selected ? FluentTheme.NavSelected : FluentTheme.NavHover))
                            g.FillPath(brush, path);
                    }
                    if (i == _selected)
                    {
                        var pillHeight = 16 * s;
                        var pill = new RectangleF(rect.X, y + (ItemHeight - pillHeight) / 2f, 3 * s, pillHeight);
                        using (var path = FluentTheme.RoundedRect(pill, 1.5f * s))
                        using (var brush = new SolidBrush(FluentTheme.Accent))
                            g.FillPath(brush, path);
                    }
                    if (i == _selected && Focused && ShowFocusCues)
                    {
                        using (var path = FluentTheme.RoundedRect(rect, FluentTheme.CornerRadius * s))
                        using (var pen = new Pen(FluentTheme.Text, 1.5f * s))
                            g.DrawPath(pen, path);
                    }

                    var iconRect = new Rectangle((int)(16 * s), y, (int)(20 * s), ItemHeight);
                    TextRenderer.DrawText(g, _items[i].Key, iconFont, iconRect, FluentTheme.Text,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    var textRect = new Rectangle((int)(48 * s), y, Width - (int)(56 * s), ItemHeight);
                    TextRenderer.DrawText(g, _items[i].Value, Font, textRect, FluentTheme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }
}
