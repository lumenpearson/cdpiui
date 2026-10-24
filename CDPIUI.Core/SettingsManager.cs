using System.Xml;
using System.Xml.Linq;
using CDPIUI.Core.Data;
using CDPIUI.Shared.Basic;

namespace CDPIUI.Core;

public class SettingsManager : XMLSettingsService
{
    private static SettingsManager? _instance;
    private static readonly object _lock = new object();

    public static SettingsManager Instance
    {
        get
        {
            lock (_lock)
            {
                _instance ??= new SettingsManager();
                return _instance;
            }
        }
    }
    private SettingsManager() : base(Directories.SettingsFilePath) { }


    protected override T GetDefaultValueForKey<T>(string group, string key)
    {
        string templatePath = Directories.TemplateSettingsFilePath;

        if (!File.Exists(templatePath))
            return GetDefaultValue<T>()!;
        try
        {
            XDocument temp_xDocument = GetTemplate(templatePath);
            return GetValue<T>(group, key, temp_xDocument.Root, raiseExceptionIfNotExits: true)!;
        }
        catch
        {
            return GetDefaultValue<T>()!;
        }
    }

    protected override T GetDefaultValueForKey<T>(IEnumerable<string> groupPath, string key)
    {
        string templatePath = Directories.TemplateSettingsFilePath;

        if (!File.Exists(templatePath))
            return GetDefaultValue<T>()!;
        try
        {
            XDocument temp_xDocument = GetTemplate(templatePath);
            return GetValue<T>(groupPath, key, temp_xDocument.Root, raiseExceptionIfNotExits: true)!;
        }
        catch
        {
            return GetDefaultValue<T>()!;
        }
    }

    private readonly object _templateLock = new();
    private XDocument? _template;
    private string? _templatePath;
    private DateTime _templateWriteTimeUtc;

    // Every missing key reads the template: it is parsed once and read again only when the
    // path or the file changes. It is only read, never changed.
    private XDocument GetTemplate(string path)
    {
        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(path);
        lock (_templateLock)
        {
            if (_template == null ||
                !string.Equals(_templatePath, path, StringComparison.Ordinal) ||
                _templateWriteTimeUtc != writeTimeUtc)
            {
                _template = LoadTemplate(path);
                _templatePath = path;
                _templateWriteTimeUtc = writeTimeUtc;
            }
            return _template;
        }
    }

    // No DTD processing: internal entities cannot blow up the memory of the process.
    private static XDocument LoadTemplate(string path)
    {
        var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, readerSettings);
        return XDocument.Load(reader);
    }

}
