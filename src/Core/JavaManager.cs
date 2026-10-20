using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;

namespace CubeLauncher.Core
{
    public sealed class JavaInfo
    {
        public string Path;
        public int Major;
        public string Version = "";
        public string Vendor = "";
    }

    public static class JavaManager
    {
        public static List<JavaInfo> FindInstalled(AppConfig config)
        {
            List<JavaInfo> result = new List<JavaInfo>();
            List<string> candidates = new List<string>();

            if (!string.IsNullOrEmpty(config.JavaPath) && File.Exists(config.JavaPath)) candidates.Add(config.JavaPath);
            if (Directory.Exists(config.JavaDir))
            {
                string[] dirs = Directory.GetDirectories(config.JavaDir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string java = Path.Combine(dirs[i], "bin", "java.exe");
                    if (File.Exists(java)) candidates.Add(java);
                }
            }

            string[] roots = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BellSoft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Eclipse Adoptium"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft")
            };
            for (int i = 0; i < roots.Length; i++)
            {
                if (!Directory.Exists(roots[i])) continue;
                try
                {
                    string[] dirs = Directory.GetDirectories(roots[i]);
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        string java = Path.Combine(dirs[d], "bin", "java.exe");
                        if (File.Exists(java)) candidates.Add(java);
                    }
                }
                catch { }
            }

            string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] pathParts = pathVariable.Split(';');
            for (int i = 0; i < pathParts.Length; i++)
            {
                try
                {
                    string java = Path.Combine(pathParts[i].Trim(), "java.exe");
                    if (File.Exists(java)) candidates.Add(java);
                }
                catch { }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < candidates.Count; i++)
            {
                string full;
                try { full = Path.GetFullPath(candidates[i]); }
                catch { continue; }
                if (!seen.Add(full)) continue;
                JavaInfo info = Inspect(full);
                if (info != null) result.Add(info);
            }
            result.Sort(delegate(JavaInfo a, JavaInfo b) { return b.Major.CompareTo(a.Major); });
            return result;
        }

        public static JavaInfo Inspect(string javaPath)
        {
            if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath)) return null;
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = javaPath;
                start.Arguments = "-version";
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                using (Process process = Process.Start(start))
                {
                    string output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
                    process.WaitForExit(6000);
                    JavaInfo info = new JavaInfo();
                    info.Path = javaPath;
                    info.Version = ParseVersionString(output);
                    info.Major = ParseMajor(info.Version);
                    if (info.Major <= 0) return null;
                    info.Vendor = ParseVendor(output);
                    return info;
                }
            }
            catch
            {
                return null;
            }
        }

        public static JavaInfo Ensure(AppConfig config, int requiredMajor, ProgressSink progress)
        {
            if (requiredMajor < 8) requiredMajor = 8;
            List<JavaInfo> installed = FindInstalled(config);
            JavaInfo best = null;
            for (int i = 0; i < installed.Count; i++)
            {
                if (installed[i].Major == requiredMajor) return installed[i];
                if (installed[i].Major > requiredMajor && installed[i].Major - requiredMajor <= 4 && best == null) best = installed[i];
            }
            if (best != null)
            {
                Log.Info("使用已安装的 Java " + best.Major + ": " + best.Path);
                return best;
            }

            progress.SetStage("未找到 Java " + requiredMajor + "，正在从清华 TUNA 镜像下载");
            string java = DownloadJava(config, requiredMajor, progress);
            JavaInfo info = Inspect(java);
            if (info == null) throw new Exception("Java 下载完成但无法运行: " + java);
            config.JavaPath = java;
            App.Save();
            return info;
        }

        public static string DownloadJava(AppConfig config, int major, ProgressSink progress)
        {
            Util.EnsureDirectory(Path.Combine(config.JavaDir, "cache"));
            string versionDir = Path.Combine(config.JavaDir, "temurin-" + major);
            string existing = Path.Combine(versionDir, "bin", "java.exe");
            if (File.Exists(existing)) return existing;

            string mirrorDir = Mirror.TunaAdoptium + "/" + major + "/jdk/x64/windows/";
            string fileName = "";
            try
            {
                progress.SetStage("查询 Java " + major + " 镜像文件");
                string listing = Http.GetText(new List<string>() { mirrorDir }, 30000);
                MatchCollection matches = Regex.Matches(listing, "href=\"\\.?/?([^\"/]+\\.zip)\"", RegexOptions.IgnoreCase);
                string best = "";
                for (int i = 0; i < matches.Count; i++)
                {
                    string name = Uri.UnescapeDataString(matches[i].Groups[1].Value);
                    if (!name.StartsWith("OpenJDK" + major + "U-jdk_x64_windows_hotspot_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrEmpty(best) || CompareJdkName(name, best) > 0) best = name;
                }
                fileName = best;
            }
            catch (Exception ex)
            {
                Log.Warn("TUNA Java 目录查询失败: " + ex.Message);
            }

            string downloadUrl = "";
            long expectedSize = 0;
            if (!string.IsNullOrEmpty(fileName))
            {
                downloadUrl = mirrorDir + Uri.EscapeDataString(fileName);
            }
            else
            {
                progress.SetStage("TUNA 无可用文件，尝试 Adoptium 官方 API");
                string api = "https://api.adoptium.net/v3/assets/latest/" + major + "/hotspot?architecture=x64&image_type=jdk&os=windows&vendor=eclipse";
                string json = Http.GetText(new List<string>() { api }, 30000);
                JList assets = new JList(Json.Parse(json));
                if (assets.Count == 0) throw new Exception("没有找到 Java " + major + " 的下载");
                JMap package = assets.Map(0).Map("binary").Map("package");
                downloadUrl = package.Str("link", "");
                fileName = package.Str("name", "jdk-" + major + ".zip");
                expectedSize = package.Long("size", 0);
                if (string.IsNullOrEmpty(downloadUrl)) throw new Exception("Java 下载地址为空");
            }

            string zip = Path.Combine(config.JavaDir, "cache", "jdk-" + major + ".zip");
            DownloadItem item = new DownloadItem();
            item.Name = "Java " + major + " (" + fileName + ")";
            item.Destination = zip;
            item.Urls = new List<string>() { downloadUrl };
            item.Size = expectedSize;
            progress.SetStage("下载 Java " + major + "，约 " + (expectedSize > 0 ? Util.HumanBytes(expectedSize) : "190 MB"));
            Http.DownloadOne(item, progress.Token, delegate(long bytes)
            {
                if (expectedSize > 0)
                    progress.Report(Math.Min(0.9, (double)bytes / expectedSize * 0.9), "Java 下载 " + Util.HumanBytes(bytes));
            });

            progress.SetStage("解压 Java 运行环境");
            string temp = Path.Combine(config.JavaDir, "temp-" + Guid.NewGuid().ToString("N"));
            try
            {
                Util.EnsureDirectory(temp);
                ZipFile.ExtractToDirectory(zip, temp);
                string java = FindJavaExe(temp);
                if (string.IsNullOrEmpty(java)) throw new Exception("压缩包内没有找到 bin\\java.exe");
                string extractedRoot = Path.GetDirectoryName(Path.GetDirectoryName(java));
                if (Directory.Exists(versionDir)) Directory.Delete(versionDir, true);
                Directory.Move(extractedRoot, versionDir);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch { }
            }
            try { File.Delete(zip); } catch { }
            string result = Path.Combine(versionDir, "bin", "java.exe");
            if (!File.Exists(result)) throw new Exception("Java 解压失败");
            progress.Report(1, "Java " + major + " 安装完成");
            Log.Info("Java " + major + " 已安装: " + result);
            return result;
        }

        private static string FindJavaExe(string root)
        {
            string direct = Path.Combine(root, "bin", "java.exe");
            if (File.Exists(direct)) return direct;
            string[] found = Directory.GetFiles(root, "java.exe", SearchOption.AllDirectories);
            for (int i = 0; i < found.Length; i++)
            {
                if (string.Equals(Path.GetFileName(Path.GetDirectoryName(found[i])), "bin", StringComparison.OrdinalIgnoreCase)) return found[i];
            }
            return "";
        }

        private static int CompareJdkName(string a, string b)
        {
            Match ma = Regex.Match(a, "hotspot_(\\d+)\\.(\\d+)\\.(\\d+)(?:\\.(\\d+))?");
            Match mb = Regex.Match(b, "hotspot_(\\d+)\\.(\\d+)\\.(\\d+)(?:\\.(\\d+))?");
            if (!ma.Success || !mb.Success) return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            for (int i = 1; i < 5; i++)
            {
                int va = ma.Groups[i].Success ? int.Parse(ma.Groups[i].Value) : 0;
                int vb = mb.Groups[i].Success ? int.Parse(mb.Groups[i].Value) : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string ParseVersionString(string output)
        {
            Match match = Regex.Match(output ?? "", "version\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
            match = Regex.Match(output ?? "", "openjdk\\s+([0-9][^\\s]*)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static int ParseMajor(string version)
        {
            if (string.IsNullOrEmpty(version)) return 0;
            Match match = Regex.Match(version, "^(\\d+)(?:\\.(\\d+))?");
            if (!match.Success) return 0;
            int first = int.Parse(match.Groups[1].Value);
            if (first == 1 && match.Groups[2].Success) return int.Parse(match.Groups[2].Value);
            return first;
        }

        private static string ParseVendor(string output)
        {
            string lower = (output ?? "").ToLowerInvariant();
            if (lower.Contains("temurin") || lower.Contains("adoptium")) return "Eclipse Temurin";
            if (lower.Contains("microsoft")) return "Microsoft OpenJDK";
            if (lower.Contains("zulu")) return "Azul Zulu";
            if (lower.Contains("bellsoft") || lower.Contains("liberica")) return "BellSoft Liberica";
            if (lower.Contains("oracle")) return "Oracle";
            return "OpenJDK";
        }
    }
}
