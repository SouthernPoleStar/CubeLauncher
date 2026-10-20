using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace CubeLauncher.Core
{
    public sealed class ForgeBuild
    {
        public string Minecraft = "";
        public string Version = "";
        public string FullVersion = "";
        public bool Recommended;
        public bool Latest;
        public string Label = "";

        public override string ToString()
        {
            return string.IsNullOrEmpty(Label) ? Version : Label;
        }
    }

    public static class ForgeService
    {
        public static List<ForgeBuild> LoadVersions(AppConfig config, string mc)
        {
            List<ForgeBuild> result = new List<ForgeBuild>();
            string recommended = "";
            string latest = "";
            try
            {
                string promos = Http.GetText(Mirror.ForgePromotions(config.MirrorFirst), 20000);
                JMap promo = JMap.Parse(promos);
                recommended = promo.Str(mc + "-recommended", "");
                latest = promo.Str(mc + "-latest", "");
            }
            catch (Exception ex)
            {
                Log.Warn("Forge 推荐版本查询失败: " + ex.Message);
            }

            try
            {
                List<string> metadataUrls = Mirror.ForgeMetadata(config.MirrorFirst);
                for (int attempt = 0; attempt < metadataUrls.Count && result.Count == 0; attempt++)
                {
                    try
                    {
                        string xml = Http.GetText(new List<string>() { metadataUrls[attempt] }, 30000);
                        ParseVersions(xml, mc, recommended, latest, result);
                        if (result.Count == 0)
                            Log.Warn("Forge 元数据源缺少 " + mc + "，尝试下一个源");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Forge 版本列表源失败 " + Http.ShortUrl(metadataUrls[attempt]) + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Forge 版本列表查询失败: " + ex.Message);
            }

            result.Sort(delegate(ForgeBuild a, ForgeBuild b)
            {
                return Util.CompareVersion(b.Version, a.Version);
            });
            return result;
        }

        private static void ParseVersions(string xml, string mc, string recommended, string latest, List<ForgeBuild> result)
        {
            XDocument document = XDocument.Parse(xml);
            foreach (XElement element in document.Descendants("version"))
            {
                string full = element.Value;
                if (!full.StartsWith(mc + "-", StringComparison.OrdinalIgnoreCase)) continue;
                string version = full.Substring(mc.Length + 1);
                if (version.IndexOf("forge", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                ForgeBuild build = new ForgeBuild();
                build.Minecraft = mc;
                build.Version = version;
                build.FullVersion = full;
                build.Recommended = string.Equals(version, recommended, StringComparison.OrdinalIgnoreCase);
                build.Latest = string.Equals(version, latest, StringComparison.OrdinalIgnoreCase);
                build.Label = version;
                if (build.Recommended) build.Label += "  推荐";
                else if (build.Latest) build.Label += "  最新";
                result.Add(build);
            }
        }

        public static string Install(AppConfig config, VersionManager versions, string mc, string forgeVersion, ProgressSink progress)
        {
            progress.SetStage("安装 Minecraft " + mc + " 原版基础");
            versions.InstallVanilla(mc, progress);

            JMap baseJson = JMap.Parse(File.ReadAllText(Path.Combine(config.VersionsDir, mc, mc + ".json"), Encoding.UTF8));
            int javaMajor = baseJson.Map("javaVersion").Int("majorVersion", 8);
            JavaInfo java = JavaManager.Ensure(config, javaMajor, progress);

            string cache = Path.Combine(config.CacheDir, "forge");
            Util.EnsureDirectory(cache);
            string installer = Path.Combine(cache, "forge-" + mc + "-" + forgeVersion + "-installer.jar");
            if (!File.Exists(installer) || new FileInfo(installer).Length < 100000)
            {
                DownloadItem item = new DownloadItem();
                item.Name = "Forge " + forgeVersion + " 安装器";
                item.Destination = installer;
                item.Urls = Mirror.ForgeInstaller(mc, forgeVersion, config.MirrorFirst);
                progress.SetStage("下载 Forge 安装器 (BMCLAPI 国内镜像)");
                Http.DownloadOne(item, progress.Token, null);
            }

            progress.SetStage("运行 Forge 安装器，正在打补丁与合并映射");
            Log.Info("Forge 安装器: " + installer);
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
                    Log.Info("[Forge] " + line);
                    if (line.Length < 140) progress.SetStage("Forge 安装器: " + line);
                });
            }
            catch (Exception ex)
            {
                Log.Warn("Forge 安装器国内镜像参数失败，尝试官方源: " + ex.Message);
                first = new ProcessResult();
                first.ExitCode = -1;
            }

            if (first.ExitCode != 0)
            {
                Log.Warn("Forge 安装器返回 " + first.ExitCode + "，尝试不使用镜像参数重试");
                string args2 = "-jar " + Util.Quote(installer) + " --installClient " + Util.Quote(config.GameDir);
                ProcessResult second = ProcessRunner.Run(java.Path, args2, config.GameDir, 45 * 60 * 1000, delegate(string line)
                {
                    if (string.IsNullOrEmpty(line)) return;
                    if (line.StartsWith("  Data ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Extracting ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Considering ", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("  Downloading ", StringComparison.OrdinalIgnoreCase)) return;
                    Log.Info("[Forge] " + line);
                    if (line.Length < 140) progress.SetStage("Forge 安装器: " + line);
                });
                if (second.ExitCode != 0)
                    throw new Exception("Forge 安装器失败 (退出码 " + second.ExitCode + ")，请查看日志获取详情");
            }

            string id = FindInstalledForge(config, mc, forgeVersion);
            if (string.IsNullOrEmpty(id))
                throw new Exception("Forge 安装器结束，但没有找到对应的版本 JSON。请查看 " + Path.Combine(config.GameDir, "installer.log"));

            Util.EnsureDirectory(config.InstanceDir(id));
            Util.EnsureDirectory(config.ModsDir(id));
            progress.SetStage("检查 Forge 运行库");
            versions.EnsureRuntime(id, progress);
            Log.Info("Forge " + forgeVersion + " 安装完成: " + id);
            return id;
        }

        private static string FindInstalledForge(AppConfig config, string mc, string forgeVersion)
        {
            string expected = mc + "-forge-" + forgeVersion;
            string json = Path.Combine(config.VersionsDir, expected, expected + ".json");
            if (File.Exists(json)) return expected;
            if (Directory.Exists(config.VersionsDir))
            {
                string[] dirs = Directory.GetDirectories(config.VersionsDir, "*forge*");
                for (int i = 0; i < dirs.Length; i++)
                {
                    string id = Path.GetFileName(dirs[i]);
                    if (id.StartsWith(mc, StringComparison.OrdinalIgnoreCase) &&
                        id.IndexOf(forgeVersion, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        File.Exists(Path.Combine(dirs[i], id + ".json")))
                        return id;
                }
            }
            return "";
        }
    }
}
