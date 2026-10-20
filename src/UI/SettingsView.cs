using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class SettingsView : BaseView
    {
        private readonly SmoothScrollPanel _scroll;
        private readonly AccentButton _save;
        private readonly PathInput _gameDir;
        private readonly PathInput _javaPath;
        private readonly AccentButton _gameBrowse;
        private readonly AccentButton _javaBrowse;
        private readonly AccentButton _detect;
        private readonly AccentButton _downloadJava;
        private readonly ModernSlider _memory;
        private readonly Label _memoryValue;
        private readonly LabeledInput _jvm;
        private readonly ToggleSwitch _close;
        private readonly ToggleSwitch _snapshots;
        private readonly ToggleSwitch _mirror;
        private readonly DarkComboBox _threads;
        private readonly LabeledInput _curseKey;
        private readonly SettingsGroup _gameGroup;
        private readonly SettingsGroup _javaGroup;
        private readonly SettingsGroup _perfGroup;
        private readonly SettingsGroup _downloadGroup;
        private readonly SettingsGroup _aboutGroup;
        private readonly Label _javaStatus;

        public SettingsView(MainForm main) : base(main)
        {
            _save = new AccentButton();
            _save.Text = "保存设置";
            _save.Icon = IconKind.Check;
            _save.Width = 130;
            _save.Click += delegate { SaveSettings(); };
            Controls.Add(_save);

            _scroll = new SmoothScrollPanel();
            Controls.Add(_scroll);

            _gameGroup = new SettingsGroup("游戏与实例", "所有版本、资源与依赖库都会保存在这里");
            _gameGroup.Height = 154;
            _scroll.AddControl(_gameGroup);

            _gameDir = new PathInput("游戏根目录", "存放 versions、libraries、assets");
            _gameGroup.Controls.Add(_gameDir);

            _gameBrowse = new AccentButton();
            _gameBrowse.Text = "浏览";
            _gameBrowse.Style = ButtonStyle.Ghost;
            _gameBrowse.Width = 80;
            _gameBrowse.Height = 36;
            _gameBrowse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "选择 Minecraft 游戏根目录";
                    if (Directory.Exists(_gameDir.Value)) dialog.SelectedPath = _gameDir.Value;
                    if (dialog.ShowDialog(this) == DialogResult.OK) _gameDir.Value = dialog.SelectedPath;
                }
            };
            _gameGroup.Controls.Add(_gameBrowse);

            _javaGroup = new SettingsGroup("Java 运行时", "启动器会自动选择匹配版本，缺少时从清华 TUNA 镜像下载");
            _javaGroup.Height = 196;
            _scroll.AddControl(_javaGroup);

            _javaPath = new PathInput("Java 路径", "留空则自动检测；也可以指定 java.exe");
            _javaGroup.Controls.Add(_javaPath);

            _javaBrowse = new AccentButton();
            _javaBrowse.Text = "浏览";
            _javaBrowse.Style = ButtonStyle.Ghost;
            _javaBrowse.Width = 80;
            _javaBrowse.Height = 36;
            _javaBrowse.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Java 可执行文件|java.exe|所有文件|*.*";
                    if (dialog.ShowDialog(this) == DialogResult.OK) _javaPath.Value = dialog.FileName;
                }
            };
            _javaGroup.Controls.Add(_javaBrowse);

            _detect = new AccentButton();
            _detect.Text = "重新检测";
            _detect.Style = ButtonStyle.Ghost;
            _detect.Icon = IconKind.Refresh;
            _detect.Width = 108;
            _detect.Height = 34;
            _detect.Click += delegate { Main.LoadJavaAsync(); Main.Toast("正在重新检测 Java..."); };
            _javaGroup.Controls.Add(_detect);

            _downloadJava = new AccentButton();
            _downloadJava.Text = "自动下载 Java";
            _downloadJava.Style = ButtonStyle.Subtle;
            _downloadJava.Icon = IconKind.Download;
            _downloadJava.Width = 144;
            _downloadJava.Height = 34;
            _downloadJava.Click += delegate { DownloadJava(); };
            _javaGroup.Controls.Add(_downloadJava);

            _javaStatus = new Label();
            _javaStatus.AutoSize = false;
            _javaStatus.BackColor = Color.Transparent;
            _javaStatus.ForeColor = Theme.Muted;
            _javaStatus.Font = Theme.Font(8f);
            _javaStatus.TextAlign = ContentAlignment.MiddleLeft;
            _javaGroup.Controls.Add(_javaStatus);

            _perfGroup = new SettingsGroup("性能与启动", "合理分配内存，避免卡顿或内存溢出");
            _perfGroup.Height = 260;
            _scroll.AddControl(_perfGroup);

            _memory = new ModernSlider();
            _memory.Minimum = 1024;
            _memory.Maximum = 32768;
            _memory.Value = 4096;
            _memory.ValueChanged += delegate { _memoryValue.Text = _memory.Value + " MB"; };
            _perfGroup.Controls.Add(_memory);

            _memoryValue = new Label();
            _memoryValue.AutoSize = false;
            _memoryValue.BackColor = Color.Transparent;
            _memoryValue.ForeColor = Theme.Accent;
            _memoryValue.Font = Theme.Display(11f, FontStyle.Bold);
            _memoryValue.TextAlign = ContentAlignment.MiddleRight;
            _memoryValue.Text = "4096 MB";
            _perfGroup.Controls.Add(_memoryValue);

            _jvm = new LabeledInput();
            _jvm.Caption = "额外 JVM 参数 (可选)";
            _jvm.Placeholder = "-XX:+UseG1GC -Dfoo=bar";
            _perfGroup.Controls.Add(_jvm);

            _close = new ToggleSwitch();
            _perfGroup.Controls.Add(_close);
            AddCaption(_perfGroup, "启动游戏后关闭启动器", 68, 192);
            _snapshots = new ToggleSwitch();
            _perfGroup.Controls.Add(_snapshots);
            AddCaption(_perfGroup, "在版本库显示快照", 348, 192);
            AddCaption(_perfGroup, "内存分配", 20, 58);

            _downloadGroup = new SettingsGroup("下载与镜像", "国内源优先，失败时回退官方；CurseForge 可填写 API Key");
            _downloadGroup.Height = 286;
            _scroll.AddControl(_downloadGroup);

            _mirror = new ToggleSwitch();
            _downloadGroup.Controls.Add(_mirror);
            AddCaption(_downloadGroup, "国内镜像优先 (BMCLAPI / MCIM / TUNA)", 68, 64);
            AddCaption(_downloadGroup, "并发下载线程", 20, 120);

            _threads = new DarkComboBox();
            _threads.SetItems(new object[] { "4 线程", "6 线程", "8 线程", "12 线程", "16 线程" });
            _downloadGroup.Controls.Add(_threads);

            _curseKey = new LabeledInput();
            _curseKey.Caption = "CurseForge API Key（可选，镜像不可用时使用官方 API）";
            _curseKey.Placeholder = "在 CurseForge 开发者控制台申请";
            _downloadGroup.Controls.Add(_curseKey);

            _aboutGroup = new SettingsGroup("关于与诊断", "方块启动器 " + App.Version + " · .NET Framework 4.0");
            _aboutGroup.Height = 160;
            _scroll.AddControl(_aboutGroup);

            AccentButton openLogs = new AccentButton();
            openLogs.Text = "打开日志";
            openLogs.Style = ButtonStyle.Ghost;
            openLogs.Width = 100;
            openLogs.Height = 34;
            openLogs.Click += delegate { Main.ShowLog(); };
            _aboutGroup.Controls.Add(openLogs);

            AccentButton openData = new AccentButton();
            openData.Text = "数据目录";
            openData.Style = ButtonStyle.Ghost;
            openData.Width = 100;
            openData.Height = 34;
            openData.Click += delegate { Util.OpenFolder(Main.Config.RootDir); };
            _aboutGroup.Controls.Add(openData);

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
            LoadSettings();
        }

        public override void OnShown()
        {
            base.OnShown();
            LoadSettings();
        }

        public override void RefreshView()
        {
            _javaStatus.Text = Main.Java == null ? "未检测到可用 Java，将在启动时自动下载。" : "已检测到 Java " + Main.Java.Major + " · " + Main.Java.Vendor + " · " + Main.Java.Path;
        }

        private void LayoutChildren()
        {
            _save.SetBounds(ClientSize.Width - 158, 18, 130, 40);
            _scroll.SetBounds(28, 74, ClientSize.Width - 56, Math.Max(100, ClientSize.Height - 98));
            int width = _scroll.Width - 4;
            _gameGroup.Width = width;
            _javaGroup.Width = width;
            _perfGroup.Width = width;
            _downloadGroup.Width = width;
            _aboutGroup.Width = width;

            _gameDir.SetBounds(20, 58, width - 130, 56);
            _gameBrowse.Location = new Point(width - 100, 68);

            _javaPath.SetBounds(20, 58, width - 130, 56);
            _javaBrowse.Location = new Point(width - 100, 68);
            _detect.Location = new Point(20, 128);
            _downloadJava.Location = new Point(138, 128);
            _javaStatus.SetBounds(300, 128, width - 320, 34);

            _memory.SetBounds(20, 84, width - 150, 32);
            _memoryValue.SetBounds(width - 122, 80, 100, 34);
            _jvm.SetBounds(20, 124, width - 40, 62);
            _close.Location = new Point(20, 200);
            _snapshots.Location = new Point(300, 200);

            _mirror.Location = new Point(20, 68);
            _threads.Location = new Point(20, 146);
            _threads.Width = 150;
            _curseKey.SetBounds(20, 198, width - 40, 62);

            openButtonsLayout(width);
        }

        private void AddCaption(Control parent, string text, int x, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.BackColor = Theme.Card;
            label.ForeColor = Theme.TextSoft;
            label.Font = Theme.Font(8.2f);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.SetBounds(x, y, 190, 22);
            parent.Controls.Add(label);
        }

        private void openButtonsLayout(int width)
        {
            int buttonX = 20;
            for (int i = 0; i < _aboutGroup.Controls.Count; i++)
            {
                Control control = _aboutGroup.Controls[i];
                if (control is AccentButton)
                {
                    control.Location = new Point(buttonX, 76);
                    buttonX += control.Width + 12;
                }
            }
        }

        private void LoadSettings()
        {
            _gameDir.Value = Main.Config.GameDir;
            _javaPath.Value = Main.Config.JavaPath;
            _memory.Value = Math.Max(1024, Math.Min(32768, Main.Config.MemoryMb));
            _memoryValue.Text = _memory.Value + " MB";
            _jvm.Value = Main.Config.JvmArgs;
            _close.Checked = Main.Config.CloseAfterLaunch;
            _snapshots.Checked = Main.Config.ShowSnapshots;
            _mirror.Checked = Main.Config.MirrorFirst;
            int threadIndex = 1;
            if (Main.Config.DownloadThreads <= 4) threadIndex = 0;
            else if (Main.Config.DownloadThreads <= 6) threadIndex = 1;
            else if (Main.Config.DownloadThreads <= 8) threadIndex = 2;
            else if (Main.Config.DownloadThreads <= 12) threadIndex = 3;
            else threadIndex = 4;
            _threads.SelectedIndex = threadIndex;
            _curseKey.Value = Main.Config.CurseForgeApiKey;
            RefreshView();
        }

        private void SaveSettings()
        {
            string gameDir = _gameDir.Value.Trim();
            if (string.IsNullOrEmpty(gameDir)) gameDir = Main.Config.GameDir;
            try
            {
                Main.Config.GameDir = gameDir;
                Main.Config.JavaPath = _javaPath.Value.Trim();
                Main.Config.MemoryMb = _memory.Value;
                Main.Config.JvmArgs = _jvm.Value.Trim();
                Main.Config.CloseAfterLaunch = _close.Checked;
                Main.Config.ShowSnapshots = _snapshots.Checked;
                Main.Config.MirrorFirst = _mirror.Checked;
                int[] threads = new int[] { 4, 6, 8, 12, 16 };
                Main.Config.DownloadThreads = threads[Math.Max(0, Math.Min(threads.Length - 1, _threads.SelectedIndex))];
                Main.Config.CurseForgeApiKey = _curseKey.Value.Trim();
                Util.EnsureDirectory(Main.Config.GameDir);
                Util.EnsureDirectory(Main.Config.VersionsDir);
                Util.EnsureDirectory(Main.Config.LibrariesDir);
                Util.EnsureDirectory(Main.Config.AssetsDir);
                App.Save();
                App.EnsureLauncherProfiles();
                Main.LoadInstalledPublic();
                Main.RefreshData();
                Main.LoadJavaAsync();
                Main.Toast("设置已保存");
            }
            catch (Exception ex)
            {
                Main.ToastError("保存失败: " + ex.Message);
            }
        }

        private void DownloadJava()
        {
            int major = 17;
            InstalledVersion version = Main.SelectedVersion;
            if (version != null)
            {
                try
                {
                    JMap json = JMap.Parse(File.ReadAllText(version.JsonPath, System.Text.Encoding.UTF8));
                    major = json.Map("javaVersion").Int("majorVersion", 17);
                }
                catch { }
            }
            using (ProgressDialog dialog = new ProgressDialog(this, "下载 Java " + major, "正在查询 TUNA 镜像..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    JavaInfo info = JavaManager.Ensure(Main.Config, major, progress);
                    Main.Java = info;
                };
                dialog.Run();
                if (dialog.Error != null) Main.ToastError("Java 下载失败: " + dialog.Error.Message);
                else
                {
                    Main.LoadJavaAsync();
                    Main.Toast("Java " + major + " 已就绪");
                }
            }
        }
    }

    public sealed class SettingsGroup : Control
    {
        private readonly string _title;
        private readonly string _subtitle;

        public SettingsGroup(string title, string subtitle)
        {
            _title = title;
            _subtitle = subtitle;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 14, Theme.Card);
            Ui.StrokeRound(g, rect, 14, Theme.BorderSoft, 1f);
            Ui.Text(g, _title, Theme.Font(10.2f, FontStyle.Bold), Theme.Text, new Rectangle(20, 12, Width - 40, 24), TextAlign.LeftMiddle);
            Ui.Text(g, _subtitle, Theme.Font(7.8f), Theme.Muted, new Rectangle(20, 33, Width - 40, 18), TextAlign.LeftMiddle);
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(pen, 20, 55, Width - 20, 55);
        }
    }

    public sealed class PathInput : Control
    {
        private readonly TextBox _input;
        private bool _focus;

        public string Caption = "";
        public string Hint = "";

        public PathInput(string caption, string hint)
        {
            Caption = caption;
            Hint = hint;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _input = new TextBox();
            _input.BorderStyle = BorderStyle.None;
            _input.BackColor = Theme.Bg2;
            _input.ForeColor = Theme.Text;
            _input.Font = Theme.Font(9f);
            _input.GotFocus += delegate { _focus = true; Invalidate(); };
            _input.LostFocus += delegate { _focus = false; Invalidate(); };
            Controls.Add(_input);
        }

        public string Value
        {
            get { return _input.Text; }
            set { _input.Text = value ?? ""; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _input.SetBounds(12, 8, Math.Max(10, Width - 24), _input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (!string.IsNullOrEmpty(Caption))
                Ui.Text(g, Caption, Theme.Font(7.8f), Theme.Muted, new Rectangle(0, 0, Width, 16), TextAlign.LeftMiddle);
            Rectangle rect = new Rectangle(0, 17, Width - 1, Height - 18);
            Ui.FillRound(g, rect, 8, Theme.Bg2);
            Ui.StrokeRound(g, rect, 8, _focus ? Theme.Alpha(Theme.Accent, 130) : Theme.BorderSoft, 1f);
        }
    }
}
