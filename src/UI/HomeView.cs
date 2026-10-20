using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class HomeView : BaseView
    {
        private readonly HomeHero _hero;
        private readonly StatCard _versions;
        private readonly StatCard _java;
        private readonly StatCard _mods;
        private readonly RecentCard _recent;
        private readonly StatusCard _status;

        public HomeView(MainForm main) : base(main)
        {
            _hero = new HomeHero(main);
            _hero.LaunchClicked += delegate
            {
                InstalledVersion version = Main.SelectedVersion;
                if (version == null) Main.OpenInstallDialog("");
                else Main.LaunchAsync(version.Id);
            };
            _hero.InstallClicked += delegate { Main.OpenInstallDialog(""); };
            _hero.InstanceSelected += delegate(string id) { if (!string.IsNullOrEmpty(id)) Main.SetSelectedVersion(id); };
            _hero.ManageClicked += delegate
            {
                InstalledVersion version = Main.SelectedVersion;
                if (version != null) Main.OpenInstanceManager(version.Id);
            };
            Controls.Add(_hero);

            _versions = new StatCard(IconKind.Layers, "已安装版本", "0", "个独立实例", Theme.Accent);
            _java = new StatCard(IconKind.Java, "Java 运行时", "待检测", "自动匹配版本", Theme.Cyan);
            _mods = new StatCard(IconKind.Puzzle, "模组数量", "0", "当前实例", Theme.Gold);
            Controls.Add(_versions);
            Controls.Add(_java);
            Controls.Add(_mods);

            _recent = new RecentCard(main);
            Controls.Add(_recent);

            _status = new StatusCard(main);
            Controls.Add(_status);
        }

        public override void RefreshView()
        {
            InstalledVersion selected = Main.SelectedVersion;
            _hero.SetInstances(Main.Installed, selected == null ? "" : selected.Id);
            _hero.SetVersion(selected);
            _versions.Value = Main.Installed.Count.ToString();
            _versions.Sub = Main.Installed.Count > 0 ? "个独立实例" : "先安装一个版本";
            if (Main.Java != null)
            {
                _java.Value = "Java " + Main.Java.Major;
                _java.Sub = Main.Java.Vendor;
            }
            else
            {
                _java.Value = "待检测";
                _java.Sub = "启动时自动下载";
            }
            int mods = 0;
            if (selected != null)
            {
                string modsDir = Main.Config.ModsDir(selected.Id);
                if (Directory.Exists(modsDir))
                {
                    string[] jars = Directory.GetFiles(modsDir, "*.jar");
                    mods = jars.Length;
                }
            }
            _mods.Value = mods.ToString();
            _mods.Sub = selected == null ? "未选择实例" : selected.LoaderLabel + " 实例";
            _recent.Invalidate();
            _status.Invalidate();
            LayoutCards();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutCards();
        }

        private void LayoutCards()
        {
            int left = 28;
            int top = 24;
            int width = Math.Max(400, ClientSize.Width - 56);
            int heroHeight = 300;
            _hero.SetBounds(left, top, width, heroHeight);

            int statsTop = top + heroHeight + 18;
            int gap = 14;
            int statWidth = (width - gap * 2) / 3;
            _versions.SetBounds(left, statsTop, statWidth, 92);
            _java.SetBounds(left + statWidth + gap, statsTop, statWidth, 92);
            _mods.SetBounds(left + (statWidth + gap) * 2, statsTop, statWidth, 92);

            int rowTop = statsTop + 92 + 18;
            int bottom = ClientSize.Height - 24;
            int recentWidth = (int)((width - gap) * 0.62);
            int statusWidth = width - gap - recentWidth;
            _recent.SetBounds(left, rowTop, recentWidth, Math.Max(180, bottom - rowTop));
            _status.SetBounds(left + recentWidth + gap, rowTop, statusWidth, Math.Max(180, bottom - rowTop));
        }
    }

    public sealed class HomeHero : Control
    {
        private readonly MainForm _main;
        private readonly Timer _timer;
        private readonly AccentButton _launch;
        private readonly AccentButton _install;
        private readonly AccentButton _manage;
        private readonly DarkComboBox _instances;
        private readonly List<InstalledVersion> _instanceList = new List<InstalledVersion>();
        private bool _updatingInstances;
        private InstalledVersion _version;
        private float _phase;

        public event EventHandler LaunchClicked;
        public event EventHandler InstallClicked;
        public event EventHandler ManageClicked;
        public event Action<string> InstanceSelected;

        public HomeHero(MainForm main)
        {
            _main = main;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 300;

            _launch = new AccentButton();
            _launch.Text = "启动游戏";
            _launch.Icon = IconKind.Play;
            _launch.IconSize = 17;
            _launch.Width = 172;
            _launch.Height = 46;
            _launch.Click += delegate { EventHandler handler = LaunchClicked; if (handler != null) handler(this, EventArgs.Empty); };
            Controls.Add(_launch);

            _install = new AccentButton();
            _install.Text = "安装新版本";
            _install.Style = ButtonStyle.Ghost;
            _install.Icon = IconKind.Plus;
            _install.Width = 154;
            _install.Height = 46;
            _install.Click += delegate { EventHandler handler = InstallClicked; if (handler != null) handler(this, EventArgs.Empty); };
            Controls.Add(_install);

            _manage = new AccentButton();
            _manage.Text = "管理实例";
            _manage.Style = ButtonStyle.Ghost;
            _manage.Width = 96;
            _manage.Height = 36;
            _manage.Location = new Point(380, 184);
            _manage.Click += delegate { EventHandler handler = ManageClicked; if (handler != null) handler(this, EventArgs.Empty); };
            Controls.Add(_manage);

            _instances = new DarkComboBox();
            _instances.SetBounds(38, 184, 330, 36);
            _instances.SelectedIndexChanged += delegate
            {
                if (_updatingInstances) return;
                int index = _instances.SelectedIndex;
                if (index >= 0 && index < _instanceList.Count && InstanceSelected != null)
                    InstanceSelected(_instanceList[index].Id);
            };
            Controls.Add(_instances);

            _timer = new Timer();
            _timer.Interval = 40;
            _timer.Tick += delegate
            {
                _phase += 0.035f;
                if (_phase > 1000f) _phase = 0;
                if (Visible) Invalidate();
            };
            _timer.Start();
            Resize += delegate { LayoutButtons(); };
            LayoutButtons();
        }

        public void SetVersion(InstalledVersion version)
        {
            _version = version;
            if (version == null)
            {
                _launch.Text = "安装游戏";
                _launch.Icon = IconKind.Download;
            }
            else
            {
                _launch.Text = "启动游戏";
                _launch.Icon = IconKind.Play;
            }
            if (version != null)
            {
                _updatingInstances = true;
                for (int i = 0; i < _instanceList.Count; i++)
                    if (string.Equals(_instanceList[i].Id, version.Id, StringComparison.OrdinalIgnoreCase))
                        _instances.SelectedIndex = i;
                _updatingInstances = false;
            }
            _launch.Invalidate();
            Invalidate();
        }

        public void SetInstances(List<InstalledVersion> instances, string selectedId)
        {
            _instanceList.Clear();
            List<object> labels = new List<object>();
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    _instanceList.Add(instances[i]);
                    labels.Add(instances[i].DisplayName);
                }
            }
            _updatingInstances = true;
            _instances.Enabled = _instanceList.Count > 0;
            _instances.SetItems(labels.Count == 0 ? new object[] { "尚未安装实例" } : labels.ToArray());
            int selected = 0;
            for (int i = 0; i < _instanceList.Count; i++)
                if (string.Equals(_instanceList[i].Id, selectedId, StringComparison.OrdinalIgnoreCase)) selected = i;
            if (_instanceList.Count > 0) _instances.SelectedIndex = selected;
            _updatingInstances = false;
        }

        private void LayoutButtons()
        {
            _launch.Location = new Point(38, Height - 76);
            _install.Location = new Point(222, Height - 76);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillLiquidGlass(g, rect, 32, _phase, Theme.Accent);

            // Ambient light and pixel grid.
            using (SolidBrush glow = new SolidBrush(Theme.Alpha(Theme.Accent, 14)))
                g.FillEllipse(glow, (int)(Width - 440 + Math.Sin(_phase * 0.7) * 24), -170, 560, 560);
            using (SolidBrush glow2 = new SolidBrush(Color.FromArgb(18, Theme.Cyan)))
                g.FillEllipse(glow2, (int)(Width - 240 + Math.Cos(_phase * 0.5) * 18), 80, 360, 360);
            DrawGrid(g, rect);

            Ui.Text(g, "MINECRAFT  //  READY TO PLAY", Theme.Mono(8f), Theme.Alpha(Theme.Accent, 230),
                new Rectangle(40, 34, Width - 80, 20), TextAlign.LeftMiddle);
            Ui.Text(g, "方块世界，", Theme.Display(26f, FontStyle.Bold), Theme.Text,
                new Rectangle(38, 58, 520, 48), TextAlign.LeftTop);
            Ui.Text(g, "随时出发。", Theme.Display(26f, FontStyle.Bold), Theme.Accent,
                new Rectangle(38, 106, 520, 48), TextAlign.LeftTop);
            Ui.Text(g, "镜像加速 · 一键安装 Forge / Fabric · 正版与皮肤站登录",
                Theme.Font(9f), Theme.TextSoft, new Rectangle(40, 160, Math.Max(300, Width - 520), 24), TextAlign.LeftMiddle);

            DrawIllustration(g, rect);
            if (Width < 700) return;
        }

        private void DrawGrid(Graphics g, Rectangle rect)
        {
            using (Pen pen = new Pen(Color.FromArgb(14, 21, 33, 25), 1f))
            {
                for (int x = 34; x < rect.Width; x += 34) g.DrawLine(pen, x, 0, x, rect.Height);
                for (int y = 0; y < rect.Height; y += 34) g.DrawLine(pen, 0, y, rect.Width, y);
            }
        }

        private void DrawIllustration(Graphics g, Rectangle rect)
        {
            int baseX = (int)(Width * 0.69f);
            int baseY = (int)(Height * 0.56f);
            if (baseX < 560) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Large floating island.
            PointF top = new PointF(baseX, baseY - 92);
            PointF left = new PointF(baseX - 168, baseY - 24);
            PointF right = new PointF(baseX + 168, baseY - 24);
            PointF mid = new PointF(baseX, baseY + 46);
            PointF bottom = new PointF(baseX, baseY + 152);
            using (SolidBrush topBrush = new SolidBrush(Color.FromArgb(110, 209, 145)))
                g.FillPolygon(topBrush, new PointF[] { top, right, mid, left });
            using (SolidBrush leftBrush = new SolidBrush(Color.FromArgb(87, 125, 97)))
                g.FillPolygon(leftBrush, new PointF[] { left, mid, bottom, new PointF(baseX - 168, baseY + 63) });
            using (SolidBrush rightBrush = new SolidBrush(Color.FromArgb(53, 90, 76)))
                g.FillPolygon(rightBrush, new PointF[] { mid, right, new PointF(baseX + 168, baseY + 63), bottom });
            using (SolidBrush dirt = new SolidBrush(Color.FromArgb(81, 59, 44)))
                g.FillPolygon(dirt, new PointF[] { new PointF(baseX - 168, baseY + 63), new PointF(baseX - 168, baseY + 86), mid, new PointF(baseX + 168, baseY + 86), new PointF(baseX + 168, baseY + 63), bottom });
            using (Pen edge = new Pen(Theme.Alpha(Theme.Accent, 90), 1.4f))
                g.DrawPolygon(edge, new PointF[] { top, right, mid, left });

            // Grass pixels.
            Random random = new Random(21);
            for (int i = 0; i < 34; i++)
            {
                float x = baseX - 148 + (float)random.NextDouble() * 296;
                float y = baseY - 60 + (float)random.NextDouble() * 80;
                using (SolidBrush pixel = new SolidBrush(Color.FromArgb(70 + random.Next(40), 218, 158)))
                    g.FillRectangle(pixel, x, y, 3, 3);
            }

            // Blocky tree.
            int treeX = baseX - 58;
            int treeY = baseY - 112;
            using (SolidBrush trunk = new SolidBrush(Color.FromArgb(113, 79, 52)))
                g.FillRectangle(trunk, treeX, treeY, 20, 66);
            using (SolidBrush leaves = new SolidBrush(Color.FromArgb(63, 163, 108)))
            {
                g.FillRectangle(leaves, treeX - 24, treeY - 4, 68, 34);
                g.FillRectangle(leaves, treeX - 10, treeY - 30, 40, 34);
                g.FillRectangle(leaves, treeX + 14, treeY + 4, 32, 26);
            }
            using (SolidBrush light = new SolidBrush(Color.FromArgb(103, 202, 137)))
            {
                g.FillRectangle(light, treeX - 22, treeY - 2, 24, 10);
                g.FillRectangle(light, treeX - 8, treeY - 28, 18, 8);
            }

            // Floating cubes with slow parallax.
            DrawCube(g, baseX + 120, baseY - 150 + (int)(Math.Sin(_phase + 1) * 7), 44, Color.FromArgb(109, 221, 170), Color.FromArgb(62, 131, 106));
            DrawCube(g, baseX + 168, baseY - 76 + (int)(Math.Cos(_phase + 2) * 5), 26, Color.FromArgb(77, 196, 210), Color.FromArgb(42, 108, 126));
            DrawCube(g, baseX - 178, baseY - 104 + (int)(Math.Sin(_phase + 3) * 6), 20, Color.FromArgb(238, 193, 104), Color.FromArgb(142, 106, 52));

            // Particles.
            for (int i = 0; i < 16; i++)
            {
                double a = _phase * 0.25 + i * 0.72;
                int px = baseX + (int)(Math.Cos(a) * (70 + i * 8));
                int py = baseY - 40 + (int)(Math.Sin(a * 1.4) * (45 + i * 2)) - i * 3;
                int alpha = 30 + (int)((Math.Sin(_phase * 2 + i) + 1) * 30);
                using (SolidBrush particle = new SolidBrush(Color.FromArgb(Math.Min(180, alpha), Theme.Accent)))
                    g.FillRectangle(particle, px, py, i % 3 == 0 ? 3 : 2, i % 3 == 0 ? 3 : 2);
            }
        }

        private void DrawCube(Graphics g, int x, int y, int size, Color light, Color dark)
        {
            float h = size;
            PointF top = new PointF(x, y);
            PointF left = new PointF(x - h * 0.72f, y + h * 0.42f);
            PointF right = new PointF(x + h * 0.72f, y + h * 0.42f);
            PointF mid = new PointF(x, y + h * 0.84f);
            PointF bottom = new PointF(x, y + h * 1.5f);
            using (SolidBrush brush = new SolidBrush(light))
                g.FillPolygon(brush, new PointF[] { top, right, mid, left });
            using (SolidBrush brush = new SolidBrush(Theme.Mix(dark, Color.Black, 0.1)))
                g.FillPolygon(brush, new PointF[] { left, mid, bottom, new PointF(left.X, left.Y + h * 0.55f) });
            using (SolidBrush brush = new SolidBrush(dark))
                g.FillPolygon(brush, new PointF[] { mid, right, new PointF(right.X, right.Y + h * 0.55f), bottom });
            using (Pen pen = new Pen(Color.FromArgb(70, 21, 33, 25), 1f))
                g.DrawPolygon(pen, new PointF[] { top, right, mid, left });
        }
    }

    public sealed class StatCard : Control
    {
        private readonly IconKind _icon;
        private readonly string _label;
        private readonly Color _accent;
        private string _value = "0";
        private string _sub = "";

        public StatCard(IconKind icon, string label, string value, string sub, Color accent)
        {
            _icon = icon;
            _label = label;
            _value = value;
            _sub = sub;
            _accent = accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public string Value { get { return _value; } set { _value = value; Invalidate(); } }
        public string Sub { get { return _sub; } set { _sub = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillLiquidGlass(g, rect, 24, 0f, _accent);
            using (SolidBrush glow = new SolidBrush(Theme.Alpha(_accent, 13)))
                g.FillEllipse(glow, Width - 100, -46, 150, 150);
            Rectangle iconRect = new Rectangle(16, 16, 34, 34);
            Ui.FillRound(g, iconRect, 9, Theme.Alpha(_accent, 24));
            Ui.DrawIcon(g, _icon, new Rectangle(24, 24, 18, 18), _accent, 1.6f);
            Ui.Text(g, _label, Theme.Font(8.2f), Theme.Muted, new Rectangle(16, 54, Width - 30, 18), TextAlign.LeftMiddle);
            using (Font valueFont = Theme.Display(19f, FontStyle.Bold))
                Ui.Text(g, _value, valueFont, Theme.Text, new Rectangle(16, 24, Width - 70, 30), TextAlign.RightMiddle);
            using (Font subFont = Theme.Font(7.6f))
                Ui.Text(g, _sub, subFont, Theme.Faint, new Rectangle(16, 70, Width - 30, 16), TextAlign.LeftMiddle);
        }
    }

    public sealed class RecentCard : Control
    {
        private readonly MainForm _main;

        public RecentCard(MainForm main)
        {
            _main = main;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int header = 46;
            if (e.Y >= Height - 56)
            {
                _main.NavigateMods();
                return;
            }
            if (e.Y < header) return;
            int index = (e.Y - header) / 52;
            if (index >= 0 && index < Math.Min(5, _main.Installed.Count))
                _main.LaunchAsync(_main.Installed[index].Id);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillLiquidGlass(g, rect, 24, 0f, Theme.Cyan);
            Ui.Text(g, "最近游玩", Theme.Font(10.5f, FontStyle.Bold), Theme.Text, new Rectangle(18, 12, 160, 26), TextAlign.LeftMiddle);
            Ui.Text(g, "点击快速启动", Theme.Font(7.8f), Theme.Muted, new Rectangle(Width - 130, 14, 112, 22), TextAlign.RightMiddle);

            int header = 46;
            if (_main.Installed.Count == 0)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Accent, 20)))
                    g.FillEllipse(brush, 22, header + 22, 42, 42);
                Ui.DrawIcon(g, IconKind.Box, new Rectangle(34, header + 34, 18, 18), Theme.Accent, 1.6f);
                Ui.Text(g, "还没有安装任何版本", Theme.Font(9.5f, FontStyle.Bold), Theme.TextSoft, new Rectangle(78, header + 20, Width - 100, 22), TextAlign.LeftMiddle);
                Ui.Text(g, "点击右上角安装按钮，开始你的第一场冒险", Theme.Font(8f), Theme.Muted, new Rectangle(78, header + 42, Width - 100, 20), TextAlign.LeftMiddle, true);
                return;
            }

            int count = Math.Min(5, Math.Max(1, (Height - header - 8) / 52));
            for (int i = 0; i < Math.Min(count, _main.Installed.Count); i++)
            {
                InstalledVersion version = _main.Installed[i];
                int y = header + i * 52;
                if (i > 0)
                {
                    using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                        g.DrawLine(pen, 18, y, Width - 18, y);
                }
                Color accent = version.Loader == "fabric" ? Theme.Cyan : (version.Loader == "forge" ? Theme.Gold : (version.Loader == "quilt" ? Theme.Purple : Theme.Accent));
                Ui.FillRound(g, new Rectangle(18, y + 9, 34, 34), 9, Theme.Alpha(accent, 20));
                Ui.DrawIcon(g, version.Loader == "vanilla" ? IconKind.Box : IconKind.Layers, new Rectangle(27, y + 18, 16, 16), accent, 1.5f);
                Ui.Text(g, version.DisplayName, Theme.Font(9f, FontStyle.Bold), Theme.Text, new Rectangle(64, y + 10, Width - 180, 18), TextAlign.LeftMiddle);
                Ui.Text(g, version.LoaderLabel + " · " + version.Id, Theme.Font(7.5f), Theme.Muted, new Rectangle(64, y + 27, Width - 180, 16), TextAlign.LeftMiddle);
                Ui.DrawIcon(g, IconKind.Play, new Rectangle(Width - 38, y + 17, 17, 17), Theme.Accent, 1.5f);
            }

            int footerY = Height - 54;
            if (footerY > header + Math.Min(count, _main.Installed.Count) * 52 + 4)
            {
                Rectangle tip = new Rectangle(18, footerY, Width - 36, 38);
                Ui.FillRound(g, tip, 9, Theme.Alpha(Theme.Accent, 8));
                Ui.DrawIcon(g, IconKind.Puzzle, new Rectangle(tip.X + 12, tip.Y + 10, 17, 17), Theme.Accent, 1.5f);
                Ui.Text(g, "去模组中心为当前实例添加模组，或安装更多版本", Theme.Font(7.8f), Theme.TextSoft,
                    new Rectangle(tip.X + 38, tip.Y, tip.Width - 58, tip.Height), TextAlign.LeftMiddle);
                Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(tip.Right - 22, tip.Y + 13, 10, 10), Theme.Faint, 1.3f);
            }
        }
    }

    public sealed class StatusCard : Control
    {
        private readonly MainForm _main;

        public StatusCard(MainForm main)
        {
            _main = main;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillLiquidGlass(g, rect, 24, 0f, Theme.Purple);
            Ui.Text(g, "启动器状态", Theme.Font(10.5f, FontStyle.Bold), Theme.Text, new Rectangle(18, 12, 180, 26), TextAlign.LeftMiddle);
            Ui.DrawIcon(g, IconKind.Settings, new Rectangle(Width - 34, 18, 15, 15), Theme.Faint, 1.5f);

            int y = 54;
            DrawRow(g, ref y, IconKind.Java, "Java 运行时", _main.JavaStatusText, _main.JavaStatusDetail, Theme.Cyan);
            DrawRow(g, ref y, IconKind.User, "当前账户", _main.SelectedAccount == null ? "未登录" : _main.SelectedAccount.Name, _main.SelectedAccount == null ? "点击添加" : _main.SelectedAccount.TypeLabel, Theme.Gold);
            DrawRow(g, ref y, IconKind.Globe, "下载源", "镜像加速", _main.Config.MirrorFirst ? "BMCLAPI / Modrinth / TUNA" : "官方源", Theme.Accent);
            DrawRow(g, ref y, IconKind.Memory, "分配内存", _main.Config.MemoryMb + " MB", "可在设置中调整", Theme.Purple);
        }

        private void DrawRow(Graphics g, ref int y, IconKind icon, string label, string value, string sub, Color accent)
        {
            Ui.FillRound(g, new Rectangle(18, y, 30, 30), 8, Theme.Alpha(accent, 18));
            Ui.DrawIcon(g, icon, new Rectangle(25, y + 7, 16, 16), accent, 1.5f);
            Ui.Text(g, label, Theme.Font(7.8f), Theme.Muted, new Rectangle(58, y, Width - 78, 15), TextAlign.LeftMiddle);
            Ui.Text(g, value, Theme.Font(8.8f, FontStyle.Bold), Theme.Text, new Rectangle(58, y + 13, Width - 78, 17), TextAlign.LeftMiddle);
            Ui.Text(g, sub, Theme.Font(7.2f), Theme.Faint, new Rectangle(Width - 150, y + 3, 132, 24), TextAlign.RightMiddle);
            y += 42;
        }
    }
}
