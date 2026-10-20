using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CubeLauncher.Core
{
    public sealed class NeoForgeBuild
    {
        public string Minecraft = "";
        public string Version = "";
        public string Artifact = "neoforge";
        public bool Stable;
        public string Label = "";

        public override string ToString()
        {
            return string.IsNullOrEmpty(Label) ? Version : Label;
        }
    }

    public static class NeoForgeService
    {
        public static List<NeoForgeBuild> LoadVersions(AppConfig config, string mc)
        {
            List<NeoForgeBuild> result = new List<NeoForgeBuild>();
            string prefix = VersionPrefix(mc);
            if (string.IsNullOrEmpty(prefix)) return result;

            ParseMetadata(config, mc, prefix, "neoforge", result);
            // NeoForge for 1.20.1 was published under the legacy "forge" artifact.
            if (result.Count == 0 && string.Equals(mc, "1.20.1", StringComparison.OrdinalIgnoreCase))
                ParseMetadata(config, mc, "47.1.", "forge", result);

            result.Sort(delegate(NeoForgeBuild a, NeoForgeBuild b)
            {
                if (a.Stable != b.Stable) return a.Stable ? -1 : 1;
                return Util.CompareVersion(b.Version, a.Version);
            });
            return result;
        }

        public static string VersionPrefix(string mc)
        {
            if (string.IsNullOrEmpty(mc)) return "";
            if (string.Equals(mc, "1.20.1", StringComparison.OrdinalIgnoreCase)) return "47.1.";
            string[] parts = mc.Split('.');
            if (parts.Length >= 3 && string.Equals(parts[0], "1", StringComparison.OrdinalIgnoreCase))
                return parts[1] + "." + parts[2] + ".";
            if (parts.Length >= 2 && string.Equals(parts[0], "1", StringComparison.OrdinalIgnoreCase))
                return parts[1] + ".0.";
            return mc + ".";
        }

        private static void ParseMetadata(AppConfig config, string mc, string prefix, string artifact, List<NeoForgeBuild> result)
        {
            List<string> urls = Mirror.NeoForgeMetadata(config.MirrorFirst, artifact);
            for (int i = 0; i < urls.Count && result.Count == 0; i++)
            {
                try
                {
                    string xml = Http.GetText(new List<string>() { urls[i] }, 30000);
                    XDocument document = XDocument.Parse(xml);
                    foreach (XElement element in document.Descendants("version"))
                    {
                        string version = element.Value;
                        if (!version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        NeoForgeBuild build = new NeoForgeBuild();
                        build.Minecraft = mc;
                        build.Version = version;
                        build.Artifact = artifact;
                        build.Stable = version.IndexOf("beta", StringComparison.OrdinalIgnoreCase) < 0;
                        build.Label = version + (build.Stable ? "  稳定" : "  测试");
                        result.Add(build);
                    }
                    if (result.Count == 0)
                        Log.Warn("NeoForge 元数据源缺少 " + mc + " (" + prefix + ")，尝试下一个源");
                }
                catch (Exception ex)
                {
                    Log.Warn("NeoForge 版本源失败 " + Http.ShortUrl(urls[i]) + ": " + ex.Message);
                }
            }
        }

        public static string Install(AppConfig config, VersionManager versions, string mc, NeoForgeBuild build, ProgressSink progress)
        {
            if (build == null) throw new Exception("请选择 NeoForge 版本");
            progress.SetStage("安装 Minecraft " + mc + " 原版基础");
            versions.InstallVanilla(mc, progress);

            JMap baseJson = JMap.Parse(File.ReadAllText(Path.Combine(config.VersionsDir, mc, mc + ".json"), Encoding.UTF8));
            int javaMajor = baseJson.Map("javaVersion").Int("majorVersion", 17);
            JavaInfo java = JavaManager.Ensure(config, javaMajor, progress);

            string cache = Path.Combine(config.CacheDir, "neoforge");
            Util.EnsureDirectory(cache);
            string installer = Path.Combine(cache, "neoforge-" + build.Version + "-installer.jar");
            if (!File.Exists(installer) || new FileInfo(installer).Length < 100000)
            {
                DownloadItem item = new DownloadItem();
                item.Name = "NeoForge " + build.Version + " 安装器";
                item.Destination = installer;
                item.Urls = Mirror.NeoForgeInstaller(build.Version, build.Artifact, config.MirrorFirst);
                progress.SetStage("下载 NeoForge 安装器 (BMCLAPI 国内镜像)");
                Http.DownloadOne(item, progress.Token, null);
            }

            progress.SetStage("运行 NeoForge 安装器，正在应用补丁");
            Log.Info("NeoForge 安装器: " + installer);
            installer = PrepareInstaller(config, installer, progress);
            if (config.MirrorFirst) PreDownloadLibraries(config, installer, progress);
            string args = "-jar " + Util.Quote(installer) + " --installClient " + Util.Quote(config.GameDir) +
                          " --mirror " + Util.Quote(Mirror.BmclApi + "/maven/");
            ProcessResult first;
            try
            {
                first = ProcessRunner.Run(java.Path, args, config.GameDir, 45 * 60 * 1000, delegate(string line)
                {
                    if (string.IsNullOrEmpty(line)) return;
                    if (line.StartsWith("  Data ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Extracting ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Considering ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Downloading ", StringComparison.OrdinalIgnoreCase)) return;
                    Log.Info("[NeoForge] " + line);
                    if (line.Length < 140) progress.SetStage("NeoForge 安装器: " + line);
                });
            }
            catch (Exception ex)
            {
                Log.Warn("NeoForge 安装器镜像参数失败: " + ex.Message);
                first = new ProcessResult();
                first.ExitCode = -1;
            }

            if (first.ExitCode != 0)
            {
                Log.Warn("NeoForge 安装器返回 " + first.ExitCode + "，尝试不使用镜像参数");
                string args2 = "-jar " + Util.Quote(installer) + " --installClient " + Util.Quote(config.GameDir);
                ProcessResult second = ProcessRunner.Run(java.Path, args2, config.GameDir, 45 * 60 * 1000, delegate(string line)
                {
                    if (string.IsNullOrEmpty(line)) return;
                    if (line.StartsWith("  Data ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Extracting ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Considering ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Downloading ", StringComparison.OrdinalIgnoreCase)) return;
                    Log.Info("[NeoForge] " + line);
                    if (line.Length < 140) progress.SetStage("NeoForge 安装器: " + line);
                });
                if (second.ExitCode != 0)
                    throw new Exception("NeoForge 安装器失败 (退出码 " + second.ExitCode + ")，请查看日志");
            }

            string id = FindInstalled(config, mc, build.Version);
            if (string.IsNullOrEmpty(id))
                throw new Exception("NeoForge 安装器结束，但没有找到对应版本 JSON，请查看 " + Path.Combine(config.GameDir, "installer.log"));

            Util.EnsureDirectory(config.InstanceDir(id));
            Util.EnsureDirectory(config.ModsDir(id));
            progress.SetStage("检查 NeoForge 运行库");
            versions.EnsureRuntime(id, progress);
            Log.Info("NeoForge " + build.Version + " 安装完成: " + id);
            return id;
        }

        private static string PrepareInstaller(AppConfig config, string installer, ProgressSink progress)
        {
            string existing = Path.Combine(config.LibrariesDir, "net", "neoforged", "JarJarFileSystems", "0.4.0", "JarJarFileSystems-0.4.0.jar");
            if (File.Exists(existing) && new FileInfo(existing).Length == 31835) return installer;

            string patched = installer.Substring(0, installer.Length - 4) + "-patched.jar";
            if (File.Exists(patched)) File.Delete(patched);

            try
            {
                progress.SetStage("修补 NeoForge 安装器：JarJarFileSystems 0.4.0 镜像缺失");
                File.Copy(installer, patched, true);
                bool changed = false;
                using (ZipArchive archive = ZipFile.Open(patched, ZipArchiveMode.Update))
                {
                    string[] names = new string[] { "install_profile.json", "version.json" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        ZipArchiveEntry entry = archive.GetEntry(names[i]);
                        if (entry == null) continue;
                        string text;
                        using (Stream stream = entry.Open())
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                            text = reader.ReadToEnd();
                        JMap root = JMap.Parse(text);
                        bool fileChanged = PatchJarJarRaw(root.Raw);
                        if (!fileChanged) continue;
                        string json = Json.Stringify(root.Raw);
                        entry.Delete();
                        ZipArchiveEntry created = archive.CreateEntry(names[i], CompressionLevel.Optimal);
                        using (Stream stream = created.Open())
                        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                            writer.Write(json);
                        changed = true;
                    }
                }
                if (!changed)
                {
                    if (File.Exists(patched)) File.Delete(patched);
                    return installer;
                }
                Log.Warn("NeoForge 安装器已修补为 JarJarFileSystems 0.4.1 兼容版本");
                return patched;
            }
            catch (Exception ex)
            {
                Log.Warn("NeoForge 安装器修补失败: " + ex.Message);
                if (File.Exists(patched)) { try { File.Delete(patched); } catch { } }
                return installer;
            }
        }

        private static void CleanupDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { }
        }

        public static void PreDownloadLibraries(AppConfig config, string installer, ProgressSink progress)
        {
            List<DownloadItem> items = new List<DownloadItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(installer))
                {
                    CollectLibraries(config, archive, "install_profile.json", items, seen);
                    CollectLibraries(config, archive, "version.json", items, seen);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("读取 NeoForge 安装配置失败，跳过预下载: " + ex.Message);
                return;
            }

            if (items.Count == 0) return;
            progress.SetStage("从 BMCLAPI 预下载 NeoForge 依赖库 (" + items.Count + " 个文件)");
            try
            {
                Http.DownloadMany(items, Math.Max(8, config.DownloadThreads + 4), delegate(int done, int total, string name)
                {
                    if (done != total && done % 8 != 0) return;
                    progress.Report(0.35 * done / Math.Max(1, total), "NeoForge 依赖 " + done + "/" + total);
                }, progress.Token);
                Log.Info("NeoForge 依赖库预下载完成");
            }
            catch (Exception ex)
            {
                Log.Warn("NeoForge 依赖库预下载失败，安装器将尝试官方源: " + ex.Message);
            }
        }

        private static void CollectLibraries(AppConfig config, ZipArchive archive, string entryName, List<DownloadItem> items, HashSet<string> seen)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            if (entry == null) return;
            string text;
            using (Stream stream = entry.Open())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                text = reader.ReadToEnd();
            JMap root = JMap.Parse(text);
            List<JMap> libraries = root.Maps("libraries");
            for (int i = 0; i < libraries.Count; i++)
            {
                JMap library = libraries[i];
                JMap artifact = library.Map("downloads").Map("artifact");
                string name = library.Str("name", "");
                MavenName maven;
                if (!MavenName.TryParse(name, out maven)) continue;
                string path = artifact.Str("path", maven.Path);
                if (string.IsNullOrEmpty(path)) continue;
                string destination = Path.Combine(config.LibrariesDir, path.Replace('/', Path.DirectorySeparatorChar));
                if (!seen.Add(destination)) continue;
                // BMCLAPI currently misses JarJarFileSystems 0.4.0; it is repaired after installation.
                if (path.IndexOf("JarJarFileSystems/0.4.0", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                DownloadItem item = new DownloadItem();
                item.Name = "NeoForge 依赖 " + maven.Artifact;
                item.Destination = destination;
                item.Sha1 = artifact.Str("sha1", "");
                item.Size = artifact.Long("size", 0);
                item.Urls = new List<string>();
                item.Urls.Add(Mirror.BmclApi + "/maven/" + path);
                string official = artifact.Str("url", "");
                if (!string.IsNullOrEmpty(official) && !item.Urls.Contains(official)) item.Urls.Add(official);
                items.Add(item);
            }
        }

        private static bool PatchJarJarRaw(object raw)
        {
            Dictionary<string, object> root = raw as Dictionary<string, object>;
            if (root == null) return false;
            object librariesValue;
            if (!root.TryGetValue("libraries", out librariesValue)) return false;
            IList<object> libraries = librariesValue as IList<object>;
            if (libraries == null) return false;
            bool changed = false;
            for (int i = 0; i < libraries.Count; i++)
            {
                Dictionary<string, object> library = libraries[i] as Dictionary<string, object>;
                if (library == null) continue;
                object nameValue;
                if (!library.TryGetValue("name", out nameValue)) continue;
                string name = Convert.ToString(nameValue);
                if (name.IndexOf("JarJarFileSystems:0.4.0", StringComparison.OrdinalIgnoreCase) < 0) continue;
                library["name"] = name.Replace("JarJarFileSystems:0.4.0", "JarJarFileSystems:0.4.1");
                object downloadsValue;
                if (!library.TryGetValue("downloads", out downloadsValue)) continue;
                Dictionary<string, object> downloads = downloadsValue as Dictionary<string, object>;
                if (downloads == null) continue;
                object artifactValue;
                if (!downloads.TryGetValue("artifact", out artifactValue)) continue;
                Dictionary<string, object> artifact = artifactValue as Dictionary<string, object>;
                if (artifact == null) continue;
                artifact["path"] = "net/neoforged/JarJarFileSystems/0.4.1/JarJarFileSystems-0.4.1.jar";
                artifact["url"] = "https://maven.neoforged.net/releases/net/neoforged/JarJarFileSystems/0.4.1/JarJarFileSystems-0.4.1.jar";
                artifact["sha1"] = "78f59f89defcd032ed788b151ca6a0d40ace796a";
                artifact["size"] = 31807;
                changed = true;
            }
            return changed;
        }

        private static string FindInstalled(AppConfig config, string mc, string version)
        {            string[] candidates = new string[]
            {
                "neoforge-" + version,
                mc + "-neoforge-" + version,
                "1.20.1-neoforge-" + version
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                string json = Path.Combine(config.VersionsDir, candidates[i], candidates[i] + ".json");
                if (File.Exists(json)) return candidates[i];
            }
            if (Directory.Exists(config.VersionsDir))
            {
                string[] dirs = Directory.GetDirectories(config.VersionsDir, "*neoforge*");
                for (int i = 0; i < dirs.Length; i++)
                {
                    string id = Path.GetFileName(dirs[i]);
                    if (id.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        File.Exists(Path.Combine(dirs[i], id + ".json")))
                        return id;
                }
            }
            return "";
        }
    }
}
