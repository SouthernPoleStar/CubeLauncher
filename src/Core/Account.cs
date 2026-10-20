using System;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace CubeLauncher.Core
{
    public sealed class Account
    {
        public const string Microsoft = "microsoft";
        public const string Yggdrasil = "yggdrasil";
        public const string Offline = "offline";

        public string Id = Guid.NewGuid().ToString("N");
        public string Type = Offline;
        public string Name = "Steve";
        public string Uuid = "";
        public string AuthServer = "";
        public string SkinModel = "classic";
        public long ExpiresAt;
        public string AccessTokenProtected = "";
        public string RefreshTokenProtected = "";
        public string ClientTokenProtected = "";
        public string Xuid = "";
        public bool Selected;

        [ScriptIgnore]
        public string AccessToken
        {
            get { return SecretStore.Unprotect(AccessTokenProtected); }
            set { AccessTokenProtected = SecretStore.Protect(value); }
        }

        [ScriptIgnore]
        public string RefreshToken
        {
            get { return SecretStore.Unprotect(RefreshTokenProtected); }
            set { RefreshTokenProtected = SecretStore.Protect(value); }
        }

        [ScriptIgnore]
        public string ClientToken
        {
            get { return SecretStore.Unprotect(ClientTokenProtected); }
            set { ClientTokenProtected = SecretStore.Protect(value); }
        }

        [ScriptIgnore]
        public bool IsMicrosoft
        {
            get { return string.Equals(Type, Microsoft, StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public bool IsYggdrasil
        {
            get { return string.Equals(Type, Yggdrasil, StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public bool IsOffline
        {
            get { return string.Equals(Type, Offline, StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public string TypeLabel
        {
            get
            {
                if (IsMicrosoft) return "微软正版";
                if (IsYggdrasil) return "皮肤站";
                return "离线账户";
            }
        }

        [ScriptIgnore]
        public string Initial
        {
            get
            {
                if (string.IsNullOrEmpty(Name)) return "?";
                return Name.Substring(0, 1).ToUpperInvariant();
            }
        }

        public string CleanUuid
        {
            get { return (Uuid ?? "").Replace("-", ""); }
        }

        public static Account CreateOffline(string name)
        {
            string clean = name ?? "Steve";
            return new Account
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = Offline,
                Name = clean,
                Uuid = Util.OfflineUuid(clean),
                AccessToken = "0",
                ExpiresAt = 0
            };
        }

        public Account Copy()
        {
            Account account = new Account();
            account.Id = Id;
            account.Type = Type;
            account.Name = Name;
            account.Uuid = Uuid;
            account.AuthServer = AuthServer;
            account.SkinModel = SkinModel;
            account.ExpiresAt = ExpiresAt;
            account.AccessTokenProtected = AccessTokenProtected;
            account.RefreshTokenProtected = RefreshTokenProtected;
            account.ClientTokenProtected = ClientTokenProtected;
            account.Xuid = Xuid;
            account.Selected = Selected;
            return account;
        }
    }

    public static class SecretStore
    {
        public static string Protect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                return "dpapi:" + Convert.ToBase64String(protectedBytes);
            }
            catch
            {
                return "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            }
        }

        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            try
            {
                if (value.StartsWith("dpapi:", StringComparison.Ordinal))
                {
                    byte[] bytes = Convert.FromBase64String(value.Substring(6));
                    byte[] clear = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(clear);
                }
                if (value.StartsWith("b64:", StringComparison.Ordinal))
                    return Encoding.UTF8.GetString(Convert.FromBase64String(value.Substring(4)));
                return value;
            }
            catch
            {
                return "";
            }
        }
    }
}
