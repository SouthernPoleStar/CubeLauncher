using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class CodeDisplay : Control
    {
        private string _code = "";
        public string Code
        {
            get { return _code; }
            set { _code = value ?? ""; Invalidate(); }
        }

        public CodeDisplay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 12, Theme.Card);
            Ui.StrokeRound(g, rect, 12, Theme.Alpha(Theme.Accent, 120), 1f);
            string spaced = "";
            for (int i = 0; i < _code.Length; i++)
            {
                if (i > 0) spaced += "  ";
                spaced += _code[i];
            }
            using (Font font = Theme.Mono(25f))
                Ui.Text(g, spaced, font, Theme.Accent, rect, TextAlign.CenterMiddle);
        }
    }

    public sealed class MicrosoftLoginDialog : DarkDialog
    {
        private readonly string _clientId;
        private readonly CodeDisplay _codeBox;
        private readonly Label _status;
        private readonly Label _hint;
        private readonly AccentButton _open;
        private readonly AccentButton _copy;
        private readonly AccentButton _cancel;
        private readonly System.Windows.Forms.Timer _spinnerTimer;
        private readonly CancellationTokenSource _cancelSource;
        private int _spinner;
        private MicrosoftDeviceCode _device;

        public Account ResultAccount;

        public MicrosoftLoginDialog(string clientId) : base("微软账户登录", "使用一次性设备代码安全登录，无需输入密码", 640, 452)
        {
            _clientId = string.IsNullOrEmpty(clientId) ? AuthService.DefaultMicrosoftClientId : clientId;
            _cancelSource = new CancellationTokenSource();

            Label step = new Label();
            step.AutoSize = false;
            step.BackColor = Color.Transparent;
            step.ForeColor = Theme.TextSoft;
            step.Font = Theme.Font(8.8f);
            step.TextAlign = ContentAlignment.MiddleLeft;
            step.SetBounds(28, 82, 584, 22);
            step.Text = "1. 点击「打开登录页面」  2. 输入下方代码  3. 选择微软账户并授权";
            Controls.Add(step);

            _codeBox = new CodeDisplay();
            _codeBox.SetBounds(28, 114, 584, 100);
            _codeBox.Code = "······";
            Controls.Add(_codeBox);

            _hint = new Label();
            _hint.AutoSize = false;
            _hint.BackColor = Color.Transparent;
            _hint.ForeColor = Theme.Muted;
            _hint.Font = Theme.Font(7.8f);
            _hint.TextAlign = ContentAlignment.MiddleCenter;
            _hint.SetBounds(28, 220, 584, 20);
            _hint.Text = "正在向微软申请设备代码...";
            Controls.Add(_hint);

            _status = new Label();
            _status.AutoSize = false;
            _status.BackColor = Color.Transparent;
            _status.ForeColor = Theme.Muted;
            _status.Font = Theme.Font(8.2f);
            _status.TextAlign = ContentAlignment.MiddleCenter;
            _status.SetBounds(28, 250, 584, 24);
            _status.Text = "准备中";
            Controls.Add(_status);

            _open = new AccentButton();
            _open.Text = "打开登录页面";
            _open.Icon = IconKind.External;
            _open.Width = 166;
            _open.Height = 40;
            _open.Location = new Point(28, 352);
            _open.Enabled = false;
            _open.Click += delegate
            {
                if (_device != null) Util.OpenUrl(_device.VerificationUri);
            };
            Controls.Add(_open);

            _copy = new AccentButton();
            _copy.Text = "复制代码";
            _copy.Style = ButtonStyle.Ghost;
            _copy.Icon = IconKind.Copy;
            _copy.Width = 120;
            _copy.Height = 40;
            _copy.Location = new Point(206, 352);
            _copy.Enabled = false;
            _copy.Click += delegate
            {
                if (_device == null) return;
                try { Clipboard.SetText(_device.UserCode); _status.Text = "代码已复制到剪贴板"; }
                catch { _status.Text = "复制失败，请手动记录代码"; }
            };
            Controls.Add(_copy);

            _cancel = new AccentButton();
            _cancel.Text = "取消";
            _cancel.Style = ButtonStyle.Ghost;
            _cancel.Width = 96;
            _cancel.Height = 40;
            _cancel.Location = new Point(516, 352);
            _cancel.Click += delegate { _cancelSource.Cancel(); Close(); };
            Controls.Add(_cancel);

            _spinnerTimer = new System.Windows.Forms.Timer();
            _spinnerTimer.Interval = 50;
            _spinnerTimer.Tick += delegate { _spinner = (_spinner + 10) % 360; Invalidate(); };
            _spinnerTimer.Start();

            Shown += delegate { RequestCode(); };
            FormClosing += delegate { _cancelSource.Cancel(); };
        }

        private void RequestCode()
        {
            _status.ForeColor = Theme.Muted;
            _status.Text = "正在向微软申请设备代码...";
            Ui.Run(this, delegate
            {
                MicrosoftDeviceCode code = AuthService.RequestMicrosoftDeviceCode(_clientId);
                Ui.Post(this, delegate
                {
                    _device = code;
                    _codeBox.Code = code.UserCode;
                    _hint.Text = "在浏览器打开的页面中输入此代码，然后完成微软账户登录";
                    _open.Enabled = true;
                    _copy.Enabled = true;
                    _status.Text = "等待浏览器授权...";
                    Util.OpenUrl(code.VerificationUri);
                    StartPolling();
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate
                {
                    _status.ForeColor = Theme.Red;
                    _status.Text = "无法启动登录: " + ex.Message;
                    _cancel.Text = "关闭";
                });
            });
        }

        private void StartPolling()
        {
            MicrosoftDeviceCode code = _device;
            ProgressSink progress = new ProgressSink();
            progress.Token = _cancelSource.Token;
            progress.Stage = delegate(string text)
            {
                Ui.Post(this, delegate { _status.Text = text; });
            };
            Ui.Run(this, delegate
            {
                Account account = AuthService.WaitMicrosoftDeviceLogin(code, _clientId, progress, _cancelSource.Token);
                Ui.Post(this, delegate
                {
                    ResultAccount = account;
                    DialogResult = DialogResult.OK;
                    Close();
                });
            }, delegate(Exception ex)
            {
                Ui.Post(this, delegate
                {
                    if (_cancelSource.IsCancellationRequested) return;
                    _status.ForeColor = Theme.Red;
                    _status.Text = ex.Message;
                    _cancel.Text = "关闭";
                });
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_device != null && ResultAccount == null)
                Ui.DrawSpinner(e.Graphics, new Rectangle(ClientSize.Width / 2 - 9, 284, 18, 18), Theme.Accent, _spinner);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _spinnerTimer.Stop();
                _spinnerTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
