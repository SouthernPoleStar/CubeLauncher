using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class InstanceDetailDialog : DarkDialog
    {
        private readonly MainForm _main;
        private readonly string _instanceId;
        private readonly string _minecraft;
        private readonly Panel _content;
        private readonly List<AccentButton> _tabs = new List<AccentButton>();
        private readonly Label _summary;
        private readonly string[] _names = new string[] { "概览", "模组", "材质包", "光影包", "世界", "截图", "导出" };
        private int _tab;

        public InstanceDetailDialog(MainForm main, InstalledVersion version)
            : base("实例管理 · " + version.DisplayName, version.LoaderLabel + " · " + version.Id, 1000, 700)
        {
            _main = main;
            _instanceId = version.Id;
            _minecraft = version.Minecraft;

            _summary = new Label();
            _summary.AutoSize = false;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = Theme.Muted;
            _summary.Font = Theme.Font(8.2f);
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.SetBounds(232, 76, 700, 24);
            _summary.Text = "游戏目录  " + main.Config.InstanceDir(version.Id);
            Controls.Add(_summary);

            for (int i = 0; i < _names.Length; i++)
            {
                int index = i;
                AccentButton tab = new AccentButton();
                tab.Text = _names[i];
                tab.Style = ButtonStyle.Ghost;
                tab.Width = 170;
                tab.Height = 42;
                tab.Location = new Point(26, 112 + i * 50);
                tab.Click += delegate { SelectTab(index); };
                Controls.Add(tab);
                _tabs.Add(tab);
            }

            _content = new BufferedPanel();
            _content.SetBounds(218, 108, 754, 558);
            _content.BackColor = Theme.Bg;
            Controls.Add(_content);
            _content.BringToFront();

            Shown += delegate { SelectTab(0); };
        }

        private void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].Style = i == index ? ButtonStyle.Subtle : ButtonStyle.Ghost;
                _tabs[i].Invalidate();
            }
            _content.SuspendLayout();
            _content.Controls.Clear();
            switch (index)
            {
                case 0: BuildOverview(); break;
                case 1: BuildFiles("模组", "mods", InstanceService.ListMods(_main.Config, _instanceId)); break;
                case 2: BuildFiles("材质包", "resourcepacks", InstanceService.ListResourcePacks(_main.Config, _instanceId)); break;
                case 3: BuildFiles("光影包", "shaderpacks", InstanceService.ListShaderPacks(_main.Config, _instanceId)); break;
                case 4: BuildWorlds(); break;
                case 5: BuildScreenshots(); break;
                case 6: BuildExport(); break;
            }
            _content.ResumeLayout();
        }

        private void BuildOverview()
        {
            string instanceDir = _main.Config.InstanceDir(_instanceId);
            AddSection("实例信息", _minecraft + "  ·  " + LoaderName() + "  ·  " + _instanceId);
            AddInfo("游戏目录", instanceDir, 62);
            AddInfo("模组数量", InstanceService.ListMods(_main.Config, _instanceId).Count.ToString(), 90);
            AddInfo("材质包数量", InstanceService.ListResourcePacks(_main.Config, _instanceId).Count.ToString(), 118);
            AddInfo("光影包数量", InstanceService.ListShaderPacks(_main.Config, _instanceId).Count.ToString(), 146);
            AddInfo("世界数量", InstanceService.ListWorlds(_main.Config, _instanceId).Count.ToString(), 174);
            AddAction("打开实例目录", 224, delegate { Util.OpenFolder(instanceDir); });
        }

        private void BuildFiles(string title, string folder, List<InstanceItem> items)
        {
            Panel head = new Panel();
            head.Height = 54;
            head.Width = _content.Width - 20;
            head.BackColor = Theme.Bg;
            Label label = new Label();
            label.Text = title + "  ·  " + items.Count + " 个";
            label.AutoSize = false;
            label.Font = Theme.Display(12f, FontStyle.Bold);
            label.ForeColor = Theme.Text;
            label.SetBounds(0, 6, 350, 26);
            head.Controls.Add(label);
            AccentButton download = new AccentButton();
            download.Text = title == "光影包" ? "下载光影包" : "下载材质包";
            download.Style = ButtonStyle.Accent;
            download.Width = 130;
            download.Height = 36;
            download.Location = new Point(head.Width - 278, 4);
            download.Click += delegate { OpenDownload(title == "光影包" ? "shader" : "resourcepack"); };
            head.Controls.Add(download);
            AccentButton folderButton = new AccentButton();
            folderButton.Text = "打开目录";
            folderButton.Style = ButtonStyle.Ghost;
            folderButton.Width = 108;
            folderButton.Height = 36;
            folderButton.Location = new Point(head.Width - 138, 4);
            folderButton.Click += delegate { Util.OpenFolder(Path.Combine(_main.Config.InstanceDir(_instanceId), folder)); };
            head.Controls.Add(folderButton);
            _content.Controls.Add(head);

            int y = 62;
            if (items.Count == 0)
            {
                _content.Controls.Add(Empty("这里还没有" + title + "，点击右上角下载。")); return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                InstanceItem item = items[i];
                InstanceItemRow row = new InstanceItemRow(item, InstanceItemRow.RowKind.File);
                row.Toggle += delegate { Toggle(item); };
                row.Delete += delegate { Delete(item); };
                row.Open += delegate { Util.OpenFolder(Path.GetDirectoryName(item.Path)); };
                row.SetBounds(0, y, _content.Width - 22, 58);
                _content.Controls.Add(row);
                y += 64;
            }
        }

        private void BuildWorlds()
        {
            List<InstanceItem> items = InstanceService.ListWorlds(_main.Config, _instanceId);
            AddSection("世界管理", items.Count + " 个存档");
            int y = 58;
            if (items.Count == 0) { _content.Controls.Add(Empty("还没有世界存档。")); return; }
            for (int i = 0; i < items.Count; i++)
            {
                InstanceItem item = items[i];
                InstanceItemRow row = new InstanceItemRow(item, InstanceItemRow.RowKind.World);
                row.Open += delegate { Util.OpenFolder(item.Path); };
                row.Delete += delegate { Delete(item); };
                row.SetBounds(0, y, _content.Width - 22, 64);
                _content.Controls.Add(row);
                y += 70;
            }
        }

        private void BuildScreenshots()
        {
            List<InstanceItem> items = InstanceService.ListScreenshots(_main.Config, _instanceId);
            AddSection("截图管理", items.Count + " 张");
            AccentButton folder = new AccentButton();
            folder.Text = "打开截图目录";
            folder.Style = ButtonStyle.Ghost;
            folder.Width = 140;
            folder.Height = 34;
            folder.Location = new Point(_content.Width - 168, 8);
            folder.Click += delegate { Util.OpenFolder(_main.Config.ScreenshotsDir(_instanceId)); };
            _content.Controls.Add(folder);
            int y = 58;
            for (int i = 0; i < items.Count && i < 40; i++)
            {
                InstanceItem item = items[i];
                InstanceItemRow row = new InstanceItemRow(item, InstanceItemRow.RowKind.Screenshot);
                row.Open += delegate { Util.OpenFile(item.Path); };
                row.Delete += delegate { Delete(item); };
                row.SetBounds(0, y, _content.Width - 22, 58);
                _content.Controls.Add(row);
                y += 64;
            }
            if (items.Count == 0) _content.Controls.Add(Empty("还没有截图。"));
        }

        private void BuildExport()
        {
            AddSection("导出版本为整合包", "导出 mods、config、resourcepacks、shaderpacks 和实例清单");
            AddInfo("格式", "Cube 实例 ZIP（包含 manifest.json）", 62);
            AddAction("选择位置并导出", 102, delegate
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Filter = "整合包 (*.zip)|*.zip";
                dialog.FileName = Util.SafeFileName(_instanceId) + "-modpack.zip";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    InstanceService.ExportModpack(_main.Config, _instanceId, dialog.FileName);
                    _main.Toast("整合包已导出");
                    Util.OpenFolder(Path.GetDirectoryName(dialog.FileName));
                }
                catch (Exception ex) { _main.ToastError("导出失败: " + ex.Message); }
            });
        }

        private void AddSection(string title, string subtitle)
        {
            Label heading = new Label();
            heading.Text = title;
            heading.AutoSize = false;
            heading.Font = Theme.Display(13f, FontStyle.Bold);
            heading.ForeColor = Theme.Text;
            heading.SetBounds(4, 4, _content.Width - 20, 26);
            _content.Controls.Add(heading);
            Label sub = new Label();
            sub.Text = subtitle;
            sub.AutoSize = false;
            sub.Font = Theme.Font(8f);
            sub.ForeColor = Theme.Muted;
            sub.SetBounds(4, 28, _content.Width - 20, 20);
            _content.Controls.Add(sub);
        }

        private void AddInfo(string key, string value, int y)
        {
            Label label = new Label();
            label.Text = key;
            label.AutoSize = false;
            label.ForeColor = Theme.Muted;
            label.Font = Theme.Font(8.6f);
            label.SetBounds(4, y, 120, 22);
            _content.Controls.Add(label);
            Label val = new Label();
            val.Text = value;
            val.AutoSize = false;
            val.ForeColor = Theme.TextSoft;
            val.Font = Theme.Font(8.6f);
            val.SetBounds(132, y, _content.Width - 150, 22);
            _content.Controls.Add(val);
        }

        private void AddAction(string text, int y, EventHandler handler)
        {
            AccentButton button = new AccentButton();
            button.Text = text;
            button.Style = ButtonStyle.Accent;
            button.Width = 190;
            button.Height = 40;
            button.Location = new Point(4, y);
            button.Click += handler;
            _content.Controls.Add(button);
        }

        private Control Empty(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.ForeColor = Theme.Muted;
            label.Font = Theme.Font(9f);
            label.SetBounds(4, 70, _content.Width - 20, 30);
            return label;
        }

        private void Toggle(InstanceItem item)
        {
            try
            {
                InstanceService.SetEnabled(item, !item.Enabled);
                SelectTab(_tab);
            }
            catch (Exception ex) { _main.ToastError(ex.Message); }
        }

        private void Delete(InstanceItem item)
        {
            if (!_main.Confirm("删除确认", "确定删除 " + item.Name + " 吗？", "删除")) return;
            try { InstanceService.Delete(item); SelectTab(_tab); }
            catch (Exception ex) { _main.ToastError(ex.Message); }
        }

        private void OpenDownload(string kind)
        {
            using (PackDownloadDialog dialog = new PackDownloadDialog(_main, _instanceId, _minecraft, kind))
            {
                dialog.ShowDialog(this);
                SelectTab(_tab);
            }
        }

        private string LoaderName()
        {
            InstalledVersion v = _main.FindInstalledVersion(_instanceId);
            return v == null ? "原版" : v.LoaderLabel;
        }
    }

    internal sealed class InstanceItemRow : Control
    {
        public enum RowKind { File, World, Screenshot }
        private readonly InstanceItem _item;
        private readonly RowKind _kind;
        private bool _hover;
        private AccentButton _toggle;
        private AccentButton _delete;
        private AccentButton _open;

        public event EventHandler Toggle;
        public event EventHandler Delete;
        public event EventHandler Open;

        public InstanceItemRow(InstanceItem item, RowKind kind)
        {
            _item = item;
            _kind = kind;
            Height = 58;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            if (kind == RowKind.File)
            {
                _toggle = MakeButton(item.Enabled ? "禁用" : "启用", 72, delegate { if (Toggle != null) Toggle(this, EventArgs.Empty); });
                _delete = MakeButton("删除", 58, delegate { if (Delete != null) Delete(this, EventArgs.Empty); });
                _open = MakeButton("目录", 58, delegate { if (Open != null) Open(this, EventArgs.Empty); });
            }
            else
            {
                _delete = MakeButton("删除", 58, delegate { if (Delete != null) Delete(this, EventArgs.Empty); });
                _open = MakeButton(kind == RowKind.Screenshot ? "查看" : "打开", 58, delegate { if (Open != null) Open(this, EventArgs.Empty); });
            }
        }

        private AccentButton MakeButton(string text, int width, EventHandler handler)
        {
            AccentButton button = new AccentButton();
            button.Text = text;
            button.Style = ButtonStyle.Ghost;
            button.Width = width;
            button.Height = 30;
            button.Click += handler;
            Controls.Add(button);
            return button;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int x = Width - 16;
            for (int i = Controls.Count - 1; i >= 0; i--)
            {
                Control c = Controls[i];
                x -= c.Width;
                c.Location = new Point(x, (Height - c.Height) / 2);
                x -= 8;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 16, _hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.StrokeRound(g, rect, 16, Theme.OutlineVariant, 1f);
            Color accent = _kind == RowKind.Screenshot ? Theme.Cyan : (_kind == RowKind.World ? Theme.Gold : Theme.Accent);
            Ui.DrawIcon(g, _kind == RowKind.World ? IconKind.Folder : (_kind == RowKind.Screenshot ? IconKind.Box : IconKind.Puzzle),
                new Rectangle(14, 16, 22, 22), accent, 1.5f);
            Ui.Text(g, _item.Name, Theme.Font(9.2f, FontStyle.Bold), _item.Enabled ? Theme.Text : Theme.Faint,
                new Rectangle(48, 8, Math.Max(80, Width - 270), 22), TextAlign.LeftMiddle, true);
            Ui.Text(g, (_item.Enabled ? "" : "已禁用 · ") + _item.SizeLabel + " · " + _item.Modified.ToString("yyyy-MM-dd"),
                Theme.Font(7.6f), Theme.Muted, new Rectangle(48, 29, Math.Max(80, Width - 270), 18), TextAlign.LeftMiddle);
        }
    }
}
