<#
.SYNOPSIS
Builds the x64 release layout of CDPI UI: the GUI publish folder with the tray, Update.exe and
EditHostFile.exe next to it. NewSetup/NewSetupFiles.wxs takes its files from the same folder.
Needs msbuild (Visual Studio or microsoft/setup-msbuild) and the .NET 8 SDK.
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

& (Join-Path $PSScriptRoot "create_secret_stub.ps1")

if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }

Invoke-Checked "Publish CDPIUI" {
    msbuild (Join-Path $root "CDPIUI/CDPIUI.csproj") /restore /t:Publish /m /nologo /v:minimal `
        "/p:Configuration=$Configuration" /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=false `
        "/p:PublishDir=$publish\"
}

Invoke-Checked "Publish CDPIUI.TrayIcon" {
    dotnet publish (Join-Path $root "CDPIUI.TrayIcon/CDPIUI.TrayIcon.csproj") -c $Configuration -p:Platform=x64 `
        -r win-x64 --self-contained false -p:PublishSingleFile=true -o (Join-Path $tools "tray")
}

Invoke-Checked "Build Update" {
    msbuild (Join-Path $root "Update/Update.csproj") /restore /p:RestorePackagesConfig=true /m /nologo /v:minimal `
        "/p:SolutionDir=$root\" "/p:Configuration=$Configuration" /p:Platform=x64
}

Invoke-Checked "Build EditHostFile" {
    msbuild (Join-Path $root "EditHostFile/EditHostFile.csproj") /m /nologo /v:minimal "/p:Configuration=$Configuration"
}

Copy-Item -LiteralPath (Join-Path $tools "tray/CDPIUI_TrayIcon.exe") -Destination $publish
Copy-Item -LiteralPath (Join-Path $root "Update/bin/x64/$Configuration/Update.exe") -Destination $publish
Copy-Item -LiteralPath (Join-Path $root "EditHostFile/bin/$Configuration/EditHostFile.exe") -Destination $publish

Write-Output "Portable layout: $publish"
