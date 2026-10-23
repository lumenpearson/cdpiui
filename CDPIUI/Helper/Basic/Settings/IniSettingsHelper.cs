using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace CDPIUI.Helper.Basic.Settings
{
    // ChatGPT -> piece of shit. 

    public class IniSettingsHelper
    {
        private readonly string _filePath;
        private readonly FileIniDataParser _parser;
        private IniData _data;
        private readonly object _lock = new object();

        public Encoding FileEncoding { get; }

        public IniSettingsHelper(string filePath, Encoding encoding = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileEncoding = encoding ?? Encoding.UTF8;

            _parser = new FileIniDataParser();
            _parser.Parser.Configuration.KeyValueAssigmentChar = '=';
            _parser.Parser.Configuration.AssigmentSpacer = string.Empty;
            _parser.Parser.Configuration.CommentString = "//";
            Reload();
        }
        public void SetValue<T>(string section, string key, T value)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (key == null) throw new ArgumentNullException(nameof(key));

            lock (_lock)
            {
                if (!_data.Sections.ContainsSection(section))
                    _data.Sections.AddSection(section);

                string strValue = value.ToString();
                _data[section][key] = strValue ?? string.Empty;
            }
        }

        private bool TryGetString(string section, string key, out string value)
        {
            lock (_lock)
            {
                value = null;
                if (!_data.Sections.ContainsSection(section)) return false;
                var sectionData = _data[section];
                if (!sectionData.ContainsKey(key)) return false;
                value = sectionData[key];
                return true;
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
                    using (var sw = new StreamWriter(tempPath, false, encoding))
                    {
                        _parser.WriteData(sw, _data);
                    }
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
                // ReadFile without an encoding reads ASCII and breaks non-Latin values.
                _data = File.Exists(_filePath)
                    ? _parser.ReadFile(_filePath, FileEncoding)
                    : new IniData();
            }
        }


        public void Dispose()
        {
            // pass
        }
    }
}
