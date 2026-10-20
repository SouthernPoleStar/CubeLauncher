using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CubeLauncher.Core
{
    public static class Util
    {
        public static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        public static void EnsureParent(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) EnsureDirectory(dir);
        }

        public static string Sha1File(string path)
        {
            if (!File.Exists(path)) return "";
            using (FileStream stream = File.OpenRead(path))
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static string Sha1Text(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static string OfflineUuid(string name)
        {
            // Mirrors Java's UUID.nameUUIDFromBytes(("OfflinePlayer:" + name).getBytes(UTF_8)).
            byte[] bytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + (name ?? "Player"));
            byte[] hash;
            using (MD5 md5 = MD5.Create()) hash = md5.ComputeHash(bytes);
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            return BytesToHex(hash);
        }

        public static string BytesToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        public static string InsertUuidDashes(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) return "";
            string clean = uuid.Replace("-", "");
            if (clean.Length != 32) return uuid;
            return clean.Substring(0, 8) + "-" + clean.Substring(8, 4) + "-" + clean.Substring(12, 4) + "-" + clean.Substring(16, 4) + "-" + clean.Substring(20, 12);
        }

        public static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "file";
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++) if (invalid[j] == c) { bad = true; break; }
                builder.Append(bad ? '_' : c);
            }
            return builder.ToString();
        }

        public static string HumanBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = new string[] { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString(unit == 0 ? "0" : "0.0") + " " + units[unit];
        }

        public static string HumanDate(string iso)
        {
            DateTime date;
            if (DateTime.TryParse(iso, out date)) return date.ToLocalTime().ToString("yyyy-MM-dd");
            return iso ?? "";
        }

        public static string ReadAllText(string path)
        {
            if (!File.Exists(path)) return "";
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public static void WriteAllTextAtomic(string path, string text)
        {
            EnsureParent(path);
            string temp = path + ".tmp";
            File.WriteAllText(temp, text ?? "", new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        public static string Quote(string value)
        {
            if (value == null) value = "";
            if (value.Length > 0 && value.IndexOfAny(new char[] { ' ', '\t', '"', '\n', '\r' }) < 0) return value;
            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int slashes = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    slashes++;
                    builder.Append(c);
                }
                else if (c == '"')
                {
                    builder.Append('\\', slashes + 1);
                    builder.Append('"');
                    slashes = 0;
                }
                else
                {
                    slashes = 0;
                    builder.Append(c);
                }
            }
            builder.Append('\\', slashes);
            builder.Append('"');
            return builder.ToString();
        }

        public static string RedactToken(string command)
        {
            if (string.IsNullOrEmpty(command)) return command;
            return Regex.Replace(command, "(--accessToken\\s+)(\\S+)", "$1<redacted>", RegexOptions.IgnoreCase);
        }

        public static int CompareVersion(string a, string b)
        {
            string[] left = (a ?? "").Split(new char[] { '.', '-', '+' });
            string[] right = (b ?? "").Split(new char[] { '.', '-', '+' });
            int count = Math.Max(left.Length, right.Length);
            for (int i = 0; i < count; i++)
            {
                string l = i < left.Length ? left[i] : "0";
                string r = i < right.Length ? right[i] : "0";
                int ln, rn;
                bool lok = int.TryParse(l, out ln);
                bool rok = int.TryParse(r, out rn);
                if (lok && rok)
                {
                    if (ln != rn) return ln.CompareTo(rn);
                }
                else
                {
                    int cmp = string.Compare(l, r, StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }
            return 0;
        }

        public static bool IsWindows64
        {
            get { return Environment.Is64BitOperatingSystem; }
        }

        public static string OsArch
        {
            get { return Environment.Is64BitOperatingSystem ? "x86_64" : "x86"; }
        }

        public static long UnixNow
        {
            get { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        }

        public static void OpenFolder(string path)
        {
            EnsureDirectory(path);
            Process.Start("explorer.exe", Quote(path));
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    public sealed class ProcessResult
    {
        public int ExitCode;
        public string Output = "";
        public string Error = "";
    }

    public static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, string arguments, string workingDirectory, int timeoutMs, Action<string> output)
        {
            ProcessResult result = new ProcessResult();
            using (Process process = new Process())
            {
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    result.Output += e.Data + Environment.NewLine;
                    if (output != null) output(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    result.Error += e.Data + Environment.NewLine;
                    if (output != null) output(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (timeoutMs > 0)
                {
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        throw new TimeoutException("进程运行超时: " + Path.GetFileName(fileName));
                    }
                }
                else
                {
                    process.WaitForExit();
                }
                process.WaitForExit();
                result.ExitCode = process.ExitCode;
            }
            return result;
        }

        public static ProcessStartResult Start(string fileName, string arguments, string workingDirectory, Action<string> output, Action<string> error)
        {
            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            if (output != null)
            {
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) output(e.Data);
                };
            }
            if (error != null)
            {
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) error(e.Data);
                };
            }
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new ProcessStartResult(process);
        }
    }

    public sealed class ProcessStartResult
    {
        private readonly Process _process;

        public ProcessStartResult(Process process)
        {
            _process = process;
        }

        public int Id
        {
            get { return _process.Id; }
        }

        public bool HasExited
        {
            get { return _process.HasExited; }
        }

        public int ExitCode
        {
            get { return _process.ExitCode; }
        }

        public void WaitForExit()
        {
            _process.WaitForExit();
        }

        public void Kill()
        {
            try { if (!_process.HasExited) _process.Kill(); }
            catch { }
        }

        public void Dispose()
        {
            try { _process.Dispose(); }
            catch { }
        }
    }
}
