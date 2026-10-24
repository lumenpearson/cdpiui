using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CDPIUI.AddOns.BlockCheck2.Models;
using CDPIUI.AddOns.BlockCheck2.Presentation;

namespace CDPIUI.ViewModels;

public sealed partial class BlockCheck2ViewModel
{
    private string _targetInput = string.Empty;
    private BlockCheckRunPreset _selectedRunPreset = BlockCheckRunPreset.Balanced;
    private bool _includeHttp;
    private bool _includeTls12 = true;
    private bool _includeTls13;
    private bool _includeTlsAuto = true;
    private bool _includeQuic;
    private bool _includeIpv4 = true;
    private bool _includeIpv6;
    private double _connectTimeoutSeconds = BlockCheck2OptionLimits.DefaultConnectTimeoutSeconds;
    private double _requestTimeoutSeconds = BlockCheck2OptionLimits.DefaultRequestTimeoutSeconds;
    private double _startupGraceSeconds = BlockCheck2OptionLimits.DefaultStartupGraceSeconds;
    private bool _testAllStrategies;
    private double _requestsPerDomain = 3d;

    public IReadOnlyList<BlockCheckRunPreset> AvailableRunPresets { get; } =
        Enum.GetValues<BlockCheckRunPreset>();

    public string TargetInput
    {
        get => _targetInput;
        set
        {
            if (_targetInput == value)
            {
                return;
            }

            _targetInput = value ?? string.Empty;
            Notify();
            Notify(nameof(DirectTargetInputCount));
            Notify(nameof(HasTargetSources));
            Notify(nameof(CanStart));
        }
    }

    public BlockCheckRunPreset SelectedRunPreset
    {
        get => _selectedRunPreset;
        set
        {
            if (_selectedRunPreset == value)
            {
                return;
            }

            _selectedRunPreset = value;
            Notify();
            BlockCheck2SettingsStore.Save(nameof(SelectedRunPreset), (int)value);
            RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(value);
        }
    }

    public bool IncludeHttp
    {
        get => _includeHttp;
        set => SetProtocolSelection(ref _includeHttp, value);
    }

    public bool IncludeTls12
    {
        get => _includeTls12;
        set => SetProtocolSelection(ref _includeTls12, value);
    }

    public bool IncludeTls13
    {
        get => _includeTls13;
        set => SetProtocolSelection(ref _includeTls13, value);
    }

    public bool IncludeTlsAuto
    {
        get => _includeTlsAuto;
        set => SetProtocolSelection(ref _includeTlsAuto, value);
    }

    public bool IncludeQuic
    {
        get => _includeQuic;
        set => SetProtocolSelection(ref _includeQuic, value);
    }

    public bool IsHttpOnly => _includeHttp && !_includeTls12 && !_includeTls13 && !_includeTlsAuto && !_includeQuic;

    public bool IncludeIpv4
    {
        get => _includeIpv4;
        set => SetIpVersionSelection(ref _includeIpv4, value);
    }

    public bool IncludeIpv6
    {
        get => _includeIpv6;
        set => SetIpVersionSelection(ref _includeIpv6, value);
    }

    public int DirectTargetInputCount => _targetInput
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    public bool HasTargetSources => DirectTargetInputCount > 0 || SelectedSiteLists.Count > 0;
    public bool HasProtocolSelection =>
        _includeHttp || _includeTls12 || _includeTls13 || _includeTlsAuto || _includeQuic;
    public bool HasIpVersionSelection => _includeIpv4 || _includeIpv6;
    public bool CanContinueFromTargets => !IsRunning && HasProtocolSelection && HasIpVersionSelection;

    public double ConnectTimeoutSeconds
    {
        get => _connectTimeoutSeconds;
        set => SetAdvancedOption(ref _connectTimeoutSeconds, value);
    }

    public double RequestTimeoutSeconds
    {
        get => _requestTimeoutSeconds;
        set => SetAdvancedOption(ref _requestTimeoutSeconds, value);
    }

