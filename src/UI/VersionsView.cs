using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class VersionsView : BaseView
    {
        private readonly SearchBox _search;
        private readonly SegmentedControl _filter;
        private readonly AccentButton _install;
        private readonly SmoothScrollPanel _list;
        private readonly List<VersionRow> _rows = new List<VersionRow>();
        private string _lastFilter = "";

        public VersionsView(MainForm main) : base(main)
        {
            _search = new SearchBox();
            _search.Placeholder = "搜索版本号，例如 1.20.1";
            _search.TextChangedCustom += delegate { BuildList(); };
            Controls.Add(_search);

            _filter = new SegmentedControl();
            _filter.Options = new string[] { "全部", "正式版", "快照" };
            _filter.SelectedIndexChanged += delegate { BuildList(); };
            Controls.Add(_filter);

            _install = new AccentButton();
            _install.Text = "安装新版本";
            _install.Icon = IconKind.Plus;
            _install.Width = 142;
            _install.Click += delegate { Main.OpenInstallDialog(""); };
            Controls.Add(_install);

            _list = new SmoothScrollPanel();
            Controls.Add(_list);
            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        public override void RefreshView()
        {
            BuildList();
        }

        private void LayoutChildren()
        {
            int left = 28;
            int width = ClientSize.Width - 56;
            _search.SetBounds(left, 18, 286, 40);
            _filter.SetBounds(left + 300, 18, 250, 40);
            _install.SetBounds(ClientSize.Width - 170, 18, 142, 40);
            _list.SetBounds(left, 74, width, Math.Max(100, ClientSize.Height - 98));
        }

        private void BuildList()
        {
            if (_list == null) return;
            string query = _search == null ? "" : _search.Value.Trim();
            int filter = _filter == null ? 0 : _filter.SelectedIndex;
            string key = query + "|" + filter + "|" + Main.Installed.Count + "|" + Main.RunningGameKey + "|" + (Main.Versions.Manifest == null ? 0 : Main.Versions.Manifest.Count);
            if (key == _lastFilter && _list.Content.Controls.Count > 0) return;
            _lastFilter = key;
            _list.Clear();
            _rows.Clear();

            HashSet<string> installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Main.Installed.Count; i++) installedIds.Add(Main.Installed[i].Id);

            int added = 0;
            for (int i = 0; i < Main.Installed.Count; i++)
            {
                InstalledVersion version = Main.Installed[i];
                if (!Matches(version.Id, query)) continue;
                if (filter == 2) continue;
                VersionRow row = new VersionRow();
                row.SetInstalled(version);
                row.SetRunning(Main.IsGameRunning(version.Id));
                row.Launch += delegate(object sender, EventArgs e) { Main.LaunchAsync(((VersionRow)sender).VersionId); };
                row.Close += delegate(object sender, EventArgs e) { Main.CloseGameAsync(((VersionRow)sender).VersionId); };
                row.Delete += delegate(object sender, EventArgs e) { Main.RemoveVersion(((VersionRow)sender).Installed); };
                row.OpenFolder += delegate(object sender, EventArgs e) { Util.OpenFolder(Main.Config.InstanceDir(((VersionRow)sender).VersionId)); };
                row.Manage += delegate(object sender, EventArgs e) { Main.OpenInstanceManager(((VersionRow)sender).VersionId); };
                Add(row);
                added++;
            }

            if (Main.Versions.Manifest != null && filter != 1)
            {
                int available = 0;
                for (int i = 0; i < Main.Versions.Manifest.Count; i++)
                {
                    VersionEntry entry = Main.Versions.Manifest[i];
                    if (installedIds.Contains(entry.Id)) continue;
                    if (!Main.Config.ShowSnapshots && !entry.IsRelease) continue;
                    if (filter == 1 && !entry.IsRelease) continue;
                    if (filter == 2 && !entry.IsSnapshot) continue;
                    if (!Matches(entry.Id, query) && !Matches(entry.TypeLabel, query)) continue;
                    VersionRow row = new VersionRow();
                    row.SetAvailable(entry);
                    row.Install += delegate(object sender, EventArgs e) { Main.OpenInstallDialog(((VersionRow)sender).VersionId); };
                    Add(row);
                    added++;
                    available++;
                    if (available >= 80) break;
                }
            }

            if (added == 0)
            {
                EmptyState empty = new EmptyState(IconKind.Layers, "没有找到版本", query.Length > 0 ? "换个关键词试试，或点击右上角安装新版本。" : "版本清单加载中，或该筛选条件下没有内容。");
                empty.Width = _list.Width - 4;
                empty.Height = 180;
                _list.AddControl(empty);
            }
            _list.ScrollToTop();
        }

        private static bool Matches(string value, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Add(VersionRow row)
        {
            row.Width = Math.Max(400, _list.Width - 4);
            _list.AddControl(row);
            _rows.Add(row);
        }
    }

    public sealed class VersionRow : Control
    {
        private readonly AccentButton _action;
        private readonly AccentButton _manage;
        private readonly IconButton _folder;
        private readonly IconButton _delete;
        private VersionEntry _entry;
        private bool _hover;
        private bool _running;

        public InstalledVersion Installed;
        public string VersionId = "";
        public bool IsInstalled;

        public event EventHandler Launch;
        public event EventHandler Install;
        public event EventHandler Close;
        public event EventHandler Delete;
        public event EventHandler OpenFolder;
        public event EventHandler Manage;

        public VersionRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 74;
            Cursor = Cursors.Hand;

            _action = new AccentButton();
            _action.Width = 84;
            _action.Height = 32;
            _action.Style = ButtonStyle.Subtle;
            _action.Click += delegate
            {
                if (IsInstalled && _running) { EventHandler h = Close; if (h != null) h(this, EventArgs.Empty); }
                else if (IsInstalled) { EventHandler h = Launch; if (h != null) h(this, EventArgs.Empty); }
                else { EventHandler h = Install; if (h != null) h(this, EventArgs.Empty); }
            };
            Controls.Add(_action);

            _manage = new AccentButton();
            _manage.Text = "管理";
            _manage.Style = ButtonStyle.Ghost;
            _manage.Width = 64;
            _manage.Height = 32;
            _manage.Click += delegate { EventHandler h = Manage; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_manage);

            _folder = new IconButton();
            _folder.Icon = IconKind.Folder;
            _folder.IconSize = 15;
            _folder.Size = new Size(30, 30);
            _folder.Click += delegate { EventHandler h = OpenFolder; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_folder);

            _delete = new IconButton();
            _delete.Icon = IconKind.Trash;
            _delete.IconSize = 15;
            _delete.Size = new Size(30, 30);
            _delete.Click += delegate { EventHandler h = Delete; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_delete);

            Resize += delegate { LayoutButtons(); };
            LayoutButtons();
        }

        public void SetInstalled(InstalledVersion version)
        {
            Installed = version;
            IsInstalled = true;
            VersionId = version.Id;
            _action.Text = "启动";
            _action.Style = ButtonStyle.Subtle;
            _action.Icon = IconKind.Play;
            _action.IconSize = 13;
            _folder.Visible = true;
            _delete.Visible = true;
            _manage.Visible = true;
            SetRunning(false);
            LayoutButtons();
            Invalidate();
        }

        public void SetRunning(bool running)
        {
            _running = running && IsInstalled;
            if (IsInstalled)
            {
                _action.Text = _running ? "关闭" : "启动";
                _action.Style = _running ? ButtonStyle.Danger : ButtonStyle.Subtle;
                _action.Icon = _running ? IconKind.Power : IconKind.Play;
                _action.IconSize = 13;
            }
            _action.Invalidate();
            Invalidate();
        }

        public void SetAvailable(VersionEntry entry)
        {
            _entry = entry;
            IsInstalled = false;
            _running = false;
            VersionId = entry.Id;
            _action.Text = "安装";
            _action.Style = ButtonStyle.Ghost;
            _action.Icon = IconKind.Download;
            _action.IconSize = 13;
            _folder.Visible = false;
            _delete.Visible = false;
            _manage.Visible = false;
            LayoutButtons();
            Invalidate();
        }

        private void LayoutButtons()
        {
            _action.Location = new Point(Width - 104, (Height - _action.Height) / 2);
            _manage.Location = new Point(Width - 176, (Height - _manage.Height) / 2);
            _delete.Location = new Point(Width - 214, (Height - 30) / 2);
            _folder.Location = new Point(Width - 250, (Height - 30) / 2);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 22, _hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 22, _hover ? Theme.Accent : Theme.OutlineVariant, 1f);

            string loader = IsInstalled ? Installed.Loader : "vanilla";
            Color accent = Ui.LoaderColor(loader);
            Rectangle iconRect = new Rectangle(16, 15, 44, 44);
            if (IsInstalled)
            {
                Ui.DrawLoaderBadge(g, loader, iconRect);
            }
            else
            {
                Ui.FillRound(g, iconRect, 14, Theme.Alpha(accent, 20));
                Ui.DrawIcon(g, IconKind.Download, new Rectangle(28, 27, 20, 20), accent, 1.6f);
            }

            string name = IsInstalled ? Installed.DisplayName : _entry.Id;
            string meta;
            if (IsInstalled)
                meta = Installed.LoaderLabel + " · " + Installed.Id + " · " + (Installed.Size > 0 ? Util.HumanBytes(Installed.Size) : "已安装");
            else
                meta = _entry.TypeLabel + " · " + Util.HumanDate(_entry.ReleaseTime);
            Ui.Text(g, name, Theme.Font(10f, FontStyle.Bold), Theme.Text, new Rectangle(74, 14, Width - 250, 24), TextAlign.LeftMiddle);
            Ui.Text(g, meta, Theme.Font(8f), Theme.Muted, new Rectangle(74, 38, Width - 250, 20), TextAlign.LeftMiddle);

            string badge = _running ? "运行中" : (IsInstalled ? "已安装" : "可安装");
            Color badgeColor = (IsInstalled || _running) ? Theme.Accent : Theme.TextSoft;
            using (Font font = Theme.Font(7.5f, FontStyle.Bold))
            {
                Size size = TextRenderer.MeasureText(badge, font);
                Rectangle badgeRect = new Rectangle(Width - 300, 25, size.Width + 18, 24);
                Ui.FillRound(g, badgeRect, 12, Theme.Alpha(badgeColor, 18));
                Ui.Text(g, badge, font, badgeColor, badgeRect, TextAlign.CenterMiddle);
            }
        }
    }

    public sealed class EmptyState : Control
    {
        private readonly IconKind _icon;
        private readonly string _title;
        private readonly string _text;
        private readonly Color _accent;

        public EmptyState(IconKind icon, string title, string text)
        {
            _icon = icon;
            _title = title;
            _text = text;
            _accent = Theme.Accent;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 170;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int center = Width / 2;
            Ui.FillRound(g, new Rectangle(center - 28, 22, 56, 56), 16, Theme.Alpha(_accent, 18));
            Ui.DrawIcon(g, _icon, new Rectangle(center - 13, 37, 26, 26), _accent, 1.6f);
            Ui.Text(g, _title, Theme.Font(10f, FontStyle.Bold), Theme.Text, new Rectangle(0, 88, Width, 24), TextAlign.CenterTop);
            Ui.Text(g, _text, Theme.Font(8.2f), Theme.Muted, new Rectangle(30, 114, Width - 60, 40), TextAlign.CenterTop, true);
        }
    }
}
