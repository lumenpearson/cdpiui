using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using CDPIUI.Shared.Exceptions.Settings;

namespace CDPIUI.Shared.Basic;

/// <summary>
/// XML Settings service
/// </summary>
public class XMLSettingsService
{
    /// <summary>
    /// Path to settings file
    /// </summary>
    public string SettingsFilePath { get; init; }

    private XDocument? _xDocument;
    private bool _persistenceUnavailable;
    public bool HasUnrecoverableLoadError { get; private set; }
    public bool WasRecoveredFromBackup { get; private set; }
    public string? LoadError { get; private set; }
    public string RecoveryNoticePath => SettingsFilePath + ".recovery-notice";

    public Action<string>? PropertyChanged;
    public Action<IEnumerable<string>>? EnumPropertyChanged;

    public XMLSettingsService(string filepath)
    {
        SettingsFilePath = filepath;
        Reload();
    }

    private readonly object _reloadLock = new();
    /// <summary>
    /// Reload setting file. Any <see cref="Action"/> won't be called any way
    /// </summary>
    public void Reload()
    {
        lock (_reloadLock)
        {
            _persistenceUnavailable = false;
            try
            {
                _xDocument = SettingsFileStore.Load(SettingsFilePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _xDocument = new XDocument(new XElement("Settings"));
                SaveSettings();
            }
            catch (Exception ex) when (SettingsFileStore.IsReadFailure(ex))
            {
                RecoverFromDamagedFile(ex);
            }
        }
    }

    // The caller holds _reloadLock.
    private void RecoverFromDamagedFile(Exception loadError)
    {
        LoadError = loadError.Message;
        try
        {
            if (_xDocument == null)
            {
                _xDocument = SettingsFileStore.Load(SettingsFilePath + ".bak");
                WasRecoveredFromBackup = true;
            }
        }
        catch (Exception backupError) when (SettingsFileStore.IsReadFailure(backupError))
        {
            HasUnrecoverableLoadError = _xDocument == null;
            _xDocument ??= new XDocument(new XElement("Settings"));
        }

        SettingsFileStore.KeepDamagedCopy(SettingsFilePath);

        if (HasUnrecoverableLoadError)
            SettingsFileStore.WriteRecoveryNotice(RecoveryNoticePath, LoadError);

        SaveSettings(keepBackup: false);
    }

    /// <summary>
    /// The UI and the tray share the settings file. Each change is applied to the file as it is on disk
    /// now, so a process with an older copy in memory does not overwrite what the other one saved.
    /// </summary>
    private void SaveChange(IEnumerable<string> groupPath, string key, string type, string? valueString)
    {
        lock (_reloadLock)
        {
            using (SettingsFileStore.AcquireLock(SettingsFilePath, out bool lockTimedOut))
            {
                if (lockTimedOut)
                {
                    // Writing without the lock could overwrite what the other process saves at
                    // this moment. The change stays in memory only, as after a failed write.
                    SettingsXmlTree.ApplySetting(_xDocument!.Root!, groupPath, key, type, valueString);
                    Trace.WriteLine($"[XMLSettingsService] The settings file stayed locked; {key} was not saved.");
                    return;
                }

                if (!_persistenceUnavailable && File.Exists(SettingsFilePath))
                {
                    try
                    {
                        _xDocument = SettingsFileStore.Load(SettingsFilePath);
                    }
                    catch (Exception ex) when (SettingsFileStore.IsReadFailure(ex))
                    {
                        // Keep the copy in memory; saving it replaces the unreadable file.
                    }
                }

                SettingsXmlTree.ApplySetting(_xDocument!.Root!, groupPath, key, type, valueString);
                SaveSettings();
            }
        }
    }

    // The caller holds _reloadLock.
    private void SaveSettings(bool keepBackup = true)
    {
        if (_persistenceUnavailable)
            return;
        if (!SettingsFileStore.Save(_xDocument!, SettingsFilePath, keepBackup))
            _persistenceUnavailable = true;
    }

    /// <summary>
    /// Get value or default. Sets default value to settings file. Throwing no exception
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="group">Group. "SYSTEM", for example</param>
    /// <param name="key">Key. "sampleKey", for example</param>
    /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
    /// <param name="defaultValue">Default value</param>
    /// <returns>Value if key exsits, otherwise default</returns>
    public T? GetValueOrDefault<T>(string group, string key, XElement? xElement = null, T? defaultValue = default)
    {
        lock (_reloadLock)
        {
            if (xElement == null)
                xElement = _xDocument.Root;
            var settingElement = SettingsXmlTree.FindSetting(SettingsXmlTree.FindGroup(xElement, group), key);

            if (settingElement == null)
            {
                SetValue(group, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group '{group}' not found.");
                return defaultValue;
            }
            else
                return GetValue<T>(group, key, xElement);
        }
    }
    /// <summary>
    /// Get value or hardcoded defailt. Sets default value to settings file.
    /// Throwing <see cref="SettingNotFoundException"/> if <paramref name="raiseExceptionIfNotExits"/> setted as true.
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="group">Group. "SYSTEM", for example</param>
    /// <param name="key">Key. "sampleKey", for example</param>
    /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
    /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="key"/> not found in file</param>
    /// <returns>Value if key exsits, otherwise default</returns>
    /// <exception cref="SettingNotFoundException"></exception>
    public T? GetValue<T>(string group, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
    {
        lock (_reloadLock)
        {
            xElement ??= _xDocument.Root;
            var settingElement = SettingsXmlTree.FindSetting(SettingsXmlTree.FindGroup(xElement, group), key);

            if (settingElement == null)
            {
                if (raiseExceptionIfNotExits)
                    throw new SettingNotFoundException("Value not exist");

                var defaultValue = GetDefaultValueForKey<T>(group, key);

                SetValue(group, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group '{group}' not found.");
                return defaultValue;
            }
            string value = settingElement.Attribute("Value")?.Value;
            string type = settingElement.Attribute("Type")?.Value;

            try
            {
                return SettingsValueCodec.Decode<T>(value, type);
            }
            catch
            {
                // A value of another type, for example after a manual edit, must not crash
                // the caller: hardly any of them expects an exception here (issue #61).
                Debug.WriteLine($"Type mismatch or unsupported type for setting '{key}' in group '{group}'.");
                return GetDefaultValueForKey<T>(group, key);
            }
        }
    }
    /// <summary>
    /// Get value or hardcoded defailt. Sets default value to settings file.
    /// Throwing <see cref="SettingNotFoundException"/> if <paramref name="raiseExceptionIfNotExits"/> setted as true.
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="groupPath">Group path. "SYSTEM"/"DATA", for example</param>
    /// <param name="key">Key. "sampleKey", for example</param>
    /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
    /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="key"/> not found in file</param>
    /// <returns>Value if key exsits, otherwise default</returns>
    /// <exception cref="SettingNotFoundException"></exception>
    public T? GetValue<T>(IEnumerable<string> groupPath, string key, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
    {
        lock (_reloadLock)
        {
            if (xElement == null)
                xElement = _xDocument.Root;
            XElement? current = SettingsXmlTree.FindGroup(xElement, groupPath);
            if (current == null)
            {
                if (raiseExceptionIfNotExits)
                    throw new SettingNotFoundException("Value not exist");
                var defaultValue = GetDefaultValueForKey<T>(groupPath, key);
                SetValue(groupPath, key, defaultValue);
                Debug.WriteLine($"Group path '{string.Join("/", groupPath)}' not found.");
                return defaultValue;
            }

            var settingElement = SettingsXmlTree.FindSetting(current, key);

            if (settingElement == null)
            {
                if (raiseExceptionIfNotExits)
                    throw new SettingNotFoundException("Value not exist");
                var defaultValue = GetDefaultValueForKey<T>(groupPath, key);
                SetValue(groupPath, key, defaultValue);
                Debug.WriteLine($"Setting '{key}' in group path '{string.Join("/", groupPath)}' not found.");
                return defaultValue;
            }

            string value = (string)settingElement.Attribute("Value");
            string type = (string)settingElement.Attribute("Type");

            try
            {
                return SettingsValueCodec.Decode<T>(value, type);
            }
            catch
            {
                Debug.WriteLine($"Type mismatch or unsupported type for setting '{key}' in group path '{string.Join("/", groupPath)}'.");
                return GetDefaultValueForKey<T>(groupPath, key);
            }
        }
    }

    /// <summary>
    /// Get key value pair.
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="groupPath"></param>
    /// <param name="xElement">Custom <see cref="XElement"/> if settings must be loaded from another file</param>
    /// <param name="raiseExceptionIfNotExits">Throw <see cref="SettingNotFoundException"/> if <paramref name="groupPath"/> not found in file</param>
    /// <returns>Dictionary if <paramref name="groupPath"/> exsits, otherwise empty dict</returns>
    /// <exception cref="SettingNotFoundException"></exception>
    public Dictionary<string, T?> GetKeyPair<T>(IEnumerable<string> groupPath, XElement? xElement = null, bool raiseExceptionIfNotExits = false)
    {
        lock (_reloadLock)
        {
            if (xElement == null)
                xElement = _xDocument.Root;

            Dictionary<string, T?> dict = [];

            XElement? current = SettingsXmlTree.FindGroup(xElement, groupPath);
            if (current == null)
            {
                if (raiseExceptionIfNotExits)
                    throw new SettingNotFoundException("Value not exist");
                var defaultValue = GetDefaultValueForKey<T>(groupPath, "$$~");
                SetValue(groupPath, "$$~", defaultValue);
                Debug.WriteLine($"Group path '{string.Join("/", groupPath)}' not found.");
                return dict;
            }

            foreach (var setting in current.Elements("Setting"))
            {
                string key = (string)setting.Attribute("Key");
                // "$$~" only marks a group created empty; it is no setting.
                if (key == null || key == "$$~")
                    continue;
                try
                {
                    dict[key] = SettingsValueCodec.Decode<T>((string)setting.Attribute("Value"), (string)setting.Attribute("Type"));
                }
                catch
                {
                    Debug.WriteLine($"Skipping setting '{key}' of another type in group path '{string.Join("/", groupPath)}'.");
                }
            }
            return dict;
        }
    }


    /// <summary>
    /// Set value to settings file
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="group">Group. "SYSTEM", for example</param>
    /// <param name="key">Key. "sampleKey", for example</param>
    /// <param name="value">Value to set</param>
    public void SetValue<T>(string group, string key, T value)
    {
        var (type, valueString) = SettingsValueCodec.EncodeForGroup(value);
        SaveChange([group], key, type, valueString);
        PropertyChanged?.Invoke(group);
    }

    /// <summary>
    /// Set value to settings file
    /// </summary>
    /// <typeparam name="T">Target value type</typeparam>
    /// <param name="groupPath">Group path. "SYSTEM"/"DATA", for example</param>
    /// <param name="key">Key. "sampleKey", for example</param>
    /// <param name="value">Value to set</param>
    public void SetValue<T>(IEnumerable<string> groupPath, string key, T value)
    {
        var (type, valueString) = SettingsValueCodec.EncodeForGroupPath(value);
        SaveChange(groupPath, key, type, valueString);
        EnumPropertyChanged?.Invoke(groupPath);
    }

    public static string FormatDouble(double value) => SettingsValueCodec.FormatDouble(value);

    public static string FormatDateTime(DateTime value) => SettingsValueCodec.FormatDateTime(value);

    public static bool TryParseDouble(string? value, out double result) =>
        SettingsValueCodec.TryParseDouble(value, out result);

    public static bool TryParseDateTime(string? value, out DateTime result) =>
        SettingsValueCodec.TryParseDateTime(value, out result);

    protected virtual T? GetDefaultValueForKey<T>(string group, string key)
    {
        return GetDefaultValue<T>();
    }
    protected virtual T? GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
    {
        return GetDefaultValue<T>();
    }

    protected T? GetDefaultValue<T>()
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)false;
        }
        else if (typeof(T) == typeof(string))
        {
            return (T)(object)"NaN";
        }
        else if (typeof(T) == typeof(int))
        {
            return (T)(object)0;
        }
        else if (typeof(T) == typeof(double))
        {
            return (T)(object)10.0;
        }
        else if (typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string>();
        }
        else
        {
            return (T)(object)default(T);
        }
    }
}