    public double StartupGraceSeconds
    {
        get => _startupGraceSeconds;
        set => SetAdvancedOption(ref _startupGraceSeconds, value);
    }

    public bool TestAllStrategies
    {
        get => _testAllStrategies;
        set
        {
            if (_testAllStrategies == value)
            {
                return;
            }

            _testAllStrategies = value;
            Notify();
            BlockCheck2SettingsStore.Save(nameof(TestAllStrategies), value);
        }
    }

    public double RequestsPerDomain
    {
        get => _requestsPerDomain;
        set
        {
            if (_requestsPerDomain.Equals(value))
            {
                return;
            }

            _requestsPerDomain = value;
            Notify();
            Notify(nameof(AdvancedOptionsValid));
            Notify(nameof(CanStart));
            if (BlockCheck2OptionLimits.IsValidRequestsPerDomain(value))
            {
                BlockCheck2SettingsStore.Save(nameof(RequestsPerDomain), (int)value);
            }
        }
    }

    public bool AdvancedOptionsValid =>
        BlockCheck2OptionLimits.IsValidConnectTimeout(_connectTimeoutSeconds) &&
        BlockCheck2OptionLimits.IsValidRequestTimeout(_requestTimeoutSeconds, _connectTimeoutSeconds) &&
        BlockCheck2OptionLimits.IsValidStartupGrace(_startupGraceSeconds) &&
        BlockCheck2OptionLimits.IsValidRequestsPerDomain(_requestsPerDomain);

    public void ResetAdvancedOptions()
    {
        ConnectTimeoutSeconds = BlockCheck2OptionLimits.DefaultConnectTimeoutSeconds;
        RequestTimeoutSeconds = BlockCheck2OptionLimits.DefaultRequestTimeoutSeconds;
        StartupGraceSeconds = BlockCheck2OptionLimits.DefaultStartupGraceSeconds;
        TestAllStrategies = false;
        RequestsPerDomain = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(_selectedRunPreset);
    }

    private void LoadSettings()
    {
        int presetValue = BlockCheck2SettingsStore.ReadInt(
            nameof(SelectedRunPreset),
            (int)BlockCheckRunPreset.Balanced);
        _selectedRunPreset = Enum.IsDefined(typeof(BlockCheckRunPreset), presetValue)
            ? (BlockCheckRunPreset)presetValue
            : BlockCheckRunPreset.Balanced;

        _includeHttp = BlockCheck2SettingsStore.ReadBool(nameof(IncludeHttp), defaultValue: false);
        _includeTls12 = BlockCheck2SettingsStore.ReadBool(nameof(IncludeTls12), defaultValue: true);
        _includeTls13 = BlockCheck2SettingsStore.ReadBool(nameof(IncludeTls13), defaultValue: false);
        _includeTlsAuto = BlockCheck2SettingsStore.ReadBool(nameof(IncludeTlsAuto), defaultValue: true);
        _includeQuic = BlockCheck2SettingsStore.ReadBool(nameof(IncludeQuic), defaultValue: false);
        _includeIpv4 = BlockCheck2SettingsStore.ReadBool(nameof(IncludeIpv4), defaultValue: true);
        _includeIpv6 = BlockCheck2SettingsStore.ReadBool(nameof(IncludeIpv6), defaultValue: false);

        double savedConnectTimeout = BlockCheck2SettingsStore.ReadDouble(
            nameof(ConnectTimeoutSeconds),
            BlockCheck2OptionLimits.DefaultConnectTimeoutSeconds);
        _connectTimeoutSeconds = BlockCheck2OptionLimits.IsValidConnectTimeout(savedConnectTimeout)
            ? savedConnectTimeout
            : BlockCheck2OptionLimits.DefaultConnectTimeoutSeconds;

        double savedRequestTimeout = BlockCheck2SettingsStore.ReadDouble(
            nameof(RequestTimeoutSeconds),
            BlockCheck2OptionLimits.DefaultRequestTimeoutSeconds);
        _requestTimeoutSeconds =
            BlockCheck2OptionLimits.IsValidRequestTimeout(savedRequestTimeout, _connectTimeoutSeconds)
                ? savedRequestTimeout
                : Math.Max(BlockCheck2OptionLimits.DefaultRequestTimeoutSeconds, _connectTimeoutSeconds);

        double savedStartupGrace = BlockCheck2SettingsStore.ReadDouble(
            nameof(StartupGraceSeconds),
            BlockCheck2OptionLimits.DefaultStartupGraceSeconds);
        _startupGraceSeconds = BlockCheck2OptionLimits.IsValidStartupGrace(savedStartupGrace)
            ? savedStartupGrace
            : BlockCheck2OptionLimits.DefaultStartupGraceSeconds;

        _testAllStrategies = BlockCheck2SettingsStore.ReadBool(nameof(TestAllStrategies), defaultValue: false);
        int recommendedAttempts = BlockCheckRunPresetFactory.GetRecommendedAttemptsPerTarget(_selectedRunPreset);
        int savedAttempts = BlockCheck2SettingsStore.ReadInt(nameof(RequestsPerDomain), recommendedAttempts);
        _requestsPerDomain = BlockCheck2OptionLimits.IsValidRequestsPerDomain(savedAttempts)
            ? savedAttempts
            : recommendedAttempts;
    }

