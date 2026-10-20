using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CubeLauncher.Core
{
    public sealed class MicrosoftDeviceCode
    {
        public string DeviceCode = "";
        public string UserCode = "";
        public string VerificationUri = "";
        public int ExpiresIn = 900;
        public int Interval = 5;
        public string Message = "";
    }

    public static class AuthService
    {
        // Public Prism Launcher client ID, approved for the Minecraft Services API.
        // It is a public client (no secret); users may override it in config.json.
        public const string DefaultMicrosoftClientId = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb";
        public const string MicrosoftRedirect = "https://login.live.com/oauth20_desktop.srf";
        public const string MicrosoftScope = "XboxLive.SignIn XboxLive.offline_access";

        private const string MicrosoftDeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
        private const string MicrosoftTokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        private const string MicrosoftAuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";

        public static string BuildMicrosoftAuthUrl(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) clientId = DefaultMicrosoftClientId;
            return MicrosoftAuthorizeUrl + "?client_id=" + Uri.EscapeDataString(clientId) +
                   "&response_type=code&redirect_uri=" + Uri.EscapeDataString(MicrosoftRedirect) +
                   "&scope=" + Uri.EscapeDataString(MicrosoftScope) + "&prompt=select_account";
        }

        public static string ExtractCode(string callbackOrCode)
        {
            if (string.IsNullOrEmpty(callbackOrCode)) return "";
            Match match = Regex.Match(callbackOrCode, "[?&]code=([^&\\s]+)", RegexOptions.IgnoreCase);
            if (match.Success) return Uri.UnescapeDataString(match.Groups[1].Value);
            match = Regex.Match(callbackOrCode.Trim(), "^[A-Za-z0-9._~-]+$");
            if (match.Success) return callbackOrCode.Trim();
            return "";
        }

        public static MicrosoftDeviceCode RequestMicrosoftDeviceCode(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) clientId = DefaultMicrosoftClientId;
            string body = "client_id=" + Uri.EscapeDataString(clientId) +
                          "&scope=" + Uri.EscapeDataString(MicrosoftScope);
            HttpResult result = Http.Request("POST", MicrosoftDeviceCodeUrl,
                "application/x-www-form-urlencoded", body, null, 30000);
            if (!result.Ok)
                throw new Exception("无法启动微软设备代码登录: " + ErrorText(result));
            JMap json = JMap.Parse(result.Body);
            MicrosoftDeviceCode code = new MicrosoftDeviceCode();
            code.DeviceCode = json.Str("device_code", "");
            code.UserCode = json.Str("user_code", "");
            code.VerificationUri = json.Str("verification_uri", "https://www.microsoft.com/link");
            code.ExpiresIn = json.Int("expires_in", 900);
            code.Interval = Math.Max(3, json.Int("interval", 5));
            code.Message = json.Str("message", "");
            if (string.IsNullOrEmpty(code.DeviceCode) || string.IsNullOrEmpty(code.UserCode))
                throw new Exception("微软设备代码响应不完整，请重试。");
            return code;
        }

        public static Account WaitMicrosoftDeviceLogin(MicrosoftDeviceCode code, string clientId, ProgressSink progress, System.Threading.CancellationToken token)
        {
            if (code == null) throw new ArgumentNullException("code");
            if (string.IsNullOrEmpty(clientId)) clientId = DefaultMicrosoftClientId;
            int interval = Math.Max(3, code.Interval);
            DateTime deadline = DateTime.UtcNow.AddSeconds(Math.Max(60, code.ExpiresIn));
            while (DateTime.UtcNow < deadline)
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                for (int i = 0; i < interval; i++)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException(token);
                    System.Threading.Thread.Sleep(1000);
                }
                string body = "client_id=" + Uri.EscapeDataString(clientId) +
                              "&grant_type=urn:ietf:params:oauth:grant-type:device_code" +
                              "&device_code=" + Uri.EscapeDataString(code.DeviceCode);
                HttpResult result = Http.Request("POST", MicrosoftTokenUrl,
                    "application/x-www-form-urlencoded", body, null, 30000);
                if (result.Ok)
                {
                    JMap tokenJson = JMap.Parse(result.Body);
                    return LoginMicrosoftWithToken(tokenJson, progress);
                }

                JMap error = JMap.Parse(result.Body);
                string errorCode = error.Str("error", "");
                if (string.Equals(errorCode, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    progress.SetStage("等待浏览器中完成微软授权...");
                    continue;
                }
                if (string.Equals(errorCode, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    interval += 5;
                    continue;
                }
                if (string.Equals(errorCode, "authorization_declined", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("你取消了微软授权。");
                if (string.Equals(errorCode, "expired_token", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("设备代码已过期，请重新开始登录。");
                throw new Exception("微软登录失败: " + ErrorText(result));
            }
            throw new Exception("设备代码已过期，请重新开始登录。");
        }

        public static Account LoginMicrosoftWithToken(JMap token, ProgressSink progress)
        {
            string access = token.Str("access_token", "");
            string refresh = token.Str("refresh_token", "");
            long expires = token.Long("expires_in", 3600);
            if (string.IsNullOrEmpty(access)) throw new Exception("微软登录失败: 未获取到 access_token");
            return CompleteMicrosoftLogin(access, refresh, expires, progress);
        }

        public static Account LoginMicrosoft(string code, string clientId, ProgressSink progress)
        {
            progress.SetStage("正在验证微软账户");
            JMap token = ExchangeMicrosoftCode(code, clientId);
            return LoginMicrosoftWithToken(token, progress);
        }

        private static Account CompleteMicrosoftLogin(string access, string refresh, long expires, ProgressSink progress)
        {
            progress.SetStage("正在验证 Xbox Live");
            JMap xbl = XboxAuthenticate(access);
            string uhs = XuiUhs(xbl);
            string xblToken = xbl.Str("Token", "");
            if (string.IsNullOrEmpty(uhs) || string.IsNullOrEmpty(xblToken))
                throw new Exception("Xbox Live 验证失败: 响应中没有 userhash 或 Token（HTTP 401）。");

            progress.SetStage("正在验证 XSTS 权限");
            JMap xsts = XstsAuthorize(xblToken);
            string xstsToken = xsts.Str("Token", "");
            if (string.IsNullOrEmpty(xstsToken))
                throw new Exception("XSTS 验证失败: 响应不完整");

            progress.SetStage("正在登录 Minecraft 服务");
            JMap mc = MinecraftLogin(xstsToken, uhs);
            string mcAccess = mc.Str("access_token", "");
            if (string.IsNullOrEmpty(mcAccess))
                throw new Exception("Minecraft 登录失败: " + AuthError(mc));

            progress.SetStage("正在读取 Minecraft 档案");
            JMap profile = MinecraftProfile(mcAccess);
            string uuid = profile.Str("id", "");
            string name = profile.Str("name", "");
            if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(name))
                throw new Exception("该微软账户没有 Minecraft Java 版档案，可能尚未购买游戏。" + AuthError(profile));

            Account account = new Account();
            account.Id = Guid.NewGuid().ToString("N");
            account.Type = Account.Microsoft;
            account.Name = name;
            account.Uuid = uuid;
            account.AccessToken = mcAccess;
            account.RefreshToken = refresh;
            account.ClientToken = "";
            account.Xuid = XuiXid(xsts);
            account.ExpiresAt = Util.UnixNow + expires;
            return account;
        }

        public static Account RefreshMicrosoft(Account account, string clientId, ProgressSink progress)
        {
            if (string.IsNullOrEmpty(account.RefreshToken)) throw new Exception("微软账户缺少刷新令牌，请重新登录");
            progress.SetStage("正在刷新微软登录状态");
            JMap token = RefreshMicrosoftToken(account.RefreshToken, clientId);
            string access = token.Str("access_token", "");
            if (string.IsNullOrEmpty(access)) throw new Exception("微软登录已过期，请重新登录");
            long expires = token.Long("expires_in", 3600);

            JMap xbl = XboxAuthenticate(access);
            string uhs = XuiUhs(xbl);
            JMap xsts = XstsAuthorize(xbl.Str("Token", ""));
            JMap mc = MinecraftLogin(xsts.Str("Token", ""), uhs);
            string mcAccess = mc.Str("access_token", "");
            if (string.IsNullOrEmpty(mcAccess)) throw new Exception("刷新 Minecraft 登录失败: " + AuthError(mc));
            JMap profile = MinecraftProfile(mcAccess);

            account.AccessToken = mcAccess;
            account.RefreshToken = token.Str("refresh_token", account.RefreshToken);
            account.ExpiresAt = Util.UnixNow + expires;
            if (!string.IsNullOrEmpty(profile.Str("name", ""))) account.Name = profile.Str("name");
            if (!string.IsNullOrEmpty(profile.Str("id", ""))) account.Uuid = profile.Str("id");
            return account;
        }

        public static void EnsureReady(Account account, AppConfig config, ProgressSink progress)
        {
            if (account == null) throw new Exception("请先选择一个账户");
            if (account.IsOffline)
            {
                if (string.IsNullOrEmpty(account.Uuid)) account.Uuid = Util.OfflineUuid(account.Name);
                if (string.IsNullOrEmpty(account.AccessToken)) account.AccessToken = "0";
                return;
            }
            if (account.IsMicrosoft)
            {
                long now = Util.UnixNow;
                if (account.ExpiresAt > 0 && account.ExpiresAt - 180 < now)
                    RefreshMicrosoft(account, config.MicrosoftClientId, progress);
                return;
            }
            if (account.IsYggdrasil)
            {
                if (string.IsNullOrEmpty(account.AccessToken))
                {
                    if (!RefreshYggdrasil(account, progress)) throw new Exception("皮肤站登录已过期，请重新登录");
                }
                return;
            }
            throw new Exception("未知账户类型");
        }

        private static JMap ExchangeMicrosoftCode(string code, string clientId)
        {
            if (string.IsNullOrEmpty(code)) throw new Exception("没有获取到微软授权码");
            string body = "client_id=" + Uri.EscapeDataString(clientId) +
                          "&redirect_uri=" + Uri.EscapeDataString(MicrosoftRedirect) +
                          "&grant_type=authorization_code&code=" + Uri.EscapeDataString(code);
            HttpResult result = Http.Request("POST", "https://login.live.com/oauth20_token.srf",
                "application/x-www-form-urlencoded", body, null, 30000);
            if (!result.Ok) throw new Exception("微软授权码换取令牌失败: " + ErrorText(result));
            return JMap.Parse(result.Body);
        }

        private static JMap RefreshMicrosoftToken(string refreshToken, string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) clientId = DefaultMicrosoftClientId;
            string body;
            string url;
            if (clientId.IndexOf('-') >= 0)
            {
                body = "client_id=" + Uri.EscapeDataString(clientId) +
                       "&grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken) +
                       "&scope=" + Uri.EscapeDataString(MicrosoftScope);
                url = MicrosoftTokenUrl;
            }
            else
            {
                body = "client_id=" + Uri.EscapeDataString(clientId) +
                       "&redirect_uri=" + Uri.EscapeDataString(MicrosoftRedirect) +
                       "&grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken);
                url = "https://login.live.com/oauth20_token.srf";
            }
            HttpResult result = Http.Request("POST", url, "application/x-www-form-urlencoded", body, null, 30000);
            if (!result.Ok) throw new Exception("微软令牌刷新失败: " + ErrorText(result));
            return JMap.Parse(result.Body);
        }

        private static JMap XboxAuthenticate(string accessToken)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["AuthMethod"] = "RPS";
            properties["SiteName"] = "user.auth.xboxlive.com";
            properties["RpsTicket"] = "d=" + accessToken;
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["Properties"] = properties;
            body["RelyingParty"] = "http://auth.xboxlive.com";
            body["TokenType"] = "JWT";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Accept"] = "application/json";
            headers["x-xbl-contract-version"] = "1";
            HttpResult result = Http.Request("POST", "https://user.auth.xboxlive.com/user/authenticate",
                "application/json", Json.Stringify(body), headers, 30000);
            if (!result.Ok)
                throw new Exception("Xbox Live 验证失败 (HTTP " + result.Status + "): " + ErrorText(result) +
                    "\n请确认微软授权已完成，并尝试重新登录。");
            return JMap.Parse(result.Body);
        }

        private static JMap XstsAuthorize(string xblToken)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties["SandboxId"] = "RETAIL";
            properties["UserTokens"] = new string[] { xblToken };
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["Properties"] = properties;
            body["RelyingParty"] = "rp://api.minecraftservices.com/";
            body["TokenType"] = "JWT";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Accept"] = "application/json";
            headers["x-xbl-contract-version"] = "1";
            HttpResult result = Http.Request("POST", "https://xsts.auth.xboxlive.com/xsts/authorize",
                "application/json", Json.Stringify(body), headers, 30000);
            if (!result.Ok)
            {
                JMap error = JMap.Parse(result.Body);
                long xerr = error.Long("XErr", 0);
                if (xerr == 2148916233L) throw new Exception("该微软账户尚未创建 Xbox 档案，请先在 xbox.com 登录一次。");
                if (xerr == 2148916238L) throw new Exception("该账户是未成年人账户，需要由家庭组成人批准。");
                if (xerr == 2148916235L) throw new Exception("该账户所在地区不支持 Xbox Live。");
                throw new Exception("XSTS 授权失败 (HTTP " + result.Status + "): " + ErrorText(result));
            }
            return JMap.Parse(result.Body);
        }

        private static JMap MinecraftLogin(string xstsToken, string uhs)
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["identityToken"] = "XBL3.0 x=" + uhs + ";" + xstsToken;
            HttpResult result = Http.Request("POST", "https://api.minecraftservices.com/authentication/login_with_xbox",
                "application/json", Json.Stringify(body), null, 30000);
            if (!result.Ok) throw new Exception("Minecraft 服务登录失败: " + ErrorText(result));
            return JMap.Parse(result.Body);
        }

        private static JMap MinecraftProfile(string accessToken)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Authorization"] = "Bearer " + accessToken;
            HttpResult result = Http.Request("GET", "https://api.minecraftservices.com/minecraft/profile",
                null, null, headers, 30000);
            if (!result.Ok) throw new Exception("读取 Minecraft 档案失败: " + ErrorText(result));
            return JMap.Parse(result.Body);
        }

        public static JMap MinecraftProfileByToken(string accessToken)
        {
            return MinecraftProfile(accessToken);
        }

        private static string XuiUhs(JMap response)
        {
            JMap claims = response.Map("DisplayClaims");
            List<JMap> xui = claims.Maps("xui");
            if (xui.Count == 0) return "";
            return xui[0].Str("uhs", "");
        }

        private static string XuiXid(JMap response)
        {
            JMap claims = response.Map("DisplayClaims");
            List<JMap> xui = claims.Maps("xui");
            if (xui.Count == 0) return "";
            return xui[0].Str("xid", "");
        }

        public static string NormalizeYggdrasil(string server)
        {
            if (string.IsNullOrEmpty(server)) return "";
            string value = server.Trim();
            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                value = "https://" + value;
            while (value.EndsWith("/")) value = value.Substring(0, value.Length - 1);
            if (value.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - "/authserver".Length);
            if (value.EndsWith("/sessionserver", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - "/sessionserver".Length);
            Uri uri = new Uri(value);
            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                value += "/api/yggdrasil";
            return value;
        }

        public static Account LoginYggdrasil(string server, string username, string password, ProgressSink progress)
        {
            progress.SetStage("正在连接皮肤站认证服务器");
            string baseUrl = NormalizeYggdrasil(server);
            if (string.IsNullOrEmpty(baseUrl)) throw new Exception("请填写皮肤站认证服务器地址");
            GetYggdrasilMetadata(baseUrl); // validates connectivity

            string clientToken = Guid.NewGuid().ToString("N");
            Dictionary<string, object> agent = new Dictionary<string, object>();
            agent["name"] = "Minecraft";
            agent["version"] = 1;
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["agent"] = agent;
            body["username"] = username;
            body["password"] = password;
            body["clientToken"] = clientToken;
            body["requestUser"] = true;

            HttpResult result = Http.Request("POST", baseUrl + "/authserver/authenticate",
                "application/json", Json.Stringify(body), null, 30000);
            if (!result.Ok) throw new Exception("皮肤站登录失败: " + ErrorText(result));
            JMap response = JMap.Parse(result.Body);
            string access = response.Str("accessToken", "");
            if (string.IsNullOrEmpty(access)) throw new Exception("皮肤站登录失败: 未返回 accessToken");

            JMap profile = response.Map("selectedProfile");
            if (profile.Count == 0)
            {
                List<JMap> available = response.Maps("availableProfiles");
                if (available.Count > 0) profile = available[0];
            }
            if (profile.Count == 0) throw new Exception("该皮肤站账户还没有创建角色，请先在皮肤站创建一个角色。");

            Account account = new Account();
            account.Id = Guid.NewGuid().ToString("N");
            account.Type = Account.Yggdrasil;
            account.Name = profile.Str("name", username);
            account.Uuid = profile.Str("id", "");
            account.AuthServer = baseUrl;
            account.AccessToken = access;
            account.ClientToken = response.Str("clientToken", clientToken);
            return account;
        }

        public static bool RefreshYggdrasil(Account account, ProgressSink progress)
        {
            if (account == null || string.IsNullOrEmpty(account.AuthServer)) return false;
            if (string.IsNullOrEmpty(account.AccessToken) || string.IsNullOrEmpty(account.ClientToken)) return false;
            try
            {
                progress.SetStage("正在刷新皮肤站登录状态");
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["accessToken"] = account.AccessToken;
                body["clientToken"] = account.ClientToken;
                body["requestUser"] = true;
                HttpResult result = Http.Request("POST", account.AuthServer + "/authserver/refresh",
                    "application/json", Json.Stringify(body), null, 20000);
                if (!result.Ok) return false;
                JMap response = JMap.Parse(result.Body);
                string access = response.Str("accessToken", "");
                if (string.IsNullOrEmpty(access)) return false;
                account.AccessToken = access;
                JMap profile = response.Map("selectedProfile");
                if (profile.Count > 0)
                {
                    account.Name = profile.Str("name", account.Name);
                    account.Uuid = profile.Str("id", account.Uuid);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("皮肤站令牌刷新失败: " + ex.Message);
                return false;
            }
        }

        public static string GetYggdrasilMetadata(string server)
        {
            string baseUrl = NormalizeYggdrasil(server);
            string metadata = Http.GetText(new List<string>() { baseUrl }, 20000);
            JMap json = JMap.Parse(metadata);
            if (!json.Has("meta") || !json.Has("signaturePublickey"))
                throw new Exception("该地址不是有效的 authlib-injector 皮肤站 API");
            return metadata;
        }

        public static string EnsureAuthlibInjector(AppConfig config, ProgressSink progress)
        {
            string cache = Path.Combine(config.CacheDir, "authlib-injector");
            Util.EnsureDirectory(cache);
            string metaPath = Path.Combine(cache, "latest.json");
            string version = "";
            try
            {
                string meta = Http.GetText(Mirror.AuthlibInjectorMeta(config.MirrorFirst), 30000);
                JMap json = JMap.Parse(meta);
                version = json.Str("version", "");
                string expected = json.Str("download_url", "");
                string jar = Path.Combine(cache, "authlib-injector-" + version + ".jar");
                if (File.Exists(jar) && new FileInfo(jar).Length > 50000) return jar;
                if (string.IsNullOrEmpty(expected)) throw new Exception("未找到 authlib-injector 下载地址");
                DownloadItem item = new DownloadItem();
                item.Name = "authlib-injector " + version;
                item.Destination = jar;
                item.Urls = Mirror.Urls(expected, config.MirrorFirst);
                progress.SetStage("下载第三方皮肤站支持组件 authlib-injector");
                Http.DownloadOne(item, progress.Token, null);
                return jar;
            }
            catch (Exception ex)
            {
                Log.Warn("authlib-injector 下载失败: " + ex.Message);
                if (Directory.Exists(cache))
                {
                    string[] jars = Directory.GetFiles(cache, "authlib-injector-*.jar");
                    if (jars.Length > 0) return jars[0];
                }
                throw new Exception("无法下载 authlib-injector，第三方皮肤站账户无法启动: " + ex.Message);
            }
        }

        private static string AuthError(JMap json)
        {
            string message = json.Str("errorMessage", "");
            if (string.IsNullOrEmpty(message)) message = json.Str("error_description", "");
            if (string.IsNullOrEmpty(message)) message = json.Str("error", "");
            if (string.IsNullOrEmpty(message)) message = json.Str("message", "");
            return message;
        }

        private static string ErrorText(HttpResult result)
        {
            if (result == null) return "无响应";
            string text = result.Body;
            try
            {
                JMap json = JMap.Parse(text);
                string message = AuthError(json);
                if (!string.IsNullOrEmpty(message)) text = message;
                if (json.Has("XErr")) text += " (XErr " + json.Str("XErr", "") + ")";
            }
            catch { }
            if (string.IsNullOrEmpty(text)) text = result.Error;
            if (text != null && text.Length > 400) text = text.Substring(0, 400);
            return text;
        }
    }
}
