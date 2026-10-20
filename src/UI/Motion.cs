using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CubeLauncher.UI
{
    public static class Motion
    {
        public static float EaseOutCubic(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t, float amplitude)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            float c1 = 1.70158f * (1f + amplitude);
            float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }

    /// <summary>
    /// A WinForms version of the sliding navigation highlight. It lives behind
    /// the NavItems and springs toward the selected row.
    /// </summary>
    public sealed class NavIndicator : Control
    {
        private readonly Timer _timer;
        private float _fromY;
        private float _toY;
        private float _progress;
        private int _duration = 380;

        public NavIndicator()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += delegate
            {
                _progress += 16f / Math.Max(1, _duration);
                if (_progress >= 1f)
                {
                    _progress = 1f;
                    _timer.Stop();
                }
                float eased = Motion.EaseOutBack(_progress, 0.28f);
                Top = (int)Math.Round(_fromY + (_toY - _fromY) * eased);
                Invalidate();
            };
        }

        public void MoveTo(Rectangle target, bool animate)
        {
            int oldTop = Top;
            Width = target.Width;
            Height = target.Height;
            Left = target.Left;
            if (!animate || oldTop <= 0)
            {
                _timer.Stop();
                Top = target.Top;
                Invalidate();
                return;
            }
            _fromY = oldTop;
            _toY = target.Top;
            _progress = 0f;
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
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Ui.FillRound(g, rect, 22, Theme.SecondaryContainer);
        }
    }
}
