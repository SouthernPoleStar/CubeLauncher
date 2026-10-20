using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace CubeLauncher.Core
{
    public sealed class DownloadItem
    {
        public string Name;
        public string Destination;
        public List<string> Urls = new List<string>();
        public string Sha1 = "";
        public long Size;
        public long Downloaded;
        public bool Quiet;
    }

    public sealed class HttpResult
    {
        public int Status;
        public string Body = "";
        public string Error = "";

        public bool Ok
        {
            get { return Status >= 200 && Status < 300; }
        }
    }

    public static class Http
    {
        public const string UserAgent = "CubeLauncher/1.0.0 (Minecraft launcher; China mirror first)";

        static Http()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.DefaultConnectionLimit = 48;
                ServicePointManager.Expect100Continue = false;
            }
            catch
            {
            }
        }

        public static string GetText(IList<string> urls, int timeoutMs)
        {
            return GetText(urls, timeoutMs, null);
        }

        public static string GetText(IList<string> urls, int timeoutMs, IDictionary<string, string> headers)
        {
            Exception last = null;
            for (int i = 0; i < urls.Count; i++)
            {
                if (string.IsNullOrEmpty(urls[i])) continue;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        HttpWebRequest request = CreateRequest(urls[i], "GET", timeoutMs);
                        if (headers != null)
                        {
                            foreach (KeyValuePair<string, string> pair in headers)
                            {
                                if (string.Equals(pair.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                                    request.Accept = pair.Value;
                                else
                                    request.Headers[pair.Key] = pair.Value;
                            }
                        }
                        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        Log.Warn("请求失败 " + ShortUrl(urls[i]) + " (" + (attempt + 1) + "/2) :: " + ex.Message);
                        WebException web = ex as WebException;
                        if (web != null && web.Response != null) break;
                        if (attempt == 0) System.Threading.Thread.Sleep(500);
                    }
                }
            }
            if (last != null) throw new Exception("所有下载源均不可用: " + last.Message, last);
            throw new Exception("没有可用的下载源");
        }

        public static byte[] GetBytes(IList<string> urls, int timeoutMs)
        {
            Exception last = null;
            for (int i = 0; i < urls.Count; i++)
            {
                if (string.IsNullOrEmpty(urls[i])) continue;
                try
                {
                    HttpWebRequest request = CreateRequest(urls[i], "GET", timeoutMs);
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    using (MemoryStream memory = new MemoryStream())
                    {
                        Copy(stream, memory, 64 * 1024, null, CancellationToken.None);
                        return memory.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    last = ex;
                    Log.Warn("图片请求失败 " + ShortUrl(urls[i]) + " :: " + ex.Message);
                }
            }
            if (last != null) throw last;
            return new byte[0];
        }

        public static HttpResult Request(string method, string url, string contentType, string body, IDictionary<string, string> headers, int timeoutMs)
        {
            HttpResult result = new HttpResult();
            try
            {
                HttpWebRequest request = CreateRequest(url, method, timeoutMs);
                if (!string.IsNullOrEmpty(contentType)) request.ContentType = contentType;
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> pair in headers)
                    {
                        if (string.Equals(pair.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                            request.Headers[HttpRequestHeader.Authorization] = pair.Value;
                        else if (string.Equals(pair.Key, "Accept", StringComparison.OrdinalIgnoreCase))
                            request.Accept = pair.Value;
                        else
                            request.Headers[pair.Key] = pair.Value;
                    }
                }
                if (!string.IsNullOrEmpty(body))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    request.ContentLength = bytes.Length;
                    using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                }

                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        result.Status = (int)response.StatusCode;
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                            result.Body = reader.ReadToEnd();
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    if (response == null) throw;
                    result.Status = (int)response.StatusCode;
                    result.Error = ex.Message;
                    using (response)
                    using (Stream stream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
                        result.Body = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                result.Status = 0;
                result.Error = ex.Message;
            }
            return result;
        }

        private static HttpWebRequest CreateRequest(string url, string method, int timeoutMs)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.UserAgent = UserAgent;
            request.Accept = "application/json, text/plain, */*";
            request.Timeout = timeoutMs > 0 ? timeoutMs : 30000;
            request.ReadWriteTimeout = timeoutMs > 0 ? timeoutMs : 60000;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.KeepAlive = true;
            return request;
        }

        public static void DownloadMany(IList<DownloadItem> items, int workers, Action<int, int, string> progress, CancellationToken token)
        {
            if (items == null || items.Count == 0) return;
            int total = items.Count;
            int completed = 0;
            int next = -1;
            Exception firstError = null;
            object errorGate = new object();

            int workerCount = Math.Max(1, Math.Min(workers, total));
            Thread[] threads = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                threads[i] = new Thread(delegate()
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested) return;
                        int index = Interlocked.Increment(ref next);
                        if (index >= total) return;
                        DownloadItem item = items[index];
                        try
                        {
                            DownloadOne(item, token, null);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            lock (errorGate) if (firstError == null) firstError = ex;
                            Log.Error("下载失败: " + item.Name + " :: " + ex.Message);
                        }
                        int done = Interlocked.Increment(ref completed);
                        if (progress != null)
                        {
                            try { progress(done, total, item.Name); }
                            catch { }
                        }
                    }
                });
                threads[i].IsBackground = true;
                threads[i].Start();
            }

            for (int i = 0; i < threads.Length; i++) threads[i].Join();
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            if (firstError != null && completed < total)
                throw new Exception("有 " + (total - completed) + " 个文件下载失败，首个错误: " + firstError.Message, firstError);
        }

        public static void DownloadOne(DownloadItem item, CancellationToken token, Action<long> bytesProgress)
        {
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.Destination) && !string.IsNullOrEmpty(item.Sha1) && File.Exists(item.Destination))
            {
                if (string.Equals(Util.Sha1File(item.Destination), item.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    item.Downloaded = item.Size > 0 ? item.Size : new FileInfo(item.Destination).Length;
                    if (bytesProgress != null) bytesProgress(item.Downloaded);
                    return;
                }
                try { File.Delete(item.Destination); } catch { }
            }
            if (!string.IsNullOrEmpty(item.Destination) && File.Exists(item.Destination) && string.IsNullOrEmpty(item.Sha1))
            {
                item.Downloaded = new FileInfo(item.Destination).Length;
                if (bytesProgress != null) bytesProgress(item.Downloaded);
                return;
            }

            Util.EnsureParent(item.Destination);
            Exception last = null;
            for (int u = 0; u < item.Urls.Count; u++)
            {
                if (string.IsNullOrEmpty(item.Urls[u])) continue;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    string temp = item.Destination + ".part";
                    try
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                        HttpWebRequest request = CreateRequest(item.Urls[u], "GET", 30000);
                        request.ReadWriteTimeout = 30000;
                        Timer watchdog = new Timer(delegate(object state)
                        {
                            try { request.Abort(); }
                            catch { }
                        }, null, 60000, System.Threading.Timeout.Infinite);
                        try
                        {
                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                            using (Stream input = response.GetResponseStream())
                            using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.SequentialScan))
                            {
                                Copy(input, output, 128 * 1024, bytesProgress, token);
                            }
                        }
                        finally
                        {
                            watchdog.Dispose();
                        }

                        if (!string.IsNullOrEmpty(item.Sha1))
                        {
                            string actual = Util.Sha1File(temp);
                            if (!string.Equals(actual, item.Sha1, StringComparison.OrdinalIgnoreCase))
                                throw new Exception("SHA1 校验失败，期望 " + item.Sha1 + "，实际 " + actual);
                        }
                        if (item.Size > 0 && new FileInfo(temp).Length != item.Size)
                            throw new Exception("文件大小不一致，期望 " + item.Size + " 字节，实际 " + new FileInfo(temp).Length + " 字节");

                        if (File.Exists(item.Destination)) File.Delete(item.Destination);
                        File.Move(temp, item.Destination);
                        item.Downloaded = new FileInfo(item.Destination).Length;
                        if (!item.Quiet) Log.Info("下载完成 " + item.Name + " <- " + ShortUrl(item.Urls[u]));
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        TryDelete(temp);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        TryDelete(temp);
                        Log.Warn("下载重试 " + ShortUrl(item.Urls[u]) + " (" + (attempt + 1) + "/2) :: " + ex.Message);
                    }
                }
            }
            throw new Exception("下载 " + item.Name + " 失败: " + (last == null ? "无可用源" : last.Message), last);
        }

        private static void Copy(Stream input, Stream output, int bufferSize, Action<long> progress, CancellationToken token)
        {
            byte[] buffer = new byte[bufferSize];
            long total = 0;
            while (true)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                int read = input.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                output.Write(buffer, 0, read);
                total += read;
                if (progress != null) progress(total);
            }
            output.Flush();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        public static string ShortUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            int query = url.IndexOf('?');
            string clean = query >= 0 ? url.Substring(0, query) : url;
            if (clean.Length > 110) return clean.Substring(0, 60) + "..." + clean.Substring(clean.Length - 40);
            return clean;
        }
    }
}
