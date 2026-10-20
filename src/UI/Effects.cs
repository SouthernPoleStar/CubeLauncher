using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CubeLauncher.UI
{
    /// <summary>
    /// A lightweight page-change curtain. WinForms cannot fade arbitrary child
    /// controls, so a translucent gradient is faded over the new page instead.
    /// </summary>
    public sealed class PageTransition : Control
    {
        private readonly Timer _timer;
        private float _alpha;

        public PageTransition()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Visible = false;
            Enabled = false;
            _timer = new Timer();
            _timer.Interval = 15;
            _timer.Tick += delegate
            {
                _alpha -= 0.085f;
                if (_alpha <= 0f)
                {
                    _alpha = 0f;
                    _timer.Stop();
                    Visible = false;
                }
                Invalidate();
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Let clicks pass through the fading curtain; otherwise the first
                // click after navigation is swallowed by this overlay.
                cp.ExStyle |= 0x20; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        public void Play()
        {
            if (Parent == null) return;
            Bounds = new Rectangle(0, 0, Parent.ClientSize.Width, Parent.ClientSize.Height);
            _alpha = 0.86f;
            Visible = true;
            BringToFront();
            _timer.Stop();
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_alpha <= 0f) return;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int alpha = (int)(255 * Math.Max(0f, Math.Min(1f, _alpha)));
            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(alpha, Theme.Bg), Color.FromArgb((int)(alpha * 0.55f), Theme.Bg2), 90f))
                g.FillRectangle(brush, ClientRectangle);

            // A soft light band makes the transition feel like glass rather than a black flash.
            int bandHeight = Math.Max(80, Height / 5);
            Rectangle band = new Rectangle(0, (Height - bandHeight) / 2, Width, bandHeight);
            using (LinearGradientBrush light = new LinearGradientBrush(band,
                Color.Transparent, Theme.Alpha(Theme.Accent, (int)(18 * _alpha)), 90f))
                g.FillRectangle(light, band);
        }
    }
}
