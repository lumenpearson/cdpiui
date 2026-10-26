<#
.SYNOPSIS
Writes CDPIUI.Shared/Secrets/Secret.cs for a release build from environment variables.

The stub of create_secret_stub.ps1 has an empty TrustedCertificates list, and a build with it
rejects every signed catalog, also its own patches. This script fills the list.

Environment variables (all optional):
  STORE_GITHUB_TOKEN, STORE_GITLAB_TOKEN   Store repository tokens. They are compiled into the
                                           application, so anyone can read them. The script refuses
                                           a token that can change anything (see Test-ReadOnlyToken).
  STORE_PROXY_URL, STORE_PROXY_KEY         Store reverse proxy. The application sends only the key to
                                           the proxy, never the Store tokens, so the proxy needs a
                                           GitHub token of its own.
  TRUSTED_CERTIFICATES                     SHA-1 thumbprints, separated by spaces, commas or semicolons
  CATALOG_CERTIFICATE                      Base64 PFX that signs the catalogs. If TRUSTED_CERTIFICATES
  CATALOG_CERTIFICATE_PASSWORD             is empty, the thumbprint of this certificate is used.
#>
param(
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$path = Join-Path $PSScriptRoot '../../CDPIUI.Shared/Secrets/Secret.cs'
if ((Test-Path -LiteralPath $path) -and -not $Force) {
    Write-Output "Secret.cs already exists; use -Force to replace it."
    return
}

function Get-Env([string] $name) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ($null -eq $value) { return "" }
    return $value
}

# A C# string literal: the values come from secrets and must not break the source.
function ConvertTo-CSharpString([string] $value) {
    return '"' + $value.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '') + '"'
}

# A token in the application is public. Only a token without write access is allowed:
# a classic GitHub token with no scopes, a fine-grained GitHub token (read-only permissions are
# checked by the person who creates it), or a GitLab token with read scopes only.
function Test-ReadOnlyToken([string] $kind, [string] $token) {
    if (-not $token) { return }
    try {
        if ($kind -eq 'GitHub') {
            $response = Invoke-WebRequest -Uri 'https://api.github.com/rate_limit' -UseBasicParsing `
                -Headers @{ Authorization = "Bearer $token"; 'User-Agent' = 'CDPIUI-release' }
            $scopes = @($response.Headers['X-OAuth-Scopes'] | ForEach-Object { $_ -split ',\s*' } | Where-Object { $_ })
            if ($scopes.Count -gt 0) {
                throw "STORE_GITHUB_TOKEN has the scopes '$($scopes -join ', ')'. Use a token without scopes, because the application makes it public."
            }
        }
        else {
            $response = Invoke-RestMethod -Uri 'https://gitlab.com/api/v4/personal_access_tokens/self' `
                -Headers @{ 'PRIVATE-TOKEN' = $token }
            $writable = @($response.scopes | Where-Object { $_ -notin @('read_api', 'read_repository', 'read_registry') })
            if ($writable.Count -gt 0) {
                throw "STORE_GITLAB_TOKEN has the scopes '$($writable -join ', ')'. Use read scopes only, because the application makes the token public."
            }
        }
    }
    catch [System.Net.WebException], [Microsoft.PowerShell.Commands.HttpResponseException] {
        throw "Cannot check STORE_$($kind.ToUpperInvariant())_TOKEN: $($_.Exception.Message)"
    }
}

Test-ReadOnlyToken 'GitHub' (Get-Env 'STORE_GITHUB_TOKEN')
Test-ReadOnlyToken 'GitLab' (Get-Env 'STORE_GITLAB_TOKEN')

$thumbprints = @((Get-Env 'TRUSTED_CERTIFICATES') -split '[\s,;]+' |
    ForEach-Object { ($_ -replace '[^0-9A-Fa-f]', '').ToUpperInvariant() } |
    Where-Object { $_.Length -eq 40 })

if ($thumbprints.Count -eq 0 -and (Get-Env 'CATALOG_CERTIFICATE')) {
    $bytes = [Convert]::FromBase64String((Get-Env 'CATALOG_CERTIFICATE'))
    $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $bytes, (Get-Env 'CATALOG_CERTIFICATE_PASSWORD'),
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    $thumbprints = @($cert.Thumbprint)
    $cert.Dispose()
}

if ($thumbprints.Count -eq 0) {
    Write-Warning "No trusted certificate: the build will reject signed packages and patches."
}

$list = ($thumbprints | ForEach-Object { ConvertTo-CSharpString $_ }) -join ', '

@"
using System.Collections.Generic;

namespace CDPIUI.Shared.Secrets
{
    public static class Secret
    {
        public static string GitHubToken = $(ConvertTo-CSharpString (Get-Env 'STORE_GITHUB_TOKEN'));
        public static string GitLabToken = $(ConvertTo-CSharpString (Get-Env 'STORE_GITLAB_TOKEN'));
        public const string ProxyURL = $(ConvertTo-CSharpString (Get-Env 'STORE_PROXY_URL'));
        public const string ProxyKey = $(ConvertTo-CSharpString (Get-Env 'STORE_PROXY_KEY'));
        public static readonly List<string> TrustedCertificates = [$list];
    }
}
"@ | Set-Content -LiteralPath $path -Encoding utf8

Write-Output ("Secret.cs written with {0} trusted certificate(s)." -f $thumbprints.Count)
