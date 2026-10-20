using System;
using System.Collections.Generic;

namespace CubeLauncher.Core
{
    /// <summary>
    /// Central place for China-first endpoint selection. Every network operation uses these
    /// helpers, so switching "mirror first" in settings changes the whole launcher.
    /// </summary>
    public static class Mirror
    {
        public const string BmclApi = "https://bmclapi2.bangbang93.com";
        public const string Mcim = "https://mod.mcimirror.top";
        public const string TunaAdoptium = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium";

        public static List<string> Urls(string official, bool mirrorFirst)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(official)) return result;
            if (mirrorFirst)
            {
                string mirror = Rewrite(official);
                if (!string.IsNullOrEmpty(mirror) && !Same(mirror, official)) result.Add(mirror);
            }
            if (!Contains(result, official)) result.Add(official);
            return result;
        }

        public static List<string> Urls(string official, string extra, bool mirrorFirst)
        {
            List<string> result = Urls(official, mirrorFirst);
            if (!string.IsNullOrEmpty(extra) && !Contains(result, extra)) result.Add(extra);
            return result;
        }

        public static string Rewrite(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return url;

            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath;
            string query = uri.Query;

            switch (host)
            {
                case "launchermeta.mojang.com":
                case "piston-meta.mojang.com":
                case "piston-data.mojang.com":
                case "launcher.mojang.com":
                    return BmclApi + path + query;
                case "resources.download.minecraft.net":
                    return BmclApi + "/assets" + path + query;
                case "libraries.minecraft.net":
                    return BmclApi + "/maven" + path + query;
                case "maven.minecraftforge.net":
                case "files.minecraftforge.net":
                    if (path.StartsWith("/maven/", StringComparison.OrdinalIgnoreCase))
                        return BmclApi + "/maven" + path.Substring(6) + query;
                    return BmclApi + "/maven" + path + query;
                case "maven.fabricmc.net":
                    return BmclApi + "/maven" + path + query;
                case "meta.fabricmc.net":
                    return BmclApi + "/fabric-meta" + path + query;
                case "authlib-injector.yushi.moe":
                    return BmclApi + "/mirrors/authlib-injector" + path + query;
                case "api.modrinth.com":
                    return Mcim + "/modrinth" + path + query;
                case "cdn.modrinth.com":
                    return Mcim + path + query;
                default:
                    return url;
            }
        }

        public static List<string> VersionManifest(bool mirrorFirst)
        {
            return Urls("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", mirrorFirst);
        }

        public static List<string> VersionJson(string id, string officialUrl, bool mirrorFirst)
        {
            List<string> urls = new List<string>();
            if (mirrorFirst) urls.Add(BmclApi + "/version/" + Uri.EscapeDataString(id) + "/json");
            if (!string.IsNullOrEmpty(officialUrl)) urls.Add(officialUrl);
            if (!mirrorFirst) urls.Add(BmclApi + "/version/" + Uri.EscapeDataString(id) + "/json");
            return urls;
        }

        public static List<string> FabricMeta(string path, bool mirrorFirst)
        {
            string official = "https://meta.fabricmc.net" + path;
            List<string> urls = new List<string>();
            if (mirrorFirst) urls.Add(BmclApi + "/fabric-meta" + path);
            urls.Add(official);
            return urls;
        }

        public static List<string> QuiltMeta(string path)
        {
            return new List<string>() { "https://meta.quiltmc.org" + path };
        }

        public static List<string> ForgeMetadata(bool mirrorFirst)
        {
            return Urls("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml", mirrorFirst);
        }

        public static List<string> NeoForgeMetadata(bool mirrorFirst, string artifact)
        {
            string official = "https://maven.neoforged.net/releases/net/neoforged/" + artifact + "/maven-metadata.xml";
            List<string> urls = new List<string>();
            urls.Add("https://neoforged.forgecdn.net/releases/net/neoforged/" + artifact + "/maven-metadata.xml");
            if (mirrorFirst) urls.Add(BmclApi + "/maven/net/neoforged/" + artifact + "/maven-metadata.xml");
            urls.Add(official);
            return urls;
        }

        public static List<string> NeoForgeInstaller(string version, string artifact, bool mirrorFirst)
        {
            string path = artifact + "/" + version + "/" + artifact + "-" + version + "-installer.jar";
            string official = "https://maven.neoforged.net/releases/net/neoforged/" + path;
            List<string> urls = new List<string>();
            urls.Add("https://neoforged.forgecdn.net/releases/net/neoforged/" + path);
            if (mirrorFirst) urls.Add(BmclApi + "/maven/net/neoforged/" + path);
            urls.Add(official);
            return urls;
        }

        public static List<string> ForgePromotions(bool mirrorFirst)
        {
            // BMCLAPI does not mirror this small JSON; the official page is normally reachable.
            return new List<string>() { "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json" };
        }

        public static List<string> ForgeInstaller(string mc, string forge, bool mirrorFirst)
        {
            string official = "https://maven.minecraftforge.net/net/minecraftforge/forge/" + mc + "-" + forge + "/forge-" + mc + "-" + forge + "-installer.jar";
            List<string> urls = new List<string>();
            if (mirrorFirst)
                urls.Add(BmclApi + "/forge/download?mcversion=" + Uri.EscapeDataString(mc) + "&version=" + Uri.EscapeDataString(forge) + "&category=installer&format=jar");
            urls.Add(official);
            return urls;
        }

        public static List<string> AuthlibInjectorMeta(bool mirrorFirst)
        {
            List<string> urls = new List<string>();
            if (mirrorFirst) urls.Add(BmclApi + "/mirrors/authlib-injector/artifact/latest.json");
            urls.Add("https://authlib-injector.yushi.moe/artifact/latest.json");
            return urls;
        }

        public static List<string> ModrinthSearch(string query, string facets, int offset, int limit)
        {
            string path = "/v2/search?query=" + Uri.EscapeDataString(query ?? "") + "&facets=" + Uri.EscapeDataString(facets ?? "") +
                          "&offset=" + offset.ToString() + "&limit=" + limit.ToString() + "&index=relevance";
            return new List<string>() { Mcim + "/modrinth" + path, "https://api.modrinth.com" + path };
        }

        public static List<string> ModrinthVersions(string project, string mc, string loader)
        {
            string path = "/v2/project/" + Uri.EscapeDataString(project) + "/version";
            if (!string.IsNullOrEmpty(mc))
                path += "?game_versions=" + Uri.EscapeDataString("[\"" + mc + "\"]");
            if (!string.IsNullOrEmpty(loader))
                path += (path.IndexOf('?') >= 0 ? "&" : "?") + "loaders=" + Uri.EscapeDataString("[\"" + loader + "\"]");
            return new List<string>() { Mcim + "/modrinth" + path, "https://api.modrinth.com" + path };
        }

        public static List<string> ModrinthProject(string project)
        {
            string path = "/v2/project/" + Uri.EscapeDataString(project);
            return new List<string>() { Mcim + "/modrinth" + path, "https://api.modrinth.com" + path };
        }

        public static List<string> ModrinthVersion(string id)
        {
            string path = "/v2/version/" + Uri.EscapeDataString(id);
            return new List<string>() { Mcim + "/modrinth" + path, "https://api.modrinth.com" + path };
        }

        public static List<string> ModFileUrls(string officialDownload, bool mirrorFirst)
        {
            return Urls(officialDownload, mirrorFirst);
        }

        private static bool Contains(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
