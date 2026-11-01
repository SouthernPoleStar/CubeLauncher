using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CubeLauncher.Core
{
    public sealed class AppConfig
    {
        public string GameDir = "";
        public string JavaPath = "";
        public string SelectedVersion = "";
        public string SelectedAccountId = "";
        public int MemoryMb = 4096;
        public string JvmArgs = "";
        public bool MirrorFirst = true;
        public bool CloseAfterLaunch = false;
        public bool ShowSnapshots = false;
        public string ModSource = "modrinth";
        public int DownloadThreads = 6;
        public string MicrosoftClientId = AuthService.DefaultMicrosoftClientId;
        public bool AutoDownloadJava = true;
        public bool PremiumUnlocked = false;
        public string PremiumUnlockedName = "";
        public long PremiumUnlockedAt = 0;
        public List<Account> Accounts = new List<Account>();

        public const string PremiumRequiredMessage = "离线账户与第三方皮肤站需要先完成一次微软正版登录才能使用";

        [ScriptIgnore]
        public string RootDir = "";
        [ScriptIgnore]
        public string ConfigPath = "";

        [ScriptIgnore]
        public string VersionsDir { get { return Path.Combine(GameDir, "versions"); } }
        [ScriptIgnore]
        public string LibrariesDir { get { return Path.Combine(GameDir, "libraries"); } }
        [ScriptIgnore]
        public string AssetsDir { get { return Path.Combine(GameDir, "assets"); } }
        [ScriptIgnore]
        public string InstancesDir { get { return Path.Combine(GameDir, "instances"); } }
        [ScriptIgnore]
        public string CacheDir { get { return Path.Combine(RootDir, "cache"); } }
        [ScriptIgnore]
        public string JavaDir { get { return Path.Combine(RootDir, "java"); } }
        [ScriptIgnore]
        public string LogsDir { get { return Path.Combine(RootDir, "logs"); } }

        public string InstanceDir(string versionId)
        {
            return Path.Combine(InstancesDir, Util.SafeFileName(versionId));
        }

        public string ModsDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "mods");
        }

        public string ResourcePacksDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "resourcepacks");
        }

        public string ShaderPacksDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "shaderpacks");
        }

        public string ModpacksDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "modpacks");
        }

        public string SavesDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "saves");
        }

        public string ScreenshotsDir(string versionId)
        {
            return Path.Combine(InstanceDir(versionId), "screenshots");
        }

        public Account FirstPremiumAccount()
        {
            if (Accounts == null) return null;
            for (int i = 0; i < Accounts.Count; i++)
                if (Accounts[i] != null && Accounts[i].IsMicrosoft) return Accounts[i];
            return null;
        }

        public bool HasPremiumAccount()
        {
            return FirstPremiumAccount() != null;
        }

        public bool IsAccountLocked(Account account)
        {
            if (account == null) return false;
            if (PremiumUnlocked) return false;
            return account.IsOffline || account.IsYggdrasil;
        }

        public bool MarkPremiumUnlocked(Account account)
        {
            if (account == null || !account.IsMicrosoft) return false;
            if (PremiumUnlocked) return false;
            PremiumUnlocked = true;
            PremiumUnlockedName = account.Name ?? "";
            PremiumUnlockedAt = Util.UnixNow;
            Log.Info("已完成正版验证，离线账户与第三方皮肤站已解锁: " + PremiumUnlockedName);
            return true;
        }

        public Account FindAccount(string id)
        {
            if (Accounts == null) return null;
            for (int i = 0; i < Accounts.Count; i++)
                if (string.Equals(Accounts[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Accounts[i];
            return null;
        }

        public Account SelectedAccount()
        {
            Account account = FindAccount(SelectedAccountId);
            if (account == null && Accounts != null && Accounts.Count > 0) account = Accounts[0];
            return account;
        }

        public static AppConfig CreateDefault()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CubeLauncher");
            AppConfig config = new AppConfig();
            config.RootDir = root;
            config.ConfigPath = Path.Combine(root, "config.json");
            config.GameDir = Path.Combine(root, "minecraft");
            config.Accounts = new List<Account>();
            config.SelectedAccountId = "";
            return config;
        }

        public static AppConfig Load()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CubeLauncher");
            string path = Path.Combine(root, "config.json");
            AppConfig config = null;
            if (File.Exists(path))
            {
                try
                {
                    string text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    config = serializer.Deserialize<AppConfig>(text);
                }
                catch (Exception ex)
                {
                    Log.Warn("配置文件读取失败，将使用默认设置: " + ex.Message);
                }
            }
            if (config == null) config = CreateDefault();
            config.RootDir = root;
            config.ConfigPath = path;
            if (string.IsNullOrEmpty(config.GameDir)) config.GameDir = Path.Combine(root, "minecraft");
            if (config.Accounts == null) config.Accounts = new List<Account>();
            if (config.MemoryMb < 1024) config.MemoryMb = 1024;
            if (config.MemoryMb > 32768) config.MemoryMb = 32768;
            if (config.DownloadThreads < 1) config.DownloadThreads = 6;
            if (config.DownloadThreads > 16) config.DownloadThreads = 16;
            if (string.IsNullOrEmpty(config.MicrosoftClientId) ||
                string.Equals(config.MicrosoftClientId, "00000000402b5328", StringComparison.OrdinalIgnoreCase))
                config.MicrosoftClientId = AuthService.DefaultMicrosoftClientId;
            if (config.Accounts == null) config.Accounts = new List<Account>();
            if (!config.PremiumUnlocked)
            {
                Account premium = config.FirstPremiumAccount();
                if (premium != null)
                {
                    config.PremiumUnlocked = true;
                    config.PremiumUnlockedName = premium.Name ?? "";
                    if (config.PremiumUnlockedAt <= 0) config.PremiumUnlockedAt = Util.UnixNow;
                    Log.Info("检测到已保存的微软正版账户，视为已完成正版验证: " + config.PremiumUnlockedName);
                }
            }
            if (config.FindAccount(config.SelectedAccountId) == null)
                config.SelectedAccountId = config.Accounts.Count > 0 ? config.Accounts[0].Id : "";
            return config;
        }

        private static readonly object SaveGate = new object();

        public void Save()
        {
            lock (SaveGate)
            {
                try
                {
                    Util.EnsureDirectory(RootDir);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    Util.WriteAllTextAtomic(ConfigPath, serializer.Serialize(this));
                }
                catch (Exception ex)
                {
                    Log.Error("保存配置失败", ex);
                }
            }
        }
    }

    public static class App
    {
        public static AppConfig Config;
        public static string Version = "1.0.0";
        public static string Name = "方块启动器";

        public static event Action Changed;

        public static void Initialize()
        {
            Config = AppConfig.Load();
            Util.EnsureDirectory(Config.GameDir);
            Util.EnsureDirectory(Config.VersionsDir);
            Util.EnsureDirectory(Config.LibrariesDir);
            Util.EnsureDirectory(Config.AssetsDir);
            Util.EnsureDirectory(Config.InstancesDir);
            Util.EnsureDirectory(Config.CacheDir);
            Util.EnsureDirectory(Config.JavaDir);
            Util.EnsureDirectory(Config.LogsDir);
            Log.Initialize(Path.Combine(Config.LogsDir, "launcher.log"));
            EnsureLauncherProfiles();
            Log.Info("方块启动器 " + Version + " 启动，游戏目录: " + Config.GameDir);
            Log.Info("下载策略: " + (Config.MirrorFirst ? "镜像加速 (BMCLAPI / TUNA)，Modrinth / CurseForge 官方源" : "官方源优先"));
        }

        public static void Save()
        {
            Config.Save();
            Action handler = Changed;
            if (handler != null) handler();
        }

        public static void EnsureLauncherProfiles()
        {
            string path = Path.Combine(Config.GameDir, "launcher_profiles.json");
            if (File.Exists(path)) return;
            try
            {
                JMap root = new JMap();
                root.Set("profiles", new Dictionary<string, object>());
                JMap settings = new JMap();
                settings.Set("profileName", "CubeLauncher");
                settings.Set("gameDir", Config.GameDir);
                settings.Set("javaDir", Config.JavaPath ?? "");
                settings.Set("javaArgs", "-Xmx" + Config.MemoryMb + "M");
                root.Set("settings", settings.Raw);
                root.Set("version", 3);
                Util.WriteAllTextAtomic(path, Json.Stringify(root.Raw));
            }
            catch (Exception ex)
            {
                Log.Warn("创建 launcher_profiles.json 失败: " + ex.Message);
            }
        }
    }
}
