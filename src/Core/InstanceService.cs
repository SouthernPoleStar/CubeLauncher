using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CubeLauncher.Core
{
    public sealed class InstanceItem
    {
        public string Name = "";
        public string Path = "";
        public bool Enabled = true;
        public long Size;
        public DateTime Modified;
        public bool IsDirectory;

        public string SizeLabel { get { return IsDirectory ? "文件夹" : Util.HumanBytes(Size); } }
    }

    public static class InstanceService
    {
        public static List<InstanceItem> ListMods(AppConfig config, string id)
        {
            return ListFiles(config.ModsDir(id), new string[] { ".jar", ".jar.disabled" }, false);
        }

        public static List<InstanceItem> ListResourcePacks(AppConfig config, string id)
        {
            return ListFiles(config.ResourcePacksDir(id), new string[] { ".zip", ".zip.disabled", ".jar", ".jar.disabled" }, true);
        }

        public static List<InstanceItem> ListShaderPacks(AppConfig config, string id)
        {
            return ListFiles(config.ShaderPacksDir(id), new string[] { ".zip", ".zip.disabled", ".jar", ".jar.disabled" }, true);
        }

        public static List<InstanceItem> ListWorlds(AppConfig config, string id)
        {
            List<InstanceItem> result = new List<InstanceItem>();
            string dir = config.SavesDir(id);
            if (!Directory.Exists(dir)) return result;
            string[] worlds = Directory.GetDirectories(dir);
            for (int i = 0; i < worlds.Length; i++)
            {
                InstanceItem item = new InstanceItem();
                item.Name = System.IO.Path.GetFileName(worlds[i]);
                item.Path = worlds[i];
                item.IsDirectory = true;
                item.Enabled = true;
                item.Modified = Directory.GetLastWriteTime(worlds[i]);
                item.Size = FolderSize(worlds[i]);
                result.Add(item);
            }
            result.Sort(delegate(InstanceItem a, InstanceItem b) { return b.Modified.CompareTo(a.Modified); });
            return result;
        }

        public static List<InstanceItem> ListScreenshots(AppConfig config, string id)
        {
            List<InstanceItem> result = new List<InstanceItem>();
            string dir = config.ScreenshotsDir(id);
            if (!Directory.Exists(dir)) return result;
            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++)
            {
                string ext = System.IO.Path.GetExtension(files[i]).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;
                InstanceItem item = new InstanceItem();
                item.Name = System.IO.Path.GetFileName(files[i]);
                item.Path = files[i];
                item.Enabled = true;
                item.Size = new FileInfo(files[i]).Length;
                item.Modified = File.GetLastWriteTime(files[i]);
                result.Add(item);
            }
            result.Sort(delegate(InstanceItem a, InstanceItem b) { return b.Modified.CompareTo(a.Modified); });
            return result;
        }

        private static List<InstanceItem> ListFiles(string dir, string[] extensions, bool includeDirectories)
        {
            List<InstanceItem> result = new List<InstanceItem>();
            if (!Directory.Exists(dir)) return result;
            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++)
            {
                string lower = files[i].ToLowerInvariant();
                bool match = false;
                for (int e = 0; e < extensions.Length; e++)
                    if (lower.EndsWith(extensions[e], StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                if (!match) continue;
                InstanceItem item = new InstanceItem();
                item.Path = files[i];
                item.Name = System.IO.Path.GetFileName(files[i]);
                item.Enabled = !lower.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                item.Size = new FileInfo(files[i]).Length;
                item.Modified = File.GetLastWriteTime(files[i]);
                result.Add(item);
            }
            if (includeDirectories)
            {
                string[] dirs = Directory.GetDirectories(dir);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string lower = dirs[i].ToLowerInvariant();
                    if (lower.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) continue;
                    InstanceItem item = new InstanceItem();
                    item.Path = dirs[i];
                    item.Name = System.IO.Path.GetFileName(dirs[i]);
                    item.Enabled = true;
                    item.IsDirectory = true;
                    item.Modified = Directory.GetLastWriteTime(dirs[i]);
                    item.Size = FolderSize(dirs[i]);
                    result.Add(item);
                }
            }
            result.Sort(delegate(InstanceItem a, InstanceItem b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            return result;
        }

        public static void SetEnabled(InstanceItem item, bool enabled)
        {
            if (item == null) return;
            string path = item.Path;
            if (item.IsDirectory)
            {
                if (enabled && path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    Directory.Move(path, path.Substring(0, path.Length - ".disabled".Length));
                else if (!enabled && !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    Directory.Move(path, path + ".disabled");
                return;
            }
            if (!File.Exists(path)) return;
            if (enabled && path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                File.Move(path, path.Substring(0, path.Length - ".disabled".Length));
            else if (!enabled && !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                File.Move(path, path + ".disabled");
        }

        public static void Delete(InstanceItem item)
        {
            if (item == null) return;
            if (item.IsDirectory) { if (Directory.Exists(item.Path)) Directory.Delete(item.Path, true); }
            else if (File.Exists(item.Path)) File.Delete(item.Path);
        }

        /// <summary>
        /// Export the instance as a real Modrinth pack. The launcher only
        /// imports .mrpack, so exporting the same format is what makes the
        /// round-trip work. Local files live in overrides/ so no download
        /// URLs or hashes are required.
        /// </summary>
        public static string ExportModpack(AppConfig config, string id, string destination)
        {
            if (string.IsNullOrEmpty(destination)) throw new Exception("请选择导出位置");
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cube-export-" + Guid.NewGuid().ToString("N"));
            string overrides = System.IO.Path.Combine(temp, "overrides");
            Directory.CreateDirectory(overrides);
            try
            {
                JMap instanceMeta = ReadInstanceMeta(config, id);
                string minecraft = instanceMeta.Str("minecraft", "");
                if (string.IsNullOrEmpty(minecraft)) minecraft = MinecraftOf(config, id);
                string loader = instanceMeta.Str("loader", "");
                if (string.IsNullOrEmpty(loader)) loader = LoaderOf(config, id);
                string loaderVersion = instanceMeta.Str("loaderVersion", "");
                if (string.IsNullOrEmpty(loaderVersion)) loaderVersion = LoaderVersionOf(id, loader, minecraft);
                string packName = instanceMeta.Str("name", "");
                if (string.IsNullOrEmpty(packName)) packName = id;

                Dictionary<string, object> index = new Dictionary<string, object>();
                index["formatVersion"] = 1;
                index["game"] = "minecraft";
                index["versionId"] = "1.0.0";
                index["name"] = packName;
                index["summary"] = "由方块启动器导出的 Modrinth 整合包";
                index["files"] = new List<object>();

                Dictionary<string, object> dependencies = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(minecraft)) dependencies["minecraft"] = minecraft;
                if (!string.IsNullOrEmpty(loaderVersion))
                {
                    if (string.Equals(loader, "fabric", StringComparison.OrdinalIgnoreCase)) dependencies["fabric-loader"] = loaderVersion;
                    else if (string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase)) dependencies["quilt-loader"] = loaderVersion;
                    else if (string.Equals(loader, "forge", StringComparison.OrdinalIgnoreCase)) dependencies["forge"] = loaderVersion;
                    else if (string.Equals(loader, "neoforge", StringComparison.OrdinalIgnoreCase)) dependencies["neoforge"] = loaderVersion;
                }
                index["dependencies"] = dependencies;
                File.WriteAllText(System.IO.Path.Combine(temp, "modrinth.index.json"),
                    Json.Stringify(index), new UTF8Encoding(false));

                string[] folders = new string[] { "mods", "config", "resourcepacks", "shaderpacks" };
                for (int i = 0; i < folders.Length; i++)
                {
                    string source = System.IO.Path.Combine(config.InstanceDir(id), folders[i]);
                    if (!Directory.Exists(source)) continue;
                    CopyDirectory(source, System.IO.Path.Combine(overrides, folders[i]));
                }
                if (File.Exists(destination)) File.Delete(destination);
                WritePack(temp, destination);
                return destination;
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
        }

        private static void WritePack(string root, string destination)
        {
            using (FileStream stream = new FileStream(destination, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddZipFile(archive, System.IO.Path.Combine(root, "modrinth.index.json"), "modrinth.index.json");
                string overrides = System.IO.Path.Combine(root, "overrides");
                if (!Directory.Exists(overrides)) return;
                string[] files = Directory.GetFiles(overrides, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string relative = files[i].Substring(overrides.Length).TrimStart('\\', '/').Replace('\\', '/');
                    AddZipFile(archive, files[i], "overrides/" + relative);
                }
            }
        }

        private static void AddZipFile(ZipArchive archive, string source, string entryName)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (Stream input = File.OpenRead(source))
            using (Stream output = entry.Open())
                input.CopyTo(output);
        }

        private static JMap ReadInstanceMeta(AppConfig config, string id)
        {
            string path = System.IO.Path.Combine(config.InstanceDir(id), "cube-instance.json");
            try
            {
                if (!File.Exists(path)) return new JMap();
                return JMap.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return new JMap(); }
        }

        private static string LoaderVersionOf(string id, string loader, string minecraft)
        {
            string value = id ?? "";
            int pack = value.IndexOf("-pack-", StringComparison.OrdinalIgnoreCase);
            if (pack >= 0) value = value.Substring(0, pack);
            if (string.Equals(loader, "fabric", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = loader + "-loader-";
                if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
                value = value.Substring(prefix.Length);
                string suffix = "-" + minecraft;
                if (!string.IsNullOrEmpty(minecraft) && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    value = value.Substring(0, value.Length - suffix.Length);
                return value;
            }
            if (string.Equals(loader, "forge", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = minecraft + "-forge-";
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return value.Substring(prefix.Length);
                int marker = value.IndexOf("-forge-", StringComparison.OrdinalIgnoreCase);
                return marker >= 0 ? value.Substring(marker + "-forge-".Length) : "";
            }
            if (string.Equals(loader, "neoforge", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = "neoforge-";
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value.Substring(prefix.Length);
                    string suffix = "-" + minecraft;
                    if (!string.IsNullOrEmpty(minecraft) && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.Length - suffix.Length);
                    return value;
                }
                prefix = minecraft + "-neoforge-";
                if (!string.IsNullOrEmpty(minecraft) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return value.Substring(prefix.Length);
            }
            return "";
        }

        public static void InstallPack(AppConfig config, ModProject project, ModFile file, string instanceId, string kind, ProgressSink progress)
        {
            if (file == null) throw new Exception("请选择要安装的文件");
            string dir;
            string label;
            if (string.Equals(kind, "shader", StringComparison.OrdinalIgnoreCase))
            {
                dir = config.ShaderPacksDir(instanceId);
                label = "光影包 ";
            }
            else if (string.Equals(kind, "modpack", StringComparison.OrdinalIgnoreCase))
            {
                dir = config.ModpacksDir(instanceId);
                label = "整合包 ";
            }
            else
            {
                dir = config.ResourcePacksDir(instanceId);
                label = "材质包 ";
            }
            Util.EnsureDirectory(dir);
            string name = string.IsNullOrEmpty(file.FileName) ? Util.SafeFileName(file.Name) + ".zip" : Util.SafeFileName(file.FileName);
            string dest = System.IO.Path.Combine(dir, name);
            DownloadItem item = new DownloadItem();
            item.Name = label + file.Name;
            item.Destination = dest;
            item.Urls = Mirror.ModFileUrls(file.DownloadUrl, config.MirrorFirst);
            item.Sha1 = file.Sha1;
            item.Size = file.Size;
            progress.SetStage("下载" + label + name);
            long expected = item.Size;
            Http.DownloadOne(item, progress.Token, delegate(long downloaded)
            {
                double fraction = expected > 0 ? Math.Min(0.99, (double)downloaded / expected) : 0.5;
                progress.Report(fraction, name + "  " + Util.HumanBytes(downloaded) +
                    (expected > 0 ? " / " + Util.HumanBytes(expected) : ""));
            });
        }

        private static string MinecraftOf(AppConfig config, string id)
        {
            string json = System.IO.Path.Combine(config.VersionsDir, Util.SafeFileName(id), Util.SafeFileName(id) + ".json");
            try
            {
                JMap map = JMap.Parse(File.ReadAllText(json, Encoding.UTF8));
                string baseId = map.Str("inheritsFrom", id);
                return baseId;
            }
            catch { return ""; }
        }

        private static string LoaderOf(AppConfig config, string id)
        {
            string lower = (id ?? "").ToLowerInvariant();
            if (lower.Contains("neoforge")) return "neoforge";
            if (lower.Contains("forge")) return "forge";
            if (lower.Contains("fabric")) return "fabric";
            if (lower.Contains("quilt")) return "quilt";
            return "vanilla";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], System.IO.Path.Combine(target, System.IO.Path.GetFileName(files[i])), true);
            string[] dirs = Directory.GetDirectories(source);
            for (int i = 0; i < dirs.Length; i++)
                CopyDirectory(dirs[i], System.IO.Path.Combine(target, System.IO.Path.GetFileName(dirs[i])));
        }

        private static long FolderSize(string dir)
        {
            try
            {
                long total = 0;
                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++) total += new FileInfo(files[i]).Length;
                return total;
            }
            catch { return 0; }
        }
    }
}
