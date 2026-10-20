using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class AccountsView : BaseView
    {
        private readonly SmoothScrollPanel _accounts;
        private readonly AccentButton _add;
        private readonly AccountHelpCard _help;

        public AccountsView(MainForm main) : base(main)
        {
            _add = new AccentButton();
            _add.Text = "添加账户";
            _add.Icon = IconKind.Plus;
            _add.Width = 130;
            _add.Click += delegate { ShowAddAccount(); };
            Controls.Add(_add);

            _accounts = new SmoothScrollPanel();
            Controls.Add(_accounts);

            _help = new AccountHelpCard(main);
            Controls.Add(_help);

            Resize += delegate { LayoutChildren(); };
            LayoutChildren();
        }

        public override void OnShown()
        {
            base.OnShown();
        }

        public override void RefreshView()
        {
            BuildAccounts();
            _help.Invalidate();
        }

        private void LayoutChildren()
        {
            _add.SetBounds(ClientSize.Width - 158, 18, 130, 40);
            int top = 76;
            int left = 28;
            int width = ClientSize.Width - 56;
            int listWidth = (int)(width * 0.62);
            _accounts.SetBounds(left, top, listWidth, Math.Max(100, ClientSize.Height - top - 24));
            _help.SetBounds(left + listWidth + 18, top, width - listWidth - 18, Math.Min(320, Math.Max(220, ClientSize.Height - top - 24)));
        }

        private void BuildAccounts()
        {
            _accounts.Clear();
            if (Main.Config.Accounts.Count == 0)
            {
                EmptyState empty = new EmptyState(IconKind.User, "还没有账户", "添加微软正版、第三方皮肤站或离线账户。");
                empty.Width = _accounts.Width - 4;
                _accounts.AddControl(empty);
                return;
            }
            for (int i = 0; i < Main.Config.Accounts.Count; i++)
            {
                Account account = Main.Config.Accounts[i];
                AccountCard card = new AccountCard(Main, account, string.Equals(account.Id, Main.Config.SelectedAccountId, StringComparison.OrdinalIgnoreCase));
                card.Width = _accounts.Width - 4;
                card.SelectRequested += delegate(object sender, EventArgs e)
                {
                    AccountCard source = (AccountCard)sender;
                    Main.SelectAccount(source.Account.Id);
                    BuildAccounts();
                };
                card.DeleteRequested += delegate(object sender, EventArgs e)
                {
                    AccountCard source = (AccountCard)sender;
                    if (Main.Confirm("删除账户", "确定删除账户 " + source.Account.Name + " 吗？", "删除"))
                    {
                        Main.Config.Accounts.Remove(source.Account);
                        if (string.Equals(Main.Config.SelectedAccountId, source.Account.Id, StringComparison.OrdinalIgnoreCase))
                            Main.Config.SelectedAccountId = Main.Config.Accounts.Count > 0 ? Main.Config.Accounts[0].Id : "";
                        App.Save();
                        Main.RefreshData();
                        BuildAccounts();
                    }
                };
                _accounts.AddControl(card);
            }
            _accounts.UpdateScroll();
        }

        private void ShowAddAccount()
        {
            using (AccountDialog dialog = new AccountDialog(Main))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.ResultAccount != null)
                {
                    Main.Config.Accounts.Add(dialog.ResultAccount);
                    Main.Config.SelectedAccountId = dialog.ResultAccount.Id;
                    App.Save();
                    Main.RefreshData();
                    BuildAccounts();
                    _help.Invalidate();
                    Main.Toast("账户已添加: " + dialog.ResultAccount.Name);
                }
            }
        }
    }

    public sealed class AccountCard : Control
    {
        private readonly AccentButton _select;
        private readonly IconButton _delete;
        private bool _selected;
        public Account Account;
        public event EventHandler SelectRequested;
        public event EventHandler DeleteRequested;

        public AccountCard(MainForm main, Account account, bool selected)
        {
            Account = account;
            _selected = selected;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Height = 92;
            Cursor = Cursors.Hand;

            _select = new AccentButton();
            _select.Text = selected ? "当前账户" : "设为默认";
            _select.Style = selected ? ButtonStyle.Subtle : ButtonStyle.Ghost;
            _select.Width = 92;
            _select.Height = 30;
            _select.Enabled = !selected;
            _select.Click += delegate { EventHandler h = SelectRequested; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_select);

            _delete = new IconButton();
            _delete.Icon = IconKind.Trash;
            _delete.IconSize = 15;
            _delete.Size = new Size(30, 30);
            _delete.Click += delegate { EventHandler h = DeleteRequested; if (h != null) h(this, EventArgs.Empty); };
            Controls.Add(_delete);

            Resize += delegate { LayoutButtons(); };
            LayoutButtons();
        }

        private void LayoutButtons()
        {
            _select.Location = new Point(Width - 138, (Height - 30) / 2);
            _delete.Location = new Point(Width - 42, (Height - 30) / 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X < Width - 160)
            {
                EventHandler handler = SelectRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 13, _selected ? Theme.Alpha(Theme.Accent, 10) : Theme.Card);
            Ui.StrokeRound(g, rect, 13, _selected ? Theme.Alpha(Theme.Accent, 90) : Theme.BorderSoft, 1f);
            Color accent = Account.IsMicrosoft ? Theme.Cyan : (Account.IsYggdrasil ? Theme.Purple : Theme.Gold);
            Ui.DrawAvatar(g, new Rectangle(18, 22, 48, 48), Account.Name, accent, _selected);
            Ui.Text(g, Account.Name, Theme.Font(10.5f, FontStyle.Bold), Theme.Text, new Rectangle(82, 18, Width - 250, 24), TextAlign.LeftMiddle);
            Ui.Text(g, Account.TypeLabel + (Account.IsYggdrasil && !string.IsNullOrEmpty(Account.AuthServer) ? " · " + ShortHost(Account.AuthServer) : ""),
                Theme.Font(8f), Theme.Muted, new Rectangle(82, 42, Width - 250, 18), TextAlign.LeftMiddle);
            Ui.Text(g, Account.CleanUuid, Theme.Mono(7.3f), Theme.Faint, new Rectangle(82, 61, Width - 250, 16), TextAlign.LeftMiddle);
        }

        private static string ShortHost(string url)
        {
            try { return new Uri(url).Host; }
            catch { return url; }
        }
    }

    public sealed class AccountHelpCard : Control
    {
        private readonly MainForm _main;

        public AccountHelpCard(MainForm main)
        {
            _main = main;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int linkY = 252;
            if (e.Y >= linkY && e.Y <= linkY + 28)
                Util.OpenUrl("https://littleskin.cn");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 14, Theme.Card);
            Ui.StrokeRound(g, rect, 14, Theme.BorderSoft, 1f);
            Ui.Text(g, "账户类型", Theme.Font(10.5f, FontStyle.Bold), Theme.Text, new Rectangle(18, 14, Width - 36, 26), TextAlign.LeftMiddle);
            Ui.Text(g, "三种方式，覆盖所有玩法", Theme.Font(7.8f), Theme.Muted, new Rectangle(18, 38, Width - 36, 20), TextAlign.LeftMiddle);

            DrawType(g, 72, IconKind.Shield, "微软正版", "使用微软账户登录，支持正版皮肤与披风。", Theme.Cyan);
            DrawType(g, 138, IconKind.Globe, "皮肤站", "LittleSkin 等 Yggdrasil 站点，自动注入皮肤。", Theme.Purple);
            DrawType(g, 204, IconKind.User, "离线账户", "无需联网，输入用户名即可开始游戏。", Theme.Gold);

            Rectangle link = new Rectangle(18, 252, Width - 36, 28);
            Ui.FillRound(g, link, 8, Theme.Alpha(Theme.Accent, 12));
            Ui.Text(g, "打开 LittleSkin 注册 →", Theme.Font(8.2f, FontStyle.Bold), Theme.Accent, link, TextAlign.CenterMiddle);
        }

        private void DrawType(Graphics g, int y, IconKind icon, string title, string text, Color color)
        {
            Ui.FillRound(g, new Rectangle(18, y, 30, 30), 8, Theme.Alpha(color, 18));
            Ui.DrawIcon(g, icon, new Rectangle(25, y + 7, 16, 16), color, 1.5f);
            Ui.Text(g, title, Theme.Font(8.6f, FontStyle.Bold), Theme.Text, new Rectangle(58, y, Width - 76, 18), TextAlign.LeftMiddle);
            Ui.Text(g, text, Theme.Font(7.5f), Theme.Muted, new Rectangle(58, y + 16, Width - 76, 18), TextAlign.LeftMiddle);
        }
    }
}
