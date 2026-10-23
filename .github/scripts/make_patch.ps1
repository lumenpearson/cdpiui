# On Windows PowerShell 5.1 (.NET Framework 4.x) ZipFile may store "\" in entry names,
# which the updater does not read as folders; PowerShell 7 always writes "/".
#Requires -Version 7.0
<#
.SYNOPSIS
Creates a .cdpipatch from a portable build, in the format ApplicationUpdateService expects:

    requirements.json        {"patch_urls": [...], "version": "x.y.z.w"}
    CDPIUI/catalog.cat       catalog of every other file in CDPIUI/
    CDPIUI/...               files that differ from the previous portable build

Update.exe cannot replace itself, so a new updater is shipped as _Update.exe and the tray
renames it on its next start.

The application accepts only catalogs signed by a pinned certificate. Pass that certificate
with -CertificatePath, otherwise the patch is built with an unsigned catalog and is rejected.
#>
param(
    [Parameter(Mandatory = $true)] [string] $CurrentDir,
    [string] $PreviousZip = "",
    [Parameter(Mandatory = $true)] [string] $Version,
    [string[]] $PatchUrls = @(),
    [string] $OutPatch = "patch.cdpipatch",
    [string] $CertificatePath = "",
    [string] $CertificatePassword = "",
    [string] $TimestampServer = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CurrentDir -PathType Container)) { throw "CurrentDir not found: $CurrentDir" }
if ($Version -notmatch '^\d+(\.\d+){1,3}$') { throw "Version must look like 1.2.3.4: $Version" }

$CurrentDir = (Resolve-Path -LiteralPath $CurrentDir).Path
$work = Join-Path ([IO.Path]::GetTempPath()) ("cdpipatch_" + [Guid]::NewGuid().ToString("N"))
$prevDir = Join-Path $work "prev"
$payload = Join-Path $work "payload"
$layer = Join-Path $payload "CDPIUI"
New-Item -ItemType Directory -Path $prevDir, $layer -Force | Out-Null

try {
    if (-not [string]::IsNullOrWhiteSpace($PreviousZip)) {
        if (-not (Test-Path -LiteralPath $PreviousZip)) { throw "PreviousZip not found: $PreviousZip" }
        Write-Output "Comparing with $PreviousZip"
        Expand-Archive -LiteralPath $PreviousZip -DestinationPath $prevDir -Force
    } else {
        Write-Output "No previous build: the patch contains every file."
    }

    $changed = New-Object System.Collections.Generic.List[string]
    foreach ($file in Get-ChildItem -LiteralPath $CurrentDir -Recurse -File) {
        $rel = $file.FullName.Substring($CurrentDir.Length).TrimStart('\', '/')
        if ($rel -ieq "catalog.cat") { continue }
        $target = if ($rel -ieq "Update.exe") { "_Update.exe" } else { $rel }

        $prevPath = Join-Path $prevDir $target
        if ((Test-Path -LiteralPath $prevPath) -and
            (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $prevPath -Algorithm SHA256).Hash) {
            continue
        }

        $dest = Join-Path $layer $target
        New-Item -ItemType Directory -Path (Split-Path $dest) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $dest
        $changed.Add($target)
    }

    if ($changed.Count -eq 0) { throw "Nothing changed since the previous build." }
    Write-Output ("Changed files: {0}" -f $changed.Count)
    $changed | ForEach-Object { Write-Output " - $_" }

    # The catalog is created outside the layer so that it does not list itself.
    $catalog = Join-Path $work "catalog.cat"
    New-FileCatalog -Path $layer -CatalogFilePath $catalog -CatalogVersion 2.0 | Out-Null

    if (-not [string]::IsNullOrWhiteSpace($CertificatePath)) {
        $password = ConvertTo-SecureString $CertificatePassword -AsPlainText -Force
        $cert = Get-PfxCertificate -FilePath $CertificatePath -Password $password
        $signArgs = @{ FilePath = $catalog; Certificate = $cert; HashAlgorithm = "SHA256" }
        if (-not [string]::IsNullOrWhiteSpace($TimestampServer)) { $signArgs.TimestampServer = $TimestampServer }
        $signature = Set-AuthenticodeSignature @signArgs
        if ($null -eq $signature.SignerCertificate) { throw "Cannot sign the catalog: $($signature.StatusMessage)" }
        Write-Output "Catalog signed by $($signature.SignerCertificate.Subject) ($($signature.SignerCertificate.Thumbprint))"
    } else {
        Write-Warning "No certificate given: the catalog is unsigned and the application will refuse this patch."
    }
    Move-Item -LiteralPath $catalog -Destination (Join-Path $layer "catalog.cat")

    $check = Test-FileCatalog -CatalogFilePath (Join-Path $layer "catalog.cat") -Path $layer -FilesToSkip catalog.cat
    if ($check -ne "Valid") { throw "The new catalog does not match the patch files: $check" }

    $requirements = [ordered]@{
        patch_urls = @($PatchUrls | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        version    = $Version
    }
    $json = ConvertTo-Json -InputObject $requirements -Depth 3
    [IO.File]::WriteAllText((Join-Path $payload "requirements.json"), $json, (New-Object System.Text.UTF8Encoding($false)))

    $outPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutPatch)
    if (Test-Path -LiteralPath $outPath) { Remove-Item -LiteralPath $outPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($payload, $outPath)
    Write-Output "Patch created: $OutPatch"
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
