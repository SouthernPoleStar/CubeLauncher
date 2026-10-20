using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class PackDownloadDialog : DarkDialog
    {
        private readonly MainForm _main;
        private readonly string _instanceId;
        private readonly string _minecraft;
        private readonly string _kind;
        private readonly SearchBox _search;
        private readonly FlowLayoutPanel _list;
        private readonly Label _status;

        public PackDownloadDialog(MainForm main, string instanceId, string minecraft, string kind)
            : base(kind == "shader" ? "下载光影包" : "下载材质包", "来自 Modrinth · 直接安装到当前实例", 780, 620)
        {
            _main = main;
            _instanceId = instanceId;
            _minecraft = minecraft;
            _kind = kind;

            _search = new SearchBox();
            _search.Placeholder = kind == "shader" ? "搜索光影包，例如 Complementary" : "搜索材质包，例如 Faithful";
            _search.SetBounds(28, 82, 520, 40);
            _search.TextChangedCustom += delegate { };
            Controls.Add(_search);

            AccentButton search = new AccentButton();
            search.Text = "搜索";
            search.Style = ButtonStyle.Accent;
            search.Width = 100;
            search.Height = 40;
            search.Location = new Point(560, 82);
            search.Click += delegate { Search(); };
            Controls.Add(search);

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.Font(8f);
            _status.SetBounds(28, 126, 720, 20);
            Controls.Add(_status);

            _list = new FlowLayoutPanel();
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.BackColor = Theme.Bg;
            _list.Padding = new Padding(0, 4, 0, 12);
            _list.SetBounds(28, 150, 722, 444);
            Controls.Add(_list);

            Shown += delegate { Search(); };
        }

        private void Search()
        {
            string query = _search.Value.Trim();
            _status.Text = "正在搜索 Modrinth...";
            _list.Controls.Clear();
            int token = ++_request;
            Ui.Run(this, delegate
            {
                ModSearchResult result = ModService.SearchType(_main.Config, "modrinth", query, _minecraft, "", 0, 20, _kind);
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    _status.Text = result.Total + " 个结果";
                    if (result.Projects.Count == 0)
                    {
                        Label empty = new Label();
                        empty.Text = "没有找到匹配的" + (_kind == "shader" ? "光影包" : "材质包");
                        empty.ForeColor = Theme.Muted;
                        empty.Font = Theme.Font(9f);
                        empty.Width = _list.Width - 24;
                        empty.Height = 40;
                        _list.Controls.Add(empty);
                        return;
                    }
                    for (int i = 0; i < result.Projects.Count; i++)
                    {
                        PackRow row = new PackRow(result.Projects[i], _kind);
                        row.Width = _list.Width - 24;
                        row.Height = 86;
                        row.Install += delegate { Install(row.Project); };
                        _list.Controls.Add(row);
                    }
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate { _status.Text = "搜索失败: " + ex.Message; });
            });
        }

        private int _request;

        private void Install(ModProject project)
        {
            _status.Text = "正在获取 " + project.Name + " 的文件...";
            Ui.Run(this, delegate
            {
                List<ModFile> files = ModService.LoadFiles(_main.Config, project, _minecraft, "");
                if (files.Count == 0) throw new Exception("没有找到适配该版本的" + (_kind == "shader" ? "光影包" : "材质包"));
                ModFile file = files[0];
                Ui.Post(this, delegate
                {
                    using (ProgressDialog dialog = new ProgressDialog(this, "安装 " + project.Name, "准备下载"))
                    {
                        dialog.Work = delegate(ProgressSink progress)
                        {
                            InstanceService.InstallPack(_main.Config, project, file, _instanceId, _kind, progress);
                        };
                        dialog.Run();
                        if (dialog.Error != null)
                        {
                            _status.Text = "安装失败: " + dialog.Error.Message;
                            return;
                        }
                    }
                    _status.Text = "已安装到实例: " + project.Name;
                    _main.Toast("已安装: " + project.Name);
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate { _status.Text = "安装失败: " + ex.Message; });
            });
        }
    }

    internal sealed class PackRow : Control
    {
        public readonly ModProject Project;
        private readonly string _kind;
        private bool _hover;
        private readonly AccentButton _install;

        public event EventHandler Install;

        public PackRow(ModProject project, string kind)
        {
            Project = project;
            _kind = kind;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _install = new AccentButton();
            _install.Text = "安装";
            _install.Style = ButtonStyle.Subtle;
            _install.Width = 74;
            _install.Height = 34;
            _install.Click += delegate { if (Install != null) Install(this, EventArgs.Empty); };
            Controls.Add(_install);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _install.Location = new Point(Width - 92, (Height - _install.Height) / 2);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 18, _hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 18, Theme.OutlineVariant, 1f);
            Color accent = _kind == "shader" ? Theme.Cyan : Theme.Gold;
            Ui.FillRound(g, new Rectangle(14, 16, 50, 50), 14, Theme.Alpha(accent, 20));
            Ui.DrawIcon(g, _kind == "shader" ? IconKind.Spark : IconKind.Box, new Rectangle(28, 30, 22, 22), accent, 1.5f);
            Ui.Text(g, Project.Name, Theme.Font(9.5f, FontStyle.Bold), Theme.Text,
                new Rectangle(78, 12, Math.Max(100, Width - 185), 22), TextAlign.LeftMiddle, true);
            Ui.Text(g, "by " + Project.Author + "  ·  " + Project.DownloadLabel + " 下载", Theme.Font(7.5f), Theme.Muted,
                new Rectangle(78, 33, Math.Max(100, Width - 185), 16), TextAlign.LeftMiddle);
            Ui.Text(g, Project.Summary, Theme.Font(7.8f), Theme.TextSoft,
                new Rectangle(78, 50, Math.Max(100, Width - 100), 30), TextAlign.LeftTop, true);
        }
    }
}
