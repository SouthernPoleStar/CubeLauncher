using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    internal static class InstanceNaming
    {
        private static readonly string[] Numerals = new string[]
        {
            "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十"
        };

        public static Dictionary<string, string> Build(IList<InstalledVersion> instances)
        {
            Dictionary<string, string> bases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (instances != null)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    InstalledVersion version = instances[i];
                    string name = (version.DisplayName ?? "").Trim();
                    if (string.IsNullOrEmpty(name)) name = version.Id;
                    bases[version.Id] = name;
                    int count;
                    counts.TryGetValue(name, out count);
                    counts[name] = count + 1;
                }
            }

            Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (instances == null) return labels;
            for (int i = 0; i < instances.Count; i++)
            {
                InstalledVersion version = instances[i];
                string name = bases[version.Id];
                if (counts[name] > 1)
                {
                    int number;
                    used.TryGetValue(name, out number);
                    number++;
                    used[name] = number;
                    labels[version.Id] = name + "（" + Numeral(number) + "）";
                }
                else
                {
                    labels[version.Id] = name;
                }
            }
            return labels;
        }

        private static string Numeral(int value)
        {
            if (value >= 0 && value < Numerals.Length) return Numerals[value];
            return value.ToString();
        }
    }

    /// <summary>
    /// The compact control on the home hero. It only navigates to the full
    /// instance page; it deliberately has no drop-down behaviour.
    /// </summary>
    public sealed class InstanceButton : Control
    {
        private readonly List<InstalledVersion> _instances = new List<InstalledVersion>();
        private Dictionary<string, string> _labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _selectedId = "";
        private bool _hover;

        public InstanceButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Font(9.2f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            Height = 40;
        }

        public void SetInstances(IList<InstalledVersion> instances, string selectedId)
        {
            _instances.Clear();
            if (instances != null) _instances.AddRange(instances);
            _labels = InstanceNaming.Build(_instances);
            _selectedId = selectedId ?? "";
            if (!Contains(_selectedId) && _instances.Count > 0) _selectedId = _instances[0].Id;
            Invalidate();
        }

        public void SetSelected(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _selectedId = id;
            Invalidate();
        }

        private bool Contains(string id)
        {
            for (int i = 0; i < _instances.Count; i++)
                if (string.Equals(_instances[i].Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private InstalledVersion Selected
        {
            get
            {
                for (int i = 0; i < _instances.Count; i++)
                    if (string.Equals(_instances[i].Id, _selectedId, StringComparison.OrdinalIgnoreCase)) return _instances[i];
                return _instances.Count > 0 ? _instances[0] : null;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color background = !Enabled ? Theme.Card : (_hover ? Theme.SurfaceContainerHighest : Theme.SurfaceContainerHigh);
            Ui.FillRound(g, rect, 16, background);
            Ui.StrokeRound(g, rect, 16, _hover ? Theme.Accent : Theme.OutlineVariant, 1f);

            InstalledVersion version = Selected;
            if (version == null)
            {
                Ui.Text(g, "尚未安装实例", Font, Theme.Faint, new Rectangle(16, 0, Width - 40, Height), TextAlign.LeftMiddle);
            }
            else
            {
                Ui.DrawLoaderBadge(g, version.Loader, new Rectangle(9, 6, 27, 27));
                string label = _labels.ContainsKey(version.Id) ? _labels[version.Id] : version.DisplayName;
                Ui.Text(g, label, Font, Enabled ? Theme.Text : Theme.Faint,
                    new Rectangle(44, 3, Width - 84, 20), TextAlign.LeftMiddle);
                string sub = version.LoaderLabel + " · " + version.Minecraft;
                if (!string.IsNullOrEmpty(version.LoaderVersion)) sub += " " + version.LoaderVersion;
                using (Font small = Theme.Font(7.3f))
                    Ui.Text(g, sub, small, Theme.Muted, new Rectangle(44, 20, Width - 78, 16), TextAlign.LeftMiddle);
            }

            using (Font hint = Theme.Font(7.2f, FontStyle.Bold))
                Ui.Text(g, "选择", hint, Theme.Accent, new Rectangle(Width - 70, 2, 38, Height), TextAlign.RightMiddle);
            Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(Width - 25, (Height - 14) / 2, 13, 14), Theme.Muted, 1.6f);
        }
    }

    /// <summary>
    /// Full second-level page for choosing the active instance. The left side
    /// groups by Minecraft version, the right side shows the loader instances.
    /// </summary>
    public sealed class GameSelectView : BaseView
    {
        private sealed class VersionGroup
        {
            public string Minecraft = "";
            public string Loaders = "";
            public readonly List<InstalledVersion> Items = new List<InstalledVersion>();
        }

        private readonly AccentButton _back;
        private readonly SmoothScrollPanel _groupList;
        private readonly SmoothScrollPanel _instanceList;
        private readonly List<VersionGroup> _groups = new List<VersionGroup>();
        private readonly List<GameGroupCard> _groupCards = new List<GameGroupCard>();
        private Dictionary<string, string> _labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int _activeGroup;

        public GameSelectView(MainForm main) : base(main)
        {
            _back = new AccentButton();
            _back.Text = "返回首页";
            _back.Icon = IconKind.ChevronLeft;
            _back.Style = ButtonStyle.Ghost;
            _back.Width = 126;
            _back.Height = 40;
            _back.Click += delegate { Main.CloseGameSelect(); };
            Controls.Add(_back);

            _groupList = new SmoothScrollPanel();
            Controls.Add(_groupList);

            _instanceList = new SmoothScrollPanel();
            Controls.Add(_instanceList);

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        public override void RefreshView()
        {
            string activeMinecraft = _activeGroup >= 0 && _activeGroup < _groups.Count ? _groups[_activeGroup].Minecraft : "";
            _labels = InstanceNaming.Build(Main.Installed);
            BuildGroups(activeMinecraft);
            RenderGroups();
            RenderInstances();
            Invalidate();
        }

        private void BuildGroups(string activeMinecraft)
        {
            _groups.Clear();
            Dictionary<string, VersionGroup> map = new Dictionary<string, VersionGroup>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Main.Installed.Count; i++)
            {
                InstalledVersion version = Main.Installed[i];
                string minecraft = string.IsNullOrEmpty(version.Minecraft) ? "未知版本" : version.Minecraft;
                VersionGroup group;
                if (!map.TryGetValue(minecraft, out group))
                {
                    group = new VersionGroup();
                    group.Minecraft = minecraft;
                    map[minecraft] = group;
                    _groups.Add(group);
                }
                group.Items.Add(version);
                if (group.Loaders.IndexOf(version.LoaderLabel, StringComparison.OrdinalIgnoreCase) < 0)
                    group.Loaders += (group.Loaders.Length == 0 ? "" : " / ") + version.LoaderLabel;
            }

            _groups.Sort(delegate(VersionGroup a, VersionGroup b)
            {
                return Util.CompareVersion(b.Minecraft, a.Minecraft);
            });

            _activeGroup = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                if (!string.IsNullOrEmpty(activeMinecraft) &&
                    string.Equals(_groups[i].Minecraft, activeMinecraft, StringComparison.OrdinalIgnoreCase))
                {
                    _activeGroup = i;
                    return;
                }
                if (string.Equals(_groups[i].Minecraft, Main.SelectedVersion == null ? "" : Main.SelectedVersion.Minecraft, StringComparison.OrdinalIgnoreCase))
                    _activeGroup = i;
            }
        }

        private void RenderGroups()
        {
            _groupList.Clear();
            _groupCards.Clear();
            if (_groups.Count == 0)
            {
                EmptyState empty = new EmptyState(IconKind.Box, "还没有安装游戏", "先去版本库安装一个版本，再回来选择。");
                empty.Height = 180;
                _groupList.AddControl(empty);
                return;
            }

            for (int i = 0; i < _groups.Count; i++)
            {
                VersionGroup group = _groups[i];
                GameGroupCard card = new GameGroupCard();
                card.Set(group.Minecraft, group.Items.Count, group.Loaders, i == _activeGroup);
                card.Height = 72;
                int index = i;
                card.Click += delegate
                {
                    _activeGroup = index;
                    for (int c = 0; c < _groupCards.Count; c++) _groupCards[c].Selected = c == _activeGroup;
                    RenderInstances();
                };
                _groupCards.Add(card);
                _groupList.AddControl(card);
            }
        }

        private void RenderInstances()
        {
            _instanceList.Clear();
            if (_activeGroup < 0 || _activeGroup >= _groups.Count) return;
            VersionGroup group = _groups[_activeGroup];
            if (group.Items.Count == 0)
            {
                EmptyState empty = new EmptyState(IconKind.Layers, "这个版本还没有实例", "安装加载器后，实例会出现在这里。");
                empty.Height = 180;
                _instanceList.AddControl(empty);
                return;
            }

            for (int i = 0; i < group.Items.Count; i++)
            {
                InstalledVersion version = group.Items[i];
                string label = _labels.ContainsKey(version.Id) ? _labels[version.Id] : version.DisplayName;
                GameInstanceCard card = new GameInstanceCard();
                card.Set(version, label, Main.IsGameRunning(version.Id),
                    string.Equals(version.Id, Main.SelectedVersionId, StringComparison.OrdinalIgnoreCase));
                card.Height = 100;
                card.Click += delegate
                {
                    Main.CloseGameSelect();
                    Main.SetSelectedVersion(version.Id);
                };
                _instanceList.AddControl(card);
            }
            _instanceList.ScrollToTop();
        }

        private void LayoutChildren()
        {
            int left = 28;
            int top = 94;
            int bottom = Math.Max(top + 100, ClientSize.Height - 24);
            _back.SetBounds(left, 24, 126, 40);
            _groupList.SetBounds(left, top, 252, bottom - top);
            int right = left + 252 + 18;
            _instanceList.SetBounds(right, top, Math.Max(300, ClientSize.Width - right - 28), bottom - top);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Ui.Text(g, "我的实例", Theme.Display(20f, FontStyle.Bold), Theme.Text,
                new Rectangle(170, 14, 360, 36), TextAlign.LeftMiddle);
            using (Font sub = Theme.Font(8.8f))
                Ui.Text(g, "按 Minecraft 版本分组；点击右侧实例即可切换当前游戏", sub, Theme.Muted,
                    new Rectangle(172, 48, 560, 20), TextAlign.LeftMiddle);

            string count = Main.Installed.Count + " 个实例";
            using (Font countFont = Theme.Font(8.2f, FontStyle.Bold))
            {
                Size size = TextRenderer.MeasureText(count, countFont);
                Rectangle badge = new Rectangle(ClientSize.Width - size.Width - 46, 28, size.Width + 24, 28);
                Ui.FillRound(g, badge, 14, Theme.Alpha(Theme.Accent, 16));
                Ui.Text(g, count, countFont, Theme.Accent, badge, TextAlign.CenterMiddle);
            }

            Ui.Text(g, "Minecraft 版本", Theme.Font(7.8f, FontStyle.Bold), Theme.Faint,
                new Rectangle(32, 76, 220, 16), TextAlign.LeftMiddle);
            Ui.Text(g, "实例", Theme.Font(7.8f, FontStyle.Bold), Theme.Faint,
                new Rectangle(298, 76, 120, 16), TextAlign.LeftMiddle);
        }
    }

    internal sealed class GameGroupCard : Control
    {
        private string _minecraft = "";
        private int _count;
        private string _loaders = "";
        private bool _selected;
        private bool _hover;

        public bool Selected
        {
            get { return _selected; }
            set { _selected = value; Invalidate(); }
        }

        public GameGroupCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 72;
        }

        public void Set(string minecraft, int count, string loaders, bool selected)
        {
            _minecraft = minecraft ?? "";
            _count = count;
            _loaders = loaders ?? "";
            _selected = selected;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color background = _selected ? Theme.SecondaryContainer : (_hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.FillRound(g, rect, 18, background);
            Ui.StrokeRound(g, rect, 18, _selected ? Theme.Accent : Theme.OutlineVariant, 1f);
            if (_selected) Ui.FillRound(g, new Rectangle(0, 18, 3, Height - 36), 2, Theme.Accent);

            Ui.DrawLoaderBadge(g, "vanilla", new Rectangle(14, 16, 40, 40));
            using (Font title = Theme.Font(10f, FontStyle.Bold))
                Ui.Text(g, _minecraft, title, Theme.Text, new Rectangle(66, 11, Width - 84, 24), TextAlign.LeftMiddle);
            using (Font small = Theme.Font(7.4f))
                Ui.Text(g, _count + " 个实例 · " + _loaders, small, Theme.Muted,
                    new Rectangle(66, 35, Width - 82, 18), TextAlign.LeftMiddle);
            Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(Width - 24, 29, 11, 12),
                _selected ? Theme.Accent : Theme.Faint, 1.4f);
        }
    }

    internal sealed class GameInstanceCard : Control
    {
        public InstalledVersion Version;
        private string _label = "";
        private bool _running;
        private bool _current;
        private bool _hover;

        public GameInstanceCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 100;
        }

        public void Set(InstalledVersion version, string label, bool running, bool current)
        {
            Version = version;
            _label = label ?? "";
            _running = running;
            _current = current;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color background = _current ? Theme.Alpha(Theme.Accent, 12) : (_hover ? Theme.CardHover : Theme.SurfaceContainerHigh);
            Ui.FillRound(g, rect, 22, background);
            Ui.StrokeRound(g, rect, 22, _current ? Theme.Alpha(Theme.Accent, 150) : Theme.OutlineVariant, 1f);

            Ui.DrawLoaderBadge(g, Version.Loader, new Rectangle(18, 23, 52, 52));
            using (Font title = Theme.Font(11f, FontStyle.Bold))
            {
                string label = Ui.Clip(g, _label, title, Math.Max(80, Width - 230));
                Ui.Text(g, label, title, Theme.Text, new Rectangle(88, 13, Width - 220, 28), TextAlign.LeftMiddle);
            }
            string meta = Version.LoaderLabel;
            if (!string.IsNullOrEmpty(Version.LoaderVersion)) meta += " " + Version.LoaderVersion;
            meta += " · Minecraft " + Version.Minecraft;
            using (Font small = Theme.Font(8.2f))
                Ui.Text(g, meta, small, Theme.TextSoft, new Rectangle(88, 42, Width - 200, 20), TextAlign.LeftMiddle);
            using (Font mono = Theme.Mono(7.2f))
                Ui.Text(g, Version.Id, mono, Theme.Faint, new Rectangle(88, 63, Width - 200, 18), TextAlign.LeftMiddle);

            if (_running)
            {
                DrawPill(g, "运行中", Theme.Red, Width - 112);
            }
            else if (_current)
            {
                DrawPill(g, "当前", Theme.Accent, Width - 96);
            }
            Ui.DrawIcon(g, IconKind.ChevronRight, new Rectangle(Width - 30, 43, 12, 14), Theme.Muted, 1.5f);
        }

        private void DrawPill(Graphics g, string text, Color color, int x)
        {
            using (Font font = Theme.Font(7.2f, FontStyle.Bold))
            {
                Size size = TextRenderer.MeasureText(text, font);
                Rectangle pill = new Rectangle(x, 12, size.Width + 18, 22);
                Ui.FillRound(g, pill, 11, Theme.Alpha(color, 20));
                Ui.Text(g, text, font, color, pill, TextAlign.CenterMiddle);
            }
        }
    }
}
