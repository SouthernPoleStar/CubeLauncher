using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace CubeLauncher.Core
{
    public sealed class VersionEntry
    {
        public string Id;
        public string Type;
        public string Url;
        public string ReleaseTime;
        public string Sha1;
        public int ComplianceLevel;

        public bool IsRelease
        {
            get { return string.Equals(Type, "release", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsSnapshot
        {
            get { return string.Equals(Type, "snapshot", StringComparison.OrdinalIgnoreCase); }
        }

        public string TypeLabel
        {
            get
            {
                if (IsRelease) return "正式版";
                if (IsSnapshot) return "快照";
                if (string.Equals(Type, "old_beta", StringComparison.OrdinalIgnoreCase)) return "远古 Beta";
                if (string.Equals(Type, "old_alpha", StringComparison.OrdinalIgnoreCase)) return "远古 Alpha";
                return Type;
            }
        }

        public override string ToString()
        {
            return Id + "    " + TypeLabel;
        }
    }

    public sealed class InstalledVersion
    {
        public string Id;
        public string Type;
        public string Loader;
        public string LoaderVersion;
        public string Minecraft;
        public string JsonPath;
        public string JarPath;
        public string Directory;
        public DateTime InstalledAt;
        public long Size;

        public string LoaderLabel
        {
            get
            {
                if (string.Equals(Loader, "fabric", StringComparison.OrdinalIgnoreCase)) return "Fabric";
                if (string.Equals(Loader, "forge", StringComparison.OrdinalIgnoreCase)) return "Forge";
                if (string.Equals(Loader, "quilt", StringComparison.OrdinalIgnoreCase)) return "Quilt";
                if (string.Equals(Loader, "neoforge", StringComparison.OrdinalIgnoreCase)) return "NeoForge";
                return "原版";
            }
        }

        public string DisplayName
        {
            get
            {
                if (string.Equals(Loader, "vanilla", StringComparison.OrdinalIgnoreCase)) return Id;
                return Minecraft + " · " + LoaderLabel + (string.IsNullOrEmpty(LoaderVersion) ? "" : " " + LoaderVersion);
            }
        }
    }

    public sealed class ResolvedVersion
    {
        public string Id;
        public string BaseId;
        public string Type;
        public string MainClass;
        public int JavaMajor;
        public JMap Json;
        public List<LibraryFile> Libraries = new List<LibraryFile>();
        public string ClientJarPath;
        public string NativesDir;
        public string AssetIndexId;
        public bool IsLoader;
    }

    public sealed class VersionManager
    {
        private readonly AppConfig _config;
        private List<VersionEntry> _manifest;

        public VersionManager(AppConfig config)
        {
            _config = config;
        }

        public IList<VersionEntry> Manifest
        {
            get { return _manifest; }
        }

        public void LoadManifest(ProgressSink progress, bool force)
        {
            string cache = Path.Combine(_config.CacheDir, "version_manifest_v2.json");
            if (!force && File.Exists(cache))
            {
                TimeSpan age = DateTime.Now - File.GetLastWriteTime(cache);
                if (age.TotalHours < 6)
                {
                    try
                    {
                        ParseManifest(File.ReadAllText(cache, Encoding.UTF8));
                        return;
                    }
                    catch { }
                }
            }

            progress.SetStage("连接版本清单 (BMCLAPI 国内镜像)");
            string text = Http.GetText(Mirror.VersionManifest(_config.MirrorFirst), 30000);
            ParseManifest(text);
            try
            {
                Util.EnsureDirectory(_config.CacheDir);
                File.WriteAllText(cache, text, new UTF8Encoding(false));
            }
            catch { }
        }

        private void ParseManifest(string text)
        {
            JMap root = JMap.Parse(text);
            _manifest = new List<VersionEntry>();
            List<JMap> versions = root.Maps("versions");
            for (int i = 0; i < versions.Count; i++)
            {
                JMap item = versions[i];
                VersionEntry entry = new VersionEntry();
                entry.Id = item.Str("id");
                entry.Type = item.Str("type");
                entry.Url = item.Str("url");
                entry.ReleaseTime = item.Str("releaseTime");
                entry.Sha1 = item.Str("sha1");
                entry.ComplianceLevel = item.Int("complianceLevel");
                if (!string.IsNullOrEmpty(entry.Id)) _manifest.Add(entry);
            }
            if (_manifest == null || _manifest.Count == 0) throw new Exception("版本清单为空");
            Log.Info("版本清单加载完成，共 " + _manifest.Count + " 个版本");
        }

        public VersionEntry FindVersion(string id)
        {
            if (_manifest == null) return null;
            for (int i = 0; i < _manifest.Count; i++)
                if (string.Equals(_manifest[i].Id, id, StringComparison.OrdinalIgnoreCase)) return _manifest[i];
            return null;
        }

        public List<InstalledVersion> GetInstalled()
        {
            List<InstalledVersion> result = new List<InstalledVersion>();
            if (!Directory.Exists(_config.VersionsDir)) return result;
            string[] dirs = Directory.GetDirectories(_config.VersionsDir);
            for (int i = 0; i < dirs.Length; i++)
            {
                string id = Path.GetFileName(dirs[i]);
                string jsonPath = Path.Combine(dirs[i], id + ".json");
                if (!File.Exists(jsonPath)) continue;
                try
                {
                    JMap json = JMap.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                    InstalledVersion version = new InstalledVersion();
                    version.Id = json.Str("id", id);
                    version.Type = json.Str("type", "release");
                    version.Minecraft = json.Str("inheritsFrom", version.Id);
                    version.JsonPath = jsonPath;
                    version.Directory = dirs[i];
                    version.JarPath = Path.Combine(dirs[i], id + ".jar");
                    version.InstalledAt = Directory.GetCreationTime(dirs[i]);
                    version.Loader = DetectLoader(version.Id, json);
                    version.LoaderVersion = DetectLoaderVersion(version.Id, version.Loader);
                    if (!File.Exists(version.JarPath))
                    {
                        string parentJar = Path.Combine(_config.VersionsDir, version.Minecraft, version.Minecraft + ".jar");
                        if (File.Exists(parentJar)) version.JarPath = parentJar;
                    }
                    try
                    {
                        if (File.Exists(version.JarPath)) version.Size = new FileInfo(version.JarPath).Length;
                    }
                    catch { }
                    result.Add(version);
                }
                catch (Exception ex)
                {
                    Log.Warn("跳过损坏的版本 " + id + ": " + ex.Message);
                }
            }
            result.Sort(delegate(InstalledVersion a, InstalledVersion b)
            {
                if (string.Equals(a.Loader, "vanilla", StringComparison.OrdinalIgnoreCase) != string.Equals(b.Loader, "vanilla", StringComparison.OrdinalIgnoreCase))
                    return string.Equals(a.Loader, "vanilla", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
                return Util.CompareVersion(b.Minecraft, a.Minecraft);
            });
            return result;
        }

        public static string DetectLoader(string id, JMap json)
        {
            string lower = (id ?? "").ToLowerInvariant();
            if (lower.Contains("neoforge")) return "neoforge";
            if (lower.Contains("forge")) return "forge";
            if (lower.Contains("fabric")) return "fabric";
            if (lower.Contains("quilt")) return "quilt";
            string main = json.Str("mainClass", "").ToLowerInvariant();
            if (main.Contains("neoforge")) return "neoforge";
            if (main.Contains("forge")) return "forge";
            if (main.Contains("fabric")) return "fabric";
            if (main.Contains("quilt")) return "quilt";
            return "vanilla";
        }

        public static string DetectLoaderVersion(string id, string loader)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (string.Equals(loader, "forge", StringComparison.OrdinalIgnoreCase))
            {
                int index = id.IndexOf("-forge-", StringComparison.OrdinalIgnoreCase);
                if (index >= 0) return id.Substring(index + 7);
            }
            if (string.Equals(loader, "neoforge", StringComparison.OrdinalIgnoreCase))
            {
                if (id.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase)) return id.Substring("neoforge-".Length);
                int index = id.IndexOf("-neoforge-", StringComparison.OrdinalIgnoreCase);
                if (index >= 0) return id.Substring(index + 10);
            }
            if (string.Equals(loader, "fabric", StringComparison.OrdinalIgnoreCase) && id.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
            {
                string rest = id.Substring("fabric-loader-".Length);
                int dash = rest.IndexOf('-');
                if (dash > 0) return rest.Substring(0, dash);
            }
            if (string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase) && id.StartsWith("quilt-loader-", StringComparison.OrdinalIgnoreCase))
            {
                string rest = id.Substring("quilt-loader-".Length);
                int dash = rest.IndexOf('-');
                if (dash > 0) return rest.Substring(0, dash);
            }
            return "";
        }

        public JMap GetVersionJson(string id, VersionEntry entry, bool saveInstalled, ProgressSink progress)
        {
            string installed = Path.Combine(_config.VersionsDir, Util.SafeFileName(id), Util.SafeFileName(id) + ".json");
            if (File.Exists(installed))
            {
                try { return JMap.Parse(File.ReadAllText(installed, Encoding.UTF8)); }
                catch { }
            }
            string cache = Path.Combine(_config.CacheDir, "versions", Util.SafeFileName(id) + ".json");
            if (File.Exists(cache))
            {
                try { return JMap.Parse(File.ReadAllText(cache, Encoding.UTF8)); }
                catch { }
            }

            if (progress != null) progress.SetStage("获取 " + id + " 版本信息");
            List<string> urls = Mirror.VersionJson(id, entry == null ? "" : entry.Url, _config.MirrorFirst);
            string text = Http.GetText(urls, 30000);
            JMap json = JMap.Parse(text);
            try
            {
                Util.EnsureDirectory(Path.GetDirectoryName(cache));
                File.WriteAllText(cache, text, new UTF8Encoding(false));
            }
            catch { }
            if (saveInstalled)
            {
                Util.EnsureDirectory(Path.GetDirectoryName(installed));
                File.WriteAllText(installed, text, new UTF8Encoding(false));
            }
            return json;
        }

        public void InstallVanilla(string id, ProgressSink progress)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("版本号不能为空");
            if (_manifest == null) LoadManifest(progress, false);
            VersionEntry entry = FindVersion(id);
            if (entry == null) throw new Exception("找不到版本 " + id);

            progress.SetStage("安装原版 " + id);
            JMap json = GetVersionJson(id, entry, true, progress);
            string versionDir = Path.Combine(_config.VersionsDir, Util.SafeFileName(id));
            Util.EnsureDirectory(versionDir);
            Util.EnsureDirectory(_config.InstanceDir(id));
            Util.EnsureDirectory(_config.ModsDir(id));

            EnsureRuntime(id, progress);
            progress.Report(1, id + " 安装完成");
            Log.Info("原版 " + id + " 安装完成");
        }

        public ResolvedVersion Resolve(string id, ProgressSink progress)
        {
            JMap merged = ResolveJson(id, new List<string>());
            ResolvedVersion version = new ResolvedVersion();
            version.Id = id;
            version.Json = merged;
            version.BaseId = merged.Str("inheritsFrom", id);
            version.Type = merged.Str("type", "release");
            version.MainClass = merged.Str("mainClass", "");
            version.JavaMajor = merged.Map("javaVersion").Int("majorVersion", 8);
            version.Libraries = LibraryResolver.Resolve(merged);
            version.IsLoader = !string.Equals(version.BaseId, id, StringComparison.OrdinalIgnoreCase) ||
                              version.Libraries.Count > 100;

            string baseJar = Path.Combine(_config.VersionsDir, Util.SafeFileName(version.BaseId), Util.SafeFileName(version.BaseId) + ".jar");
            version.ClientJarPath = baseJar;
            if (!File.Exists(baseJar))
            {
                string ownJar = Path.Combine(_config.VersionsDir, Util.SafeFileName(id), Util.SafeFileName(id) + ".jar");
                if (File.Exists(ownJar)) version.ClientJarPath = ownJar;
            }
            version.NativesDir = Path.Combine(_config.VersionsDir, Util.SafeFileName(id), "natives");
            JMap assetIndex = merged.Map("assetIndex");
            version.AssetIndexId = assetIndex.Str("id", merged.Str("assets", "legacy"));
            return version;
        }

        private JMap ResolveJson(string id, List<string> stack)
        {
            if (stack.Contains(id)) throw new Exception("版本继承出现循环: " + string.Join(" -> ", stack.ToArray()));
            stack.Add(id);

            string path = Path.Combine(_config.VersionsDir, Util.SafeFileName(id), Util.SafeFileName(id) + ".json");
            if (!File.Exists(path))
                throw new Exception("版本 " + id + " 尚未安装，请先安装该版本");
            JMap child;
            try { child = JMap.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception ex) { throw new Exception("版本 JSON 损坏: " + id + " (" + ex.Message + ")"); }

            string parentId = child.Str("inheritsFrom", "");
            JMap parent = null;
            if (!string.IsNullOrEmpty(parentId)) parent = ResolveJson(parentId, stack);
            stack.RemoveAt(stack.Count - 1);
            if (parent == null) return child;

            JMap merged = new JMap();
            foreach (KeyValuePair<string, object> pair in parent.Raw) merged.Set(pair.Key, pair.Value);
            foreach (KeyValuePair<string, object> pair in child.Raw)
            {
                if (string.Equals(pair.Key, "libraries", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "arguments", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, "minecraftArguments", StringComparison.OrdinalIgnoreCase))
                    continue;
                merged.Set(pair.Key, pair.Value);
            }

            List<JMap> parentLibs = parent.Maps("libraries");
            List<JMap> childLibs = child.Maps("libraries");
            List<object> libraries = new List<object>();
            for (int i = 0; i < parentLibs.Count; i++) libraries.Add(parentLibs[i].Raw);
            for (int i = 0; i < childLibs.Count; i++) libraries.Add(childLibs[i].Raw);
            merged.Set("libraries", libraries);

            JMap parentArgs = parent.Map("arguments");
            JMap childArgs = child.Map("arguments");
            JMap arguments = new JMap();
            MergeArgumentList(arguments, "game", parentArgs, childArgs);
            MergeArgumentList(arguments, "jvm", parentArgs, childArgs);
            if (arguments.Count > 0) merged.Set("arguments", arguments.Raw);

            if (child.Has("minecraftArguments")) merged.Set("minecraftArguments", child.Get("minecraftArguments"));
            else if (parent.Has("minecraftArguments")) merged.Set("minecraftArguments", parent.Get("minecraftArguments"));
            return merged;
        }

        private static void MergeArgumentList(JMap target, string key, JMap parentArgs, JMap childArgs)
        {
            IList<object> parentList = parentArgs.List(key);
            IList<object> childList = childArgs.List(key);
            if ((parentList == null || parentList.Count == 0) && (childList == null || childList.Count == 0)) return;
            List<object> list = new List<object>();
            if (parentList != null) for (int i = 0; i < parentList.Count; i++) list.Add(parentList[i]);
            if (childList != null) for (int i = 0; i < childList.Count; i++) list.Add(childList[i]);
            target.Set(key, list);
        }

        public ResolvedVersion EnsureRuntime(string id, ProgressSink progress)
        {
            ResolvedVersion version = Resolve(id, progress);
            progress.SetStage("检查 " + id + " 游戏文件");

            JMap downloads = version.Json.Map("downloads");
            JMap client = downloads.Map("client");
            if (client.Count > 0)
            {
                string url = client.Str("url", "");
                if (!string.IsNullOrEmpty(url))
                {
                    Util.EnsureDirectory(Path.GetDirectoryName(version.ClientJarPath));
                    DownloadItem item = new DownloadItem();
                    item.Name = "Minecraft 客户端 " + version.BaseId;
                    item.Destination = version.ClientJarPath;
                    item.Urls = Mirror.Urls(url, _config.MirrorFirst);
                    item.Sha1 = client.Str("sha1", "");
                    item.Size = client.Long("size", 0);
                    progress.SetStage("下载 Minecraft 客户端 " + version.BaseId);
                    Http.DownloadOne(item, progress.Token, null);
                }
            }
            if (!File.Exists(version.ClientJarPath))
                throw new Exception("缺少客户端 jar: " + version.ClientJarPath);

            LibraryResolver.DownloadLibraries(_config, version.Libraries, progress, 0.12, 0.52);
            LibraryResolver.ExtractNatives(_config, version.Libraries, version.NativesDir, progress);

            JMap assetIndex = version.Json.Map("assetIndex");
            if (assetIndex.Count > 0)
            {
                AssetInstaller.Ensure(_config, assetIndex, version.AssetIndexId, progress, 0.55, 0.98);
            }
            else
            {
                progress.Report(0.98, "该版本不需要资源索引");
            }
            progress.Report(1, "游戏文件就绪");
            return version;
        }
    }

    public static class AssetInstaller
    {
        public static void Ensure(AppConfig config, JMap assetIndex, string versionId, ProgressSink progress, double from, double to)
        {
            string indexId = assetIndex.Str("id", versionId);
            string indexPath = Path.Combine(config.AssetsDir, "indexes", indexId + ".json");
            string url = assetIndex.Str("url", "");
            if (!string.IsNullOrEmpty(url))
            {
                DownloadItem item = new DownloadItem();
                item.Name = "资源索引 " + indexId;
                item.Destination = indexPath;
                item.Urls = Mirror.Urls(url, config.MirrorFirst);
                item.Sha1 = assetIndex.Str("sha1", "");
                item.Size = assetIndex.Long("size", 0);
                if (!File.Exists(indexPath) || (!string.IsNullOrEmpty(item.Sha1) && !string.Equals(Util.Sha1File(indexPath), item.Sha1, StringComparison.OrdinalIgnoreCase)))
                {
                    progress.SetStage("下载资源索引 " + indexId);
                    Http.DownloadOne(item, progress.Token, null);
                }
            }
            if (!File.Exists(indexPath))
            {
                progress.Report(to, "无资源索引");
                return;
            }

            JMap root = JMap.Parse(File.ReadAllText(indexPath, Encoding.UTF8));
            JMap objects = root.Map("objects");
            bool virtualAssets = root.Bool("virtual", false);
            bool mapResources = root.Bool("map_to_resources", false);
            string virtualRoot = Path.Combine(config.AssetsDir, virtualAssets ? "virtual" : "objects", indexId);

            List<DownloadItem> items = new List<DownloadItem>();
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, object> pair in objects.Raw)
            {
                JMap obj = new JMap(pair.Value);
                string hash = obj.Str("hash", "");
                if (string.IsNullOrEmpty(hash)) continue;
                string rel = hash.Substring(0, 2) + "/" + hash;
                string dest = Path.Combine(config.AssetsDir, "objects", rel.Substring(0, 2), rel.Substring(3));
                DownloadItem item = new DownloadItem();
                item.Name = "资源 " + pair.Key;
                item.Destination = dest;
                item.Urls = Mirror.Urls("https://resources.download.minecraft.net/" + rel, config.MirrorFirst);
                item.Sha1 = hash;
                item.Size = obj.Long("size", 0);
                item.Quiet = true;
                items.Add(item);
                names.Add(pair.Key);
            }

            progress.SetStage("下载游戏资源 (" + items.Count + " 个文件)");
            int assetWorkers = Math.Max(24, config.DownloadThreads * 2);
            Http.DownloadMany(items, assetWorkers, delegate(int done, int total, string name)
            {
                if (done != total && done % 12 != 0) return;
                double fraction = from + (to - from) * ((double)done / total);
                progress.Report(fraction, "资源 " + done + "/" + total);
            }, progress.Token);

            if (virtualAssets || mapResources)
            {
                progress.SetStage("整理旧版资源目录");
                for (int i = 0; i < items.Count; i++)
                {
                    string source = items[i].Destination;
                    if (!File.Exists(source)) continue;
                    string dest;
                    if (virtualAssets)
                        dest = Path.Combine(virtualRoot, names[i].Replace('/', Path.DirectorySeparatorChar));
                    else
                        dest = Path.Combine(config.GameDir, "resources", names[i].Replace('/', Path.DirectorySeparatorChar));
                    Util.EnsureParent(dest);
                    try { if (!File.Exists(dest)) File.Copy(source, dest); }
                    catch { }
                }
            }
        }
    }
}
