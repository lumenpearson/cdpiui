# Creates CDPIUI.Shared/Secrets/Secret.cs with empty values for a local or CI build.
# The pipe rejects an empty AuthGuid, so the stub gets a random one. An older stub with an
# empty AuthGuid is updated in place.
$path = Join-Path $PSScriptRoot '../../CDPIUI.Shared/Secrets/Secret.cs'
$guid = [Guid]::NewGuid().ToString()

if (Test-Path $path) {
    $text = Get-Content -Raw -Path $path
    if ($text -match 'AuthGuid\s*=\s*""') {
        ($text -replace 'AuthGuid\s*=\s*""', "AuthGuid = `"$guid`"") | Set-Content -Path $path -Encoding utf8 -NoNewline
    }
    return
}

@"
using System.Collections.Generic;

namespace CDPIUI.Shared.Secrets
{
    public static class Secret
    {
        public static string GitHubToken = "";
        public static string GitLabToken = "";
        public const string ProxyURL = "";
        public const string ProxyKey = "";
        public static string AuthGuid = "$guid";
        public static readonly List<string> TrustedCertificates = [];
    }
}
"@ | Set-Content -Path $path -Encoding utf8
