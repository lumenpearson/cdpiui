$path = Join-Path $PSScriptRoot '../../CDPIUI.Shared/Secrets/Secret.cs'
if (Test-Path $path) { return }

@'
using System.Collections.Generic;

namespace CDPIUI.Shared.Secrets
{
    public static class Secret
    {
        public static string GitHubToken = "";
        public static string GitLabToken = "";
        public const string ProxyURL = "";
        public const string ProxyKey = "";
        public static string AuthGuid = "";
        public static readonly List<string> TrustedCertificates = [];
    }
}
'@ | Set-Content -Path $path -Encoding utf8
