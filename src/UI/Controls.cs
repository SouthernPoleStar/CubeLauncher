using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CubeLauncher.UI
{
    public enum ButtonStyle
    {
        Accent, Ghost, Dark, Danger, Subtle
    }

    public class AccentButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _loading;
        private ButtonStyle _style;
        private float _hoverT;
        private readonly Timer _hoverTimer;

        public IconKind? Icon;
        public int IconSize = 18;

        public AccentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Font(9.5f, FontStyle.Bold);
            ForeColor = Theme.OnAccent;
            Cursor = Cursors.Hand;
            Height = 42;
            _style = ButtonStyle.Accent;
            _hoverTimer = new Timer();
            _hoverTimer.Interval = 16;
            _hoverTimer.Tick += delegate
            {
                float target = _hover && Enabled ? 1f : 0f;
                float distance = target - _hoverT;
                if (Math.Abs(distance) < 0.015f)
                {
                    _hoverT = target;
                    _hoverTimer.Stop();
                }
                else
                {
                    _hoverT += distance * 0.22f;
                }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _hoverTimer != null) _hoverTimer.Dispose();
            base.Dispose(disposing);
        }

        public ButtonStyle Style
        {
            get { return _style; }
            set { _style = value; Invalidate(); }
        }

        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; Enabled = !value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; _hoverTimer.Start(); Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; _hoverTimer.Start(); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) _hoverT = 0; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            float h = _hoverT;
            Color from, to, textColor, border;
            switch (_style)
            {
                case ButtonStyle.Ghost:
                    from = Theme.Alpha(Theme.Accent, (int)(12 * h));
                    to = from;
                    textColor = Theme.Accent;
                    border = Theme.OutlineVariant;
                    break;
                case ButtonStyle.Dark:
                    from = Theme.Mix(Theme.SurfaceContainerHigh, Theme.SurfaceContainerHighest, h);
                    to = from;
                    textColor = Theme.Text;
                    border = Theme.OutlineVariant;
                    break;
                case ButtonStyle.Danger:
                    from = Theme.Mix(Theme.Red, Theme.Mix(Theme.Red, Color.White, 0.12f), h);
                    to = Theme.Mix(from, Color.Black, 0.18f);
                    textColor = Theme.Bg;
                    border = Color.Transparent;
                    break;
                case ButtonStyle.Subtle:
                    from = Theme.Mix(Theme.Alpha(Theme.SecondaryContainer, 190), Theme.SecondaryContainer, h);
                    to = from;
                    textColor = Theme.OnSecondaryContainer;
                    border = Color.Transparent;
                    break;
                default:
                    from = Theme.Mix(Theme.Accent, Theme.Mix(Theme.Accent, Color.White, 0.12f), h);
                    to = Theme.Mix(Theme.Mix(Theme.Accent, Color.Black, 0.16f), Theme.Mix(Theme.Accent, Color.White, 0.02f), h);
                    textColor = Theme.OnAccent;
                    border = Color.Transparent;
                    break;
            }
            if (!Enabled)
            {
                from = Theme.Card;
                to = Theme.Card;
                textColor = Theme.Faint;
                border = Theme.BorderSoft;
            }
            if (_style == ButtonStyle.Accent && Enabled && h > 0.01f)
                Ui.Glow(g, rect, Theme.Alpha(Theme.Accent, (int)(38 * h)), 5);
            if (_pressed) rect = new Rectangle(1, 2, Width - 3, Height - 4);
            if (from.A > 0 || _style == ButtonStyle.Ghost || _style == ButtonStyle.Subtle)
            {
                if (_style == ButtonStyle.Ghost && from.A == 0) { }
                else
                {
                    using (GraphicsPath path = Ui.RoundRect(rect, 21))
                    using (LinearGradientBrush brush = new LinearGradientBrush(rect, from, to, 90f))
                        g.FillPath(brush, path);
                }
            }
            if (border.A > 0) Ui.StrokeRound(g, rect, 21, border, 1f);

            if (_loading)
            {
                Rectangle spinner = new Rectangle(Width / 2 - 9, Height / 2 - 9, 18, 18);
                Ui.DrawSpinner(g, spinner, textColor, Environment.TickCount / 3f);
                return;
            }

            int iconW = Icon.HasValue ? IconSize + 8 : 0;
            Size textSize = TextRenderer.MeasureText(Text, Font);
            int total = iconW + textSize.Width;
            int startX = Math.Max(8, (Width - total) / 2);
            Rectangle textRect;
            if (Icon.HasValue)
            {
                Rectangle iconRect = new Rectangle(startX, (Height - IconSize) / 2, IconSize, IconSize);
                Ui.DrawIcon(g, Icon.Value, iconRect, textColor, 1.8f);
                textRect = new Rectangle(startX + iconW, 0, Width - (startX + iconW) - 8, Height);
                Ui.Text(g, Text, Font, textColor, textRect, TextAlign.LeftMiddle);
            }
            else
            {
                textRect = new Rectangle(8, 0, Width - 16, Height);
                Ui.Text(g, Text, Font, textColor, textRect, TextAlign.CenterMiddle);
            }
        }
    }

    public class IconButton : Control
    {
        private bool _hover;
        public IconKind Icon = IconKind.Close;
        public int IconSize = 17;
        public Color HoverColor = Theme.Alpha(Theme.Text, 16);

        public IconButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(34, 34);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_hover)
            {
                using (SolidBrush brush = new SolidBrush(HoverColor))
                    g.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            }
            Color color = _hover ? Theme.Text : Theme.TextSoft;
            Rectangle icon = new Rectangle((Width - IconSize) / 2, (Height - IconSize) / 2, IconSize, IconSize);
            Ui.DrawIcon(g, Icon, icon, color, 1.7f);
        }
    }

    public class NavItem : Control
    {
        private bool _hover;
        private bool _selected;
        public IconKind Icon = IconKind.Home;
        public string Badge = "";

        public NavItem()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 44;
            Font = Theme.Font(10f, FontStyle.Regular);
            Cursor = Cursors.Hand;
        }

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_hover && !_selected)
            {
                Ui.FillRound(g, rect, 22, Theme.Alpha(Theme.Text, 8));
            }
            Color color = _selected ? Theme.OnSecondaryContainer : (_hover ? Theme.Text : Theme.Muted);
            Ui.DrawIcon(g, Icon, new Rectangle(14, (Height - 19) / 2, 19, 19), color, 1.65f);
            Ui.Text(g, Text, _selected ? Theme.Font(10f, FontStyle.Bold) : Font,
                _selected ? Theme.OnSecondaryContainer : (_hover ? Theme.TextSoft : Theme.Muted),
                new Rectangle(45, 0, Width - 60, Height), TextAlign.LeftMiddle);
            if (!string.IsNullOrEmpty(Badge))
            {
                using (Font font = Theme.Font(8f, FontStyle.Bold))
                {
                    Size size = TextRenderer.MeasureText(Badge, font);
                    Rectangle badge = new Rectangle(Width - size.Width - 26, (Height - 20) / 2, size.Width + 12, 20);
                    Ui.FillRound(g, badge, 10, Theme.Alpha(Theme.Accent, 25));
                    Ui.Text(g, Badge, font, Theme.Accent, badge, TextAlign.CenterMiddle);
                }
            }
        }
    }

    public class SearchBox : Control
    {
        private readonly TextBox _input;
        private bool _focused;
        private string _placeholder = "搜索";

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public event EventHandler TextChangedCustom;

        public SearchBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 40;
            _input = new TextBox();
            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = Theme.Card;
            _input.ForeColor = Theme.Text;
            _input.Font = Theme.Font(9.5f);
            _input.TextChanged += delegate
            {
                EventHandler handler = TextChangedCustom;
                if (handler != null) handler(this, EventArgs.Empty);
                Invalidate();
            };
            _input.GotFocus += delegate { _focused = true; Invalidate(); };
            _input.LostFocus += delegate { _focused = false; ApplyCue(); Invalidate(); };
            _input.HandleCreated += delegate { ApplyCue(); };
            _input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) _input.Text = "";
            };
            Controls.Add(_input);
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value ?? ""; ApplyCue(); Invalidate(); }
        }

        public string Value
        {
            get { return _input.Text; }
            set { _input.Text = value ?? ""; Invalidate(); }
        }

        public void FocusInput()
        {
            _input.Focus();
        }

        private void ApplyCue()
        {
            try
            {
                if (_input.IsHandleCreated)
                    SendMessage(_input.Handle, 0x1501, (IntPtr)1, _placeholder);
            }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_input == null) return;
            _input.SetBounds(42, (Height - _input.PreferredHeight) / 2 + 1, Math.Max(10, Width - 56), _input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(10, (Height - 1) / 2);
            Ui.FillRound(g, rect, radius, _focused ? Theme.SurfaceContainerHighest : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, radius, _focused ? Theme.Accent : Theme.OutlineVariant, 1f);
            Ui.DrawIcon(g, IconKind.Search, new Rectangle(14, (Height - 17) / 2, 17, 17), _focused ? Theme.Accent : Theme.Muted, 1.6f);
        }
    }

    public class LabeledInput : Control
    {
        private readonly TextBox _input;
        private bool _focused;
        private bool _password;

        public string Caption = "";
        public string Placeholder = "";

        public LabeledInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 67;
            _input = new TextBox();
            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = Theme.SurfaceContainerHigh;
            _input.ForeColor = Theme.Text;
            _input.Font = Theme.Font(10f);
            _input.GotFocus += delegate { _focused = true; Invalidate(); };
            _input.LostFocus += delegate { _focused = false; Invalidate(); };
            _input.TextChanged += delegate { Invalidate(); };
            Controls.Add(_input);
        }

        public bool Password
        {
            get { return _password; }
            set { _password = value; _input.UseSystemPasswordChar = value; }
        }

        public string Value
        {
            get { return _input.Text; }
            set { _input.Text = value ?? ""; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_input == null) return;
            _input.SetBounds(14, 35, Math.Max(10, Width - 28), _input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (!string.IsNullOrEmpty(Caption))
                Ui.Text(g, Caption, Theme.Font(8.8f, FontStyle.Bold), Theme.Muted, new Rectangle(0, 2, Width, 20), TextAlign.LeftMiddle);
            Rectangle rect = new Rectangle(0, 24, Width - 1, Height - 25);
            Ui.FillRound(g, rect, 16, Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 16, _focused ? Theme.Accent : Theme.OutlineVariant, 1f);
        }
    }

    public class DarkComboBox : Control
    {
        private readonly List<object> _items = new List<object>();
        private int _selectedIndex = -1;
        private bool _hover;
        private bool _open;

        public event EventHandler SelectedIndexChanged;

        public DarkComboBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 40;
            Font = Theme.Font(9.5f);
            Cursor = Cursors.Hand;
        }

        public IList<object> Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null; }
        }

        public string SelectedText
        {
            get { return SelectedItem == null ? "" : SelectedItem.ToString(); }
        }

        public void SetItems(IEnumerable<object> items)
        {
            _items.Clear();
            if (items != null) foreach (object item in items) _items.Add(item);
            _selectedIndex = _items.Count > 0 ? 0 : -1;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || _items.Count == 0) return;
            ShowMenu();
        }

        private void ShowMenu()
        {
            _open = true;
            Invalidate();
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Theme.Card;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.Font(9.5f);
            menu.ShowImageMargin = false;
            menu.Renderer = new DarkMenuRenderer();
            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(_items[i] == null ? "" : _items[i].ToString());
                item.BackColor = Theme.Card;
                item.ForeColor = Theme.Text;
                item.Padding = new Padding(8, 4, 8, 4);
                if (i == _selectedIndex) item.Font = Theme.Font(9.5f, FontStyle.Bold);
                item.Click += delegate
                {
                    SelectedIndex = index;
                    _open = false;
                    Invalidate();
                };
                menu.Items.Add(item);
            }
            menu.Closed += delegate { _open = false; Invalidate(); };
            menu.Show(this, new Point(0, Height + 4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 16, _hover ? Theme.SurfaceContainerHighest : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 16, _open ? Theme.Accent : Theme.OutlineVariant, 1f);
            Color color = Enabled ? Theme.Text : Theme.Faint;
            Ui.Text(g, SelectedText, Font, color, new Rectangle(14, 0, Width - 48, Height), TextAlign.LeftMiddle);
            Ui.DrawIcon(g, IconKind.ChevronDown, new Rectangle(Width - 29, (Height - 14) / 2, 14, 14), Theme.Muted, 1.6f);
        }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.Text : Theme.TextSoft;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Alpha(Theme.Accent, 45); } }
        public override Color MenuItemSelected { get { return Theme.Alpha(Theme.Accent, 28); } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Alpha(Theme.Accent, 28); } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Alpha(Theme.Accent, 28); } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.CardHover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.CardHover; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
    }

    public class SegmentedControl : Control
    {
        private string[] _options = new string[0];
        private int _selected;
        private float _slideX;
        private bool _slideReady;
        private readonly Timer _slideTimer;
        private float _slideFrom;
        private float _slideTo;
        private float _slideProgress;

        public event EventHandler SelectedIndexChanged;

        public SegmentedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 36;
            Font = Theme.Font(9f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            _slideTimer = new Timer();
            _slideTimer.Interval = 16;
            _slideTimer.Tick += delegate
            {
                _slideProgress += 16f / 340f;
                if (_slideProgress >= 1f)
                {
                    _slideProgress = 1f;
                    _slideTimer.Stop();
                }
                _slideX = _slideFrom + (_slideTo - _slideFrom) * Motion.EaseOutBack(_slideProgress, 0.25f);
                Invalidate();
            };
        }

        public string[] Options
        {
            get { return _options; }
            set { _options = value ?? new string[0]; if (_selected >= _options.Length) _selected = 0; _slideReady = false; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (value < 0 || value >= _options.Length) return;
                if (_selected == value) return;
                _selected = value;
                StartSlide();
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        private void StartSlide()
        {
            if (_options.Length == 0 || Width <= 8) return;
            int itemWidth = (Width - 8) / _options.Length;
            float target = 4 + _selected * itemWidth;
            if (!_slideReady)
            {
                _slideX = target;
                _slideReady = true;
                return;
            }
            _slideFrom = _slideX;
            _slideTo = target;
            _slideProgress = 0f;
            _slideTimer.Stop();
            _slideTimer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _slideReady = false;
            if (_options.Length > 0 && Width > 8)
            {
                int itemWidth = (Width - 8) / _options.Length;
                _slideX = 4 + _selected * itemWidth;
                _slideReady = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _slideTimer != null) _slideTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_options.Length == 0) return;
            int width = Width / _options.Length;
            int index = Math.Min(_options.Length - 1, Math.Max(0, e.X / Math.Max(1, width)));
            SelectedIndex = index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(10, (Height - 1) / 2);
            Ui.FillRound(g, rect, radius, Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, radius, Theme.OutlineVariant, 1f);
            if (_options.Length == 0) return;
            int itemWidth = (Width - 8) / _options.Length;
            if (!_slideReady && Width > 8)
            {
                _slideX = 4 + _selected * itemWidth;
                _slideReady = true;
            }
            Rectangle active = new Rectangle((int)Math.Round(_slideX), 4, itemWidth, Height - 8);
            Ui.FillRound(g, active, Math.Max(8, active.Height / 2), Theme.SecondaryContainer);
            for (int i = 0; i < _options.Length; i++)
            {
                Rectangle item = new Rectangle(4 + i * itemWidth, 4, itemWidth, Height - 8);
                Ui.Text(g, _options[i], Font, i == _selected ? Theme.OnSecondaryContainer : Theme.Muted, item, TextAlign.CenterMiddle);
            }
        }
    }

    public class ToggleSwitch : Control
    {
        private const int AnimationMs = 260;
        private readonly Timer _timer;
        private bool _checked;
        private bool _hover;
        private float _position;
        private float _from;
        private float _to;
        private float _progress = 1f;
        private bool _userToggle;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(52, 30);
            Cursor = Cursors.Hand;
            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate
            {
                _progress += 16f / AnimationMs;
                if (_progress >= 1f)
                {
                    _progress = 1f;
                    _timer.Stop();
                }
                _position = _from + (_to - _from) * Motion.EaseInOutCubic(_progress);
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                if (!_userToggle || !IsHandleCreated || !Visible)
                {
                    _timer.Stop();
                    _position = value ? 1f : 0f;
                    _progress = 1f;
                    Invalidate();
                    return;
                }
                _from = _position;
                _to = value ? 1f : 0f;
                _progress = 0f;
                _timer.Stop();
                _timer.Start();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled) return;
            _userToggle = true;
            Checked = !Checked;
            _userToggle = false;
            EventHandler handler = CheckedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float t = Math.Max(0f, Math.Min(1f, _position));

            Rectangle track = new Rectangle(1, 4, Width - 3, Height - 9);
            int radius = track.Height / 2;
            Color offTop = _hover ? Color.FromArgb(225, 232, 227) : Color.FromArgb(214, 222, 216);
            Color offBottom = _hover ? Color.FromArgb(201, 210, 204) : Color.FromArgb(190, 200, 193);
            // iOS-like light mint track; the white knob still carries the
            // contrast, so the enabled state does not need a dark fill.
            Color onTop = Color.FromArgb(192, 237, 212);
            Color onBottom = Color.FromArgb(157, 219, 184);
            Color top = Theme.Mix(offTop, onTop, t);
            Color bottom = Theme.Mix(offBottom, onBottom, t);
            if (!Enabled)
            {
                top = Color.FromArgb(230, 234, 231);
                bottom = Color.FromArgb(214, 220, 216);
            }

            using (GraphicsPath path = Ui.RoundRect(track, radius))
            using (LinearGradientBrush brush = new LinearGradientBrush(track, top, bottom, 90f))
                g.FillPath(brush, path);

            // Inner shadow and bottom highlight give the track a little depth.
            using (GraphicsPath path = Ui.RoundRect(track, radius))
            {
                GraphicsState state = g.Save();
                g.SetClip(path);
                using (Pen shadow = new Pen(Color.FromArgb(t > 0.5f ? 28 : 32, 0, 0, 0), 1.2f))
                    g.DrawLine(shadow, track.Left + 6, track.Top + 1, track.Right - 6, track.Top + 1);
                using (Pen shine = new Pen(Color.FromArgb(38, 255, 255, 255), 1f))
                    g.DrawLine(shine, track.Left + 7, track.Bottom - 2, track.Right - 7, track.Bottom - 2);
                g.Restore(state);
            }
            Ui.StrokeRound(g, track, radius, Theme.Mix(Color.FromArgb(155, Theme.Border), Color.FromArgb(128, 176, 148), t), 1f);

            int knob = track.Height - 2;
            int travel = Math.Max(1, track.Width - knob - 6);
            int x = track.Left + 3 + (int)Math.Round(travel * t);
            int y = track.Top + (track.Height - knob) / 2;

            // Soft layered shadow under the white knob.
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb((int)(30 + 22 * t), 0, 0, 0)))
                g.FillEllipse(shadow, x - 1, y + 2, knob + 2, knob + 2);
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb((int)(16 + 12 * t), 0, 0, 0)))
                g.FillEllipse(shadow, x, y + 3, knob, knob);

            Color knobTop = Enabled ? Color.White : Color.FromArgb(240, 242, 240);
            Color knobBottom = Enabled ? Color.FromArgb(245, 249, 246) : Color.FromArgb(220, 225, 221);
            Rectangle knobRect = new Rectangle(x, y, knob, knob);
            using (LinearGradientBrush brush = new LinearGradientBrush(knobRect, knobTop, knobBottom, 90f))
                g.FillEllipse(brush, knobRect);
            using (Pen border = new Pen(Color.FromArgb(Enabled ? 58 : 35, 0, 0, 0), 1f))
                g.DrawEllipse(border, knobRect);
            if (Enabled)
            {
                using (SolidBrush shine = new SolidBrush(Color.FromArgb(175, 255, 255, 255)))
                    g.FillEllipse(shine, x + knob / 4, y + 2, Math.Max(3, knob / 3), Math.Max(2, knob / 5));
            }
        }
    }

    public class ModernSlider : Control
    {
        private int _minimum = 1024;
        private int _maximum = 32768;
        private int _value = 4096;
        private bool _drag;

        public event EventHandler ValueChanged;

        public ModernSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 32;
            Cursor = Cursors.Hand;
        }

        public int Minimum { get { return _minimum; } set { _minimum = value; } }
        public int Maximum { get { return _maximum; } set { _maximum = value; } }
        public int Value
        {
            get { return _value; }
            set
            {
                int clamp = Math.Max(_minimum, Math.Min(_maximum, value));
                if (clamp == _value) return;
                _value = clamp;
                Invalidate();
                EventHandler handler = ValueChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _drag = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = false;
        }

        private void SetFromX(int x)
        {
            int pad = 8;
            int width = Math.Max(1, Width - pad * 2);
            double fraction = (double)(x - pad) / width;
            int value = _minimum + (int)((_maximum - _minimum) * Math.Max(0, Math.Min(1, fraction)));
            value = (int)(Math.Round(value / 512.0) * 512);
            Value = value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int pad = 8;
            int width = Width - pad * 2;
            double fraction = (double)(_value - _minimum) / Math.Max(1, _maximum - _minimum);
            Ui.FillRound(g, new Rectangle(pad, Height / 2 - 3, width, 6), 3, Theme.Border);
            Ui.FillRound(g, new Rectangle(pad, Height / 2 - 3, Math.Max(6, (int)(width * fraction)), 6), 3, Theme.Accent);
            int x = pad + (int)(width * fraction);
            using (SolidBrush brush = new SolidBrush(Theme.Text))
                g.FillEllipse(brush, x - 7, Height / 2 - 7, 14, 14);
            using (Pen pen = new Pen(Theme.Accent, 3f))
                g.DrawEllipse(pen, x - 6, Height / 2 - 6, 12, 12);
        }
    }

    public class SmoothScrollPanel : Control
    {
        private readonly FlowLayoutPanel _content;
        private int _scroll;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartScroll;

        public SmoothScrollPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _content = new FlowLayoutPanel();
            _content.FlowDirection = FlowDirection.TopDown;
            _content.WrapContents = false;
            _content.AutoSize = true;
            _content.AutoSizeMode = AutoSizeMode.GrowOnly;
            _content.BackColor = Theme.Bg;
            _content.Padding = new Padding(0, 2, 0, 24);
            _content.Margin = new Padding(0);
            _content.ControlAdded += delegate { QueueUpdate(); };
            _content.ControlRemoved += delegate { QueueUpdate(); };
            _content.MouseWheel += ContentMouseWheel;
            Controls.Add(_content);
        }

        public FlowLayoutPanel Content
        {
            get { return _content; }
        }

        private void QueueUpdate()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                UpdateScroll();
                return;
            }
            try { BeginInvoke(new Action(UpdateScroll)); }
            catch { }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _content.Width = Math.Max(50, Width - 13);
            int childWidth = Math.Max(50, Width - 17);
            for (int i = 0; i < _content.Controls.Count; i++)
            {
                Control control = _content.Controls[i];
                if (!(control is Label)) control.Width = childWidth;
            }
            UpdateScroll();
        }

        public void AddControl(Control control)
        {
            control.Margin = new Padding(0, 0, 0, 10);
            control.Width = Math.Max(50, Width - 17);
            _content.Controls.Add(control);
        }

        public void Clear()
        {
            _content.Controls.Clear();
            _scroll = 0;
            _content.Top = 0;
            Invalidate();
        }

        public void ScrollToTop()
        {
            _scroll = 0;
            _content.Top = 0;
            Invalidate();
        }

        public void UpdateScroll()
        {
            int max = Math.Max(0, _content.Height - Height);
            if (_scroll > max) _scroll = max;
            if (_scroll < 0) _scroll = 0;
            _content.Top = -_scroll;
            Invalidate();
        }

        private void ContentMouseWheel(object sender, MouseEventArgs e)
        {
            ScrollBy(-e.Delta);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollBy(-e.Delta);
        }

        private void ScrollBy(int delta)
        {
            int max = Math.Max(0, _content.Height - Height);
            _scroll = Math.Max(0, Math.Min(max, _scroll + delta));
            _content.Top = -_scroll;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X >= Width - 12 && MaxScroll > 0)
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartScroll = _scroll;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                int track = Math.Max(1, Height);
                int thumb = ThumbHeight;
                int range = Math.Max(1, track - thumb);
                double delta = (double)(e.Y - _dragStartY) / range * MaxScroll;
                _scroll = Math.Max(0, Math.Min(MaxScroll, _dragStartScroll + (int)delta));
                _content.Top = -_scroll;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        private int MaxScroll
        {
            get { return Math.Max(0, _content.Height - Height); }
        }

        private int ThumbHeight
        {
            get
            {
                if (MaxScroll <= 0) return Height;
                return Math.Max(38, (int)((double)Height / Math.Max(1, _content.Height) * Height));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (MaxScroll <= 0) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int x = Width - 6;
            Ui.FillRound(g, new Rectangle(x, 2, 4, Height - 4), 2, Theme.Alpha(Theme.Border, 120));
            int thumb = ThumbHeight;
            int y = 2 + (int)((double)_scroll / MaxScroll * (Height - 4 - thumb));
            Ui.FillRound(g, new Rectangle(x, y, 4, thumb), 2, Theme.Alpha(Theme.Accent, 120));
        }
    }

    public class TitleBarButton : Control
    {
        private bool _hover;
        public IconKind Icon = IconKind.Close;
        public bool Danger;

        public TitleBarButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(46, 38);
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_hover)
            {
                using (SolidBrush brush = new SolidBrush(Danger ? Theme.Alpha(Theme.Red, 190) : Theme.Alpha(Theme.Text, 13)))
                    e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            Ui.DrawIcon(e.Graphics, Icon, new Rectangle((Width - 15) / 2, (Height - 15) / 2, 15, 15),
                _hover && Danger ? Color.White : Theme.TextSoft, 1.5f);
        }
    }

    public class ToastPanel : Panel
    {
        private readonly Timer _timer;
        private readonly string _message;
        private readonly Color _accent;
        private int _life;
        private float _alpha;

        public ToastPanel(string message, Color accent)
        {
            _message = message;
            _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(320, 58);
            _timer = new Timer();
            _timer.Interval = 30;
            _timer.Tick += delegate
            {
                _life += 30;
                float fadeIn = Math.Min(1f, _life / 180f);
                float fadeOut = _life > 3000 ? Math.Max(0f, (3600 - _life) / 600f) : 1f;
                _alpha = Math.Min(fadeIn, fadeOut);
                if (_life > 3600)
                {
                    _timer.Stop();
                    if (Parent != null) Parent.Controls.Remove(this);
                    Dispose();
                    return;
                }
                Invalidate();
            };
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            float a = _alpha;
            if (a <= 0f) a = 0.01f;
            Ui.FillRound(g, rect, 11, Theme.Alpha(Theme.Card, (int)(248 * a)));
            Ui.StrokeRound(g, rect, 11, Theme.Alpha(_accent, (int)(120 * a)), 1f);
            using (SolidBrush brush = new SolidBrush(Theme.Alpha(_accent, (int)(255 * a))))
                g.FillRectangle(brush, 0, 12, 3, Height - 24);
            Ui.DrawIcon(g, IconKind.Check, new Rectangle(16, (Height - 19) / 2, 19, 19), Theme.Alpha(_accent, (int)(255 * a)), 1.8f);
            Ui.Text(g, _message, Theme.Font(9.2f), Theme.Alpha(Theme.Text, (int)(255 * a)),
                new Rectangle(46, 0, Width - 60, Height), TextAlign.LeftMiddle, true);
        }
    }
}
