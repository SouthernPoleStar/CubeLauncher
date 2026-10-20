using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CubeLauncher.Core
{
    public sealed class ModProject
    {
        public string Source = "modrinth";
        public string Id = "";
        public string Slug = "";
        public string Name = "";
        public string Summary = "";
        public string Author = "";
        public string IconUrl = "";
        public long Downloads;
        public string Categories = "";
        public string GameVersions = "";
        public string Website = "";

        public string SourceLabel
        {
            get { return "Modrinth"; }
        }

        public string DownloadLabel
        {
            get
            {
                if (Downloads >= 1000000) return (Downloads / 1000000.0).ToString("0.0") + "M";
                if (Downloads >= 1000) return (Downloads / 1000.0).ToString("0.0") + "K";
                return Downloads.ToString();
            }
        }
    }

    public sealed class ModDependency
    {
        public string ProjectId = "";
        public string VersionId = "";
        public string Type = "";
        public string Name = "";
    }

    public sealed class ModFile
    {
        public string Source = "modrinth";
        public string Id = "";
        public string ModId = "";
        public string Name = "";
        public string FileName = "";
        public string VersionNumber = "";
        public string DownloadUrl = "";
        public string Sha1 = "";
        public long Size;
        public string GameVersions = "";
        public string Loaders = "";
        public string ReleaseType = "";
        public string Changelog = "";
        public List<ModDependency> Dependencies = new List<ModDependency>();

        public string SizeLabel
        {
            get { return Util.HumanBytes(Size); }
        }

        public override string ToString()
        {
            string label = string.IsNullOrEmpty(Name) ? FileName : Name;
            if (!string.IsNullOrEmpty(VersionNumber) && label.IndexOf(VersionNumber, StringComparison.OrdinalIgnoreCase) < 0)
                label += "  ·  " + VersionNumber;
            return label + "  ·  " + SizeLabel;
        }
    }

    public sealed class ModSearchResult
    {
        public List<ModProject> Projects = new List<ModProject>();
        public int Total;
        public int Offset;
        public string Source = "modrinth";
    }

    public static class ModService
    {
        public static string ModrinthLoader(string loader)
        {
            if (string.Equals(loader, "vanilla", StringComparison.OrdinalIgnoreCase)) return "";
            return loader ?? "";
        }

        public static ModSearchResult Search(AppConfig config, string source, string query, string mc, string loader, int offset, int limit)
        {
            return SearchModrinth(config, query, mc, loader, offset, limit);
        }

        private static ModSearchResult SearchModrinth(AppConfig config, string query, string mc, string loader, int offset, int limit)
        {
            List<string> facets = new List<string>();
            facets.Add("[\"project_type:mod\"]");
            if (!string.IsNullOrEmpty(mc)) facets.Add("[\"versions:" + mc + "\"]");
            string loaderName = ModrinthLoader(loader);
            if (!string.IsNullOrEmpty(loaderName)) facets.Add("[\"categories:" + loaderName + "\"]");
            string facetJson = "[" + string.Join(",", facets.ToArray()) + "]";
            string json = Http.GetText(Mirror.ModrinthSearch(query, facetJson, offset, limit), 30000);
            JMap root = JMap.Parse(json);
            ModSearchResult result = new ModSearchResult();
            result.Source = "modrinth";
            result.Offset = root.Int("offset", offset);
            result.Total = root.Int("total_hits", 0);
            List<JMap> hits = root.Maps("hits");
            for (int i = 0; i < hits.Count; i++)
            {
                JMap hit = hits[i];
                ModProject project = new ModProject();
                project.Source = "modrinth";
                project.Id = hit.Str("project_id", "");
                project.Slug = hit.Str("slug", "");
                project.Name = hit.Str("title", "");
                project.Summary = Clean(hit.Str("description", ""));
                project.Author = hit.Str("author", "");
                project.IconUrl = hit.Str("icon_url", "");
                project.Downloads = hit.Long("downloads", 0);
                project.Categories = JoinList(hit.List("categories"));
                project.GameVersions = JoinList(hit.List("versions"));
                project.Website = "https://modrinth.com/mod/" + project.Slug;
                if (!string.IsNullOrEmpty(project.Id)) result.Projects.Add(project);
            }
            return result;
        }

        public static List<ModFile> LoadFiles(AppConfig config, ModProject project, string mc, string loader)
        {
            if (project == null) return new List<ModFile>();
            return LoadModrinthFiles(config, project, mc, loader);
        }

        private static List<ModFile> LoadModrinthFiles(AppConfig config, ModProject project, string mc, string loader)
        {
            string json = Http.GetText(Mirror.ModrinthVersions(project.Id, mc, ModrinthLoader(loader)), 30000);
            JList list = new JList(Json.Parse(json));
            List<ModFile> result = new List<ModFile>();
            for (int i = 0; i < list.Count; i++)
            {
                JMap item = list.Map(i);
                ModFile file = ParseModrinthVersion(item);
                file.Source = "modrinth";
                if (!string.IsNullOrEmpty(file.DownloadUrl)) result.Add(file);
            }
            return result;
        }

        private static ModFile ParseModrinthVersion(JMap item)
        {
            ModFile file = new ModFile();
            file.Source = "modrinth";
            file.Id = item.Str("id", "");
            file.ModId = item.Str("project_id", "");
            file.Name = item.Str("name", "");
            file.VersionNumber = item.Str("version_number", "");
            file.ReleaseType = item.Str("version_type", "");
            file.GameVersions = JoinList(item.List("game_versions"));
            file.Loaders = JoinList(item.List("loaders"));
            file.Changelog = item.Str("changelog", "");
            List<JMap> files = item.Maps("files");
            JMap primary = null;
            for (int i = 0; i < files.Count; i++)
            {
                if (files[i].Bool("primary", false)) { primary = files[i]; break; }
            }
            if (primary == null && files.Count > 0) primary = files[0];
            if (primary != null)
            {
                file.FileName = primary.Str("filename", "");
                file.DownloadUrl = primary.Str("url", "");
                file.Size = primary.Long("size", 0);
                JMap hashes = primary.Map("hashes");
                file.Sha1 = hashes.Str("sha1", "");
            }
            List<JMap> deps = item.Maps("dependencies");
            for (int i = 0; i < deps.Count; i++)
            {
                JMap dep = deps[i];
                ModDependency dependency = new ModDependency();
                dependency.ProjectId = dep.Str("project_id", "");
                dependency.VersionId = dep.Str("version_id", "");
                dependency.Type = dep.Str("dependency_type", "");
                if (!string.IsNullOrEmpty(dependency.ProjectId)) file.Dependencies.Add(dependency);
            }
            return file;
        }

        public static List<string> Install(AppConfig config, ModProject project, ModFile file, string mc, string loader, string instanceId, ProgressSink progress)
        {
            if (file == null) throw new Exception("请选择要安装的文件");
            if (string.IsNullOrEmpty(instanceId)) throw new Exception("请先选择一个已安装的版本");
            string mods = config.ModsDir(instanceId);
            Util.EnsureDirectory(mods);
            List<string> installed = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            InstallRecursive(config, file, mc, loader, mods, progress, installed, seen, 0);
            progress.Report(1, "模组安装完成，共 " + installed.Count + " 个文件");
            return installed;
        }

        private static void InstallRecursive(AppConfig config, ModFile file, string mc, string loader, string mods,
            ProgressSink progress, List<string> installed, HashSet<string> seen, int depth)
        {
            if (file == null || depth > 6) return;
            string key = (string.IsNullOrEmpty(file.Sha1) ? file.DownloadUrl : file.Sha1);
            if (!seen.Add(key)) return;

            if (!string.IsNullOrEmpty(file.DownloadUrl))
            {
                string name = string.IsNullOrEmpty(file.FileName) ? Util.SafeFileName(file.Name) + ".jar" : Util.SafeFileName(file.FileName);
                if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                    name += ".jar";
                string dest = Path.Combine(mods, name);
                if (!File.Exists(dest))
                {
                    DownloadItem item = new DownloadItem();
                    item.Name = "模组 " + file.Name;
                    item.Destination = dest;
                    item.Urls = Mirror.ModFileUrls(file.DownloadUrl, config.MirrorFirst);
                    item.Sha1 = file.Sha1;
                    item.Size = file.Size;
                    progress.SetStage("下载模组 " + name + " (国内镜像优先)");
                    Http.DownloadOne(item, progress.Token, null);
                    installed.Add(name);
                }
            }

            for (int i = 0; i < file.Dependencies.Count; i++)
            {
                ModDependency dep = file.Dependencies[i];
                if (!string.Equals(dep.Type, "required", StringComparison.OrdinalIgnoreCase)) continue;
                ModFile dependency = ResolveDependency(config, dep, mc, loader);
                if (dependency != null)
                    InstallRecursive(config, dependency, mc, loader, mods, progress, installed, seen, depth + 1);
            }
        }

        private static ModFile ResolveDependency(AppConfig config, ModDependency dep, string mc, string loader)
        {
            try
            {
                if (!string.IsNullOrEmpty(dep.VersionId))
                {
                    string json = Http.GetText(Mirror.ModrinthVersion(dep.VersionId), 30000);
                    ModFile file = ParseModrinthVersion(JMap.Parse(json));
                    file.Source = "modrinth";
                    return file;
                }
                if (!string.IsNullOrEmpty(dep.ProjectId))
                {
                    List<ModFile> files = LoadModrinthFiles(config, new ModProject { Id = dep.ProjectId, Source = "modrinth" }, mc, loader);
                    if (files.Count > 0) return files[0];
                }
            }
            catch (Exception ex)
            {
                Log.Warn("解析模组前置失败: " + ex.Message);
            }
            return null;
        }

        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static string JoinList(IList<object> list)
        {
            if (list == null || list.Count == 0) return "";
            List<string> values = new List<string>();
            for (int i = 0; i < list.Count; i++) values.Add(Convert.ToString(list[i]));
            return string.Join(", ", values.ToArray());
        }
    }
}
