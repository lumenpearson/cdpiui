using System;
using System.Diagnostics;
using System.Linq;

namespace CDPIUI.Shared
{
    public static class SharedUtils
    {
        public static string GenerateNewId()
        {
            return Guid.NewGuid().ToString().Replace("{", "").Replace("}", "");
        }

        public static bool IsOsSupportedNewGlyph()
        {
            Debug.WriteLine(Environment.OSVersion.ToString());
            var version1 = Environment.OSVersion.Version;
            string v2 = "10.0.22000.194";

            var version2 = new Version(v2);
            if (version1 >= version2) return true;
            return false;
        }

        // Used for secrets such as the TgWsProxy --secret: a shared System.Random is predictable
        // and not thread safe.
        public static string GetRandomHexNumber(int digits)
        {
            byte[] buffer = new byte[(digits + 1) / 2];
            using (var generator = System.Security.Cryptography.RandomNumberGenerator.Create())
                generator.GetBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            return result.Substring(0, digits);
        }
    }
}