    private BlockCheckSessionRequest CreateSessionRequest() => new()
    {
        TargetInput = _targetInput,
        SiteLists = SelectedSiteLists.Select(item => item.CreateInput(_generatedSiteListRoot)).ToArray(),
        RunPreset = _selectedRunPreset,
        TestAllStrategies = _testAllStrategies,
        AttemptsPerTarget = (int)_requestsPerDomain,
        Protocols = SelectedProtocols(),
        IpVersions = SelectedIpVersions(),
    };

    private IReadOnlySet<BlockCheckProtocol> SelectedProtocols()
    {
        HashSet<BlockCheckProtocol> selected = [];
        if (_includeHttp)
            selected.Add(BlockCheckProtocol.Http);
        if (_includeTls12)
            selected.Add(BlockCheckProtocol.Tls12);
        if (_includeTls13)
            selected.Add(BlockCheckProtocol.Tls13);
        if (_includeTlsAuto)
            selected.Add(BlockCheckProtocol.TlsAuto);
        if (_includeQuic)
            selected.Add(BlockCheckProtocol.Quic);
        return selected;
    }

    private IReadOnlySet<BlockCheckIpVersion> SelectedIpVersions()
    {
        HashSet<BlockCheckIpVersion> selected = [];
        if (_includeIpv4)
            selected.Add(BlockCheckIpVersion.IPv4);
        if (_includeIpv6)
            selected.Add(BlockCheckIpVersion.IPv6);
        return selected;
    }

    private void SetProtocolSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        BlockCheck2SettingsStore.Save(propertyName, value);
        Notify(nameof(IsHttpOnly));
        Notify(nameof(HasProtocolSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetIpVersionSelection(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Notify(propertyName);
        BlockCheck2SettingsStore.Save(propertyName, value);
        Notify(nameof(HasIpVersionSelection));
        Notify(nameof(CanContinueFromTargets));
        Notify(nameof(CanStart));
    }

    private void SetAdvancedOption(
        ref double field,
        double value,
        [CallerMemberName] string? propertyName = null)
    {
        if (field.Equals(value))
        {
            return;
        }

        field = value;
        Notify(propertyName);
        if (double.IsFinite(value))
        {
            BlockCheck2SettingsStore.Save(propertyName, value);
        }
        Notify(nameof(AdvancedOptionsValid));
        Notify(nameof(CanStart));
    }
}
