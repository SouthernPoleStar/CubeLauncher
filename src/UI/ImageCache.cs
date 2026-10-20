using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
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
