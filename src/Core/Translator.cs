using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CubeLauncher.Core
{
    /// <summary>
    /// Best-effort Chinese names and summaries from MC百科 (mcmod.cn).
    /// The site has no public JSON API, so this reads its public search page
    /// and keeps a small in-memory cache. If MC百科 is unavailable the local
    /// fallback table is used, and the original English text is never lost.
    /// </summary>
    public static class Translator
    {
        private sealed class McmodInfo
        {
            public string Name = "";
            public string Summary = "";
        }

        private sealed class SearchItem
        {
            public string Title = "";
            public string Body = "";
        }

        private static readonly Dictionary<string, McmodInfo> Cache =
            new Dictionary<string, McmodInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new object();
        private static readonly object RateGate = new object();
        private static DateTime _lastRequest = DateTime.MinValue;

        private static readonly Dictionary<string, string> KnownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Just Enough Items (JEI)", "JEI 物品管理器" },
            { "JEI", "JEI 物品管理器" },
            { "Sodium", "钠 (Sodium)" },
            { "Lithium", "锂 (Lithium)" },
            { "Iris Shaders", "Iris 光影" },
            { "Iris", "Iris 光影" },
            { "Entity Culling", "实体剔除" },
            { "Cloth Config API", "布料配置 API" },
            { "Fabric API", "Fabric API" },
            { "Mod Menu", "模组菜单" },
            { "FerriteCore", "铁氧体核心" },
            { "Architectury API", "Architectury API" },
            { "Create", "机械动力 (Create)" },
            { "JourneyMap", "旅行地图" },
            { "Xaero's Minimap", "Xaero 小地图" },
            { "AppleSkin", "苹果皮" }
        };

        private static readonly Dictionary<string, string> KnownSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Sodium", "Minecraft 的高性能渲染引擎替代品，大幅提高帧率并减少微卡顿。" },
            { "Iris Shaders", "现代的 Minecraft 光影包加载器，兼容现有 OptiFine 光影包。" },
            { "Entity Culling", "使用异步路径追踪隐藏不可见的方块和实体。" },
            { "Cloth Config API", "Minecraft 模组的配置库。" },
            { "Just Enough Items (JEI)", "查看物品与合成配方。" }
        };

        public static void TranslateProjects(List<ModProject> projects, Action<ModProject> updated)
        {
            if (projects == null || projects.Count == 0) return;
            List<ModProject> snapshot = new List<ModProject>();
            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] != null && !string.IsNullOrEmpty(projects[i].Name)) snapshot.Add(projects[i]);
            }
            if (snapshot.Count == 0) return;

            int count = Math.Min(3, snapshot.Count);
            int cursor = -1;
            for (int worker = 0; worker < count; worker++)
            {
                ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    while (true)
                    {
                        int index = Interlocked.Increment(ref cursor);
                        if (index >= snapshot.Count) return;
                        ModProject project = snapshot[index];
                        try
                        {
                            TranslateOne(project);
                            if (updated != null) updated(project);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("模组汉化失败: " + ex.Message);
                        }
                    }
                });
            }
        }

        private static void TranslateOne(ModProject project)
        {
            string originalName = (project.Name ?? "").Trim();
            string summary = (project.Summary ?? "").Trim();
            if (originalName.Length == 0) return;

            bool hasChineseName = HasChinese(originalName);
            bool hasChineseSummary = HasChinese(summary);
            if (hasChineseName && (summary.Length == 0 || hasChineseSummary)) return;

            McmodInfo info = Lookup(originalName, project.Slug);
            if (info != null)
            {
                if (!hasChineseName && !string.IsNullOrEmpty(info.Name) && HasChinese(info.Name))
                    project.Name = info.Name;
                if (!hasChineseSummary && !string.IsNullOrEmpty(info.Summary) && HasChinese(info.Summary))
                    project.Summary = info.Summary;
                if (!string.Equals(project.Name, originalName, StringComparison.Ordinal))
                    Log.Info("MC百科汉化: " + originalName + " -> " + project.Name);
            }

            // Local fallbacks keep the common mods readable when mcmod.cn is
            // unreachable or the page layout changes.
            if (!HasChinese(project.Name))
            {
                string known;
                if (KnownNames.TryGetValue(originalName, out known)) project.Name = known;
            }
            if (!HasChinese(project.Summary))
            {
                string knownSummary;
                if (KnownSummaries.TryGetValue(originalName, out knownSummary)) project.Summary = knownSummary;
            }
        }

        private static McmodInfo Lookup(string name, string slug)
        {
            string key = Normalize(name) + "|" + Normalize(slug);
            lock (Gate)
            {
                McmodInfo cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }

            try
            {
                McmodInfo info = Search(name, slug);
                string baseName = BaseName(name);
                if (info == null && !string.IsNullOrEmpty(baseName) &&
                    !string.Equals(baseName, name, StringComparison.OrdinalIgnoreCase))
                {
                    info = Search(baseName, slug);
                }
                if (info == null && !string.IsNullOrEmpty(slug) &&
                    !string.Equals(slug, name, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(slug, baseName, StringComparison.OrdinalIgnoreCase))
                {
                    info = Search(slug.Replace('-', ' '), slug);
                }
                if (info == null)
                {
                    // mcmod.cn occasionally serves an empty/challenge page when
                    // several cards are translated at once; retry once.
                    Thread.Sleep(600);
                    info = Search(name, slug);
                }
                lock (Gate) Cache[key] = info;
                return info;
            }
            catch (Exception ex)
            {
                Log.Warn("MC百科查询失败: " + ex.Message);
                lock (Gate) Cache[key] = null;
                return null;
            }
        }

        private static McmodInfo Search(string query, string slug)
        {
            Throttle();
            string url = "https://search.mcmod.cn/s?key=" + Uri.EscapeDataString(query);
            string html = Http.GetText(new List<string>() { url }, 15000);
            List<SearchItem> items = ParseItems(html);
            if (items.Count == 0) return null;

            string normalizedQuery = Normalize(query);
            string normalizedBase = Normalize(BaseName(query));
            string normalizedSlug = Normalize(slug);
            int bestScore = int.MinValue;
            SearchItem best = null;
            for (int i = 0; i < items.Count; i++)
            {
                SearchItem item = items[i];
                int score = Score(item, normalizedQuery, normalizedBase, normalizedSlug, i);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }
            if (best == null || bestScore < 50) return null;

            McmodInfo info = new McmodInfo();
            info.Name = NameFromTitle(best.Title);
            info.Summary = SummaryFromBody(best.Body);
            if (!HasChinese(info.Name)) info.Name = "";
            if (!HasChinese(info.Summary)) info.Summary = "";
            if (string.IsNullOrEmpty(info.Name) && string.IsNullOrEmpty(info.Summary)) return null;
            return info;
        }

        private static List<SearchItem> ParseItems(string html)
        {
            List<SearchItem> result = new List<SearchItem>();
            MatchCollection blocks = Regex.Matches(html,
                "<div class=\"result-item\">(.*?)<div class=\"foot\">",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            for (int i = 0; i < blocks.Count; i++)
            {
                string block = blocks[i].Groups[1].Value;
                Match link = Regex.Match(block,
                    "<a[^>]+href=\"https?://www\\.mcmod\\.cn/class/\\d+\\.html\"[^>]*>(.*?)</a>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!link.Success) continue;
                Match body = Regex.Match(block,
                    "<div class=\"body\">(.*?)</div>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                SearchItem item = new SearchItem();
                item.Title = CleanText(link.Groups[1].Value);
                item.Body = body.Success ? CleanText(body.Groups[1].Value) : "";
                if (!string.IsNullOrEmpty(item.Title)) result.Add(item);
            }
            return result;
        }

        private static int Score(SearchItem item, string query, string baseQuery, string slug, int index)
        {
            string title = Normalize(item.Title);
            int score = -index;
            // The English part before a trailing "(JEI)" is the strongest
            // signal; otherwise add-ons such as "JEI Integrations" can win.
            if (baseQuery.Length > 1 && title.IndexOf(baseQuery, StringComparison.Ordinal) >= 0) score += 220;
            if (query.Length > 1 && title.IndexOf(query, StringComparison.Ordinal) >= 0) score += 120;
            if (slug.Length > 1 && title.IndexOf(slug, StringComparison.Ordinal) >= 0) score += 100;

            MatchCollection aliases = Regex.Matches(item.Title, "[\\(（]([^\\)）]*)[\\)）]");
            for (int i = 0; i < aliases.Count; i++)
            {
                string alias = Normalize(aliases[i].Groups[1].Value);
                if (alias.Length == 0) continue;
                if (alias == query) score += 500;
                else if (alias == baseQuery) score += 400;
                else if (alias == slug) score += 350;
                else if (baseQuery.Length > 1 && alias.IndexOf(baseQuery, StringComparison.Ordinal) >= 0) score += 150;
            }
            if (baseQuery.Length > 1 && Normalize(item.Body).IndexOf(baseQuery, StringComparison.Ordinal) >= 0) score += 40;
            return score;
        }

        private static string BaseName(string name)
        {
            string value = (name ?? "").Trim();
            while (true)
            {
                Match trailing = Regex.Match(value, "\\s*[\\(（]([^\\)）]*)[\\)）]\\s*$");
                if (!trailing.Success) break;
                value = value.Substring(0, trailing.Index).Trim();
            }
            return value;
        }

        private static string NameFromTitle(string title)
        {
            string name = (title ?? "").Trim();
            name = Regex.Replace(name, "^\\s*\\[[^\\]]*\\]\\s*", "");
            while (true)
            {
                Match trailing = Regex.Match(name, "\\s*[\\(（]([^\\)）]*)[\\)）]\\s*$");
                if (!trailing.Success) break;
                if (HasChinese(trailing.Groups[1].Value)) break;
                name = name.Substring(0, trailing.Index).Trim();
            }
            name = name.Replace("_", "").Trim(' ', '-', '—', '·');
            return name;
        }

        private static string SummaryFromBody(string body)
        {
            string summary = (body ?? "").Trim();
            summary = Regex.Replace(summary, "\\[[^\\]]*\\]", " ");
            summary = Regex.Replace(summary, "\\s+", " ").Trim();
            if (summary.Length > 220)
            {
                summary = summary.Substring(0, 220).TrimEnd(' ', '，', '。', '；', '：', ',', '.', ';', ':');
                summary += "…";
            }
            return summary;
        }

        private static string CleanText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string text = Regex.Replace(html, "<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            text = text.Replace('\u00a0', ' ');
            return Regex.Replace(text, "\\s+", " ").Trim();
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z') builder.Append((char)(c + 32));
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) builder.Append(c);
            }
            return builder.ToString();
        }

        private static bool HasChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '\u4e00' && text[i] <= '\u9fff') return true;
            }
            return false;
        }

        private static void Throttle()
        {
            lock (RateGate)
            {
                int wait = 1000 - (int)(DateTime.UtcNow - _lastRequest).TotalMilliseconds;
                if (wait > 0) Thread.Sleep(wait);
                _lastRequest = DateTime.UtcNow;
            }
        }
    }
}
