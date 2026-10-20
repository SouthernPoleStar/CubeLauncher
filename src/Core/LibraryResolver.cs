using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;

namespace CubeLauncher.Core
{
    public sealed class LibraryFile
    {
        public string Name;
        public string Group;
        public string Artifact;
        public string Version;
        public string Classifier;
        public string Path;
        public string Url;
        public string Sha1;
        public long Size;
        public bool Native;
        public List<string> Excludes = new List<string>();
        public JMap Raw;
    }

    public static class LibraryResolver
    {
        public static List<LibraryFile> Resolve(JMap version)
        {
            List<LibraryFile> artifacts = new List<LibraryFile>();
            Dictionary<string, int> keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            List<JMap> libraries = version.Maps("libraries");
            for (int i = 0; i < libraries.Count; i++)
            {
                JMap library = libraries[i];
                if (!Rules.Allowed(library.List("rules"))) continue;
                string name = library.Str("name");
                if (string.IsNullOrEmpty(name)) continue;

                MavenName maven;
                if (!MavenName.TryParse(name, out maven)) continue;

                JMap downloads = library.Map("downloads");
                JMap artifact = downloads.Map("artifact");
                LibraryFile file = new LibraryFile();
                file.Name = name;
                file.Group = maven.Group;
                file.Artifact = maven.Artifact;
                file.Version = maven.Version;
                file.Classifier = maven.Classifier;
                file.Path = artifact.Str("path", maven.Path);
                file.Url = artifact.Str("url", "");
                file.Sha1 = artifact.Str("sha1", "");
                file.Size = artifact.Long("size", 0);
                file.Raw = library;
                if (string.IsNullOrEmpty(file.Url))
                {
                    string baseUrl = library.Str("url", "https://libraries.minecraft.net/");
                    if (!baseUrl.EndsWith("/")) baseUrl += "/";
                    file.Url = baseUrl + file.Path;
                }
                bool classifiedNative = !string.IsNullOrEmpty(maven.Classifier) &&
                                        maven.Classifier.IndexOf("natives-windows", StringComparison.OrdinalIgnoreCase) >= 0;
                if (classifiedNative && !NativeCompatible(maven.Classifier)) continue;
                if (classifiedNative) file.Native = true;

                string key = maven.Group + ":" + maven.Artifact + ":" + maven.Classifier;
                if (keys.ContainsKey(key))
                {
                    artifacts[keys[key]] = file;
                }
                else
                {
                    keys[key] = artifacts.Count;
                    artifacts.Add(file);
                }

                string nativeClassifier = NativeClassifier(library);
                if (!classifiedNative && !string.IsNullOrEmpty(nativeClassifier))
                {
                    JMap classifiers = downloads.Map("classifiers");
                    JMap nativeArtifact = classifiers.Map(nativeClassifier);
                    LibraryFile native = new LibraryFile();
                    native.Name = name + ":" + nativeClassifier;
                    native.Group = maven.Group;
                    native.Artifact = maven.Artifact;
                    native.Version = maven.Version;
                    native.Classifier = nativeClassifier;
                    native.Path = nativeArtifact.Str("path", maven.PathWithClassifier(nativeClassifier));
                    native.Url = nativeArtifact.Str("url", "");
                    native.Sha1 = nativeArtifact.Str("sha1", "");
                    native.Size = nativeArtifact.Long("size", 0);
                    native.Native = true;
                    native.Raw = library;
                    if (string.IsNullOrEmpty(native.Url))
                    {
                        string baseUrl = library.Str("url", "https://libraries.minecraft.net/");
                        if (!baseUrl.EndsWith("/")) baseUrl += "/";
                        native.Url = baseUrl + native.Path;
                    }
                    JMap extract = library.Map("extract");
                    IList<object> excludes = extract.List("exclude");
                    if (excludes != null)
                        for (int e = 0; e < excludes.Count; e++) native.Excludes.Add(Convert.ToString(excludes[e]));
                    artifacts.Add(native);
                }
            }
            return artifacts;
        }

        public static void DownloadLibraries(AppConfig config, List<LibraryFile> libraries, ProgressSink progress, double from, double to)
        {
            List<DownloadItem> items = new List<DownloadItem>();
            for (int i = 0; i < libraries.Count; i++)
            {
                LibraryFile library = libraries[i];
                DownloadItem item = new DownloadItem();
                item.Name = "库 " + library.Artifact + " " + library.Version;
                item.Destination = Path.Combine(config.LibrariesDir, library.Path.Replace('/', Path.DirectorySeparatorChar));
                item.Sha1 = library.Sha1 ?? "";
                item.Size = library.Size;
                item.Urls = Mirror.Urls(library.Url, config.MirrorFirst);
                items.Add(item);
            }
            if (items.Count == 0)
            {
                progress.Report(to, "无需下载依赖库");
                return;
            }
            progress.SetStage("下载游戏依赖库 (" + items.Count + " 个文件)");
            Http.DownloadMany(items, config.DownloadThreads, delegate(int done, int total, string name)
            {
                double fraction = from + (to - from) * ((double)done / total);
                progress.Report(fraction, "依赖库 " + done + "/" + total);
            }, progress.Token);
        }

