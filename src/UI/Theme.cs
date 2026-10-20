using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CubeLauncher.UI
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(20, 18, 24);
        public static readonly Color Bg2 = Color.FromArgb(29, 27, 32);
        public static readonly Color Sidebar = Color.FromArgb(29, 27, 32);
        public static readonly Color Panel = Color.FromArgb(33, 31, 38);
        public static readonly Color Card = Color.FromArgb(33, 31, 38);
        public static readonly Color CardHover = Color.FromArgb(43, 41, 48);
        public static readonly Color CardActive = Color.FromArgb(54, 52, 59);
        public static readonly Color Border = Color.FromArgb(73, 69, 79);
        public static readonly Color BorderSoft = Color.FromArgb(51, 45, 65);
        public static readonly Color Text = Color.FromArgb(230, 225, 229);
        public static readonly Color TextSoft = Color.FromArgb(202, 196, 208);
        public static readonly Color Muted = Color.FromArgb(147, 143, 153);
        public static readonly Color Faint = Color.FromArgb(121, 116, 126);
        public static readonly Color Accent = Color.FromArgb(208, 188, 255);
        public static readonly Color AccentDark = Color.FromArgb(79, 55, 139);
        public static readonly Color AccentDeep = Color.FromArgb(56, 30, 114);
        public static readonly Color OnAccent = Color.FromArgb(56, 30, 114);
        public static readonly Color PrimaryContainer = Color.FromArgb(79, 55, 139);
        public static readonly Color OnPrimaryContainer = Color.FromArgb(234, 221, 255);
        public static readonly Color Secondary = Color.FromArgb(204, 194, 220);
        public static readonly Color SecondaryContainer = Color.FromArgb(74, 68, 88);
        public static readonly Color OnSecondaryContainer = Color.FromArgb(232, 222, 248);
        public static readonly Color SurfaceContainerHigh = Color.FromArgb(43, 41, 48);
        public static readonly Color SurfaceContainerHighest = Color.FromArgb(54, 52, 59);
        public static readonly Color Outline = Color.FromArgb(147, 143, 153);
        public static readonly Color OutlineVariant = Color.FromArgb(73, 69, 79);
        public static readonly Color Cyan = Color.FromArgb(156, 202, 255);
        public static readonly Color Gold = Color.FromArgb(239, 184, 200);
        public static readonly Color Red = Color.FromArgb(255, 180, 171);
        public static readonly Color Purple = Color.FromArgb(208, 188, 255);

        private static PrivateFontCollection _embeddedFonts;
        private static IntPtr _embeddedFontMemory = IntPtr.Zero;
        private static readonly FontFamily EmbeddedUiFamily = LoadEmbeddedUiFamily();

        public static readonly string UiFont = EmbeddedUiFamily != null ? EmbeddedUiFamily.Name : PickFont(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" });
        public static readonly string DisplayFont = EmbeddedUiFamily != null ? EmbeddedUiFamily.Name : PickFont(new string[] { "Bahnschrift SemiBold", "Segoe UI Semibold", "Microsoft YaHei UI" });
        public static readonly string MonoFont = PickFont(new string[] { "Cascadia Mono", "Consolas", "Courier New" });

        public static Font Font(float size)
        {
            return CreateFont(EmbeddedUiFamily, UiFont, size, FontStyle.Regular);
        }

        public static Font Font(float size, FontStyle style)
        {
            return CreateFont(EmbeddedUiFamily, UiFont, size, style);
        }

        public static Font Display(float size, FontStyle style)
        {
            return CreateFont(EmbeddedUiFamily, DisplayFont, size, style);
        }

        public static Font Mono(float size)
        {
            return CreateFont(EmbeddedUiFamily, MonoFont, size, FontStyle.Regular);
        }

        private static Font CreateFont(FontFamily family, string fallbackName, float size, FontStyle style)
        {
            if (family != null)
            {
                try
                {
                    if (!family.IsStyleAvailable(style)) style = FontStyle.Regular;
                    return new Font(family, size, style, GraphicsUnit.Point);
                }
                catch
                {
                }
            }
            return new Font(fallbackName, size, style, GraphicsUnit.Point);
        }

        private static FontFamily LoadEmbeddedUiFamily()
        {
            try
            {
                // The font is embedded into the EXE by build.ps1. No external
                // asset is required at runtime.
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("CubeLauncher.Resources.ShangShouBiaoTiSong.ttf"))
                {
                    if (stream != null)
                    {
                        byte[] bytes = new byte[stream.Length];
                        int read = 0;
                        while (read < bytes.Length)
                        {
                            int count = stream.Read(bytes, read, bytes.Length - read);
                            if (count <= 0) break;
                            read += count;
                        }
                        if (read > 0)
                        {
                            _embeddedFontMemory = Marshal.AllocHGlobal(read);
                            Marshal.Copy(bytes, 0, _embeddedFontMemory, read);
                            _embeddedFonts = new PrivateFontCollection();
                            _embeddedFonts.AddMemoryFont(_embeddedFontMemory, read);
                            if (_embeddedFonts.Families.Length > 0) return _embeddedFonts.Families[0];
                        }
                    }
                }
            }
            catch
            {
            }

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[]
                {
                    Path.Combine(baseDir, "assets", "fonts", "ShangShouBiaoTiSong.ttf"),
                    Path.Combine(baseDir, "assets", "fonts", "上首标题宋体.ttf"),
                    Path.Combine(baseDir, "上首标题宋体.ttf")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (!File.Exists(candidates[i])) continue;
                    _embeddedFonts = new PrivateFontCollection();
                    _embeddedFonts.AddFontFile(candidates[i]);
                    if (_embeddedFonts.Families.Length > 0) return _embeddedFonts.Families[0];
                }
            }
            catch
            {
            }
            return null;
        }

        public static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color.R, color.G, color.B);
        }

        public static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        private static string PickFont(string[] candidates)
        {
            try
            {
                using (InstalledFontCollection collection = new InstalledFontCollection())
                {
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        for (int f = 0; f < collection.Families.Length; f++)
                        {
                            if (string.Equals(collection.Families[f].Name, candidates[i], StringComparison.OrdinalIgnoreCase))
                                return candidates[i];
                        }
                    }
                }
            }
            catch
            {
            }
            return "Segoe UI";
        }
    }
}
