using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CubeLauncher.Core
{
    /// <summary>
    /// Lightweight, best-effort English to Chinese translation for search cards.
    /// MyMemory is keyless and returns a single string; results are cached in
    /// memory so paging/reopening does not translate the same mod twice.
    /// </summary>
    public static class Translator
    {
        private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>();
        private static readonly object Gate = new object();
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

        public static void TranslateProjects(List<ModProject> projects, Action<ModProject> updated)
        {
            if (projects == null || projects.Count == 0) return;
            List<ModProject> snapshot = new List<ModProject>();
            for (int i = 0; i < projects.Count; i++)
            {
                if (projects[i] != null && !string.IsNullOrEmpty(projects[i].Name)) snapshot.Add(projects[i]);
            }
            if (snapshot.Count == 0) return;

            int count = Math.Min(4, snapshot.Count);
            int cursor = -1;
            int remaining = snapshot.Count;
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
                            Log.Warn("模组翻译失败: " + ex.Message);
                        }
                        Interlocked.Decrement(ref remaining);
                    }
                });
            }
        }

        private static void TranslateOne(ModProject project)
        {
            string name = (project.Name ?? "").Trim();
            string summary = (project.Summary ?? "").Trim();
            if (name.Length == 0) return;

            string known;
            bool hasKnown = KnownNames.TryGetValue(name, out known);
            if (hasKnown) project.Name = known;
            if (HasChinese(name) && (summary.Length == 0 || HasChinese(summary))) return;

            if (summary.Length > 260) summary = summary.Substring(0, 260);
            string source = name + (summary.Length == 0 ? "" : "\n" + summary);
            string translated = TranslateText(source);
            if (string.IsNullOrEmpty(translated)) return;

            string[] lines = translated.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 2)
            {
                if (!hasKnown) project.Name = lines[0].Trim();
                if (summary.Length > 0) project.Summary = string.Join(" ", lines, 1, lines.Length - 1).Trim();
            }
            else if (lines.Length == 1 && summary.Length == 0 && !hasKnown)
            {
                project.Name = lines[0].Trim();
            }
        }

        private static string TranslateText(string text)
        {
            string key = text.Trim();
            lock (Gate)
            {
                string cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }
            try
            {
                string url = "https://api.mymemory.translated.net/get?q=" +
                             Uri.EscapeDataString(key) + "&langpair=en%7Czh-CN";
                string json = Http.GetText(new List<string>() { url }, 20000);
                JMap root = JMap.Parse(json);
                JMap response = root.Map("responseData");
                string translated = response.Str("translatedText", "");
                if (string.IsNullOrEmpty(translated) || translated.IndexOf("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "";
                translated = translated.Replace("&quot;", "\"");
                lock (Gate) Cache[key] = translated;
                return translated;
            }
            catch (Exception ex)
            {
                Log.Warn("翻译接口失败: " + ex.Message);
                return "";
            }
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
    }
}