        public static void ExtractNatives(AppConfig config, List<LibraryFile> libraries, string nativesDir, ProgressSink progress)
        {
            Util.EnsureDirectory(nativesDir);
            List<LibraryFile> natives = new List<LibraryFile>();
            for (int i = 0; i < libraries.Count; i++) if (libraries[i].Native) natives.Add(libraries[i]);
            if (natives.Count == 0) return;
            progress.SetStage("解压本地运行库");
            for (int i = 0; i < natives.Count; i++)
            {
                LibraryFile native = natives[i];
                string jar = Path.Combine(config.LibrariesDir, native.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(jar)) continue;
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(jar))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue;
                            string full = entry.FullName.Replace('\\', '/');
                            if (full.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;
                            bool excluded = false;
                            for (int e = 0; e < native.Excludes.Count; e++)
                            {
                                if (full.StartsWith(native.Excludes[e], StringComparison.OrdinalIgnoreCase))
                                {
                                    excluded = true;
                                    break;
                                }
                            }
                            if (excluded) continue;
                            string target = Path.Combine(nativesDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                            Util.EnsureParent(target);
                            try { entry.ExtractToFile(target, true); }
                            catch (IOException) { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("解压 " + native.Artifact + " 本地库失败: " + ex.Message);
                }
            }
        }

        private static string NativeClassifier(JMap library)
        {
            JMap natives = library.Map("natives");
            if (natives.Count > 0)
            {
                string classifier = natives.Str("windows", "");
                if (!string.IsNullOrEmpty(classifier))
                {
                    classifier = classifier.Replace("${arch}", Util.IsWindows64 ? "64" : "32");
                    return classifier;
                }
            }
            string name = library.Str("name", "");
            if (name.IndexOf("natives-windows", StringComparison.OrdinalIgnoreCase) >= 0) return "natives-windows";
            return "";
        }

        private static bool NativeCompatible(string classifier)
        {
            if (string.IsNullOrEmpty(classifier)) return false;
            string value = classifier.ToLowerInvariant();
            if (value.Contains("natives-linux") || value.Contains("natives-macos") || value.Contains("natives-osx")) return false;
            if (value.Contains("arm64") || value.Contains("aarch64")) return false;
            if (value.Contains("natives-windows-x86") || value.EndsWith("-x86", StringComparison.Ordinal))
                return !Util.IsWindows64;
            return true;
        }
    }

    public sealed class MavenName
    {
        public string Group = "";
        public string Artifact = "";
        public string Version = "";
        public string Classifier = "";
        public string Extension = "jar";

        public string Path
        {
            get
            {
                string file = Artifact + "-" + Version + (string.IsNullOrEmpty(Classifier) ? "" : "-" + Classifier) + "." + Extension;
                return Group.Replace('.', '/') + "/" + Artifact + "/" + Version + "/" + file;
            }
        }

        public string PathWithClassifier(string classifier)
        {
            string file = Artifact + "-" + Version + "-" + classifier + "." + Extension;
            return Group.Replace('.', '/') + "/" + Artifact + "/" + Version + "/" + file;
        }

        public static bool TryParse(string value, out MavenName name)
        {
            name = new MavenName();
            if (string.IsNullOrEmpty(value)) return false;
            string text = value.Trim();
            int at = text.IndexOf('@');
            if (at > 0)
            {
                name.Extension = text.Substring(at + 1);
                text = text.Substring(0, at);
            }
            string[] parts = text.Split(':');
            if (parts.Length < 3) return false;
            name.Group = parts[0];
            name.Artifact = parts[1];
            name.Version = parts[2];
            if (parts.Length > 3) name.Classifier = parts[3];
            return true;
        }
    }

    public static class Rules
    {
        public static bool Allowed(IList<object> rules)
        {
            if (rules == null || rules.Count == 0) return true;
            bool allowed = false;
            for (int i = 0; i < rules.Count; i++)
            {
                JMap rule = new JMap(rules[i]);
                if (!Matches(rule)) continue;
                string action = rule.Str("action", "allow");
                if (string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase)) allowed = true;
                else if (string.Equals(action, "disallow", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return allowed;
        }

        private static bool Matches(JMap rule)
        {
            JMap os = rule.Map("os");
            if (os.Count > 0)
            {
                string name = os.Str("name", "");
                if (!string.IsNullOrEmpty(name) && !string.Equals(name, "windows", StringComparison.OrdinalIgnoreCase)) return false;
                string arch = os.Str("arch", "");
                if (!string.IsNullOrEmpty(arch))
                {
                    string current = Util.IsWindows64 ? "x86_64" : "x86";
                    if (!string.Equals(arch, current, StringComparison.OrdinalIgnoreCase)) return false;
                }
                string version = os.Str("version", "");
                if (!string.IsNullOrEmpty(version))
                {
                    try
                    {
                        if (!Regex.IsMatch(Environment.OSVersion.VersionString, version)) return false;
                    }
                    catch { return false; }
                }
            }

            JMap features = rule.Map("features");
            if (features.Count > 0)
            {
                foreach (KeyValuePair<string, object> pair in features.Raw)
                {
                    bool expected = Convert.ToBoolean(pair.Value);
                    if (expected) return false; // The launcher does not enable optional features by default.
                }
            }
            return true;
        }
    }
}
