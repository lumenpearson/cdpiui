using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

public sealed class BlockCheck2SiteListItem : INotifyPropertyChanged
{
    private int _processingModeIndex;
    private string _editableContent;
    private int _domainCount;

    public BlockCheck2SiteListItem(
        string displayName,
        string sourceName,
        string filePath,
        bool isCustom = false,
        string? editableContent = null,
        string? individualSiteListRoot = null)
    {
        DisplayName = displayName;
        SourceName = sourceName;
        FilePath = Path.GetFullPath(filePath);
        IsCustom = isCustom;
        _editableContent = editableContent ?? string.Empty;
        _domainCount = CountDomains(_editableContent, FilePath);
        IndividualSiteListRoot = individualSiteListRoot;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }
    public string SourceName { get; }
    public string FilePath { get; }
    public bool IsCustom { get; }
    public string EditableContent => _editableContent;
    public int DomainCount => _domainCount;
    private string? IndividualSiteListRoot { get; }

    public int ProcessingModeIndex
    {
        get => _processingModeIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (_processingModeIndex == normalized)
            {
                return;
            }

            _processingModeIndex = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingModeIndex)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessingMode)));
        }
    }

    public BlockCheckSiteListProcessingMode ProcessingMode => _processingModeIndex == 1
        ? BlockCheckSiteListProcessingMode.EachSite
        : BlockCheckSiteListProcessingMode.WholeList;

    public BlockCheckSiteListInput CreateInput(string? individualSiteListRoot = null) => new(
        DisplayName,
        FilePath,
        ProcessingMode,
        ProcessingMode == BlockCheckSiteListProcessingMode.EachSite
            ? CreateIndividualSiteListDirectory(
                FilePath,
                individualSiteListRoot ??
                IndividualSiteListRoot ??
                BlockCheck2SiteListStorage.CreateFallbackIndividualSiteListRoot())
            : null);

    public void UpdateCustomContent(string content)
    {
        _editableContent = content ?? string.Empty;
        _domainCount = CountDomains(_editableContent, FilePath);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EditableContent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DomainCount)));
    }

    private static int CountDomains(string content, string filePath)
    {
        try
        {
            IEnumerable<string> lines = string.IsNullOrWhiteSpace(content)
                ? File.ReadLines(filePath)
                : content.Split(['\r', '\n']);
            return lines
                .Select(line => (line ?? string.Empty).Trim().TrimStart('﻿'))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string CreateIndividualSiteListDirectory(string filePath, string rootDirectory)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            Path.GetFullPath(filePath).ToUpperInvariant()));
        string id = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
        return Path.Combine(Path.GetFullPath(rootDirectory), id);
    }
}
