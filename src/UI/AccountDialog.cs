using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class AccountDialog : DarkDialog
    {
        private readonly MainForm _main;
        private readonly SegmentedControl _method;
        private readonly Panel _microsoftPanel;
        private readonly Panel _yggdrasilPanel;
        private readonly Panel _offlinePanel;
        private readonly AccentButton _msLogin;
        private LabeledInput _ysServer;
        private LabeledInput _ysUser;
        private LabeledInput _ysPassword;
        private AccentButton _ysLogin;
        private LabeledInput _offlineName;
        private AccentButton _offlineCreate;
        private readonly Label _status;

        public Account ResultAccount;

        public AccountDialog(MainForm main) : base("添加账户", "选择一种登录方式，启动器不会保存你的密码", 700, 610)
        {
            _main = main;

            _method = new SegmentedControl();
            _method.Options = new string[] { "微软正版", "第三方皮肤站", "离线账户" };
            _method.SetBounds(28, 80, 644, 38);
            _method.SelectedIndexChanged += delegate { SwitchPanel(); };
            Controls.Add(_method);

            _microsoftPanel = MakePanel();
            _yggdrasilPanel = MakePanel();
            _offlinePanel = MakePanel();
            Controls.Add(_microsoftPanel);
            Controls.Add(_yggdrasilPanel);
            Controls.Add(_offlinePanel);

            Label msTitle = MakeLabel("使用微软账户登录", 20, 14, 500, Theme.Text, 11f, true);
            _microsoftPanel.Controls.Add(msTitle);
            Label msDesc = new Label();
            msDesc.Text = "使用微软官方设备代码登录：启动器会显示一个一次性代码，\n在浏览器打开微软页面并输入代码，即可安全授权。";
            msDesc.AutoSize = false;
            msDesc.ForeColor = Theme.Muted;
            msDesc.Font = Theme.Font(8.4f);
            msDesc.SetBounds(20, 42, 600, 46);
            _microsoftPanel.Controls.Add(msDesc);

            _msLogin = new AccentButton();
            _msLogin.Text = "开始微软登录";
            _msLogin.Icon = IconKind.External;
            _msLogin.Width = 180;
            _msLogin.Height = 42;
            _msLogin.Location = new Point(20, 110);
            _msLogin.Click += delegate { MicrosoftLogin(); };
            _microsoftPanel.Controls.Add(_msLogin);

            Label msNote = new Label();
            msNote.Text = "设备代码方式不需要输入密码，也不会在启动器中保存密码。\n如果微软页面提示地区或家庭限制，请按页面提示处理。";
            msNote.AutoSize = false;
            msNote.ForeColor = Theme.Faint;
            msNote.Font = Theme.Font(7.8f);
            msNote.SetBounds(20, 174, 600, 42);
            _microsoftPanel.Controls.Add(msNote);

            BuildYggdrasilPanel();
            BuildOfflinePanel();

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.Font(8f);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.SetBounds(28, 558, 644, 30);
            Controls.Add(_status);

            Shown += delegate { SwitchPanel(); };
        }

        private Panel MakePanel()
        {
            Panel panel = new Panel();
            panel.BackColor = Color.Transparent;
            panel.SetBounds(28, 136, 644, 410);
            return panel;
        }

        private void BuildYggdrasilPanel()
        {
            Label title = MakeLabel("连接皮肤站", 20, 14, 500, Theme.Text, 11f, true);
            _yggdrasilPanel.Controls.Add(title);
            Label desc = new Label();
            desc.Text = "兼容 authlib-injector / Yggdrasil API，支持 LittleSkin 等第三方皮肤站。\n启动时自动注入 authlib-injector，并加载你的皮肤。";
            desc.AutoSize = false;
            desc.ForeColor = Theme.Muted;
            desc.Font = Theme.Font(8.4f);
            desc.SetBounds(20, 42, 600, 42);
            _yggdrasilPanel.Controls.Add(desc);

            _ysServer = new LabeledInput();
            _ysServer.Caption = "认证服务器地址";
            _ysServer.Placeholder = "https://littleskin.cn/api/yggdrasil";
            _ysServer.Value = "https://littleskin.cn/api/yggdrasil";
            _ysServer.SetBounds(20, 92, 600, 62);
            _yggdrasilPanel.Controls.Add(_ysServer);

            _ysUser = new LabeledInput();
            _ysUser.Caption = "邮箱 / 用户名";
            _ysUser.Placeholder = "LittleSkin 登录邮箱";
            _ysUser.SetBounds(20, 160, 292, 62);
            _yggdrasilPanel.Controls.Add(_ysUser);

            _ysPassword = new LabeledInput();
            _ysPassword.Caption = "密码";
            _ysPassword.Password = true;
            _ysPassword.Placeholder = "不会保存密码";
            _ysPassword.SetBounds(328, 160, 292, 62);
            _yggdrasilPanel.Controls.Add(_ysPassword);

            _ysLogin = new AccentButton();
            _ysLogin.Text = "登录皮肤站";
            _ysLogin.Icon = IconKind.Shield;
            _ysLogin.Width = 160;
            _ysLogin.Height = 40;
            _ysLogin.Location = new Point(20, 238);
            _ysLogin.Click += delegate { YggdrasilLogin(); };
            _yggdrasilPanel.Controls.Add(_ysLogin);

            AccentButton register = new AccentButton();
            register.Text = "LittleSkin 注册";
            register.Style = ButtonStyle.Ghost;
            register.Width = 150;
            register.Height = 40;
            register.Location = new Point(192, 238);
            register.Click += delegate { Util.OpenUrl("https://littleskin.cn/auth/register"); };
            _yggdrasilPanel.Controls.Add(register);
        }

        private void BuildOfflinePanel()
        {
            Label title = MakeLabel("创建一个离线账户", 20, 14, 500, Theme.Text, 11f, true);
            _offlinePanel.Controls.Add(title);
            Label desc = new Label();
            desc.Text = "无需联网，也不验证密码。适合本地测试、局域网联机或没有正版的情况。\n用户名会用于生成离线 UUID。";
            desc.AutoSize = false;
            desc.ForeColor = Theme.Muted;
            desc.Font = Theme.Font(8.4f);
            desc.SetBounds(20, 42, 600, 42);
            _offlinePanel.Controls.Add(desc);

            _offlineName = new LabeledInput();
            _offlineName.Caption = "游戏内用户名";
            _offlineName.Placeholder = "3-16 位字母、数字或下划线";
            _offlineName.Value = "Steve";
            _offlineName.SetBounds(20, 104, 400, 62);
            _offlinePanel.Controls.Add(_offlineName);

            _offlineCreate = new AccentButton();
            _offlineCreate.Text = "创建离线账户";
            _offlineCreate.Icon = IconKind.User;
            _offlineCreate.Width = 160;
            _offlineCreate.Height = 40;
            _offlineCreate.Location = new Point(20, 184);
            _offlineCreate.Click += delegate { CreateOffline(); };
            _offlinePanel.Controls.Add(_offlineCreate);

            Label note = new Label();
            note.Text = "离线账户不能进入需要正版验证的服务器。";
            note.AutoSize = false;
            note.ForeColor = Theme.Gold;
            note.Font = Theme.Font(7.8f);
            note.SetBounds(20, 238, 500, 22);
            _offlinePanel.Controls.Add(note);
        }

        private void SwitchPanel()
        {
            _microsoftPanel.Visible = _method.SelectedIndex == 0;
            _yggdrasilPanel.Visible = _method.SelectedIndex == 1;
            _offlinePanel.Visible = _method.SelectedIndex == 2;
            _status.ForeColor = Theme.Muted;
            if (_method.SelectedIndex == 0) _status.Text = "支持正版账户，使用微软官方设备代码登录。";
            else if (_method.SelectedIndex == 1) _status.Text = "请先在皮肤站创建角色，然后使用邮箱和密码登录。";
            else _status.Text = "离线账户仅用于本地或离线服务器。";
        }

        private void MicrosoftLogin()
        {
            using (MicrosoftLoginDialog login = new MicrosoftLoginDialog(_main.Config.MicrosoftClientId))
            {
                if (login.ShowDialog(this) == DialogResult.OK && login.ResultAccount != null)
                    Finish(login.ResultAccount);
            }
        }

        private void YggdrasilLogin()
        {
            string server = _ysServer.Value.Trim();
            string user = _ysUser.Value.Trim();
            string password = _ysPassword.Value;
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                _status.ForeColor = Theme.Red;
                _status.Text = "请填写服务器地址、用户名和密码。";
                return;
            }
            Account account = null;
            using (ProgressDialog dialog = new ProgressDialog(this, "皮肤站登录", "正在验证账户..."))
            {
                dialog.Work = delegate(ProgressSink progress)
                {
                    account = AuthService.LoginYggdrasil(server, user, password, progress);
                };
                dialog.Run();
                if (dialog.Error != null)
                {
                    _status.ForeColor = Theme.Red;
                    _status.Text = "登录失败: " + dialog.Error.Message;
                    return;
                }
            }
            if (account != null) Finish(account);
        }

        private void CreateOffline()
        {
            string name = _offlineName.Value.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]{3,16}$"))
            {
                _status.ForeColor = Theme.Red;
                _status.Text = "用户名需要 3-16 位字母、数字或下划线。";
                return;
            }
            Finish(Account.CreateOffline(name));
        }

        private void Finish(Account account)
        {
            ResultAccount = account;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
