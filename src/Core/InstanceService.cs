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

        public static string ExportModpack(AppConfig config, string id, string destination)
        {
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cube-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                string manifest = "{\"name\":" + Quote(id) + ",\"minecraft\":" + Quote(MinecraftOf(config, id)) +
                                  ",\"loader\":" + Quote(LoaderOf(config, id)) + ",\"format\":\"cube-instance-v1\"}";
                File.WriteAllText(System.IO.Path.Combine(temp, "manifest.json"), manifest, new UTF8Encoding(false));

                string[] folders = new string[] { "mods", "config", "resourcepacks", "shaderpacks" };
                for (int i = 0; i < folders.Length; i++)
                {
                    string source = System.IO.Path.Combine(config.InstanceDir(id), folders[i]);
                    if (!Directory.Exists(source)) continue;
                    string target = System.IO.Path.Combine(temp, folders[i]);
                    CopyDirectory(source, target);
                }
                if (File.Exists(destination)) File.Delete(destination);
                ZipFile.CreateFromDirectory(temp, destination, CompressionLevel.Optimal, false);
                return destination;
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            }
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
