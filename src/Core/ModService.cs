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
            get { return string.Equals(Source, "curseforge", StringComparison.OrdinalIgnoreCase) ? "CurseForge" : "Modrinth"; }
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
        public long ModId;
        public long FileId;
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
        public string Source = "";
    }

    public static class ModService
    {
        public static int CurseLoaderType(string loader)
        {
            if (string.Equals(loader, "forge", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(loader, "fabric", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase)) return 5;
            if (string.Equals(loader, "neoforge", StringComparison.OrdinalIgnoreCase)) return 6;
            return 0;
        }

        public static string ModrinthLoader(string loader)
        {
            if (string.Equals(loader, "vanilla", StringComparison.OrdinalIgnoreCase)) return "";
            return loader ?? "";
        }

        public static ModSearchResult Search(AppConfig config, string source, string query, string mc, string loader, int offset, int limit)
        {
            if (string.Equals(source, "curseforge", StringComparison.OrdinalIgnoreCase))
                return SearchCurse(config, query, mc, loader, offset, limit);
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

        private static ModSearchResult SearchCurse(AppConfig config, string query, string mc, string loader, int offset, int limit)
        {
            int loaderType = CurseLoaderType(loader);
            try
            {
                string json = GetCurseJson(config, Mirror.CurseSearch(query, mc, loaderType, offset, limit));
                JMap root = JMap.Parse(json);
                ModSearchResult result = new ModSearchResult();
                result.Source = "curseforge";
                result.Offset = offset;
                result.Total = root.Map("pagination").Int("totalCount", 0);
                List<JMap> hits = root.Maps("data");
                for (int i = 0; i < hits.Count; i++)
                {
                    JMap hit = hits[i];
                    ModProject project = new ModProject();
                    project.Source = "curseforge";
                    project.Id = hit.Str("id", "");
                    project.Slug = hit.Str("slug", "");
                    project.Name = hit.Str("name", "");
                    project.Summary = Clean(hit.Str("summary", ""));
                    JMap logo = hit.Map("logo");
                    project.IconUrl = logo.Str("thumbnailUrl", logo.Str("url", ""));
                    project.Downloads = hit.Long("downloadCount", 0);
                    List<JMap> authors = hit.Maps("authors");
                    if (authors.Count > 0) project.Author = authors[0].Str("name", "");
                    project.Categories = JoinMaps(hit.Maps("categories"), "name");
                    project.Website = "https://www.curseforge.com/minecraft/mc-mods/" + project.Slug;
                    if (!string.IsNullOrEmpty(project.Id)) result.Projects.Add(project);
                }
                return result;
            }
            catch (Exception ex)
            {
                Log.Warn("CurseForge 镜像/API 请求失败，尝试 CFWidget 项目回退: " + ex.Message);
                ModSearchResult fallback = SearchCfWidget(config, query, mc, loader);
                if (fallback != null) return fallback;
                throw new Exception("CurseForge 国内镜像暂不可用。" +
                    (string.IsNullOrEmpty(config.CurseForgeApiKey)
                        ? "可在「设置 → 下载与镜像」填写 CurseForge API Key，或直接输入模组的 CurseForge slug（例如 jei、sodium）。"
                        : "请稍后重试。"));
            }
        }

        private static string GetCurseJson(AppConfig config, List<string> urls)
        {
            if (urls == null || urls.Count == 0) throw new Exception("CurseForge 地址为空");
            try
            {
                return Http.GetText(new List<string>() { urls[0] }, 20000);
            }
            catch (Exception mirrorError)
            {
                if (string.IsNullOrEmpty(config.CurseForgeApiKey))
                    throw new Exception("CurseForge 镜像不可用: " + mirrorError.Message, mirrorError);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers["x-api-key"] = config.CurseForgeApiKey;
                headers["Accept"] = "application/json";
                return Http.GetText(new List<string>() { urls[urls.Count - 1] }, 30000, headers);
            }
        }

        public static List<ModFile> LoadFiles(AppConfig config, ModProject project, string mc, string loader)
        {
            if (project == null) return new List<ModFile>();
            if (string.Equals(project.Source, "curseforge", StringComparison.OrdinalIgnoreCase))
                return LoadCurseFiles(config, project, mc, loader);
            return LoadModrinthFiles(config, project, mc, loader);
        }

        private static ModSearchResult SearchCfWidget(AppConfig config, string query, string mc, string loader)
        {
            string slug = ExtractSlug(query);
            if (string.IsNullOrEmpty(slug)) return null;
            try
            {
                JMap project = FetchCfWidget(config, slug, mc, loader);
                if (project.Count == 0) return null;
                ModSearchResult result = new ModSearchResult();
                result.Source = "curseforge";
                result.Total = 1;
                ModProject item = ParseCfWidgetProject(project, slug);
                if (string.IsNullOrEmpty(item.Id)) return null;
                result.Projects.Add(item);
                Log.Info("CurseForge 使用 CFWidget 回退: " + item.Name);
                return result;
            }
            catch (Exception ex)
            {
                Log.Warn("CFWidget 回退失败: " + ex.Message);
                return null;
            }
        }

        private static ModProject ParseCfWidgetProject(JMap json, string slug)
        {
            ModProject project = new ModProject();
            project.Source = "curseforge";
            project.Id = json.Str("id", "");
            project.Slug = slug;
            project.Name = json.Str("title", slug);
            project.Summary = Clean(json.Str("summary", ""));
            project.IconUrl = json.Str("thumbnail", "");
            project.Downloads = json.Long("downloads", 0);
            List<JMap> members = json.Maps("members");
            if (members.Count > 0) project.Author = members[0].Str("username", members[0].Str("name", ""));
            project.Website = "https://www.curseforge.com/minecraft/mc-mods/" + slug;
            return project;
        }

        private static JMap FetchCfWidget(AppConfig config, string slug, string mc, string loader)
        {
            string url = "https://api.cfwidget.com/minecraft/mc-mods/" + Uri.EscapeDataString(slug);
            if (!string.IsNullOrEmpty(mc) || !string.IsNullOrEmpty(loader))
            {
                url += "?";
                if (!string.IsNullOrEmpty(mc)) url += "version=" + Uri.EscapeDataString(mc);
                if (!string.IsNullOrEmpty(loader)) url += (url.EndsWith("?") ? "" : "&") + "loader=" + Uri.EscapeDataString(loader);
            }
            string json = Http.GetText(new List<string>() { url }, 30000);
            return JMap.Parse(json);
        }

        private static string ExtractSlug(string query)
        {
            if (string.IsNullOrEmpty(query)) return "";
            string value = query.Trim();
            int marker = value.IndexOf("/minecraft/mc-mods/", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) value = value.Substring(marker + "/minecraft/mc-mods/".Length);
            else
            {
                marker = value.IndexOf("curseforge.com/minecraft/", StringComparison.OrdinalIgnoreCase);
                if (marker >= 0)
                {
                    value = value.Substring(marker + "curseforge.com/minecraft/".Length);
                    int slash = value.IndexOf('/');
                    if (slash >= 0) value = value.Substring(slash + 1);
                }
            }
            int cut = value.IndexOfAny(new char[] { '?', '#', '/' });
            if (cut >= 0) value = value.Substring(0, cut);
            value = value.Trim().ToLowerInvariant();
            if (value.IndexOf(' ') >= 0) value = value.Replace(' ', '-');
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-') builder.Append(c);
            }
            value = builder.ToString().Trim('-');
            return value.Length >= 2 ? value : "";
        }

        private static List<ModFile> LoadCfWidgetFiles(AppConfig config, ModProject project, string mc, string loader)
        {
            List<ModFile> result = new List<ModFile>();
            if (project == null || string.IsNullOrEmpty(project.Slug)) return result;
            JMap json = FetchCfWidget(config, project.Slug, mc, loader);
            JMap download = json.Map("download");
            string id = download.Str("id", "");
            string fileName = download.Str("name", "");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(fileName)) return result;
            ModFile file = new ModFile();
            file.Source = "curseforge";
            file.Id = id;
            file.ModId = project.Id;
            file.Name = download.Str("display", fileName);
            file.FileName = fileName;
            file.VersionNumber = file.Name;
            file.Size = download.Long("filesize", 0);
            file.DownloadUrl = BuildCurseCdnUrl(file.Id, fileName);
            file.GameVersions = JoinList(download.List("versions"));
            result.Add(file);
            return result;
        }

        private static string BuildCurseCdnUrl(string fileId, string fileName)
        {
            long id;
            if (!long.TryParse(fileId, out id) || string.IsNullOrEmpty(fileName)) return "";
            long folder = id / 1000;
            long leaf = id % 1000;
            return "https://edge.forgecdn.net/files/" + folder.ToString() + "/" + leaf.ToString("000") + "/" + Uri.EscapeDataString(fileName);
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

        private static List<ModFile> LoadCurseFiles(AppConfig config, ModProject project, string mc, string loader)
        {
            try
            {
                string json = GetCurseJson(config, Mirror.CurseFiles(project.Id, mc, CurseLoaderType(loader)));
                JMap root = JMap.Parse(json);
                List<JMap> items = root.Maps("data");
                List<ModFile> result = new List<ModFile>();
                for (int i = 0; i < items.Count; i++)
                {
                    ModFile file = ParseCurseFile(items[i], project.Id);
                    if (!string.IsNullOrEmpty(file.DownloadUrl)) result.Add(file);
                }
                return result;
            }
            catch (Exception ex)
            {
                List<ModFile> fallback = LoadCfWidgetFiles(config, project, mc, loader);
                if (fallback.Count > 0)
                {
                    Log.Warn("CurseForge 文件列表使用 CFWidget 回退: " + ex.Message);
                    return fallback;
                }
                throw;
            }
        }

        private static ModFile ParseCurseFile(JMap item, string modId)
        {
            ModFile file = new ModFile();
            file.Source = "curseforge";
            file.Id = item.Str("id", "");
            file.ModId = string.IsNullOrEmpty(modId) ? item.Str("modId", "") : modId;
            file.Name = item.Str("displayName", "");
            file.FileName = item.Str("fileName", "");
            file.VersionNumber = file.Name;
            file.DownloadUrl = item.Str("downloadUrl", "");
            file.Size = item.Long("fileLength", 0);
            file.ReleaseType = item.Int("releaseType", 1) == 1 ? "release" : "beta";
            file.GameVersions = JoinList(item.List("gameVersions"));
            List<JMap> deps = item.Maps("dependencies");
            for (int d = 0; d < deps.Count; d++)
            {
                JMap dep = deps[d];
                ModDependency dependency = new ModDependency();
                dependency.ModId = dep.Long("modId", 0);
                dependency.FileId = dep.Long("fileId", 0);
                dependency.Type = dep.Int("relationType", 0) == 3 ? "required" : "optional";
                if (dependency.ModId > 0) file.Dependencies.Add(dependency);
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
                if (dep.ModId > 0)
                {
                    string json;
                    if (dep.FileId > 0)
                    {
                        json = GetCurseJson(config, Mirror.CurseFile(dep.ModId.ToString(), dep.FileId.ToString()));
                        JMap root = JMap.Parse(json);
                        JMap data = root.Map("data");
                        if (data.Count > 0) return ParseCurseFile(data, dep.ModId.ToString());
                    }
                    json = GetCurseJson(config, Mirror.CurseFiles(dep.ModId.ToString(), mc, CurseLoaderType(loader)));
                    JMap listRoot = JMap.Parse(json);
                    List<JMap> items = listRoot.Maps("data");
                    if (items.Count > 0) return ParseCurseFile(items[0], dep.ModId.ToString());
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

        private static string JoinMaps(List<JMap> maps, string key)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < maps.Count; i++) values.Add(maps[i].Str(key, ""));
            return string.Join(", ", values.ToArray());
        }
    }
}
