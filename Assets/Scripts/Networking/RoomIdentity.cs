using System;
using System.Security.Cryptography;
using System.Text;

namespace Mishi.Networking
{
    public static class RoomIdentity
    {
        // Project namespace, not a password. Never share this identifier with another game.
        public const string Game = "mishi.sim.9e0e8a31-7c62-42d5-a173-a6cb80791f24";
        public const int Protocol = 27;
        public const ushort SteamPort = 27341;
        public const uint AppId = 480;
        public const int ReconnectSeconds = 90;
        public static string RandomToken()
        {
            var bytes = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        public static string Password(string value)
        {
            value = (value ?? "").Normalize(NormalizationForm.FormC);
            if (value.Length > 64 || Array.Exists(value.ToCharArray(), char.IsControl)) throw new ArgumentException("房间密码最多 64 字，不能包含控制字符。");
            return value;
        }
        public static byte[] Key(string password, string salt)
        {
            using var derive = new Rfc2898DeriveBytes(Password(password), Convert.FromBase64String(salt), 100000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }
        public static string Proof(byte[] key, string room, string nonce, string version, string name, string resume)
        {
            using var mac = new HMACSHA256(key);
            // Length prefixes keep user-provided names/tokens unambiguous.
            var text = new StringBuilder();
            foreach (string field in new[] { Game, Protocol.ToString(), room, nonce, version, name, resume })
            { string value = field ?? ""; text.Append(value.Length).Append(':').Append(value); }
            return Convert.ToBase64String(mac.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        public static bool Equal(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length) return false;
            int diff = 0; for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }
        public static bool Compatible(string game, string version, string protocol, string localVersion) =>
            game == Game && version == localVersion && protocol == Protocol.ToString();
    }
}
