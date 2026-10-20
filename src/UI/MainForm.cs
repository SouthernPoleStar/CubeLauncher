using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class MainForm : Form
    {
        public enum Page
        {
            Home, Versions, Mods, Accounts, Settings
        }

        private readonly Sidebar _sidebar;
        private readonly TopBar _topBar;
        private readonly BufferedPanel _viewHost;
        private readonly Page[] _pages = new Page[] { Page.Home, Page.Versions, Page.Mods, Page.Accounts, Page.Settings };
        private readonly BaseView[] _views = new BaseView[5];
        private Page _page = Page.Home;

        public readonly AppConfig Config;
        public readonly VersionManager Versions;
        public readonly ImageCache Images;
        public List<InstalledVersion> Installed = new List<InstalledVersion>();
        public string SelectedVersionId = "";
        public JavaInfo Java;
        public bool Busy;
        public event Action DataChanged;
        private PageTransition _transition;
        private Timer _windowFade;

        public MainForm()
        {
            Config = App.Config;
            Versions = new VersionManager(Config);
            Images = new ImageCache(Config);

            Text = "方块启动器";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 800);
            MinimumSize = new Size(1080, 700);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Padding(1);
            KeyPreview = true;

            _sidebar = new Sidebar(this);
            _sidebar.Dock = DockStyle.Left;

            BufferedPanel main = new BufferedPanel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Theme.Bg;

            _topBar = new TopBar(this);
            _topBar.Dock = DockStyle.Top;
            _topBar.Height = 68;

            _viewHost = new BufferedPanel();
            _viewHost.Dock = DockStyle.Fill;
            _viewHost.BackColor = Theme.Bg;

            main.Controls.Add(_viewHost);
            main.Controls.Add(_topBar);
            _transition = new PageTransition();
            _transition.Dock = DockStyle.Fill;
            main.Controls.Add(_transition);
            _transition.BringToFront();
            Controls.Add(main);
            Controls.Add(_sidebar);

            LoadInstalled();
            SelectInitialVersion();
            CreateViews();
            ShowPage(Page.Home);
            UpdateSidebar();
            Resize += delegate { UpdateRegion(); };
            Shown += delegate
            {
                UpdateRegion();
                try
                {
                    Opacity = 0;
                    _windowFade = new Timer();
                    _windowFade.Interval = 16;
                    _windowFade.Tick += delegate
                    {
                        if (Opacity >= 1)
                        {
                            _windowFade.Stop();
                            _windowFade.Dispose();
                            _windowFade = null;
                            return;
                        }
                        Opacity = Math.Min(1, Opacity + 0.09);
                    };
                    _windowFade.Start();
                }
                catch
                {
                    Opacity = 1;
                }
                LoadManifestAsync();
                LoadJavaAsync();
            };
        }

        public InstalledVersion SelectedVersion
        {
            get
            {
                for (int i = 0; i < Installed.Count; i++)
                    if (string.Equals(Installed[i].Id, SelectedVersionId, StringComparison.OrdinalIgnoreCase)) return Installed[i];
                return Installed.Count > 0 ? Installed[0] : null;
            }
        }

        public Account SelectedAccount
        {
            get { return Config.SelectedAccount(); }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) // WM_NCHITTEST
            {
                int x = (short)(m.LParam.ToInt32() & 0xffff);
                int y = (short)((m.LParam.ToInt32() >> 16) & 0xffff);
                Point p = PointToClient(new Point(x, y));
                int edge = 7;
                bool left = p.X <= edge;
                bool right = p.X >= ClientSize.Width - edge;
                bool top = p.Y <= edge;
                bool bottom = p.Y >= ClientSize.Height - edge;
                if (left && top) { m.Result = (IntPtr)13; return; }
                if (right && top) { m.Result = (IntPtr)14; return; }
                if (left && bottom) { m.Result = (IntPtr)16; return; }
                if (right && bottom) { m.Result = (IntPtr)17; return; }
                if (left) { m.Result = (IntPtr)10; return; }
                if (right) { m.Result = (IntPtr)11; return; }
                if (top) { m.Result = (IntPtr)12; return; }
                if (bottom) { m.Result = (IntPtr)15; return; }
            }
            base.WndProc(ref m);
        }

        private void UpdateRegion()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                Region = null;
                return;
            }
            using (GraphicsPath path = Ui.RoundRect(new Rectangle(0, 0, Width, Height), 12))
                Region = new Region(path);
        }

        private void CreateViews()
        {
            _views[0] = new HomeView(this);
            _views[1] = new VersionsView(this);
            _views[2] = new ModsView(this);
            _views[3] = new AccountsView(this);
            _views[4] = new SettingsView(this);
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].Visible = false;
                _views[i].Dock = DockStyle.Fill;
                _viewHost.Controls.Add(_views[i]);
            }
        }

        private void LoadInstalled()
        {
            Installed = Versions.GetInstalled();
            if (string.IsNullOrEmpty(SelectedVersionId) || FindInstalled(SelectedVersionId) == null)
            {
                if (!string.IsNullOrEmpty(Config.SelectedVersion) && FindInstalled(Config.SelectedVersion) != null)
                    SelectedVersionId = Config.SelectedVersion;
                else if (Installed.Count > 0)
                    SelectedVersionId = Installed[0].Id;
            }
        }

        private InstalledVersion FindInstalled(string id)
        {
            for (int i = 0; i < Installed.Count; i++)
                if (string.Equals(Installed[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Installed[i];
            return null;
        }

        private void SelectInitialVersion()
        {
            LoadInstalled();
        }

        public void ShowPage(Page page)
        {
            if ((int)page < 0 || (int)page >= _views.Length) return;
            bool changed = _page != page;
            _page = page;
            Log.Info("切换到页面 " + page);
            for (int i = 0; i < _views.Length; i++) _views[i].Visible = i == (int)page;
            _sidebar.SetPage((int)page);
            _topBar.SetPage((int)page);
            BaseView view = _views[(int)page];
            if (view != null) view.OnShown();
            if (changed && _transition != null) _transition.Play();
        }

        public void NavigateHome() { ShowPage(Page.Home); }
        public void NavigateVersions() { ShowPage(Page.Versions); }
        public void NavigateMods() { ShowPage(Page.Mods); }
        public void NavigateAccounts() { ShowPage(Page.Accounts); }
        public void NavigateSettings() { ShowPage(Page.Settings); }

        public void SetSelectedVersion(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            SelectedVersionId = id;
            Config.SelectedVersion = id;
            App.Save();
            UpdateSidebar();
            RefreshData();
        }

        public void SelectAccount(string id)
        {
            Account account = Config.FindAccount(id);
            if (account == null) return;
            Config.SelectedAccountId = id;
            App.Save();
            UpdateSidebar();
            RefreshData();
        }

        public void RefreshData()
        {
            LoadInstalled();
            if (FindInstalled(SelectedVersionId) == null)
                SelectedVersionId = Installed.Count > 0 ? Installed[0].Id : "";
            UpdateSidebar();
            for (int i = 0; i < _views.Length; i++) if (_views[i] != null) _views[i].RefreshView();
            Action handler = DataChanged;
            if (handler != null) handler();
        }

        public void LoadInstalledPublic()
        {
            LoadInstalled();
        }

        public void UpdateSidebar()
        {
            _sidebar.UpdateState();
        }

        public void Toast(string message)
        {
            ToastPanel toast = new ToastPanel(message, Theme.Accent);
            toast.Location = new Point(ClientSize.Width - toast.Width - 28, 82);
            Controls.Add(toast);
            toast.BringToFront();
        }

        public void ToastError(string message)
        {
            ToastPanel toast = new ToastPanel(message, Theme.Red);
            toast.Location = new Point(ClientSize.Width - toast.Width - 28, 82);
            Controls.Add(toast);
            toast.BringToFront();
        }

        public bool Confirm(string title, string message, string okText)
        {
            using (ConfirmDialog dialog = new ConfirmDialog(title, message, okText))
                return dialog.ShowDialog(this) == DialogResult.OK;
        }

        public void OpenInstallDialog(string baseVersion)
        {
            using (InstallDialog dialog = new InstallDialog(this, baseVersion))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Result != null)
                    InstallAsync(dialog.Result);
            }
        }

        public void InstallAsync(InstallChoice choice)
        {
            string resultId = "";
            Exception error = null;
            using (ProgressDialog dialog = new ProgressDialog(this, "安装 " + choice.DisplayName, "正在准备安装..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    if (choice.Loader == "fabric")
                        resultId = FabricService.Install(Config, Versions, choice.Minecraft, choice.LoaderVersion, progress);
                    else if (choice.Loader == "forge")
                        resultId = ForgeService.Install(Config, Versions, choice.Minecraft, choice.LoaderVersion, progress);
                    else if (choice.Loader == "quilt")
                        resultId = QuiltService.Install(Config, Versions, choice.Minecraft, choice.LoaderVersion, progress);
                    else if (choice.Loader == "neoforge")
                    {
                        NeoForgeBuild build = new NeoForgeBuild();
                        build.Minecraft = choice.Minecraft;
                        build.Version = choice.LoaderVersion;
                        build.Artifact = choice.Minecraft == "1.20.1" ? "forge" : "neoforge";
                        build.InstallerVersion = choice.Minecraft == "1.20.1" ? "1.20.1-" + choice.LoaderVersion : choice.LoaderVersion;
                        resultId = NeoForgeService.Install(Config, Versions, choice.Minecraft, build, progress);
                    }
                    else
                    {
                        Versions.InstallVanilla(choice.Minecraft, progress);
                        resultId = choice.Minecraft;
                    }
                };
                dialog.Run();
                if (dialog.Error != null) error = dialog.Error;
            }
            if (error != null)
            {
                ToastError("安装失败: " + ShortError(error.Message));
                return;
            }
            if (!string.IsNullOrEmpty(resultId))
            {
                LoadInstalled();
                SelectedVersionId = resultId;
                Config.SelectedVersion = resultId;
                App.Save();
                RefreshData();
                Toast("安装完成: " + resultId);
            }
        }

        public void LaunchAsync(string versionId)
        {
            if (string.IsNullOrEmpty(versionId)) return;
            Account account = SelectedAccount;
            if (account == null)
            {
                ToastError("请先添加一个账户");
                ShowPage(Page.Accounts);
                return;
            }
            if (FindInstalled(versionId) == null)
            {
                ToastError("该版本尚未安装");
                return;
            }
            LaunchPlan plan = null;
            using (ProgressDialog dialog = new ProgressDialog(this, "启动游戏", "正在准备运行环境..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    LaunchOptions options = new LaunchOptions();
                    options.VersionId = versionId;
                    options.Account = account;
                    options.MemoryMb = Config.MemoryMb;
                    options.ExtraJvmArgs = Config.JvmArgs;
                    plan = LaunchService.Prepare(Config, options, progress);
                };
                dialog.Run();
                if (dialog.Error != null)
                {
                    ToastError("启动失败: " + ShortError(dialog.Error.Message));
                    return;
                }
            }
            if (plan == null) return;
            try
            {
                LaunchService.Start(plan);
                Toast("游戏已启动，祝你玩得开心");
                if (Config.CloseAfterLaunch) BeginInvoke(new Action(Close));
            }
            catch (Exception ex)
            {
                Log.Error("启动进程失败", ex);
                ToastError("启动失败: " + ShortError(ex.Message));
            }
        }

        public void ShowLog()
        {
            LogForm form = new LogForm();
            form.Show(this);
        }

        public void RemoveVersion(InstalledVersion version)
        {
            if (version == null) return;
            if (!Confirm("删除版本", "确定删除 " + version.Id + " 吗？\n此操作不会删除存档与模组（位于 instances 目录）。", "删除")) return;
            try
            {
                string dir = Path.Combine(Config.VersionsDir, version.Id);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                string instance = Config.InstanceDir(version.Id);
                if (Directory.Exists(instance) && Confirm("删除实例", "是否同时删除该版本的存档、模组与配置？", "一起删除"))
                    Directory.Delete(instance, true);
                Toast("已删除 " + version.Id);
                RefreshData();
            }
            catch (Exception ex)
            {
                ToastError("删除失败: " + ex.Message);
            }
        }

        private void LoadManifestAsync()
        {
            Ui.Run(this, delegate
            {
                ProgressSink progress = new ProgressSink();
                Versions.LoadManifest(progress, false);
                Ui.Post(this, delegate
                {
                    RefreshData();
                    _topBar.SetStatus("镜像在线", true);
                });
            }, delegate(Exception ex)
            {
                Log.Warn("版本清单加载失败: " + ex.Message);
                _topBar.SetStatus("镜像离线", false);
            });
        }

        public void LoadJavaAsync()
        {
            Ui.Run(this, delegate
            {
                JavaInfo found = null;
                List<JavaInfo> installed = JavaManager.FindInstalled(Config);
                if (installed.Count > 0) found = installed[0];
                Java = found;
                Ui.Post(this, delegate
                {
                    UpdateSidebar();
                    RefreshData();
                });
            }, delegate(Exception ex) { Log.Warn("Java 检测失败: " + ex.Message); });
        }

        public string JavaStatusText
        {
            get
            {
                if (Java != null) return "Java " + Java.Major;
                return "Java 未检测";
            }
        }

        public string JavaStatusDetail
        {
            get
            {
                if (Java == null) return "启动时自动下载";
                return Java.Version;
            }
        }

        private static string ShortError(string message)
        {
            if (string.IsNullOrEmpty(message)) return "未知错误";
            if (message.Length > 160) return message.Substring(0, 160) + "...";
            return message;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.D1) { ShowPage(Page.Home); e.Handled = true; }
                else if (e.KeyCode == Keys.D2) { ShowPage(Page.Versions); e.Handled = true; }
                else if (e.KeyCode == Keys.D3) { ShowPage(Page.Mods); e.Handled = true; }
                else if (e.KeyCode == Keys.D4) { ShowPage(Page.Accounts); e.Handled = true; }
                else if (e.KeyCode == Keys.D5) { ShowPage(Page.Settings); e.Handled = true; }
                else if (e.KeyCode == Keys.R) { RefreshData(); e.Handled = true; }
            }
            base.OnKeyDown(e);
        }

        public sealed class Sidebar : BufferedPanel
        {
            private readonly MainForm _main;
            private readonly List<NavItem> _nav = new List<NavItem>();
            private readonly AccountChip _accountChip;
            private readonly Label _java;
            private readonly Label _version;
            private readonly string[] _labels = new string[] { "首页", "版本库", "模组中心", "账户中心", "设置" };
            private readonly IconKind[] _icons = new IconKind[] { IconKind.Home, IconKind.Layers, IconKind.Puzzle, IconKind.User, IconKind.Settings };
            private readonly Timer _glassTimer;
            private float _glassPhase;

            public Sidebar(MainForm main)
            {
                _main = main;
                Width = 218;
                BackColor = Theme.Sidebar;
                for (int i = 0; i < _labels.Length; i++)
                {
                    NavItem item = new NavItem();
                    item.Text = _labels[i];
                    item.Icon = _icons[i];
                    item.Bounds = new Rectangle(14, 124 + i * 48, 190, 44);
                    int page = i;
                    item.Click += delegate { _main.ShowPage((Page)page); };
                    Controls.Add(item);
                    _nav.Add(item);
                }

                _java = new Label();
                _java.AutoSize = false;
                _java.BackColor = Color.Transparent;
                _java.ForeColor = Theme.Muted;
                _java.Font = Theme.Font(8.5f);
                _java.TextAlign = ContentAlignment.MiddleLeft;
                _java.Bounds = new Rectangle(20, 0, 180, 20);
                _java.Text = "Java 检测中...";
                Controls.Add(_java);

                _accountChip = new AccountChip(main);
                _accountChip.Bounds = new Rectangle(12, 0, 194, 76);
                _accountChip.Cursor = Cursors.Hand;
                _accountChip.Click += delegate { _main.NavigateAccounts(); };
                Controls.Add(_accountChip);

                _version = new Label();
                _version.AutoSize = false;
                _version.BackColor = Color.Transparent;
                _version.ForeColor = Theme.Faint;
                _version.Font = Theme.Mono(7.5f);
                _version.TextAlign = ContentAlignment.MiddleLeft;
                _version.Bounds = new Rectangle(20, 0, 180, 18);
                _version.Text = "CUBE LAUNCHER  v" + App.Version;
                Controls.Add(_version);
                _glassTimer = new Timer();
                _glassTimer.Interval = 70;
                _glassTimer.Tick += delegate
                {
                    _glassPhase += 0.045f;
                    if (_glassPhase > 1000f) _glassPhase = 0f;
                    Invalidate();
                };
                _glassTimer.Start();
                Resize += delegate { LayoutBottom(); };
                LayoutBottom();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _glassTimer != null) _glassTimer.Dispose();
                base.Dispose(disposing);
            }

            private void LayoutBottom()
            {
                int bottom = Height - 22;
                _version.Top = bottom - 16;
                _accountChip.Top = bottom - 108;
                _java.Top = bottom - 132;
            }

            public void SetPage(int page)
            {
                for (int i = 0; i < _nav.Count; i++) _nav[i].Selected = i == page;
            }

            public void UpdateState()
            {
                _accountChip.Invalidate();
                _java.Text = _main.JavaStatusText + "  ·  " + _main.JavaStatusDetail;
                int installed = _main.Installed.Count;
                _nav[1].Badge = installed > 0 ? installed.ToString() : "";
                _nav[1].Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                Ui.FillLiquidGlass(g, new Rectangle(0, 0, Width, Height), 0, _glassPhase, Theme.Cyan);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Accent, 9)))
                    g.FillEllipse(brush, -90, Height - 220, 300, 300);
                using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                    g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

                // Logo mark: isometric grass block.
                DrawCube(g, new Rectangle(24, 26, 38, 38));
                Ui.Text(g, "CUBE", Theme.Display(17f, FontStyle.Bold), Theme.Text, new Rectangle(72, 24, 120, 26), TextAlign.LeftMiddle);
                Ui.Text(g, "方块启动器", Theme.Font(8.5f), Theme.Muted, new Rectangle(72, 46, 120, 20), TextAlign.LeftMiddle);

                Ui.Text(g, "导航", Theme.Font(7.8f, FontStyle.Bold), Theme.Faint, new Rectangle(18, 96, 100, 20), TextAlign.LeftMiddle);
                using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                    g.DrawLine(pen, 18, 118, 200, 118);
            }

            private void DrawCube(Graphics g, Rectangle r)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF top = new PointF(r.X + r.Width / 2f, r.Y + 2);
                PointF left = new PointF(r.X + 3, r.Y + r.Height * 0.28f);
                PointF right = new PointF(r.Right - 3, r.Y + r.Height * 0.28f);
                PointF mid = new PointF(r.X + r.Width / 2f, r.Y + r.Height * 0.52f);
                PointF bottom = new PointF(r.X + r.Width / 2f, r.Bottom - 3);
                using (SolidBrush light = new SolidBrush(Color.FromArgb(126, 235, 174)))
                    g.FillPolygon(light, new PointF[] { top, right, mid, left });
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(41, 150, 104)))
                    g.FillPolygon(dark, new PointF[] { left, mid, bottom, new PointF(r.X + 3, r.Y + r.Height * 0.72f) });
                using (SolidBrush darker = new SolidBrush(Color.FromArgb(28, 111, 82)))
                    g.FillPolygon(darker, new PointF[] { mid, right, new PointF(r.Right - 3, r.Y + r.Height * 0.72f), bottom });
                using (Pen pen = new Pen(Theme.Alpha(Theme.Accent, 70), 1f))
                    g.DrawPolygon(pen, new PointF[] { top, right, mid, left });
            }
        }

        public sealed class AccountChip : Control
        {
            private readonly MainForm _main;
            private bool _hover;

            public AccountChip(MainForm main)
            {
                _main = main;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
                Ui.FillRound(g, rect, 11, _hover ? Theme.CardHover : Theme.Card);
                Ui.StrokeRound(g, rect, 11, Theme.BorderSoft, 1f);
                Account account = _main.SelectedAccount;
                if (account == null)
                {
                    using (SolidBrush brush = new SolidBrush(Theme.Muted))
                        g.FillEllipse(brush, 14, 18, 38, 38);
                    Ui.Text(g, "添加账户", Theme.Font(9f, FontStyle.Bold), Theme.Text, new Rectangle(64, 16, 100, 20), TextAlign.LeftMiddle);
                    Ui.Text(g, "点击登录", Theme.Font(8f), Theme.Muted, new Rectangle(64, 38, 100, 18), TextAlign.LeftMiddle);
                    return;
                }
                Ui.DrawAvatar(g, new Rectangle(14, 16, 40, 40), account.Name, account.IsMicrosoft ? Theme.Cyan : (account.IsYggdrasil ? Theme.Purple : Theme.Gold), false);
                Ui.Text(g, account.Name, Theme.Font(9.2f, FontStyle.Bold), Theme.Text, new Rectangle(64, 15, 110, 20), TextAlign.LeftMiddle);
                Ui.Text(g, account.TypeLabel, Theme.Font(7.8f), Theme.Muted, new Rectangle(64, 37, 105, 18), TextAlign.LeftMiddle);
                Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(Width - 24, 30, 12, 12), Theme.Faint, 1.4f);
            }
        }

        public sealed class TopBar : BufferedPanel
        {
            private readonly MainForm _main;
            private readonly Label _title;
            private readonly Label _subtitle;
            private readonly IconButton _log;
            private readonly TitleBarButton _min;
            private readonly TitleBarButton _max;
            private readonly TitleBarButton _close;
            private readonly Label _status;
            private string _statusText = "正在连接镜像";
            private bool _online;
            private int _page;

            private readonly string[] _titles = new string[] { "首页", "版本库", "模组中心", "账户中心", "设置" };
            private readonly string[] _subtitles = new string[] { "为下一次冒险做好准备", "管理你的游戏版本与模组加载器", "从 CurseForge 与 Modrinth 发现模组", "正版、皮肤站与离线账户", "调整启动器的每一个细节" };

            public TopBar(MainForm main)
            {
                _main = main;
                Height = 68;
                BackColor = Theme.Bg;
                _title = new Label();
                _title.AutoSize = false;
                _title.BackColor = Color.Transparent;
                _title.ForeColor = Theme.Text;
                _title.Font = Theme.Display(15f, FontStyle.Bold);
                _title.TextAlign = ContentAlignment.MiddleLeft;
                _title.Bounds = new Rectangle(28, 12, 300, 26);
                Controls.Add(_title);

                _subtitle = new Label();
                _subtitle.AutoSize = false;
                _subtitle.BackColor = Color.Transparent;
                _subtitle.ForeColor = Theme.Muted;
                _subtitle.Font = Theme.Font(8.5f);
                _subtitle.TextAlign = ContentAlignment.MiddleLeft;
                _subtitle.Bounds = new Rectangle(28, 36, 420, 18);
                Controls.Add(_subtitle);

                _status = new Label();
                _status.AutoSize = false;
                _status.BackColor = Color.Transparent;
                _status.Font = Theme.Font(8.2f, FontStyle.Bold);
                _status.ForeColor = Theme.Accent;
                _status.TextAlign = ContentAlignment.MiddleRight;
                _status.Bounds = new Rectangle(0, 17, 210, 32);
                Controls.Add(_status);

                _log = new IconButton();
                _log.Icon = IconKind.Log;
                _log.Size = new Size(34, 34);
                _log.Click += delegate { _main.ShowLog(); };
                Controls.Add(_log);

                _min = new TitleBarButton();
                _min.Icon = IconKind.Minimize;
                _min.Click += delegate { _main.WindowState = FormWindowState.Minimized; };
                Controls.Add(_min);

                _max = new TitleBarButton();
                _max.Icon = IconKind.Maximize;
                _max.Click += delegate
                {
                    _main.WindowState = _main.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                    _max.Icon = _main.WindowState == FormWindowState.Maximized ? IconKind.Restore : IconKind.Maximize;
                    _max.Invalidate();
                };
                Controls.Add(_max);

                _close = new TitleBarButton();
                _close.Icon = IconKind.Close;
                _close.Danger = true;
                _close.Click += delegate { _main.Close(); };
                Controls.Add(_close);

                Resize += delegate { LayoutChildren(); };
                MouseDown += DragWindow;
                _title.MouseDown += DragWindow;
                _subtitle.MouseDown += DragWindow;
                LayoutChildren();
                SetStatus("正在连接镜像", false);
            }

            private void DragWindow(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(_main.Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool ReleaseCapture();
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            public void SetPage(int page)
            {
                _page = page;
                _title.Text = _titles[page];
                _subtitle.Text = _subtitles[page];
            }

            public void SetStatus(string text, bool online)
            {
                _statusText = text;
                _online = online;
                _status.Text = online ? "●  " + text + "   |   国内镜像" : "●  " + text;
                _status.ForeColor = online ? Theme.Accent : Theme.Gold;
                Invalidate();
            }

            private void LayoutChildren()
            {
                int right = Width - 8;
                _close.Bounds = new Rectangle(right - 46, 0, 46, 38);
                _max.Bounds = new Rectangle(right - 92, 0, 46, 38);
                _min.Bounds = new Rectangle(right - 138, 0, 46, 38);
                _log.Bounds = new Rectangle(right - 188, 2, 34, 34);
                _status.Bounds = new Rectangle(right - 430, 17, 232, 32);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                Ui.FillLiquidGlass(g, new Rectangle(0, 0, Width, Height), 0, 0f, Theme.Accent);
                using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                    g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                using (SolidBrush brush = new SolidBrush(Theme.Alpha(Theme.Accent, 18)))
                    g.FillEllipse(brush, Width - 610, -130, 380, 260);
            }
        }
    }
}
