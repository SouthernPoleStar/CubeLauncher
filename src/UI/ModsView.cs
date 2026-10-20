using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class ModsView : BaseView
    {
        private readonly SearchBox _search;
        private readonly AccentButton _refresh;
        private readonly InstancePill _pill;
        private readonly SmoothScrollPanel _list;
        private readonly Timer _debounce;
        private readonly Timer _watchdog;
        private readonly List<ModProject> _projects = new List<ModProject>();
        private int _request;
        private int _watchdogRequest;
        private int _total;
        private bool _loading;
        private string _lastContext = "";
        private AccentButton _more;

        public ModsView(MainForm main) : base(main)
        {
            _search = new SearchBox();
            _search.Placeholder = "搜索 Modrinth 模组、光影、优化模组...";
            _search.TextChangedCustom += delegate { _debounce.Stop(); _debounce.Start(); };
            Controls.Add(_search);

            _refresh = new AccentButton();
            _refresh.Text = "刷新";
            _refresh.Icon = IconKind.Refresh;
            _refresh.Style = ButtonStyle.Ghost;
            _refresh.Width = 96;
            _refresh.Click += delegate { StartSearch(); };
            Controls.Add(_refresh);

            _pill = new InstancePill(main);
            Controls.Add(_pill);

            _list = new SmoothScrollPanel();
            Controls.Add(_list);

            _debounce = new Timer();
            _debounce.Interval = 420;
            _debounce.Tick += delegate { _debounce.Stop(); StartSearch(); };

            _watchdog = new Timer();
            _watchdog.Interval = 45000;
            _watchdog.Tick += delegate
            {
                if (!_loading || _watchdogRequest != _request) return;
                _watchdog.Stop();
                _loading = false;
                _list.Clear();
                EmptyState empty = new EmptyState(IconKind.Warning, "搜索超时", "模组接口暂时没有响应，请检查网络后点刷新重试。");
                empty.Width = _list.Width - 4;
                _list.AddControl(empty);
            };

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        public override void OnShown()
        {
            base.OnShown();
            _pill.Invalidate();
            string context = ContextKey();
            if (context != _lastContext)
            {
                _lastContext = context;
                StartSearch();
            }
        }

        public override void RefreshView()
        {
            _pill.Invalidate();
            string context = ContextKey();
            if (context != _lastContext)
            {
                _lastContext = context;
                StartSearch();
            }
        }

        private string ContextKey()
        {
            InstalledVersion version = Main.SelectedVersion;
            return version == null ? "none" : version.Id + "|" + version.Loader + "|" + version.Minecraft;
        }

        private void LayoutChildren()
        {
            int left = 28;
            _search.SetBounds(left, 18, Math.Max(320, ClientSize.Width - 300), 40);
            _refresh.SetBounds(ClientSize.Width - 132, 18, 96, 40);
            _pill.SetBounds(left, 72, ClientSize.Width - 56, 42);
            _list.SetBounds(left, 124, ClientSize.Width - 56, Math.Max(100, ClientSize.Height - 148));
        }

        private void StartSearch()
        {
            _debounce.Stop();
            IntPtr handle = Handle;
            _request++;
            int token = _request;
            _projects.Clear();
            _total = 0;
            _loading = true;
            _list.Clear();
            ShowLoading();
            _watchdogRequest = token;
            _watchdog.Stop();
            _watchdog.Start();
            InstalledVersion version = Main.SelectedVersion;
            string source = "modrinth";
            string query = _search.Value.Trim();
            string mc = version == null ? "" : version.Minecraft;
            string loader = version == null ? "" : version.Loader;
            Ui.Run(this, delegate
            {
                ModSearchResult result = ModService.Search(Main.Config, source, query, mc, loader, 0, 20);
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    _watchdog.Stop();
                    _loading = false;
                    _total = result.Total;
                    _projects.AddRange(result.Projects);
                    RenderResults(result.Projects, true);
                    TranslateResults(result.Projects, token);
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    _watchdog.Stop();
                    _loading = false;
                    _list.Clear();
                    EmptyState empty = new EmptyState(IconKind.Warning, "搜索失败", ex.Message);
                    empty.Width = _list.Width - 4;
                    _list.AddControl(empty);
                });
            });
        }

        private void LoadMore()
        {
            if (_loading) return;
            _loading = true;
            _request++;
            int token = _request;
            int offset = _projects.Count;
            InstalledVersion version = Main.SelectedVersion;
            string source = "modrinth";
            string query = _search.Value.Trim();
            string mc = version == null ? "" : version.Minecraft;
            string loader = version == null ? "" : version.Loader;
            if (_more != null) { _more.Text = "加载中..."; _more.Enabled = false; }
            _watchdogRequest = token;
            _watchdog.Stop();
            _watchdog.Start();
            Ui.Run(this, delegate
            {
                ModSearchResult result = ModService.Search(Main.Config, source, query, mc, loader, offset, 20);
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    _watchdog.Stop();
                    _loading = false;
                    _total = result.Total;
                    _projects.AddRange(result.Projects);
                    RenderResults(result.Projects, false);
                    TranslateResults(result.Projects, token);
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    _watchdog.Stop();
                    _loading = false;
                    if (_more != null) { _more.Text = "加载更多"; _more.Enabled = true; }
                    Main.ToastError("加载失败: " + ex.Message);
                });
            });
        }

        private void TranslateResults(List<ModProject> projects, int token)
        {
            if (projects == null || projects.Count == 0) return;
            Translator.TranslateProjects(projects, delegate(ModProject project)
            {
                Ui.Post(this, delegate
                {
                    if (token != _request || IsDisposed) return;
                    for (int i = 0; i < _list.Content.Controls.Count; i++)
                    {
                        ModCard card = _list.Content.Controls[i] as ModCard;
                        if (card != null && object.ReferenceEquals(card.Project, project)) card.Invalidate();
                    }
                });
            });
        }

        private void ShowLoading()
        {
            LoadingCard loading = new LoadingCard("正在从 Modrinth 获取模组...");
            loading.Width = _list.Width - 4;
            _list.AddControl(loading);
        }

        private void RenderResults(List<ModProject> projects, bool firstPage)
        {
            if (firstPage) _list.Clear();
            if (_more != null) { _list.Content.Controls.Remove(_more); _more.Dispose(); _more = null; }
            if (_projects.Count == 0)
            {
                EmptyState empty = new EmptyState(IconKind.Puzzle, "没有找到模组", "尝试更换关键词、来源或切换到匹配的版本。");
                empty.Width = _list.Width - 4;
                _list.AddControl(empty);
                return;
            }

            if (firstPage) _list.Clear();
            for (int i = 0; i < projects.Count; i++)
            {
                ModCard card = new ModCard(Main, projects[i]);
                card.Width = _list.Width - 4;
                card.Height = 148;
                card.InstallRequested += delegate(object sender, EventArgs e) { InstallLatest(((ModCard)sender).Project); };
                card.OpenRequested += delegate(object sender, EventArgs e) { OpenDetail(((ModCard)sender).Project); };
                _list.AddControl(card);
            }

            if (_projects.Count < _total)
            {
                _more = new AccentButton();
                _more.Text = "加载更多";
                _more.Icon = IconKind.ChevronDown;
                _more.Style = ButtonStyle.Ghost;
                _more.Height = 40;
                _more.Width = _list.Width - 4;
                _more.Click += delegate { LoadMore(); };
                _list.AddControl(_more);
            }
            else
            {
                Label end = new Label();
                end.Text = "已经到底了 · 共 " + _total + " 个结果";
                end.ForeColor = Theme.Faint;
                end.Font = Theme.Font(8f);
                end.TextAlign = ContentAlignment.MiddleCenter;
                end.Height = 36;
                end.Width = _list.Width - 4;
                end.BackColor = Color.Transparent;
                _list.AddControl(end);
            }
            _list.UpdateScroll();
        }

        private void OpenDetail(ModProject project)
        {
            InstalledVersion version = Main.SelectedVersion;
            if (version == null)
            {
                Main.ToastError("请先安装一个游戏版本");
                Main.NavigateVersions();
                return;
            }
            using (ModDetailDialog dialog = new ModDetailDialog(Main, project, version))
                dialog.ShowDialog(this);
        }

        private void InstallLatest(ModProject project)
        {
            InstalledVersion version = Main.SelectedVersion;
            if (version == null)
            {
                Main.ToastError("请先安装一个游戏版本");
                return;
            }
            if (version.Loader == "vanilla")
            {
                Main.ToastError("原版无法加载模组，请安装 Fabric 或 Forge 版本");
                return;
            }
            int count = 0;
            using (ProgressDialog dialog = new ProgressDialog(this, "安装模组", "正在获取 " + project.Name + " 的文件列表..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    List<ModFile> files = ModService.LoadFiles(Main.Config, project, version.Minecraft, version.Loader);
                    if (files.Count == 0) throw new Exception("没有找到适配 " + version.Minecraft + " / " + version.Loader + " 的文件");
                    List<string> installed = ModService.Install(Main.Config, project, files[0], version.Minecraft, version.Loader, version.Id, progress);
                    count = installed.Count;
                };
                dialog.Run();
                if (dialog.Error != null)
                {
                    Main.ToastError("安装失败: " + dialog.Error.Message);
                    return;
                }
            }
            Main.Toast("已安装 " + (count > 0 ? count + " 个文件" : project.Name));
            Main.RefreshData();
        }
    }

    public sealed class InstancePill : Control
    {
        private readonly MainForm _main;

        public InstancePill(MainForm main)
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
            if (e.X > Width - 150) _main.NavigateVersions();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            InstalledVersion version = _main.SelectedVersion;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 22, Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 22, Theme.OutlineVariant, 1f);
            Ui.DrawIcon(g, IconKind.Box, new Rectangle(13, (Height - 17) / 2, 17, 17), Theme.Accent, 1.6f);
            if (version == null)
            {
                Ui.Text(g, "尚未选择版本", Theme.Font(8.8f, FontStyle.Bold), Theme.TextSoft, new Rectangle(40, 0, Width - 220, Height), TextAlign.LeftMiddle);
                Ui.Text(g, "请先安装游戏 →", Theme.Font(8f), Theme.Accent, new Rectangle(Width - 170, 0, 150, Height), TextAlign.RightMiddle);
                return;
            }
            Ui.Text(g, version.DisplayName, Theme.Font(8.8f, FontStyle.Bold), Theme.Text, new Rectangle(40, 0, Width - 370, Height), TextAlign.LeftMiddle);
            Ui.Text(g, version.Minecraft + "  ·  " + version.LoaderLabel + "  ·  " + MainModCount(version) + " 个模组",
                Theme.Font(7.8f), Theme.Muted, new Rectangle(40 + Math.Min(260, Width / 2), 0, 280, Height), TextAlign.LeftMiddle);
            using (Font font = Theme.Font(7.6f, FontStyle.Bold))
            {
                Size size = TextRenderer.MeasureText("MCIM 国内镜像", font);
                Rectangle badge = new Rectangle(Width - 180, (Height - 24) / 2, size.Width + 18, 24);
                Ui.FillRound(g, badge, 12, Theme.Alpha(Theme.Accent, 18));
                Ui.Text(g, "MCIM 国内镜像", font, Theme.Accent, badge, TextAlign.CenterMiddle);
            }
        }

        private int MainModCount(InstalledVersion version)
        {
            try
            {
                string dir = _main.Config.ModsDir(version.Id);
                return System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "*.jar").Length : 0;
            }
            catch { return 0; }
        }
    }

    public sealed class ModCard : Control
    {
        private readonly MainForm _main;
        private readonly AccentButton _install;
        private Image _icon;
        private bool _hover;

        public ModProject Project;
        public event EventHandler InstallRequested;
        public event EventHandler OpenRequested;

        public ModCard(MainForm main, ModProject project)
        {
            _main = main;
            Project = project;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 148;

            _install = new AccentButton();
            _install.Text = "安装";
            _install.Style = ButtonStyle.Subtle;
            _install.Icon = IconKind.Download;
            _install.IconSize = 13;
            _install.Width = 76;
            _install.Height = 32;
            _install.Click += delegate { EventHandler h = InstallRequested; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_install);
            Resize += delegate { _install.Location = new Point(Width - 96, 18); };
            _install.Location = new Point(Width - 96, 18);

            if (!string.IsNullOrEmpty(project.IconUrl))
            {
                main.Images.Request(this, project.IconUrl, delegate(Image image)
                {
                    _icon = image;
                    Invalidate();
                });
            }        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X < Width - 110)
            {
                EventHandler handler = OpenRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 24, _hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 24, _hover ? Theme.Accent : Theme.OutlineVariant, 1f);

            Rectangle iconRect = new Rectangle(16, 16, 58, 58);
            Ui.FillRound(g, iconRect, 16, Theme.Alpha(Theme.Accent, 18));
            if (_icon != null)
            {
                using (GraphicsPath path = Ui.RoundRect(iconRect, 16))
                {
                    GraphicsState state = g.Save();
                    g.SetClip(path);
                    g.DrawImage(_icon, iconRect);
                    g.Restore(state);
                }
            }
            else
            {
                Color fallback = Theme.Accent;
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        if (((x + y) & 1) == 0)
                        {
                            using (SolidBrush pixel = new SolidBrush(Theme.Alpha(fallback, 12)))
                                g.FillRectangle(pixel, iconRect.X + 12 + x * 12, iconRect.Y + 12 + y * 12, 10, 10);
                        }
                    }
                }
                using (Font font = Theme.Display(20f, FontStyle.Bold))
                    Ui.Text(g, Project.Name.Length > 0 ? Project.Name.Substring(0, 1).ToUpperInvariant() : "M", font,
                        Theme.Alpha(fallback, 230), iconRect, TextAlign.CenterMiddle);
            }

            Ui.Text(g, Project.Name, Theme.Font(10f, FontStyle.Bold), Theme.Text, new Rectangle(88, 16, Width - 200, 24), TextAlign.LeftMiddle);
            Ui.Text(g, "by " + Project.Author, Theme.Font(7.8f), Theme.Muted, new Rectangle(88, 38, Width - 200, 18), TextAlign.LeftMiddle);
            Ui.Text(g, Project.Summary, Theme.Font(8.2f), Theme.TextSoft, new Rectangle(88, 59, Width - 112, 40), TextAlign.LeftTop, true);

            using (Font font = Theme.Font(7.6f, FontStyle.Bold))
            {
                Rectangle sourceBadge = new Rectangle(16, Height - 34, 76, 22);
                Ui.FillRound(g, sourceBadge, 11, Theme.Alpha(Theme.Accent, 18));
                Ui.Text(g, Project.SourceLabel, font, Theme.Accent, sourceBadge, TextAlign.CenterMiddle);
                Ui.DrawIcon(g, IconKind.Download, new Rectangle(104, Height - 28, 13, 13), Theme.Faint, 1.4f);
                Ui.Text(g, Project.DownloadLabel + " 下载", Theme.Font(7.6f), Theme.Faint, new Rectangle(121, Height - 34, 150, 22), TextAlign.LeftMiddle);
            }
            Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(Width - 26, Height - 27, 12, 12), Theme.Faint, 1.4f);
        }
    }

    public sealed class LoadingCard : Control
    {
        private readonly string _text;
        private readonly Timer _timer;
        private float _angle;

        public LoadingCard(string text)
        {
            _text = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 120;
            _timer = new Timer();
            _timer.Interval = 30;
            _timer.Tick += delegate { _angle += 8; Invalidate(); };
            _timer.Start();
            Disposed += delegate { _timer.Stop(); _timer.Dispose(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 24, Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 24, Theme.OutlineVariant, 1f);
            Ui.DrawSpinner(g, new Rectangle(18, Height / 2 - 10, 20, 20), Theme.Accent, _angle);
            Ui.Text(g, _text, Theme.Font(8.8f), Theme.TextSoft, new Rectangle(50, 0, Width - 70, Height), TextAlign.LeftMiddle);
        }
    }
}
