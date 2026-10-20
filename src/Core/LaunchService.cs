using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CubeLauncher.Core
{
    public sealed class LaunchOptions
    {
        public string VersionId = "";
        public Account Account;
        public int MemoryMb = 4096;
        public string ExtraJvmArgs = "";
        public string ExtraGameArgs = "";
    }

    public sealed class LaunchPlan
    {
        public string JavaPath = "";
        public string Arguments = "";
        public string WorkingDirectory = "";
        public string InstanceDirectory = "";
        public string VersionId = "";
        public int JavaMajor;
        public ResolvedVersion Version;

        public string CommandPreview
        {
            get { return Util.RedactToken(Util.Quote(JavaPath) + " " + Arguments); }
        }
    }

    public static class LaunchService
    {
        public static event Action<string, int> GameExited;

        public static LaunchPlan Prepare(AppConfig config, LaunchOptions options, ProgressSink progress)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (string.IsNullOrEmpty(options.VersionId)) throw new Exception("请选择一个版本");
            if (options.Account == null) throw new Exception("请先选择一个账户");

            VersionManager manager = new VersionManager(config);
            ResolvedVersion resolved = manager.Resolve(options.VersionId, progress);
            progress.SetStage("检查账户登录状态");
            AuthService.EnsureReady(options.Account, config, progress);
            App.Save();

            progress.SetStage("准备 Java " + resolved.JavaMajor);
            JavaInfo java = JavaManager.Ensure(config, resolved.JavaMajor, progress);
            resolved = manager.EnsureRuntime(options.VersionId, progress);

            if (IsFabricLike(resolved))
            {
                EnsureFabricApi(config, resolved, progress);
            }

            progress.SetStage("生成启动参数");
            LaunchPlan plan = Build(config, resolved, options, java, progress);
            progress.Report(1, "启动准备完成");
            return plan;
        }

        private static bool IsFabricLike(ResolvedVersion version)
        {
            string id = (version.Id ?? "").ToLowerInvariant();
            if (id.Contains("fabric") || id.Contains("quilt")) return true;
            string main = (version.MainClass ?? "").ToLowerInvariant();
            return main.Contains("fabric") || main.Contains("quilt") || main.Contains("knot");
        }

        private static void EnsureFabricApi(AppConfig config, ResolvedVersion version, ProgressSink progress)
        {
            string mods = config.ModsDir(version.Id);
            if (!Directory.Exists(mods)) return;
            string[] jars = Directory.GetFiles(mods, "*.jar");
            if (jars.Length == 0) return;
            bool hasApi = false;
            bool hasOther = false;
            for (int i = 0; i < jars.Length; i++)
            {
                string name = Path.GetFileName(jars[i]).ToLowerInvariant();
                if (name.Contains("fabric-api") || name.Contains("fabric_api")) hasApi = true;
                else if (!name.Contains("sodium")) hasOther = true;
            }
            if (hasApi || !hasOther) return;

            progress.SetStage("检测到 Fabric 模组，自动安装 Fabric API 前置");
            try
            {
                ModSearchResult search = ModService.Search(config, "modrinth", "fabric api", version.BaseId, "fabric", 0, 8);
                ModProject project = null;
                for (int i = 0; i < search.Projects.Count; i++)
                {
                    if (string.Equals(search.Projects[i].Slug, "fabric-api", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(search.Projects[i].Id, "P7dR8mSH", StringComparison.OrdinalIgnoreCase))
                    {
                        project = search.Projects[i];
                        break;
                    }
                }
                if (project == null && search.Projects.Count > 0) project = search.Projects[0];
                if (project == null) throw new Exception("没有找到 Fabric API");
                List<ModFile> files = ModService.LoadFiles(config, project, version.BaseId, "fabric");
                if (files.Count == 0) throw new Exception("没有找到适配 " + version.BaseId + " 的 Fabric API");
                List<string> installed = ModService.Install(config, project, files[0], version.BaseId, "fabric", version.Id, progress);
                Log.Info("已自动安装 Fabric API 前置: " + string.Join(", ", installed.ToArray()));
            }
            catch (Exception ex)
            {
                Log.Warn("Fabric API 自动安装失败: " + ex.Message);
                throw new Exception("检测到 Fabric 模组缺少 Fabric API，自动安装前置失败: " + ex.Message);
            }
        }

        public static LaunchPlan Build(AppConfig config, ResolvedVersion version, LaunchOptions options, JavaInfo java, ProgressSink progress)
        {            string instanceDir = config.InstanceDir(version.Id);
            Util.EnsureDirectory(instanceDir);
            Util.EnsureDirectory(Path.Combine(instanceDir, "mods"));

            string classpath = BuildClasspath(config, version);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Account account = options.Account;
            string uuid = string.IsNullOrEmpty(account.Uuid) ? Util.OfflineUuid(account.Name) : account.Uuid.Replace("-", "");
            string token = account.AccessToken;
            if (string.IsNullOrEmpty(token)) token = "0";
            string userType = account.IsMicrosoft ? "msa" : (account.IsYggdrasil ? "mojang" : "legacy");
            string virtualAssets = Path.Combine(config.AssetsDir, "virtual", version.AssetIndexId);

            values["auth_player_name"] = account.Name;
            values["version_name"] = string.IsNullOrEmpty(version.BaseId) ? version.Id : version.BaseId;
            values["game_directory"] = instanceDir;
            values["assets_root"] = config.AssetsDir;
            values["assets_index_name"] = version.AssetIndexId;
            values["auth_uuid"] = uuid;
            values["auth_access_token"] = token;
            values["clientid"] = "";
            values["auth_xuid"] = account.Xuid ?? "";
            values["user_type"] = userType;
            values["version_type"] = version.Type;
            values["natives_directory"] = version.NativesDir;
            values["launcher_name"] = "CubeLauncher";
            values["launcher_version"] = App.Version;
            values["classpath"] = classpath;
            values["classpath_separator"] = ";";
            values["library_directory"] = config.LibrariesDir;
            values["game_assets"] = virtualAssets;
            values["auth_session"] = "token:" + token + ":" + uuid;
            values["user_properties"] = "{}";
            values["resolution_width"] = "1280";
            values["resolution_height"] = "720";
            values["quickPlayPath"] = "";
            values["quickPlaySingleplayer"] = "";
            values["quickPlayMultiplayer"] = "";
            values["quickPlayRealms"] = "";

            EnsureLogConfig(config, version, values, progress);

            List<string> jvmArgs = new List<string>();
            jvmArgs.Add("-Xmx" + options.MemoryMb + "M");
            jvmArgs.Add("-Xms" + Math.Min(options.MemoryMb, 1024) + "M");
            jvmArgs.Add("-Dfile.encoding=UTF-8");
            jvmArgs.Add("-Dstdout.encoding=UTF-8");
            jvmArgs.Add("-Dstderr.encoding=UTF-8");
            jvmArgs.Add("-Dminecraft.launcher.brand=CubeLauncher");
            jvmArgs.Add("-Dminecraft.launcher.version=" + App.Version);
            jvmArgs.Add("-Dlog4j2.formatMsgNoLookups=true");

            if (account.IsYggdrasil)
            {
                progress.SetStage("配置第三方皮肤站登录");
                string injector = AuthService.EnsureAuthlibInjector(config, progress);
                string metadata = AuthService.GetYggdrasilMetadata(account.AuthServer);
                string prefetched = Convert.ToBase64String(Encoding.UTF8.GetBytes(metadata));
                jvmArgs.Add("-javaagent:" + injector + "=" + account.AuthServer);
                jvmArgs.Add("-Dauthlibinjector.yggdrasil.prefetched=" + prefetched);
            }

            JMap arguments = version.Json.Map("arguments");
            List<string> configuredJvm = CollectArguments(arguments.List("jvm"), values);
            if (configuredJvm.Count > 0)
            {
                jvmArgs.AddRange(configuredJvm);
            }
            else
            {
                jvmArgs.Add("-Djava.library.path=" + version.NativesDir);
                jvmArgs.Add("-cp");
                jvmArgs.Add(classpath);
            }

            if (!HasClasspath(jvmArgs))
            {
                jvmArgs.Add("-cp");
                jvmArgs.Add(classpath);
            }
            if (!HasLibraryPath(jvmArgs))
                jvmArgs.Add("-Djava.library.path=" + version.NativesDir);

            List<string> gameArgs = new List<string>();
            if (arguments.Has("game"))
            {
                gameArgs.AddRange(CollectArguments(arguments.List("game"), values));
            }
            else if (version.Json.Has("minecraftArguments"))
            {
                gameArgs.AddRange(SplitArgs(Replace(version.Json.Str("minecraftArguments", ""), values)));
            }
            if (!HasArgument(gameArgs, "--gameDir") && !HasArgument(gameArgs, "--gameDirectory"))
            {
                gameArgs.Insert(0, "--gameDir");
                gameArgs.Insert(1, instanceDir);
            }
            if (!HasArgument(gameArgs, "--assetsDir"))
            {
                gameArgs.Add("--assetsDir");
                gameArgs.Add(config.AssetsDir);
            }
            if (!HasArgument(gameArgs, "--assetIndex"))
            {
                gameArgs.Add("--assetIndex");
                gameArgs.Add(version.AssetIndexId);
            }
            if (!HasArgument(gameArgs, "--username"))
            {
                gameArgs.Add("--username");
                gameArgs.Add(account.Name);
            }
            if (!HasArgument(gameArgs, "--uuid"))
            {
                gameArgs.Add("--uuid");
                gameArgs.Add(uuid);
            }
            if (!HasArgument(gameArgs, "--accessToken"))
            {
                gameArgs.Add("--accessToken");
                gameArgs.Add(token);
            }
            if (!HasArgument(gameArgs, "--version"))
            {
                gameArgs.Add("--version");
                gameArgs.Add(version.Id);
            }
            if (!HasArgument(gameArgs, "--userType"))
            {
                gameArgs.Add("--userType");
                gameArgs.Add(userType);
            }
            if (!string.IsNullOrEmpty(options.ExtraGameArgs))
                gameArgs.AddRange(SplitArgs(options.ExtraGameArgs));

            if (!string.IsNullOrEmpty(options.ExtraJvmArgs))
            {
                List<string> extra = SplitArgs(options.ExtraJvmArgs);
                jvmArgs.InsertRange(0, extra);
            }

            string mainClass = version.MainClass;
            if (string.IsNullOrEmpty(mainClass)) throw new Exception("版本 JSON 缺少 mainClass");

            List<string> all = new List<string>();
            all.AddRange(jvmArgs);
            all.Add(mainClass);
            all.AddRange(gameArgs);

            string argumentsText = string.Join(" ", QuoteAll(all).ToArray());
            LaunchPlan plan = new LaunchPlan();
            plan.JavaPath = java.Path;
            plan.Arguments = argumentsText;
            plan.WorkingDirectory = instanceDir;
            plan.InstanceDirectory = instanceDir;
            plan.VersionId = version.Id;
            plan.JavaMajor = java.Major;
            plan.Version = version;
            return plan;
        }

        public static ProcessStartResult Start(LaunchPlan plan)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            Log.Info("启动 " + plan.VersionId + " (Java " + plan.JavaMajor + ")");
            Log.Info("命令: " + plan.CommandPreview);
            ProcessStartResult process = ProcessRunner.Start(plan.JavaPath, plan.Arguments, plan.WorkingDirectory,
                delegate(string line) { Log.Info("[MC] " + line); },
                delegate(string line) { Log.Warn("[MC] " + line); });
            System.Threading.Thread watcher = new System.Threading.Thread(delegate()
            {
                try
                {
                    process.WaitForExit();
                    int code = process.ExitCode;
                    Log.Info("游戏进程退出，退出码 " + code);
                    Action<string, int> handler = GameExited;
                    if (handler != null) handler(plan.VersionId, code);
                }
                catch (Exception ex)
                {
                    Log.Warn("等待游戏进程结束失败: " + ex.Message);
                }
            });
            watcher.IsBackground = true;
            watcher.Start();
            return process;
        }

        public static string BuildClasspath(AppConfig config, ResolvedVersion version)
        {
            List<string> entries = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < version.Libraries.Count; i++)
            {
                LibraryFile library = version.Libraries[i];
                string path = Path.Combine(config.LibrariesDir, library.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) continue;
                if (seen.Add(path)) entries.Add(path);
            }
            if (File.Exists(version.ClientJarPath) && seen.Add(version.ClientJarPath)) entries.Add(version.ClientJarPath);
            return string.Join(";", entries.ToArray());
        }

        private static List<string> CollectArguments(IList<object> list, Dictionary<string, string> values)
        {
            List<string> result = new List<string>();
            if (list == null) return result;
            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                if (item is string)
                {
                    result.Add(Replace((string)item, values));
                    continue;
                }
                JMap map = new JMap(item);
                if (map.Count == 0) continue;
                if (!Rules.Allowed(map.List("rules"))) continue;
                object value = map.Get("value");
                IList<object> multiple = JMap.AsList(value);
                if (multiple != null)
                {
                    for (int v = 0; v < multiple.Count; v++)
                        result.Add(Replace(Convert.ToString(multiple[v]), values));
                }
                else if (value != null)
                {
                    result.Add(Replace(Convert.ToString(value), values));
                }
            }
            return result;
        }

        private static string Replace(string input, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string output = input;
            foreach (KeyValuePair<string, string> pair in values)
            {
                string token = "${" + pair.Key + "}";
                if (output.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    output = output.Replace(token, pair.Value ?? "");
            }
            return output;
        }

        private static bool HasClasspath(List<string> args)
        {
            for (int i = 0; i < args.Count; i++)
                if (string.Equals(args[i], "-cp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "-classpath", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasLibraryPath(List<string> args)
        {
            for (int i = 0; i < args.Count; i++)
                if (args[i].StartsWith("-Djava.library.path=", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool HasArgument(List<string> args, string name)
        {
            for (int i = 0; i < args.Count; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void EnsureLogConfig(AppConfig config, ResolvedVersion version, Dictionary<string, string> values, ProgressSink progress)
        {
            JMap logging = version.Json.Map("logging");
            JMap client = logging.Map("client");
            if (client.Count == 0) return;
            string argument = client.Str("argument", "");
            JMap file = client.Map("file");
            string id = file.Str("id", "client");
            string url = file.Str("url", "");
            if (string.IsNullOrEmpty(url)) return;
            string path = Path.Combine(config.AssetsDir, "log_configs", Util.SafeFileName(id));
            try
            {
                DownloadItem item = new DownloadItem();
                item.Name = "日志配置 " + id;
                item.Destination = path;
                item.Urls = Mirror.Urls(url, config.MirrorFirst);
                item.Sha1 = file.Str("sha1", "");
                item.Size = file.Long("size", 0);
                if (!File.Exists(path) || (!string.IsNullOrEmpty(item.Sha1) && !string.Equals(Util.Sha1File(path), item.Sha1, StringComparison.OrdinalIgnoreCase)))
                {
                    progress.SetStage("更新日志配置");
                    Http.DownloadOne(item, progress.Token, null);
                }
                values["path"] = path;
            }
            catch (Exception ex)
            {
                Log.Warn("日志配置下载失败: " + ex.Message);
            }
        }

        private static List<string> QuoteAll(List<string> args)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < args.Count; i++)
                result.Add(Util.Quote(args[i] ?? ""));
            return result;
        }

        public static List<string> SplitArgs(string text)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;
            MatchCollection matches = Regex.Matches(text, "\"([^\"]*)\"|(\\S+)");
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].Groups[1].Success) result.Add(matches[i].Groups[1].Value);
                else result.Add(matches[i].Groups[2].Value);
            }
            return result;
        }
    }
}
