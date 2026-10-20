using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CubeLauncher.Core
{
    public sealed class LoaderBuild
    {
        public string Version = "";
        public bool Stable;
        public string Minecraft = "";
        public string Label = "";

        public override string ToString()
        {
            return string.IsNullOrEmpty(Label) ? Version : Label;
        }
    }

    public static class FabricService
    {
        public static List<LoaderBuild> Loaders(AppConfig config, string mc)
        {
            string path = "/v2/versions/loader/" + Uri.EscapeDataString(mc);
            string json = Http.GetText(Mirror.FabricMeta(path, config.MirrorFirst), 30000);
            JList list = new JList(Json.Parse(json));
            List<LoaderBuild> result = new List<LoaderBuild>();
            for (int i = 0; i < list.Count; i++)
            {
                JMap item = list.Map(i);
                JMap loader = item.Map("loader");
                string version = loader.Str("version", "");
                if (string.IsNullOrEmpty(version)) continue;
                LoaderBuild build = new LoaderBuild();
                build.Version = version;
                build.Stable = loader.Bool("stable", false);
                build.Minecraft = mc;
                build.Label = version + (build.Stable ? "  稳定" : "  测试");
                result.Add(build);
            }
            result.Sort(delegate(LoaderBuild a, LoaderBuild b)
            {
                if (a.Stable != b.Stable) return a.Stable ? -1 : 1;
                return Util.CompareVersion(b.Version, a.Version);
            });
            return result;
        }

        public static string Install(AppConfig config, VersionManager versions, string mc, string loaderVersion, ProgressSink progress)
        {
            progress.SetStage("安装 Minecraft " + mc + " 原版基础");
            versions.InstallVanilla(mc, progress);

            progress.SetStage("获取 Fabric Loader " + loaderVersion + " 配置");
            string path = "/v2/versions/loader/" + Uri.EscapeDataString(mc) + "/" + Uri.EscapeDataString(loaderVersion) + "/profile/json";
            string jsonText = Http.GetText(Mirror.FabricMeta(path, config.MirrorFirst), 30000);
            JMap json = JMap.Parse(jsonText);
            string id = json.Str("id", "fabric-loader-" + loaderVersion + "-" + mc);
            string dir = Path.Combine(config.VersionsDir, Util.SafeFileName(id));
            Util.EnsureDirectory(dir);
            File.WriteAllText(Path.Combine(dir, Util.SafeFileName(id) + ".json"), jsonText, new UTF8Encoding(false));
            Util.EnsureDirectory(config.InstanceDir(id));
            Util.EnsureDirectory(config.ModsDir(id));
            progress.Report(0.08, "Fabric 配置已写入");

            progress.SetStage("下载 Fabric 运行库");
            versions.EnsureRuntime(id, progress);
            Log.Info("Fabric " + loaderVersion + " 安装完成: " + id);
            return id;
        }
    }

    public static class QuiltService
    {
        public static List<LoaderBuild> Loaders(AppConfig config, string mc)
        {
            string path = "/v3/versions/loader/" + Uri.EscapeDataString(mc);
            string json = Http.GetText(Mirror.QuiltMeta(path), 30000);
            JList list = new JList(Json.Parse(json));
            List<LoaderBuild> result = new List<LoaderBuild>();
            for (int i = 0; i < list.Count; i++)
            {
                JMap item = list.Map(i);
                JMap loader = item.Map("loader");
                string version = loader.Str("version", "");
                if (string.IsNullOrEmpty(version)) continue;
                LoaderBuild build = new LoaderBuild();
                build.Version = version;
                build.Stable = version.IndexOf("beta", StringComparison.OrdinalIgnoreCase) < 0;
                build.Minecraft = mc;
                build.Label = version + (build.Stable ? "  稳定" : "  测试");
                result.Add(build);
            }
            result.Sort(delegate(LoaderBuild a, LoaderBuild b)
            {
                if (a.Stable != b.Stable) return a.Stable ? -1 : 1;
                return Util.CompareVersion(b.Version, a.Version);
            });
            return result;
        }

        public static string Install(AppConfig config, VersionManager versions, string mc, string loaderVersion, ProgressSink progress)
        {
            progress.SetStage("安装 Minecraft " + mc + " 原版基础");
            versions.InstallVanilla(mc, progress);
            progress.SetStage("获取 Quilt Loader " + loaderVersion + " 配置");
            string path = "/v3/versions/loader/" + Uri.EscapeDataString(mc) + "/" + Uri.EscapeDataString(loaderVersion) + "/profile/json";
            string jsonText = Http.GetText(Mirror.QuiltMeta(path), 30000);
            JMap json = JMap.Parse(jsonText);
            string id = json.Str("id", "quilt-loader-" + loaderVersion + "-" + mc);
            string dir = Path.Combine(config.VersionsDir, Util.SafeFileName(id));
            Util.EnsureDirectory(dir);
            File.WriteAllText(Path.Combine(dir, Util.SafeFileName(id) + ".json"), jsonText, new UTF8Encoding(false));
            Util.EnsureDirectory(config.InstanceDir(id));
            Util.EnsureDirectory(config.ModsDir(id));
            versions.EnsureRuntime(id, progress);
            Log.Info("Quilt " + loaderVersion + " 安装完成: " + id);
            return id;
        }
    }
}
