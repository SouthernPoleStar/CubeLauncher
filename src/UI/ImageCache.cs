using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using CubeLauncher.Core;

namespace CubeLauncher.UI
{
    public sealed class ImageCache
    {
        private readonly Dictionary<string, Image> _images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly AppConfig _config;
        private readonly object _gate = new object();

        public ImageCache(AppConfig config)
        {
            _config = config;
        }

        public Image Get(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            lock (_gate)
            {
                Image image;
                return _images.TryGetValue(url, out image) ? image : null;
            }
        }

        public void Request(Control owner, string url, Action<Image> callback)
        {
            if (string.IsNullOrEmpty(url) || callback == null) return;
            lock (_gate)
            {
                Image image;
                if (_images.TryGetValue(url, out image))
                {
                    callback(image);
                    return;
                }
                if (_loading.Contains(url) || _failed.Contains(url)) return;
                _loading.Add(url);
            }
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Image image = null;
                try
                {
                    byte[] bytes = Http.GetBytes(Mirror.Urls(url, false), 20000);
                    if (bytes != null && bytes.Length > 0)
                    {
                        image = Decode(bytes);
                    }
                }
                catch
                {
                }
                lock (_gate)
                {
                    _loading.Remove(url);
                    if (image != null) _images[url] = image;
                    else _failed.Add(url);
                }
                if (image != null)
                    Ui.Post(owner, delegate { callback(image); });
            });
        }

        public void RequestHead(Control owner, Account account, Action<Image> callback)
        {
            if (owner == null || account == null || callback == null || !account.IsMicrosoft || string.IsNullOrEmpty(account.CleanUuid)) return;
            string key = "head:" + account.CleanUuid.ToLowerInvariant();
            lock (_gate)
            {
                Image cached;
                if (_images.TryGetValue(key, out cached))
                {
                    callback(cached);
                    return;
                }
                if (_loading.Contains(key) || _failed.Contains(key)) return;
                _loading.Add(key);
            }
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                Image head = null;
                try
                {
                    head = LoadCachedHead(account.CleanUuid);
                    if (head == null) head = DownloadHead(account);
                }
                catch
                {
                }
                lock (_gate)
                {
                    _loading.Remove(key);
                    if (head != null) _images[key] = head;
                    else _failed.Add(key);
                }
                if (head != null)
                    Ui.Post(owner, delegate { callback(head); });
            });
        }

        private string HeadCachePath(string uuid)
        {
            return Path.Combine(_config.CacheDir, "heads", uuid.ToLowerInvariant() + ".png");
        }

        private Image LoadCachedHead(string uuid)
        {
            try
            {
                string path = HeadCachePath(uuid);
                if (!File.Exists(path)) return null;
                using (FileStream stream = File.OpenRead(path))
                using (Image loaded = Image.FromStream(stream))
                    return new Bitmap(loaded);
            }
            catch { return null; }
        }

        private Image DownloadHead(Account account)
        {
            string skinUrl = null;
            try
            {
                string profile = Http.GetText(new string[] { "https://sessionserver.mojang.com/session/minecraft/profile/" +
                                             account.CleanUuid + "?unsigned=false" }, 15000);
                JMap root = JMap.Parse(profile);
                List<JMap> properties = root.Maps("properties");
                for (int i = 0; i < properties.Count; i++)
                {
                    string value = properties[i].Str("value", "");
                    if (string.IsNullOrEmpty(value)) continue;
                    try
                    {
                        JMap decoded = JMap.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
                        JMap skin = decoded.Map("textures").Map("SKIN");
                        skinUrl = skin.Str("url", "");
                        if (!string.IsNullOrEmpty(skinUrl)) break;
                    }
                    catch { }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(skinUrl))
            {
                try
                {
                    byte[] bytes = Http.GetBytes(Mirror.Urls(skinUrl, false), 20000);
                    Image skin = Decode(bytes);
                    if (skin != null)
                    {
                        Image head = CropHead(skin);
                        skin.Dispose();
                        if (head != null)
                        {
                            SaveHead(account.CleanUuid, head);
                            return head;
                        }
                    }
                }
                catch { }
            }

            // Fallback: a rendered head image, still avoids using the name initial.
            string[] fallbacks = new string[]
            {
                "https://crafatar.com/avatars/" + account.CleanUuid + "?size=64&overlay",
                "https://mc-heads.net/avatar/" + account.CleanUuid + "/64"
            };
            for (int i = 0; i < fallbacks.Length; i++)
            {
                try
                {
                    byte[] bytes = Http.GetBytes(new string[] { fallbacks[i] }, 15000);
                    Image image = Decode(bytes);
                    if (image != null)
                    {
                        SaveHead(account.CleanUuid, image);
                        return image;
                    }
                }
                catch { }
            }
            return null;
        }

        private static Image CropHead(Image skin)
        {
            if (skin == null || skin.Width < 64 || skin.Height < 32) return null;
            int scale = Math.Max(1, skin.Width / 64);
            int sourceSize = 8 * scale;
            int overlayX = 40 * scale;
            int overlayY = 8 * scale;
            Bitmap head = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(head))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(skin, new Rectangle(0, 0, 64, 64),
                    new Rectangle(8 * scale, 8 * scale, sourceSize, sourceSize), GraphicsUnit.Pixel);
                if (skin.Width >= 64 * scale && skin.Height >= 16 * scale)
                {
                    try
                    {
                        g.DrawImage(skin, new Rectangle(0, 0, 64, 64),
                            new Rectangle(overlayX, overlayY, sourceSize, sourceSize), GraphicsUnit.Pixel);
                    }
                    catch { }
                }
            }
            return head;
        }

        private void SaveHead(string uuid, Image image)
        {
            try
            {
                string path = HeadCachePath(uuid);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (Bitmap bitmap = new Bitmap(image))
                    bitmap.Save(path, ImageFormat.Png);
            }
            catch { }
        }

        private static Image Decode(byte[] bytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    Image loaded = Image.FromStream(stream);
                    Bitmap bitmap = new Bitmap(loaded);
                    loaded.Dispose();
                    return bitmap;
                }
            }
            catch
            {
            }

            // System.Drawing on .NET 4.0 cannot decode WebP. WIC on Windows
            // does support it through PresentationCore, so use it as a fallback.
            try
            {
                using (MemoryStream input = new MemoryStream(bytes))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(input,
                        BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count == 0) return null;
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));
                    using (MemoryStream output = new MemoryStream())
                    {
                        encoder.Save(output);
                        output.Position = 0;
                        using (Image loaded = Image.FromStream(output))
                            return new Bitmap(loaded);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
