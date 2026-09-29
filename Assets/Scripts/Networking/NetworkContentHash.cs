using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Mishi.Networking
{
    public static class NetworkContentHash
    {
        // Lua/rule changes reject the connection rather than silently changing gameplay.
        // Art does not affect rules and is never transferred over the network.
        public static string Compute(string root, string map)
        {
            var text = new StringBuilder(map);
            using (var hash = SHA256.Create())
            {
                foreach (string directory in new[] { "Cards", "Rules" })
                {
                    string folder = Path.Combine(root, directory);
                    if (!Directory.Exists(folder)) continue;
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                        .Where(p => p.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p, StringComparer.Ordinal))
                    {
                        text.Append('\n').Append(path.Substring(root.Length).Replace('\\', '/'))
                            .Append(':').Append(Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
                    }
                }
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
            }
        }
    }
}
