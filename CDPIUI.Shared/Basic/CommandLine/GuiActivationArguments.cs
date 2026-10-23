using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Basic.CommandLine
{
    /// <summary>
    /// Carries the command-line arguments of a GUI start through the tray. When the GUI finds no
    /// tray, it starts the tray and exits; the tray then starts the GUI again. Without these
    /// arguments the opened file, cdpiui:// link or --direct: command was lost.
    /// </summary>
    public static class GuiActivationArguments
    {
        public const string Prefix = "--gui-args:";

        // Arguments longer than this are dropped rather than passed half.
        private const int MaxEncodedLength = 16 * 1024;

        public static string? Encode(IEnumerable<string> arguments)
        {
            var list = arguments.Where(a => !string.IsNullOrEmpty(a)).ToList();
            if (list.Count == 0)
                return null;

            string encoded = Prefix + string.Join(".", list.Select(a => Convert.ToBase64String(Encoding.UTF8.GetBytes(a))));
            return encoded.Length <= MaxEncodedLength ? encoded : null;
        }

        public static bool TryDecode(IEnumerable<string> arguments, out string[] decoded)
        {
            decoded = Array.Empty<string>();
            string? value = arguments.FirstOrDefault(a => a.StartsWith(Prefix, StringComparison.Ordinal));
            if (value == null || value.Length > MaxEncodedLength)
                return false;

            try
            {
                decoded = value.Substring(Prefix.Length)
                    .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => Encoding.UTF8.GetString(Convert.FromBase64String(part)))
                    .ToArray();
                return decoded.Length > 0;
            }
            catch (FormatException)
            {
                decoded = Array.Empty<string>();
                return false;
            }
        }

        /// <summary>
        /// Builds a command line that CommandLineToArgvW and the .NET runtime split back into
        /// exactly <paramref name="arguments"/>.
        /// </summary>
        public static string JoinCommandLine(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(Quote));
        }

        private static string Quote(string argument)
        {
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return argument;

            var builder = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in argument)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    // Backslashes before a quote are escaped, and so is the quote.
                    builder.Append('\\', backslashes * 2 + 1);
                }
                else
                {
                    builder.Append('\\', backslashes);
                }
                backslashes = 0;
                builder.Append(c);
            }
            // Backslashes before the closing quote are escaped too.
            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }
}
