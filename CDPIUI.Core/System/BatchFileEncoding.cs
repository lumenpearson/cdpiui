using System.Text;

namespace CDPIUI.Core.System
{
    /// <summary>
    /// Reads batch files that can be saved as UTF-8, UTF-16 or in a Cyrillic code page
    /// </summary>
    public static class BatchFileEncoding
    {
        public static string ReadAllText(string path) => Decode(File.ReadAllBytes(path));

        public static string[] ReadAllLines(string path) =>
            ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        public static string Decode(byte[] bytes) => Detect(bytes).GetString(bytes).TrimStart('\uFEFF');

        public static Encoding Detect(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            try
            {
                _ = new UTF8Encoding(false, true).GetString(bytes);
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
            }

            // Without a BOM a Cyrillic file is either OEM 866 (what cmd uses) or ANSI 1251 (Notepad).
            // Lowercase letters fall into 0xA0-0xAF and 0xE0-0xEF in 866 and into 0xE0-0xFF in 1251,
            // and 0x80-0xBF holds the 866 letters that 1251 uses only for rare symbols.
            int oemScore = 0, ansiScore = 0;
            foreach (byte value in bytes)
            {
                if (value >= 0x80 && value <= 0xAF) oemScore++;
                else if (value >= 0xC0) ansiScore++;
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(oemScore > ansiScore ? 866 : 1251);
        }
    }
}
