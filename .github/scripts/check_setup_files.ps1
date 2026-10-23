<#
.SYNOPSIS
Compares the publish folder with the file list of NewSetup/NewSetupFiles.wxs. WiX fails on
listed files that are missing, but a published file that is not listed would silently be left
out of the MSI.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$wxs = Join-Path $root "NewSetup/NewSetupFiles.wxs"
$publish = Join-Path $root "CDPIUI/bin/x64/Release/net8.0-windows10.0.22621.0/publish"

$listed = [regex]::Matches((Get-Content -Raw $wxs), 'Source="\$\(var\.SourceDir\)\\([^"]+)"') |
    ForEach-Object { $_.Groups[1].Value.ToLowerInvariant() }
$published = Get-ChildItem -LiteralPath $publish -Recurse -File |
    Where-Object { $_.Extension -ne ".pdb" } |
    ForEach-Object { $_.FullName.Substring($publish.Length).TrimStart('\', '/').Replace('/', '\').ToLowerInvariant() }

$notListed = @($published | Where-Object { $listed -notcontains $_ })
$notPublished = @($listed | Where-Object { $published -notcontains $_ })

$notListed | ForEach-Object { Write-Output "Published but not in NewSetupFiles.wxs: $_" }
$notPublished | ForEach-Object { Write-Output "In NewSetupFiles.wxs but not published: $_" }
if ($notListed.Count + $notPublished.Count -gt 0) { throw "NewSetupFiles.wxs does not match the publish folder." }
Write-Output "NewSetupFiles.wxs matches the publish folder ($($listed.Count) files)."
