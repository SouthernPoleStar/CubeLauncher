using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public class DarkDialog : Form
    {
        protected readonly string DialogTitle;
        protected readonly string DialogSubtitle;
        protected readonly IconButton CloseButton;
        protected int ContentTop = 76;

        public DarkDialog(string title, string subtitle, int width, int height)
        {
            DialogTitle = title;
            DialogSubtitle = subtitle;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Bg;
            ClientSize = new Size(width, height);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            KeyPreview = true;
            Padding = new Padding(1);
            CloseButton = new IconButton();
            CloseButton.Icon = IconKind.Close;
            CloseButton.Size = new Size(34, 34);
            CloseButton.Click += delegate { Close(); };
            Controls.Add(CloseButton);
            Resize += delegate { UpdateRegion(); CloseButton.Location = new Point(ClientSize.Width - 46, 14); };
            MouseDown += DragDialog;
            UpdateRegion();
            CloseButton.Location = new Point(ClientSize.Width - 46, 14);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        private void DragDialog(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.Y > 58) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void UpdateRegion()
        {
            using (GraphicsPath path = Ui.RoundRect(new Rectangle(0, 0, Width, Height), 12))
                Region = new Region(path);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, Theme.Bg2, Theme.Bg, 90f))
                g.FillRectangle(brush, ClientRectangle);
            using (SolidBrush glow = new SolidBrush(Theme.Alpha(Theme.Accent, 12)))
                g.FillEllipse(glow, ClientSize.Width - 300, -180, 360, 360);
            using (Pen pen = new Pen(Theme.Border, 1f))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(pen, 24, 63, Width - 24, 63);
            Ui.DrawIcon(g, IconKind.Box, new Rectangle(24, 18, 28, 28), Theme.Accent, 1.6f);
            Ui.Text(g, DialogTitle, Theme.Display(14f, FontStyle.Bold), Theme.Text, new Rectangle(64, 14, ClientSize.Width - 130, 24), TextAlign.LeftMiddle);
            if (!string.IsNullOrEmpty(DialogSubtitle))
                Ui.Text(g, DialogSubtitle, Theme.Font(7.8f), Theme.Muted, new Rectangle(64, 38, ClientSize.Width - 130, 18), TextAlign.LeftMiddle);
        }

        protected Label MakeLabel(string text, int x, int y, int width, Color color, float size, bool bold)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.BackColor = Color.Transparent;
            label.ForeColor = color;
            label.Font = bold ? Theme.Font(size, FontStyle.Bold) : Theme.Font(size);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(x, y, width, 22);
            Controls.Add(label);
            return label;
        }

        protected void CenterInParent()
        {
            if (Owner != null)
            {
                Location = new Point(Owner.Left + (Owner.Width - Width) / 2, Owner.Top + (Owner.Height - Height) / 2);
            }
        }
    }

    public sealed class ConfirmDialog : DarkDialog
    {
        public ConfirmDialog(string title, string message, string okText) : base(title, "", 460, 230)
        {
            Label text = new Label();
            text.Text = message;
            text.AutoSize = false;
            text.BackColor = Color.Transparent;
            text.ForeColor = Theme.TextSoft;
            text.Font = Theme.Font(9f);
            text.TextAlign = ContentAlignment.MiddleLeft;
            text.SetBounds(28, 84, 404, 66);
            Controls.Add(text);

            AccentButton cancel = new AccentButton();
            cancel.Text = "取消";
            cancel.Style = ButtonStyle.Ghost;
            cancel.Width = 100;
            cancel.Height = 38;
            cancel.Location = new Point(244, 164);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AccentButton ok = new AccentButton();
            ok.Text = okText;
            ok.Style = ButtonStyle.Danger;
            ok.Width = 100;
            ok.Height = 38;
            ok.Location = new Point(356, 164);
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);
        }
    }

    public sealed class ProgressTrack : Control
    {
        private double _value;
        public double Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(1, value)); Invalidate(); }
        }

        public ProgressTrack()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 10;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Ui.FillRound(g, new Rectangle(0, 1, Width - 1, Height - 2), (Height - 2) / 2, Theme.Border);
            int width = (int)((Width - 1) * _value);
            if (width > 4)
                Ui.FillRoundGradient(g, new Rectangle(0, 1, width, Height - 2), (Height - 2) / 2, Theme.Accent, Theme.Cyan, 0f);
        }
    }

    public sealed class ProgressDialog : DarkDialog
    {
        private readonly Label _stage;
        private readonly Label _detail;
        private readonly Label _percent;
        private readonly ProgressTrack _track;
        private readonly AccentButton _cancel;
        private readonly Timer _spinnerTimer;
        private readonly System.Threading.CancellationTokenSource _cancelSource;
        private int _spinner;
        private bool _running;
        private bool _completed;

        public Action<ProgressSink> Work;
        public Exception Error;
        public object Result;

        public ProgressDialog(Control owner, string title, string subtitle) : base(title, subtitle, 560, 286)
        {
            Form parent = owner as Form;
            if (parent == null && owner != null) parent = owner.FindForm();
            if (parent != null) Owner = parent;
            _cancelSource = new System.Threading.CancellationTokenSource();
            _stage = new Label();
            _stage.AutoSize = false;
            _stage.BackColor = Color.Transparent;
            _stage.ForeColor = Theme.TextSoft;
            _stage.Font = Theme.Font(9f);
            _stage.TextAlign = ContentAlignment.MiddleLeft;
            _stage.SetBounds(28, 86, 500, 22);
            _stage.Text = subtitle;
            Controls.Add(_stage);

            _track = new ProgressTrack();
            _track.SetBounds(28, 122, 502, 10);
            Controls.Add(_track);

            _percent = new Label();
            _percent.AutoSize = false;
            _percent.BackColor = Color.Transparent;
            _percent.ForeColor = Theme.Accent;
            _percent.Font = Theme.Display(10f, FontStyle.Bold);
            _percent.TextAlign = ContentAlignment.MiddleRight;
            _percent.SetBounds(438, 136, 92, 24);
            _percent.Text = "0%";
            Controls.Add(_percent);

            _detail = new Label();
            _detail.AutoSize = false;
            _detail.BackColor = Color.Transparent;
            _detail.ForeColor = Theme.Muted;
            _detail.Font = Theme.Font(7.8f);
            _detail.TextAlign = ContentAlignment.MiddleLeft;
            _detail.SetBounds(28, 140, 400, 24);
            Controls.Add(_detail);

            _cancel = new AccentButton();
            _cancel.Text = "取消";
            _cancel.Style = ButtonStyle.Ghost;
            _cancel.Width = 96;
            _cancel.Height = 36;
            _cancel.Location = new Point(436, 220);
            _cancel.Click += delegate
            {
                if (_running)
                {
                    _cancelSource.Cancel();
                    _cancel.Text = "正在取消...";
                    _cancel.Enabled = false;
                }
                else Close();
            };
            Controls.Add(_cancel);

            _spinnerTimer = new Timer();
            _spinnerTimer.Interval = 40;
            _spinnerTimer.Tick += delegate { _spinner += 9; if (_spinner > 360) _spinner = 0; Invalidate(); };
            _spinnerTimer.Start();

            Shown += delegate { StartWork(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_running && !_completed)
                {
                    _cancelSource.Cancel();
                    e.Cancel = true;
                }
            };
        }

        private void StartWork()
        {
            if (Work == null) return;
            _running = true;
            ProgressSink progress = new ProgressSink();
            progress.Token = _cancelSource.Token;
            progress.Stage = delegate(string text)
            {
                Ui.Post(this, delegate
                {
                    _stage.Text = text;
                    _detail.Text = text;
                });
            };
            progress.Value = delegate(double value, string detail)
            {
                Ui.Post(this, delegate
                {
                    _track.Value = value;
                    _percent.Text = (int)(value * 100) + "%";
                    _detail.Text = detail;
                });
            };

            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    Work(progress);
                    Ui.Post(this, delegate
                    {
                        Result = null;
                        _completed = true;
                        _running = false;
                        _track.Value = 1;
                        _percent.Text = "100%";
                        _stage.Text = "完成";
                        _spinnerTimer.Stop();
                        DialogResult = DialogResult.OK;
                        Close();
                    });
                }
                catch (OperationCanceledException)
                {
                    Ui.Post(this, delegate
                    {
                        _completed = true;
                        _running = false;
                        DialogResult = DialogResult.Cancel;
                        Close();
                    });
                }
                catch (Exception ex)
                {
                    Ui.Post(this, delegate
                    {
                        Error = ex;
                        _completed = true;
                        _running = false;
                        _spinnerTimer.Stop();
                        _stage.Text = "操作失败";
                        _stage.ForeColor = Theme.Red;
                        _detail.Text = ex.Message;
                        _detail.ForeColor = Theme.Red;
                        _percent.Text = "!";
                        _cancel.Text = "关闭";
                        _cancel.Enabled = true;
                        _cancel.Click += delegate { Close(); };
                    });
                }
            });
        }

        public void Run()
        {
            ShowDialog(Owner);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_running)
            {
                Ui.DrawSpinner(e.Graphics, new Rectangle(28, 210, 18, 18), Theme.Alpha(Theme.Accent, 190), _spinner);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinnerTimer.Stop();
                _spinnerTimer.Dispose();
                _cancelSource.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class InstallChoice
    {
        public string Minecraft = "";
        public string Loader = "vanilla";
        public string LoaderVersion = "";
        public string DisplayName = "";

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public sealed class InstallDialog : DarkDialog
    {
        private readonly MainForm _main;
        private readonly DarkComboBox _baseVersion;
        private readonly SegmentedControl _loader;
        private readonly DarkComboBox _loaderVersion;
        private readonly Label _summary;
        private readonly Label _hint;
        private readonly AccentButton _install;
        private readonly AccentButton _cancel;
        private string _prefillBase;
        private int _request;

        public InstallChoice Result;

        private static readonly string[] LoaderNames = new string[] { "原版", "Fabric", "Forge", "Quilt" };
        private static readonly string[] LoaderKeys = new string[] { "vanilla", "fabric", "forge", "quilt" };

        public InstallDialog(MainForm main, string baseVersion) : base("安装新版本", "选择游戏版本与模组加载器，全部资源自动下载", 700, 566)
        {
            _main = main;
            _prefillBase = baseVersion;

            Label baseLabel = MakeLabel("Minecraft 版本", 28, 82, 200, Theme.TextSoft, 8.2f, true);
            _baseVersion = new DarkComboBox();
            _baseVersion.SetBounds(28, 104, 300, 40);
            _baseVersion.SelectedIndexChanged += delegate { ReloadLoaderVersions(); };
            Controls.Add(_baseVersion);

            Label loaderLabel = MakeLabel("模组加载器", 350, 82, 200, Theme.TextSoft, 8.2f, true);
            _loader = new SegmentedControl();
            _loader.Options = LoaderNames;
            _loader.SetBounds(350, 104, 318, 40);
            _loader.SelectedIndexChanged += delegate { ReloadLoaderVersions(); };
            Controls.Add(_loader);

            Label lvLabel = MakeLabel("加载器版本", 28, 166, 200, Theme.TextSoft, 8.2f, true);
            _loaderVersion = new DarkComboBox();
            _loaderVersion.SetBounds(28, 188, 640, 40);
            _loaderVersion.SelectedIndexChanged += delegate { UpdateSummary(); };
            Controls.Add(_loaderVersion);

            _summary = new Label();
            _summary.AutoSize = false;
            _summary.BackColor = Color.Transparent;
            _summary.ForeColor = Theme.Text;
            _summary.Font = Theme.Font(9f, FontStyle.Bold);
            _summary.TextAlign = ContentAlignment.MiddleLeft;
            _summary.SetBounds(28, 248, 640, 24);
            Controls.Add(_summary);

            _hint = new Label();
            _hint.AutoSize = false;
            _hint.BackColor = Color.Transparent;
            _hint.ForeColor = Theme.Muted;
            _hint.Font = Theme.Font(8f);
            _hint.TextAlign = ContentAlignment.TopLeft;
            _hint.SetBounds(28, 278, 640, 110);
            _hint.Text = "安装时会自动补齐 Minecraft 本体、依赖库、资源文件与 Java 运行时。\n国内下载优先使用 BMCLAPI / MCIM / TUNA 镜像，失败后自动回退到官方源。";
            Controls.Add(_hint);

            Label source = new Label();
            source.AutoSize = false;
            source.BackColor = Color.Transparent;
            source.ForeColor = Theme.Accent;
            source.Font = Theme.Font(8f, FontStyle.Bold);
            source.TextAlign = ContentAlignment.MiddleLeft;
            source.SetBounds(28, 404, 640, 22);
            source.Text = "下载策略    BMCLAPI 游戏与 Forge    ·    MCIM 模组    ·    TUNA Java";
            Controls.Add(source);

            _cancel = new AccentButton();
            _cancel.Text = "取消";
            _cancel.Style = ButtonStyle.Ghost;
            _cancel.Width = 96;
            _cancel.Height = 40;
            _cancel.Location = new Point(456, 500);
            _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancel);

            _install = new AccentButton();
            _install.Text = "开始安装";
            _install.Icon = IconKind.Download;
            _install.Width = 130;
            _install.Height = 40;
            _install.Location = new Point(562, 500);
            _install.Click += delegate { Accept(); };
            Controls.Add(_install);

            Shown += delegate { LoadVersions(); };
            SetControlVisibility(false);
        }

        private void SetControlVisibility(bool enabled)
        {
            _install.Enabled = enabled;
            _loaderVersion.Enabled = enabled;
        }

        private void LoadVersions()
        {
            SetControlVisibility(false);
            _summary.Text = "正在读取版本清单...";
            Ui.Run(this, delegate
            {
                if (_main.Versions.Manifest == null) _main.Versions.LoadManifest(new ProgressSink(), false);
                Ui.Post(this, delegate
                {
                    _baseVersion.Items.Clear();
                    string selected = _prefillBase;
                    string latest = "";
                    for (int i = 0; i < _main.Versions.Manifest.Count; i++)
                    {
                        VersionEntry entry = _main.Versions.Manifest[i];
                        if (!entry.IsRelease && !_main.Config.ShowSnapshots) continue;
                        _baseVersion.Items.Add(entry);
                        if (string.IsNullOrEmpty(latest) && entry.IsRelease) latest = entry.Id;
                    }
                    int index = 0;
                    for (int i = 0; i < _baseVersion.Items.Count; i++)
                    {
                        VersionEntry entry = _baseVersion.Items[i] as VersionEntry;
                        if (entry != null && string.Equals(entry.Id, selected, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
                        if (string.IsNullOrEmpty(selected) && entry != null && string.Equals(entry.Id, latest, StringComparison.OrdinalIgnoreCase)) index = i;
                    }
                    _baseVersion.SelectedIndex = index;
                    ReloadLoaderVersions();
                });
            }, delegate(Exception ex)
            {
                _summary.Text = "版本清单加载失败: " + ex.Message;
                _install.Enabled = false;
            });
        }

        private void ReloadLoaderVersions()
        {
            _request++;
            int token = _request;
            VersionEntry baseEntry = _baseVersion.SelectedItem as VersionEntry;
            if (baseEntry == null) return;
            string mc = baseEntry.Id;
            int loaderIndex = _loader.SelectedIndex;
            string loader = LoaderKeys[loaderIndex];
            _loaderVersion.Items.Clear();
            if (loader == "vanilla")
            {
                _loaderVersion.Enabled = false;
                _summary.Text = "原版 " + mc;
                _hint.Text = "将安装 Minecraft " + mc + " 的完整客户端、依赖库与资源文件。\n安装完成后即可直接启动，之后也可以再安装 Fabric / Forge。";
                _install.Enabled = true;
                return;
            }
            _loaderVersion.Enabled = false;
            _install.Enabled = false;
            _summary.Text = "正在获取 " + LoaderNames[loaderIndex] + " 版本...";
            Ui.Run(this, delegate
            {
                List<LoaderBuild> builds = null;
                List<ForgeBuild> forgeBuilds = null;
                if (loader == "fabric") builds = FabricService.Loaders(_main.Config, mc);
                else if (loader == "quilt") builds = QuiltService.Loaders(_main.Config, mc);
                else if (loader == "forge") forgeBuilds = ForgeService.LoadVersions(_main.Config, mc);
                Ui.Post(this, delegate
                {
                    if (token != _request) return;
                    if (loader == "forge")
                    {
                        if (forgeBuilds == null || forgeBuilds.Count == 0)
                        {
                            _summary.Text = "没有找到 Forge " + mc + " 的版本";
                            _hint.Text = "Forge 可能尚未支持该 Minecraft 版本，请尝试其他版本或 Fabric。";
                            return;
                        }
                        for (int i = 0; i < forgeBuilds.Count; i++) _loaderVersion.Items.Add(forgeBuilds[i]);
                        _loaderVersion.SelectedIndex = 0;
                    }
                    else
                    {
                        if (builds == null || builds.Count == 0)
                        {
                            _summary.Text = "没有找到 " + LoaderNames[loaderIndex] + " 的版本";
                            return;
                        }
                        for (int i = 0; i < builds.Count; i++) _loaderVersion.Items.Add(builds[i]);
                        _loaderVersion.SelectedIndex = 0;
                    }
                    _loaderVersion.Enabled = true;
                    _install.Enabled = true;
                    UpdateSummary();
                });
            }, delegate(Exception ex)
            {
                if (token != _request) return;
                _summary.Text = "加载失败: " + ex.Message;
                _hint.Text = "请检查网络连接，或稍后重试。国内镜像不可用时会自动尝试官方源。";
            });
        }

        private void UpdateSummary()
        {
            VersionEntry baseEntry = _baseVersion.SelectedItem as VersionEntry;
            if (baseEntry == null) return;
            int loaderIndex = _loader.SelectedIndex;
            if (loaderIndex == 0)
            {
                _summary.Text = "原版 " + baseEntry.Id;
                return;
            }
            object selected = _loaderVersion.SelectedItem;
            string version = selected == null ? "" : (selected is LoaderBuild ? ((LoaderBuild)selected).Version : (selected is ForgeBuild ? ((ForgeBuild)selected).Version : selected.ToString()));
            _summary.Text = baseEntry.Id + "  ·  " + LoaderNames[loaderIndex] + " " + version;
            _hint.Text = loaderIndex == 1
                ? "Fabric 安装快速、兼容性好，适合大多数优化模组与轻量整合。\n安装时会自动下载 Fabric Loader、Intermediary 映射与基础库。"
                : (loaderIndex == 2
                    ? "Forge 拥有庞大的模组生态，适合大型整合包。\n安装器会执行映射合并与二进制补丁，首次安装可能需要几分钟。"
                    : "Quilt 兼容 Fabric 生态，同时提供更现代的加载器特性。\n安装时会自动下载 Quilt Loader 与依赖。");
        }

        private void Accept()
        {
            VersionEntry baseEntry = _baseVersion.SelectedItem as VersionEntry;
            if (baseEntry == null) return;
            int loaderIndex = _loader.SelectedIndex;
            string loader = LoaderKeys[loaderIndex];
            string loaderVersion = "";
            if (loader != "vanilla")
            {
                object selected = _loaderVersion.SelectedItem;
                if (selected == null)
                {
                    _summary.Text = "请选择加载器版本";
                    return;
                }
                if (selected is LoaderBuild) loaderVersion = ((LoaderBuild)selected).Version;
                else if (selected is ForgeBuild) loaderVersion = ((ForgeBuild)selected).Version;
            }
            Result = new InstallChoice();
            Result.Minecraft = baseEntry.Id;
            Result.Loader = loader;
            Result.LoaderVersion = loaderVersion;
            Result.DisplayName = loader == "vanilla" ? baseEntry.Id : (baseEntry.Id + " · " + LoaderNames[loaderIndex] + " " + loaderVersion);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
