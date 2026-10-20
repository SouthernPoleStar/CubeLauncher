using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class ModDetailDialog : DarkDialog
    {
        private readonly MainForm _main;
        private readonly ModProject _project;
        private readonly InstalledVersion _version;
        private readonly PictureBox _icon;
        private readonly Label _title;
        private readonly Label _author;
        private readonly Label _desc;
        private readonly DarkComboBox _files;
        private readonly Label _fileInfo;
        private readonly Label _status;
        private readonly AccentButton _install;
        private readonly AccentButton _website;
        private List<ModFile> _fileList;

        public ModDetailDialog(MainForm main, ModProject project, InstalledVersion version)
            : base(project.Name, project.SourceLabel + "  ·  " + project.Author, 790, 660)
        {
            _main = main;
            _project = project;
            _version = version;

            _icon = new PictureBox();
            _icon.SizeMode = PictureBoxSizeMode.Zoom;
            _icon.BackColor = Theme.Card;
            _icon.SetBounds(28, 84, 92, 92);
            Controls.Add(_icon);
            main.Images.Request(_icon, project.IconUrl, delegate(Image image) { if (!_icon.IsDisposed) _icon.Image = image; });

            _title = new Label();
            _title.AutoSize = false;
            _title.BackColor = Color.Transparent;
            _title.ForeColor = Theme.Text;
            _title.Font = Theme.Display(17f, FontStyle.Bold);
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.SetBounds(138, 82, 620, 32);
            _title.Text = project.Name;
            Controls.Add(_title);

            _author = new Label();
            _author.AutoSize = false;
            _author.BackColor = Color.Transparent;
            _author.ForeColor = Theme.Accent;
            _author.Font = Theme.Font(8.5f);
            _author.TextAlign = ContentAlignment.MiddleLeft;
            _author.SetBounds(138, 116, 620, 20);
            _author.Text = "by " + project.Author + "  ·  " + project.DownloadLabel + " 次下载";
            Controls.Add(_author);

            _desc = new Label();
            _desc.AutoSize = false;
            _desc.BackColor = Color.Transparent;
            _desc.ForeColor = Theme.TextSoft;
            _desc.Font = Theme.Font(8.8f);
            _desc.TextAlign = ContentAlignment.TopLeft;
            _desc.SetBounds(28, 194, 732, 82);
            _desc.Text = project.Summary;
            Controls.Add(_desc);

            Label filesLabel = MakeLabel("选择文件版本", 28, 288, 300, Theme.TextSoft, 8.2f, true);
            _files = new DarkComboBox();
            _files.SetBounds(28, 310, 732, 40);
            _files.SelectedIndexChanged += delegate { UpdateFileInfo(); };
            Controls.Add(_files);

            _fileInfo = new Label();
            _fileInfo.AutoSize = false;
            _fileInfo.BackColor = Color.Transparent;
            _fileInfo.ForeColor = Theme.Muted;
            _fileInfo.Font = Theme.Font(7.8f);
            _fileInfo.TextAlign = ContentAlignment.MiddleLeft;
            _fileInfo.SetBounds(28, 356, 732, 22);
            Controls.Add(_fileInfo);

            Label warn = new Label();
            warn.AutoSize = false;
            warn.BackColor = Color.Transparent;
            warn.ForeColor = Theme.Gold;
            warn.Font = Theme.Font(7.8f);
            warn.TextAlign = ContentAlignment.MiddleLeft;
            warn.SetBounds(28, 388, 732, 22);
            warn.Text = "安装目标: " + version.DisplayName + "  ·  必需前置模组会自动安装";
            Controls.Add(warn);

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.Font(8f);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.SetBounds(28, 420, 732, 24);
            _status.Text = "正在获取文件列表...";
            Controls.Add(_status);

            _website = new AccentButton();
            _website.Text = "打开项目页";
            _website.Style = ButtonStyle.Ghost;
            _website.Icon = IconKind.External;
            _website.Width = 132;
            _website.Height = 38;
            _website.Location = new Point(28, 594);
            _website.Click += delegate { Util.OpenUrl(_project.Website); };
            Controls.Add(_website);

            AccentButton close = new AccentButton();
            close.Text = "关闭";
            close.Style = ButtonStyle.Ghost;
            close.Width = 88;
            close.Height = 38;
            close.Location = new Point(556, 594);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            _install = new AccentButton();
            _install.Text = "安装到实例";
            _install.Icon = IconKind.Download;
            _install.Width = 124;
            _install.Height = 38;
            _install.Location = new Point(636, 594);
            _install.Enabled = false;
            _install.Click += delegate { InstallSelected(); };
            Controls.Add(_install);

            Shown += delegate { LoadFiles(); };
        }

        private void LoadFiles()
        {
            if (string.Equals(_version.Loader, "vanilla", StringComparison.OrdinalIgnoreCase))
            {
                _status.ForeColor = Theme.Red;
                _status.Text = "当前是原版实例，无法加载模组。请安装 Fabric 或 Forge 版本。";
                return;
            }
            Ui.Run(this, delegate
            {
                List<ModFile> files = ModService.LoadFiles(_main.Config, _project, _version.Minecraft, _version.Loader);
                Ui.Post(this, delegate
                {
                    _fileList = files;
                    _files.Items.Clear();
                    if (files.Count == 0)
                    {
                        _status.ForeColor = Theme.Gold;
                        _status.Text = "没有找到适配 " + _version.Minecraft + " / " + _version.Loader + " 的文件。";
                        return;
                    }
                    for (int i = 0; i < files.Count; i++) _files.Items.Add(files[i]);
                    _files.SelectedIndex = 0;
                    _status.Text = "找到 " + files.Count + " 个兼容文件。";
                    _install.Enabled = true;
                });
            }, delegate(Exception ex)
            {
                _status.ForeColor = Theme.Red;
                _status.Text = "文件列表加载失败: " + ex.Message;
            });
        }

        private void UpdateFileInfo()
        {
            ModFile file = _files.SelectedItem as ModFile;
            if (file == null) return;
            string deps = file.Dependencies.Count > 0 ? "  ·  " + file.Dependencies.Count + " 个关联项目" : "";
            _fileInfo.Text = file.FileName + "  ·  " + file.SizeLabel + deps;
        }

        private void InstallSelected()
        {
            ModFile file = _files.SelectedItem as ModFile;
            if (file == null) return;
            int count = 0;
            using (ProgressDialog dialog = new ProgressDialog(this, "安装模组", "正在下载 " + file.FileName + "..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    List<string> installed = ModService.Install(_main.Config, _project, file, _version.Minecraft, _version.Loader, _version.Id, progress);
                    count = installed.Count;
                };
                dialog.Run();
                if (dialog.Error != null)
                {
                    _status.ForeColor = Theme.Red;
                    _status.Text = "安装失败: " + dialog.Error.Message;
                    return;
                }
            }
            _main.Toast("已安装 " + (count > 0 ? count + " 个文件" : _project.Name));
            _main.RefreshData();
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed class LogForm : Form
    {
        private readonly RichTextBox _log;
        private bool _closing;

        public LogForm()
        {
            Text = "运行日志";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(860, 620);
            MinimumSize = new Size(620, 420);
            BackColor = Theme.Bg;
            FormBorderStyle = FormBorderStyle.Sizable;
            ShowIcon = false;
            DoubleBuffered = true;

            _log = new RichTextBox();
            _log.BackColor = Theme.Bg;
            _log.ForeColor = Theme.Text;
            _log.BorderStyle = BorderStyle.None;
            _log.Font = Theme.Mono(8.4f);
            _log.ReadOnly = true;
            _log.DetectUrls = false;
            _log.Dock = DockStyle.Fill;
            _log.WordWrap = false;
            _log.ScrollBars = RichTextBoxScrollBars.Both;
            _log.SetBounds(18, 58, ClientSize.Width - 36, ClientSize.Height - 116);

            BufferedPanel header = new BufferedPanel();
            header.Dock = DockStyle.Top;
            header.Height = 46;
            header.BackColor = Theme.Bg2;
            Label title = new Label();
            title.Text = "运行日志";
            title.ForeColor = Theme.Text;
            title.Font = Theme.Display(13f, FontStyle.Bold);
            title.AutoSize = false;
            title.BackColor = Color.Transparent;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.SetBounds(18, 0, 300, 46);
            header.Controls.Add(title);
            Controls.Add(header);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 58;
            footer.BackColor = Theme.Bg2;
            AccentButton clear = new AccentButton();
            clear.Text = "清空";
            clear.Style = ButtonStyle.Ghost;
            clear.Width = 88;
            clear.Height = 34;
            clear.Location = new Point(18, 12);
            clear.Click += delegate { _log.Clear(); };
            footer.Controls.Add(clear);
            AccentButton folder = new AccentButton();
            folder.Text = "日志目录";
            folder.Style = ButtonStyle.Ghost;
            folder.Width = 104;
            folder.Height = 34;
            folder.Location = new Point(116, 12);
            folder.Click += delegate { Util.OpenFolder(Path.GetDirectoryName(Log.FilePath)); };
            footer.Controls.Add(folder);
            AccentButton close = new AccentButton();
            close.Text = "关闭";
            close.Style = ButtonStyle.Ghost;
            close.Width = 88;
            close.Height = 34;
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(Width - 128, 12);
            close.Click += delegate { Close(); };
            footer.Controls.Add(close);
            Controls.Add(footer);

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Bg;
            body.Controls.Add(_log);
            Controls.Add(body);
            body.BringToFront();
            header.BringToFront();
            footer.BringToFront();

            Load += delegate
            {
                LoadExisting();
                Log.Message += OnLog;
            };
            FormClosed += delegate
            {
                _closing = true;
                Log.Message -= OnLog;
            };
        }

        private void LoadExisting()
        {
            try
            {
                if (!File.Exists(Log.FilePath)) return;
                string[] lines = File.ReadAllLines(Log.FilePath);
                int start = Math.Max(0, lines.Length - 500);
                for (int i = start; i < lines.Length; i++) AppendLine(lines[i]);
            }
            catch { }
        }

        private void OnLog(string line)
        {
            if (_closing || IsDisposed) return;
            Ui.Post(this, delegate { AppendLine(line); });
        }

        private void AppendLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0 ? Theme.Red :
                                  (line.IndexOf("[WARN]", StringComparison.OrdinalIgnoreCase) >= 0 ? Theme.Gold : Theme.TextSoft);
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
            if (_log.TextLength > 400000)
            {
                _log.Select(0, 100000);
                _log.SelectedText = "";
            }
        }
    }
}
