using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace CubeLauncher.Core
{
    public static class MrpackImporter
    {
        public static string Import(AppConfig config, VersionManager versions, string mrpackPath, ProgressSink progress)
        {
            if (string.IsNullOrEmpty(mrpackPath) || !File.Exists(mrpackPath))
                throw new Exception("找不到 .mrpack 文件");

            JMap index;
            using (ZipArchive archive = ZipFile.OpenRead(mrpackPath))
            {
                ZipArchiveEntry entry = archive.GetEntry("modrinth.index.json");
                if (entry == null) throw new Exception("不是有效的 Modrinth 整合包：缺少 modrinth.index.json");
                using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                    index = JMap.Parse(reader.ReadToEnd());
            }

            JMap dependencies = index.Map("dependencies");
            string minecraft = dependencies.Str("minecraft", "");
            if (string.IsNullOrEmpty(minecraft)) throw new Exception("整合包没有指定 Minecraft 版本");

            string loader = "vanilla";
            string loaderVersion = "";
            if (!string.IsNullOrEmpty(dependencies.Str("fabric-loader", "")))
            {
                loader = "fabric";
                loaderVersion = dependencies.Str("fabric-loader");
            }
            else if (!string.IsNullOrEmpty(dependencies.Str("quilt-loader", "")))
            {
                loader = "quilt";
                loaderVersion = dependencies.Str("quilt-loader");
            }
            else if (!string.IsNullOrEmpty(dependencies.Str("neoforge", "")))
            {
                loader = "neoforge";
                loaderVersion = dependencies.Str("neoforge");
            }
            else if (!string.IsNullOrEmpty(dependencies.Str("forge", "")))
            {
                loader = "forge";
                loaderVersion = dependencies.Str("forge");
            }

            string packName = index.Str("name", Path.GetFileNameWithoutExtension(mrpackPath));
            if (string.IsNullOrEmpty(packName)) packName = "整合包";
            progress.SetStage("安装整合包运行环境 " + minecraft + (string.IsNullOrEmpty(loaderVersion) ? "" : " / " + loaderVersion));

            string canonicalId = InstallLoader(config, versions, minecraft, loader, loaderVersion, progress);
            string canonicalJsonPath = Path.Combine(config.VersionsDir, Util.SafeFileName(canonicalId), Util.SafeFileName(canonicalId) + ".json");
            if (!File.Exists(canonicalJsonPath)) throw new Exception("加载器已安装，但没有生成版本 JSON: " + canonicalId);

            JMap versionJson = JMap.Parse(File.ReadAllText(canonicalJsonPath, Encoding.UTF8));
            if (string.IsNullOrEmpty(versionJson.Str("inheritsFrom", "")))
                versionJson.Set("inheritsFrom", minecraft);

            string idBase = BuildInstanceId(loader, loaderVersion, minecraft, packName);
            string instanceId = idBase;
            int suffix = 2;
            while (Directory.Exists(Path.Combine(config.VersionsDir, instanceId)) ||
                   Directory.Exists(config.InstanceDir(instanceId)))
            {
                instanceId = idBase + "-" + suffix;
                suffix++;
            }

            string versionDir = Path.Combine(config.VersionsDir, instanceId);
            string instanceDir = config.InstanceDir(instanceId);
            Util.EnsureDirectory(versionDir);
            Util.EnsureDirectory(instanceDir);
            Util.EnsureDirectory(config.ModsDir(instanceId));

            versionJson.Set("id", instanceId);
            File.WriteAllText(Path.Combine(versionDir, instanceId + ".json"),
                Json.Stringify(versionJson.Raw), new UTF8Encoding(false));

            Dictionary<string, object> meta = new Dictionary<string, object>();
            meta["name"] = packName;
            meta["format"] = "mrpack";
            meta["minecraft"] = minecraft;
            meta["loader"] = loader;
            meta["loaderVersion"] = loaderVersion;
            meta["source"] = Path.GetFileName(mrpackPath);
            File.WriteAllText(Path.Combine(instanceDir, "cube-instance.json"),
                Json.Stringify(meta), new UTF8Encoding(false));

            progress.SetStage("下载整合包文件");
            DownloadFiles(config, index, instanceDir, progress);
            progress.SetStage("解压整合包覆盖文件");
            ExtractOverrides(mrpackPath, instanceDir, progress);
            progress.Report(1, packName + " 导入完成");
            Log.Info("整合包已导入为新实例: " + instanceId + " (" + packName + ")");
            return instanceId;
        }

        private static string InstallLoader(AppConfig config, VersionManager versions, string mc, string loader, string loaderVersion, ProgressSink progress)
        {
            if (loader == "fabric") return FabricService.Install(config, versions, mc, loaderVersion, progress);
            if (loader == "quilt") return QuiltService.Install(config, versions, mc, loaderVersion, progress);
            if (loader == "forge") return ForgeService.Install(config, versions, mc, loaderVersion, progress);
            if (loader == "neoforge")
            {
                NeoForgeBuild build = new NeoForgeBuild();
                build.Minecraft = mc;
                build.Version = loaderVersion;
                build.Artifact = mc == "1.20.1" ? "forge" : "neoforge";
                build.InstallerVersion = mc == "1.20.1" ? "1.20.1-" + loaderVersion : loaderVersion;
                return NeoForgeService.Install(config, versions, mc, build, progress);
            }
            versions.InstallVanilla(mc, progress);
            return mc;
        }

        private static string BuildInstanceId(string loader, string loaderVersion, string mc, string name)
        {
            string slug = Slug(name);
            if (string.IsNullOrEmpty(slug)) slug = "pack";
            if (slug.Length > 32) slug = slug.Substring(0, 32);
            if (loader == "fabric") return "fabric-loader-" + loaderVersion + "-" + mc + "-pack-" + slug;
            if (loader == "quilt") return "quilt-loader-" + loaderVersion + "-" + mc + "-pack-" + slug;
            if (loader == "neoforge") return "neoforge-" + loaderVersion + "-" + mc + "-pack-" + slug;
            if (loader == "forge") return mc + "-forge-" + loaderVersion + "-pack-" + slug;
            return mc + "-pack-" + slug;
        }

        private static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    builder.Append(c);
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                    builder.Append('-');
            }
            return builder.ToString().Trim('-');
        }

        private static void DownloadFiles(AppConfig config, JMap index, string instanceDir, ProgressSink progress)
        {
            List<JMap> files = index.Maps("files");
            int total = Math.Max(1, files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                progress.ThrowIfCancelled();
                JMap file = files[i];
                string clientEnv = file.Map("env").Str("client", "required");
                if (string.Equals(clientEnv, "unsupported", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = file.Str("path", "");
                if (string.IsNullOrEmpty(relative)) continue;
                string destination = SafeJoin(instanceDir, relative);
                IList<object> downloads = JMap.AsList(file.Get("downloads"));
                string url = downloads != null && downloads.Count > 0 ? Convert.ToString(downloads[0]) : "";
                if (string.IsNullOrEmpty(url)) continue;

                DownloadItem item = new DownloadItem();
                item.Name = relative;
                item.Destination = destination;
                item.Urls = Mirror.Urls(url, config.MirrorFirst);
                item.Sha1 = file.Map("hashes").Str("sha1", "");
                item.Size = file.Long("fileSize", 0);
                progress.SetStage("下载 " + Path.GetFileName(relative));
                int fileIndex = i;
                long expected = item.Size;
                Http.DownloadOne(item, progress.Token, delegate(long downloaded)
                {
                    double ratio = expected > 0 ? Math.Min(1, (double)downloaded / expected) : 0.5;
                    progress.Report(0.1 + 0.75 * ((fileIndex + ratio) / total), Path.GetFileName(relative));
                });
                progress.Report(0.1 + 0.75 * (i + 1) / total, Path.GetFileName(relative));
            }
        }

        private static void ExtractOverrides(string mrpackPath, string instanceDir, ProgressSink progress)
        {
            using (ZipArchive archive = ZipFile.OpenRead(mrpackPath))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    progress.ThrowIfCancelled();
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string full = entry.FullName.Replace('\\', '/');
                    string relative = null;
                    if (full.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase))
                        relative = full.Substring("overrides/".Length);
                    else if (full.StartsWith("client-overrides/", StringComparison.OrdinalIgnoreCase))
                        relative = full.Substring("client-overrides/".Length);
                    if (string.IsNullOrEmpty(relative)) continue;
                    string destination = SafeJoin(instanceDir, relative);
                    Util.EnsureParent(destination);
                    using (Stream input = entry.Open())
                    using (FileStream output = File.Create(destination))
                        input.CopyTo(output);
                }
            }
        }

        private static string SafeJoin(string root, string relative)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new Exception("整合包包含非法路径: " + relative);
            return full;
        }
    }
}
