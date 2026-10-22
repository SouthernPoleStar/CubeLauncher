using System;
using System.Collections.Generic;

namespace CubeLauncher.Core
{
    /// <summary>
    /// CurseForge Core API access. The launcher ships a key so search works out of
    /// the box; set the CURSEFORGE_API_KEY environment variable to override it.
    /// </summary>
    public static class CurseForge
    {
        private const string DefaultApiKey = "$2a$10$jlm7NaCURqwUWMFnC8ZV4uTr.x6ncZeWMWvNVaybK2Re4ip.12ybe";
        public const string BaseUrl = "https://api.curseforge.com";
        public const int GameId = 432;

        public static string ApiKey
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY");
                return string.IsNullOrEmpty(custom) ? DefaultApiKey : custom.Trim();
            }
        }

        public static IDictionary<string, string> Headers()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers["x-api-key"] = ApiKey;
            headers["Accept"] = "application/json";
            return headers;
        }

        public static string GetJson(string url, int timeoutMs)
        {
            try
            {
                return Http.GetText(new string[] { url }, timeoutMs, Headers());
            }
            catch (Exception ex)
            {
                string message = ex.Message ?? "";
                if (message.IndexOf("403", StringComparison.Ordinal) >= 0)
                    throw new Exception("CurseForge API Key 无效或已失效，请更换 Key 后重试。", ex);
                if (message.IndexOf("429", StringComparison.Ordinal) >= 0)
                    throw new Exception("CurseForge 请求过于频繁，请稍后再试。", ex);
                throw;
            }
        }

        public static string ClassId(string kind)
        {
            if (string.Equals(kind, "modpack", StringComparison.OrdinalIgnoreCase)) return "4471";
            if (string.Equals(kind, "resourcepack", StringComparison.OrdinalIgnoreCase)) return "12";
            if (string.Equals(kind, "shader", StringComparison.OrdinalIgnoreCase)) return "6552";
            return "6";
        }

        public static string WebPath(string kind)
        {
            if (string.Equals(kind, "modpack", StringComparison.OrdinalIgnoreCase)) return "modpacks";
            if (string.Equals(kind, "resourcepack", StringComparison.OrdinalIgnoreCase)) return "texture-packs";
            if (string.Equals(kind, "shader", StringComparison.OrdinalIgnoreCase)) return "shaders";
            return "mc-mods";
        }

        /// <summary>Forge=1, Fabric=4, Quilt=5, NeoForge=6; 0 means no loader filter.</summary>
        public static int LoaderType(string loader)
        {
            if (string.Equals(loader, "forge", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(loader, "fabric", StringComparison.OrdinalIgnoreCase)) return 4;
            // Quilt 直接加载 Fabric 模组，用 Fabric 过滤可以拿到更多可用结果。
            if (string.Equals(loader, "quilt", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(loader, "neoforge", StringComparison.OrdinalIgnoreCase)) return 6;
            return 0;
        }

        public static string LoaderName(int type)
        {
            switch (type)
            {
                case 1: return "Forge";
                case 4: return "Fabric";
                case 5: return "Quilt";
                case 6: return "NeoForge";
                default: return "";
            }
        }

        public static string SearchUrl(string query, string mc, string loader, int offset, int limit, string kind)
        {
            string url = BaseUrl + "/v1/mods/search?gameId=" + GameId +
                         "&classId=" + ClassId(kind) +
                         "&index=" + offset.ToString() +
                         "&pageSize=" + limit.ToString() +
                         "&sortField=2&sortOrder=desc";
            if (!string.IsNullOrEmpty(query)) url += "&searchFilter=" + Uri.EscapeDataString(query);
            if (!string.IsNullOrEmpty(mc)) url += "&gameVersion=" + Uri.EscapeDataString(mc);
            int loaderType = LoaderType(loader);
            if (loaderType > 0 && string.Equals(kind, "mod", StringComparison.OrdinalIgnoreCase))
                url += "&modLoaderType=" + loaderType.ToString();
            return url;
        }

        public static string FilesUrl(string modId, string mc, string loader)
        {
            string url = BaseUrl + "/v1/mods/" + Uri.EscapeDataString(modId) + "/files?pageSize=50";
            if (!string.IsNullOrEmpty(mc)) url += "&gameVersion=" + Uri.EscapeDataString(mc);
            int loaderType = LoaderType(loader);
            if (loaderType > 0) url += "&modLoaderType=" + loaderType.ToString();
            return url;
        }

        public static string ModUrl(string modId)
        {
            return BaseUrl + "/v1/mods/" + Uri.EscapeDataString(modId);
        }

        public static string DownloadUrl(string modId, string fileId)
        {
            return BaseUrl + "/v1/mods/" + Uri.EscapeDataString(modId) + "/files/" +
                   Uri.EscapeDataString(fileId) + "/download-url";
        }

        /// <summary>Some authors disable third-party downloads; the CDN path still works.</summary>
        public static string CdnUrl(string fileId, string fileName)
        {
            long id;
            if (!long.TryParse(fileId, out id) || string.IsNullOrEmpty(fileName)) return "";
            return "https://edge.forgecdn.net/files/" + (id / 1000).ToString() + "/" +
                   (id % 1000).ToString() + "/" + Uri.EscapeDataString(fileName);
        }

        public static void EnsureDownloadUrl(ModFile file)
        {
            if (file == null || !string.IsNullOrEmpty(file.DownloadUrl)) return;
            if (!string.Equals(file.Source, "curseforge", StringComparison.OrdinalIgnoreCase)) return;
            if (string.IsNullOrEmpty(file.Id)) return;
            if (!string.IsNullOrEmpty(file.ModId))
            {
                try
                {
                    string json = GetJson(DownloadUrl(file.ModId, file.Id), 20000);
                    string direct = JMap.Parse(json).Str("data", "");
                    if (!string.IsNullOrEmpty(direct))
                    {
                        file.DownloadUrl = direct;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("获取 CurseForge 下载地址失败: " + ex.Message);
                }
            }
            file.DownloadUrl = CdnUrl(file.Id, file.FileName);
        }
    }
}
