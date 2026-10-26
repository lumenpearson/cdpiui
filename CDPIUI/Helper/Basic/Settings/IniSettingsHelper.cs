using CDPIUI.Core.Basic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace CDPIUI.Helper.Basic.Settings
{
    public class IniSettingsHelper
    {
        private readonly string _filePath;
        private IniDocument _data;
        private readonly object _lock = new object();

        public Encoding FileEncoding { get; }

        public IniSettingsHelper(string filePath, Encoding encoding = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileEncoding = encoding ?? Encoding.UTF8;

            Reload();
        }
        public void SetValue<T>(string section, string key, T value)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_lock)
            {
                _data.SetValue(section, key, value?.ToString() ?? string.Empty);
            }
        }

        private bool TryGetString(string section, string key, out string value)
        {
            lock (_lock)
            {
                return _data.TryGetValue(section, key, out value);
            }
        }

        public bool TryGetValue<T>(string section, string key, out T value)
        {
            value = default;

            if (!TryGetString(section, key, out var raw)) return false;
            if (raw == null) return false;

            var targetType = typeof(T);
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                object converted;

                if (underlying.IsEnum)
                {
                    converted = Enum.Parse(underlying, raw, ignoreCase: true);
                }
                else if (underlying == typeof(bool))
                {
                    var s = raw.Trim();

                    converted = bool.Parse(s);
                }
                else if (underlying == typeof(Guid))
                {
                    converted = Guid.Parse(raw);
                }
                else if (underlying == typeof(TimeSpan))
                {
                    converted = TimeSpan.Parse(raw);
                }
                else
                {
                    try
                    {
                        converted = Convert.ChangeType(raw, underlying);
                    }
                    catch
                    {
                        var conv = TypeDescriptor.GetConverter(underlying);
                        if (conv != null && conv.CanConvertFrom(typeof(string)))
                        {
                            converted = conv.ConvertFromInvariantString(raw);
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                value = (T)converted!;
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        public T GetValue<T>(string section, string key, T defaultValue)
        {
            if (TryGetValue<T>(section, key, out var val)) return val!;
            return defaultValue;
        }

        /// <summary>
        /// The value, or the default of <typeparamref name="T"/> (an empty string for strings) when
        /// the key is missing: a config.ini of another GoodCheck version must not crash the page.
        /// </summary>
        public T GetValue<T>(string section, string key)
        {
            if (TryGetValue<T>(section, key, out var val)) return val!;
            return typeof(T) == typeof(string) ? (T)(object)string.Empty : default!;
        }

        public void Save()
        {
            lock (_lock)
            {
                // Written next to the file and moved over it, so an interrupted save cannot leave a
                // half-written config.ini. No BOM: GoodCheck does not expect one.
                Encoding encoding = FileEncoding is UTF8Encoding ? new UTF8Encoding(false) : FileEncoding;
                string tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    File.WriteAllText(tempPath, _data.ToString(), encoding);
                    File.Move(tempPath, _filePath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }

                Reload();
            }
        }

        public void Reload()
        {
            lock (_lock)
            {
                _data = IniDocument.Parse(File.Exists(_filePath) ? File.ReadAllText(_filePath, FileEncoding) : string.Empty);
            }
        }


        public void Dispose()
        {
            // pass
        }
    }
}
