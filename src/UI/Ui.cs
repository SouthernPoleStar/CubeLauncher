using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public enum IconKind
    {
        Home, Layers, Puzzle, User, Settings, Play, Plus, Search, Close, Minimize, Maximize, Restore,
        Folder, Refresh, Download, Trash, Check, X, Globe, Java, Memory, Shield, Log, ChevronDown,
        ChevronRight, Edit, Box, Spark, Link, Info, Warning, Copy, External
    }

    public enum TextAlign
    {
        LeftTop, LeftMiddle, CenterTop, CenterMiddle, RightTop, RightMiddle
    }

    public static class Ui
    {
        public static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            return RoundRect(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        public static GraphicsPath RoundRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            float d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
        {
            using (GraphicsPath path = RoundRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        public static void FillRound(Graphics g, RectangleF rect, int radius, Color color)
        {
            using (GraphicsPath path = RoundRect(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
                g.FillPath(brush, path);
        }

        public static void FillRoundGradient(Graphics g, Rectangle rect, int radius, Color from, Color to, float angle)
        {
            using (GraphicsPath path = RoundRect(rect, radius))
            using (LinearGradientBrush brush = new LinearGradientBrush(rect, from, to, angle))
                g.FillPath(brush, path);
        }

        /// <summary>
        /// A translucent, slowly moving glass surface. It fakes frosted glass with
        /// layered alpha gradients, a soft top sheen and a travelling specular band.
        /// </summary>
        public static void FillLiquidGlass(Graphics g, Rectangle rect, int radius, float phase, Color tint)
        {
            if (rect.Width < 4 || rect.Height < 4) return;
            float wave = (float)Math.Sin(phase);
            Color top = Theme.Alpha(Theme.Mix(Theme.Card, tint, 0.20f), 232);
            Color mid = Theme.Alpha(Theme.Mix(Theme.Card, tint, 0.08f), 196);
            Color bottom = Theme.Alpha(Theme.Bg2, 222);
            using (GraphicsPath path = RoundRect(rect, radius))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, top, bottom, 90f + wave * 16f))
                {
                    ColorBlend blend = new ColorBlend();
                    blend.Colors = new Color[] { top, mid, Theme.Alpha(Theme.Mix(Theme.Card, tint, 0.16f), 205), bottom };
                    float p1 = 0.22f + wave * 0.04f;
                    float p2 = 0.58f - wave * 0.03f;
                    blend.Positions = new float[] { 0f, Math.Max(0.05f, p1), Math.Min(0.95f, p2), 1f };
                    brush.InterpolationColors = blend;
                    g.FillPath(brush, path);
                }

                GraphicsState state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                int sheenHeight = Math.Max(20, rect.Height / 3);
                Rectangle sheen = new Rectangle(rect.X - 12, rect.Y - sheenHeight / 2, rect.Width + 24, sheenHeight);
                using (LinearGradientBrush gloss = new LinearGradientBrush(sheen,
                    Theme.Alpha(Theme.Mix(tint, Color.White, 0.45f), 34), Color.Transparent, 90f))
                    g.FillRectangle(gloss, sheen);

                int bandWidth = Math.Max(90, rect.Width / 3);
                float travel = (float)((phase * 46.0) % (rect.Width + bandWidth * 2));
                int bandX = rect.X - bandWidth + (int)travel;
                Rectangle band = new Rectangle(bandX, rect.Y - 8, bandWidth, rect.Height + 16);
                using (LinearGradientBrush specular = new LinearGradientBrush(band,
                    Color.Transparent, Theme.Alpha(Color.White, 22), 24f))
                {
                    ColorBlend shine = new ColorBlend();
                    shine.Colors = new Color[] { Color.Transparent, Theme.Alpha(Color.White, 26), Theme.Alpha(tint, 20), Color.Transparent };
                    shine.Positions = new float[] { 0f, 0.36f, 0.62f, 1f };
                    specular.InterpolationColors = shine;
                    g.FillRectangle(specular, band);
                }
                g.Restore(state);

                using (Pen border = new Pen(Theme.Alpha(Theme.Mix(Theme.Border, tint, 0.45f), 150), 1f))
                    g.DrawPath(border, path);
                using (Pen inner = new Pen(Theme.Alpha(Color.White, 22), 1f))
                {
                    Rectangle innerRect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
                    using (GraphicsPath innerPath = RoundRect(innerRect, Math.Max(1, radius - 1)))
                        g.DrawPath(inner, innerPath);
                }
            }
        }

        public static void FillLiquidGlass(Graphics g, Rectangle rect, int radius, float phase)
        {
            FillLiquidGlass(g, rect, radius, phase, Theme.Accent);
        }

        public static void StrokeRound(Graphics g, Rectangle rect, int radius, Color color, float width)
        {
            using (GraphicsPath path = RoundRect(rect, radius))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, path);
        }

        public static void Glow(Graphics g, Rectangle rect, Color color, int steps)
        {
            for (int i = steps; i >= 1; i--)
            {
                int grow = i * 4;
                Rectangle glow = new Rectangle(rect.X - grow, rect.Y - grow, rect.Width + grow * 2, rect.Height + grow * 2);
                int alpha = Math.Max(1, 10 - i);
                using (GraphicsPath path = RoundRect(glow, 22 + grow / 2))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, color)))
                    g.FillPath(brush, path);
            }
        }

        public static void Text(Graphics g, string text, Font font, Color color, Rectangle bounds, TextAlign align)
        {
            Text(g, text, font, color, bounds, align, false);
        }

        public static void Text(Graphics g, string text, Font font, Color color, Rectangle bounds, TextAlign align, bool wrap)
        {
            if (text == null) text = "";
            using (StringFormat format = new StringFormat())
            {
                switch (align)
                {
                    case TextAlign.LeftTop:
                        format.Alignment = StringAlignment.Near; format.LineAlignment = StringAlignment.Near; break;
                    case TextAlign.LeftMiddle:
                        format.Alignment = StringAlignment.Near; format.LineAlignment = StringAlignment.Center; break;
                    case TextAlign.CenterTop:
                        format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Near; break;
                    case TextAlign.CenterMiddle:
                        format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center; break;
                    case TextAlign.RightTop:
                        format.Alignment = StringAlignment.Far; format.LineAlignment = StringAlignment.Near; break;
                    case TextAlign.RightMiddle:
                        format.Alignment = StringAlignment.Far; format.LineAlignment = StringAlignment.Center; break;
                }
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                using (SolidBrush brush = new SolidBrush(color))
                    g.DrawString(text, font, brush, bounds, format);
            }
        }

        public static string Clip(Graphics g, string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width <= width)
                return text;
            string value = text;
            while (value.Length > 1)
            {
                value = value.Substring(0, value.Length - 1);
                if (TextRenderer.MeasureText(g, value + "…", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width <= width)
                    return value + "…";
            }
            return "…";
        }

        public static void DrawAvatar(Graphics g, Rectangle bounds, string name, Color accent, bool selected)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Theme.Mix(accent, Color.Black, 0.25), Theme.Mix(accent, Color.Black, 0.62), 45f))
                    g.FillPath(brush, path);
                if (selected)
                {
                    using (Pen pen = new Pen(Theme.Accent, 2f))
                    using (GraphicsPath outer = new GraphicsPath())
                    {
                        Rectangle outerRect = new Rectangle(bounds.X - 3, bounds.Y - 3, bounds.Width + 6, bounds.Height + 6);
                        outer.AddEllipse(outerRect);
                        g.DrawPath(pen, outer);
                    }
                }
                string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                using (Font font = Theme.Display(bounds.Height * 0.42f, FontStyle.Bold))
                {
                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    using (SolidBrush brush = new SolidBrush(Color.White))
                        g.DrawString(initial, font, brush, bounds, format);
                }
            }
        }

        public static void DrawIcon(Graphics g, IconKind kind, Rectangle bounds, Color color, float width)
        {
            GraphicsState state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(bounds.X, bounds.Y);
            g.ScaleTransform(bounds.Width / 24f, bounds.Height / 24f);
            using (Pen pen = new Pen(color, width))
            using (SolidBrush brush = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (kind)
                {
                    case IconKind.Home:
                        g.DrawLines(pen, new PointF[] { new PointF(3, 11), new PointF(12, 3.5f), new PointF(21, 11) });
                        g.DrawLine(pen, 5.5f, 10, 5.5f, 20);
                        g.DrawLine(pen, 18.5f, 10, 18.5f, 20);
                        g.DrawLine(pen, 5.5f, 20, 18.5f, 20);
                        g.DrawLine(pen, 9.5f, 20, 9.5f, 14.5f);
                        g.DrawLine(pen, 14.5f, 20, 14.5f, 14.5f);
                        g.DrawLine(pen, 9.5f, 14.5f, 14.5f, 14.5f);
                        break;
                    case IconKind.Layers:
                        g.DrawPolygon(pen, new PointF[] { new PointF(12, 3), new PointF(21, 8), new PointF(12, 13), new PointF(3, 8) });
                        g.DrawLines(pen, new PointF[] { new PointF(3, 12), new PointF(12, 17), new PointF(21, 12) });
                        g.DrawLines(pen, new PointF[] { new PointF(3, 16), new PointF(12, 21), new PointF(21, 16) });
                        break;
                    case IconKind.Puzzle:
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddLine(new PointF(4, 9), new PointF(8, 9));
                            path.AddBezier(8, 9, 7, 5, 9, 3, 12, 4);
                            path.AddBezier(12, 4, 15, 3, 17, 5, 16, 9);
                            path.AddLine(new PointF(20, 9), new PointF(20, 13));
                            path.AddBezier(20, 13, 24, 12, 24, 17, 20, 17);
                            path.AddLine(new PointF(20, 20), new PointF(4, 20));
                            path.AddLine(new PointF(4, 20), new PointF(4, 16));
                            path.AddBezier(4, 16, 8, 17, 9, 13, 4, 13);
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                        }
                        break;
                    case IconKind.User:
                        g.DrawEllipse(pen, 8, 3, 8, 8);
                        g.DrawArc(pen, 4, 13, 16, 11, 180, 180);
                        break;
                    case IconKind.Settings:
                        g.DrawEllipse(pen, 8.5f, 8.5f, 7, 7);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = Math.PI * 2 * i / 8;
                            float x1 = 12 + (float)Math.Cos(a) * 7.2f;
                            float y1 = 12 + (float)Math.Sin(a) * 7.2f;
                            float x2 = 12 + (float)Math.Cos(a) * 9.8f;
                            float y2 = 12 + (float)Math.Sin(a) * 9.8f;
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;
                    case IconKind.Play:
                        g.FillPolygon(brush, new PointF[] { new PointF(8, 5), new PointF(19, 12), new PointF(8, 19) });
                        break;
                    case IconKind.Plus:
                        g.DrawLine(pen, 12, 5, 12, 19);
                        g.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case IconKind.Search:
                        g.DrawEllipse(pen, 4, 4, 12, 12);
                        g.DrawLine(pen, 15, 15, 20, 20);
                        break;
                    case IconKind.Close:
                    case IconKind.X:
                        g.DrawLine(pen, 6, 6, 18, 18);
                        g.DrawLine(pen, 18, 6, 6, 18);
                        break;
                    case IconKind.Minimize:
                        g.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case IconKind.Maximize:
                        g.DrawRectangle(pen, 5, 5, 14, 14);
                        break;
                    case IconKind.Restore:
                        g.DrawRectangle(pen, 4, 8, 11, 11);
                        g.DrawLines(pen, new PointF[] { new PointF(8, 8), new PointF(8, 4), new PointF(19, 4), new PointF(19, 15), new PointF(15, 15) });
                        break;
                    case IconKind.Folder:
                        g.DrawLines(pen, new PointF[] { new PointF(3, 7), new PointF(3, 20), new PointF(21, 20), new PointF(21, 8), new PointF(12, 8), new PointF(10, 5), new PointF(3, 5), new PointF(3, 7) });
                        break;
                    case IconKind.Refresh:
                        g.DrawArc(pen, 4, 4, 16, 16, -40, 280);
                        g.DrawLines(pen, new PointF[] { new PointF(19, 3), new PointF(20, 9), new PointF(14, 8) });
                        break;
                    case IconKind.Download:
                        g.DrawLine(pen, 12, 3, 12, 15);
                        g.DrawLines(pen, new PointF[] { new PointF(7, 10), new PointF(12, 15), new PointF(17, 10) });
                        g.DrawLines(pen, new PointF[] { new PointF(4, 17), new PointF(4, 21), new PointF(20, 21), new PointF(20, 17) });
                        break;
                    case IconKind.Trash:
                        g.DrawLine(pen, 4, 6, 20, 6);
                        g.DrawLine(pen, 9, 3, 15, 3);
                        g.DrawLine(pen, 6, 6, 7, 21);
                        g.DrawLine(pen, 18, 6, 17, 21);
                        g.DrawLine(pen, 7, 21, 17, 21);
                        g.DrawLine(pen, 10, 10, 10, 17);
                        g.DrawLine(pen, 14, 10, 14, 17);
                        break;
                    case IconKind.Check:
                        g.DrawLines(pen, new PointF[] { new PointF(4, 12), new PointF(9, 18), new PointF(20, 6) });
                        break;
                    case IconKind.Globe:
                        g.DrawEllipse(pen, 3, 3, 18, 18);
                        g.DrawEllipse(pen, 8, 3, 8, 18);
                        g.DrawLine(pen, 3, 12, 21, 12);
                        break;
                    case IconKind.Java:
                        g.DrawLines(pen, new PointF[] { new PointF(5, 10), new PointF(5, 16), new PointF(8, 19), new PointF(16, 19), new PointF(19, 16), new PointF(19, 10) });
                        g.DrawArc(pen, 8, 12, 8, 7, 0, 180);
                        g.DrawLine(pen, 21, 9, 21, 15);
                        g.DrawLines(pen, new PointF[] { new PointF(9, 3), new PointF(8, 5), new PointF(10, 7), new PointF(9, 9) });
                        g.DrawLines(pen, new PointF[] { new PointF(14, 3), new PointF(13, 5), new PointF(15, 7), new PointF(14, 9) });
                        break;
                    case IconKind.Memory:
                        g.DrawRectangle(pen, 5, 6, 14, 12);
                        g.DrawRectangle(pen, 8, 9, 8, 6);
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawLine(pen, 8 + i * 4, 2, 8 + i * 4, 6);
                            g.DrawLine(pen, 8 + i * 4, 18, 8 + i * 4, 22);
                        }
                        g.DrawLine(pen, 2, 10, 5, 10);
                        g.DrawLine(pen, 2, 14, 5, 14);
                        g.DrawLine(pen, 19, 10, 22, 10);
                        g.DrawLine(pen, 19, 14, 22, 14);
                        break;
                    case IconKind.Shield:
                        g.DrawLines(pen, new PointF[] { new PointF(12, 3), new PointF(20, 6), new PointF(20, 12), new PointF(12, 21), new PointF(4, 12), new PointF(4, 6), new PointF(12, 3) });
                        g.DrawLines(pen, new PointF[] { new PointF(8, 12), new PointF(11, 15), new PointF(16, 9) });
                        break;
                    case IconKind.Log:
                        g.DrawRectangle(pen, 4, 4, 16, 16);
                        g.DrawLine(pen, 8, 9, 16, 9);
                        g.DrawLine(pen, 8, 12, 14, 12);
                        g.DrawLine(pen, 8, 15, 12, 15);
                        break;
                    case IconKind.ChevronDown:
                        g.DrawLines(pen, new PointF[] { new PointF(6, 9), new PointF(12, 15), new PointF(18, 9) });
                        break;
                    case IconKind.ChevronRight:
                        g.DrawLines(pen, new PointF[] { new PointF(9, 6), new PointF(15, 12), new PointF(9, 18) });
                        break;
                    case IconKind.Edit:
                        g.DrawLines(pen, new PointF[] { new PointF(4, 20), new PointF(5, 15), new PointF(16, 4), new PointF(20, 8), new PointF(9, 19), new PointF(4, 20) });
                        break;
                    case IconKind.Box:
                        g.DrawPolygon(pen, new PointF[] { new PointF(12, 3), new PointF(21, 8), new PointF(21, 16), new PointF(12, 21), new PointF(3, 16), new PointF(3, 8) });
                        g.DrawLine(pen, 3, 8, 12, 13);
                        g.DrawLine(pen, 21, 8, 12, 13);
                        g.DrawLine(pen, 12, 13, 12, 21);
                        break;
                    case IconKind.Spark:
                        g.DrawLine(pen, 12, 2, 12, 8);
                        g.DrawLine(pen, 12, 16, 12, 22);
                        g.DrawLine(pen, 2, 12, 8, 12);
                        g.DrawLine(pen, 16, 12, 22, 12);
                        g.DrawLine(pen, 5, 5, 9, 9);
                        g.DrawLine(pen, 15, 15, 19, 19);
                        g.DrawLine(pen, 19, 5, 15, 9);
                        g.DrawLine(pen, 9, 15, 5, 19);
                        break;
                    case IconKind.Link:
                        g.DrawArc(pen, 3, 8, 12, 10, 40, 280);
                        g.DrawArc(pen, 9, 6, 12, 10, 220, 280);
                        break;
                    case IconKind.Info:
                        g.DrawEllipse(pen, 3, 3, 18, 18);
                        g.DrawLine(pen, 12, 10, 12, 17);
                        g.FillEllipse(brush, 11, 6.5f, 2, 2);
                        break;
                    case IconKind.Warning:
                        g.DrawPolygon(pen, new PointF[] { new PointF(12, 3), new PointF(22, 20), new PointF(2, 20) });
                        g.DrawLine(pen, 12, 9, 12, 14);
                        g.FillEllipse(brush, 11, 16.5f, 2, 2);
                        break;
                    case IconKind.Copy:
                        g.DrawRectangle(pen, 8, 8, 12, 12);
                        g.DrawLines(pen, new PointF[] { new PointF(16, 8), new PointF(16, 4), new PointF(4, 4), new PointF(4, 16), new PointF(8, 16) });
                        break;
                    case IconKind.External:
                        g.DrawLines(pen, new PointF[] { new PointF(14, 3), new PointF(21, 3), new PointF(21, 10) });
                        g.DrawLine(pen, 21, 3, 11, 13);
                        g.DrawLines(pen, new PointF[] { new PointF(18, 14), new PointF(18, 21), new PointF(3, 21), new PointF(3, 6), new PointF(10, 6) });
                        break;
                }
            }
            g.Restore(state);
        }

        public static void DrawCheckeredPattern(Graphics g, Rectangle bounds, Color color, int size)
        {
            using (SolidBrush brush = new SolidBrush(color))
            {
                for (int y = 0; y < bounds.Height; y += size)
                {
                    for (int x = 0; x < bounds.Width; x += size)
                    {
                        if (((x / size) + (y / size)) % 2 == 0) g.FillRectangle(brush, bounds.X + x, bounds.Y + y, size, size);
                    }
                }
            }
        }

        public static void DrawSpinner(Graphics g, Rectangle bounds, Color color, float angle)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(color, 2.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, bounds, angle, 260);
            }
        }

        public static void Post(Control control, Action action)
        {
            if (control == null || action == null) return;
            try
            {
                if (control.IsDisposed || !control.IsHandleCreated) return;
                if (control.InvokeRequired) control.BeginInvoke(action);
                else action();
            }
            catch (Exception ex)
            {
                Log.Warn("界面回调失败: " + ex.Message);
            }
        }

        public static void Run(Control owner, Action work, Action<Exception> error)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try { work(); }
                catch (Exception ex)
                {
                    if (error != null) Post(owner, delegate { error(ex); });
                    else Log.Error("后台任务失败", ex);
                }
            });
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return fallback;
                return ColorTranslator.FromHtml(hex);
            }
            catch { return fallback; }
        }

        public static void DrawProgressBar(Graphics g, Rectangle bounds, double value, Color fill, Color track)
        {
            FillRound(g, bounds, bounds.Height / 2, track);
            int width = (int)(bounds.Width * Math.Max(0, Math.Min(1, value)));
            if (width > 0)
            {
                Rectangle bar = new Rectangle(bounds.X, bounds.Y, Math.Max(bounds.Height, width), bounds.Height);
                FillRound(g, bar, bounds.Height / 2, fill);
            }
        }
    }

    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Bg;
        }
    }

    public class BufferedUserControl : UserControl
    {
        public BufferedUserControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
    }
}
