<#
.SYNOPSIS
Builds the x64 release layout of CDPI UI: the GUI publish folder with the tray, Update.exe,
EditHostFile.exe and CDPIUI_ElevatedLauncher.exe next to it. NewSetup/NewSetupFiles.wxs takes its
files from the same folder.
Needs msbuild (Visual Studio or microsoft/setup-msbuild) for the GUI, the .NET 8 SDK, and the C++
build tools of Visual Studio for the Native AOT launcher.
The tray, Update.exe and EditHostFile.exe are framework-dependent single-file executables.
CDPIUI_ElevatedLauncher.exe is a Native AOT executable.
#>
param(
    [string] $Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$publish = Join-Path $root "CDPIUI/bin/x64/$Configuration/net8.0-windows10.0.22621.0/publish"
$tools = Join-Path $root "artifacts/tools"

function Invoke-Checked([string] $name, [scriptblock] $command) {
    Write-Output "== $name"
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$name failed with exit code $LASTEXITCODE" }
}

function Test-ManagedAssembly([string] $path) {
    try {
        [void][System.Reflection.AssemblyName]::GetAssemblyName($path)
        return $true
    }
    catch {
        return $false
    }
}

& (Join-Path $PSScriptRoot "create_secret_stub.ps1")

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }

Invoke-Checked "Publish CDPIUI" {
    msbuild (Join-Path $root "CDPIUI/CDPIUI.csproj") /restore /t:Publish /m /nologo /v:minimal `
        "/p:Configuration=$Configuration" /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=false `
        "/p:PublishDir=$publish\"
}

# The Windows App SDK copies its .mui files for about 90 languages regardless of
# SatelliteResourceLanguages; the application and the MSI have English and Russian only.
$keptLanguages = @("en-us", "ru-ru")
Get-ChildItem -LiteralPath $publish -Directory |
    Where-Object { $keptLanguages -notcontains $_.Name.ToLowerInvariant() } |
    Where-Object {
        $files = @(Get-ChildItem -LiteralPath $_.FullName -File -Recurse)
        $files.Count -gt 0 -and @($files | Where-Object { $_.Extension -ne ".mui" }).Count -eq 0
    } |
    Remove-Item -Recurse -Force

Invoke-Checked "Publish CDPIUI.TrayIcon" {
    dotnet publish (Join-Path $root "CDPIUI.TrayIcon/CDPIUI.TrayIcon.csproj") -c $Configuration -p:Platform=x64 `
        -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $tools "tray")
}

# Update.exe replaces the application files while it runs, so it must not load a DLL from the
# installation folder: a single file has everything in it.
Invoke-Checked "Publish Update" {
    dotnet publish (Join-Path $root "Update/Update.csproj") -c $Configuration -p:Platform=x64 `
        -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $tools "update")
}

Invoke-Checked "Publish EditHostFile" {
    dotnet publish (Join-Path $root "EditHostFile/EditHostFile.csproj") -c $Configuration -p:Platform=x64 `
        -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $tools "edithostfile")
}

# The GUI and the autorun task start the tray and EditHostFile.exe through the launcher. It removes
# the .NET variables of the user profile (DOTNET_ROOT, CORECLR_PROFILER_PATH and others) that UAC
# gives to an elevated process. It has no .NET host, so these variables do not act on it.
Invoke-Checked "Publish CDPIUI.ElevatedLauncher" {
    dotnet publish (Join-Path $root "CDPIUI.ElevatedLauncher/CDPIUI.ElevatedLauncher.csproj") -c $Configuration -p:Platform=x64 `
        -r win-x64 -o (Join-Path $tools "launcher")
}
$launcher = Join-Path $tools "launcher/CDPIUI_ElevatedLauncher.exe"
if (-not (Test-Path -LiteralPath $launcher)) { throw "The launcher publish has no CDPIUI_ElevatedLauncher.exe." }
if (Test-ManagedAssembly $launcher) { throw "CDPIUI_ElevatedLauncher.exe is a .NET assembly. It must be a Native AOT executable." }

Copy-Item -LiteralPath (Join-Path $tools "tray/CDPIUI_TrayIcon.exe") -Destination $publish
Copy-Item -LiteralPath (Join-Path $tools "update/Update.exe") -Destination $publish
Copy-Item -LiteralPath (Join-Path $tools "edithostfile/EditHostFile.exe") -Destination $publish
Copy-Item -LiteralPath $launcher -Destination $publish

Write-Output "Portable layout: $publish"
