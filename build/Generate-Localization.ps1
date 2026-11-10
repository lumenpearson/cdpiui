param(
    [Parameter(Mandatory = $true)][string] $SourceDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$catalogs = Get-ChildItem -LiteralPath $SourceDirectory -Recurse -Filter '*.json' |
    Where-Object { $_.BaseName -notin @('Tray', 'Update', 'EditHostFile') }

foreach ($catalog in $catalogs) {
    $translations = Get-Content -LiteralPath $catalog.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $outputFolder = Join-Path $OutputDirectory $catalog.Directory.Name
    [System.IO.Directory]::CreateDirectory($outputFolder) | Out-Null
    $outputPath = Join-Path $outputFolder ($catalog.BaseName + '.resw')
    $document = New-Object System.Xml.XmlDocument
    $declaration = $document.CreateXmlDeclaration('1.0', 'utf-8', $null)
    $document.AppendChild($declaration) | Out-Null
    $root = $document.CreateElement('root')
    $document.AppendChild($root) | Out-Null
    foreach ($entry in $translations.PSObject.Properties) {
        if ($entry.Value -isnot [string] -or [string]::IsNullOrWhiteSpace($entry.Value)) {
            throw "Invalid translation in $($catalog.FullName): $($entry.Name)"
        }
        $data = $document.CreateElement('data')
        $data.SetAttribute('name', $entry.Name)
        $data.SetAttribute('space', 'http://www.w3.org/XML/1998/namespace', 'preserve') | Out-Null
        $value = $document.CreateElement('value')
        $value.InnerText = $entry.Value
        $data.AppendChild($value) | Out-Null
        $root.AppendChild($data) | Out-Null
    }
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = $utf8
    # Preserve embedded CR/LF exactly, including help pages and multiline templates.
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Entitize
    $buffer = New-Object System.IO.MemoryStream
    $writer = [System.Xml.XmlWriter]::Create($buffer, $settings)
    try { $document.Save($writer) } finally { $writer.Dispose() }
    $content = $utf8.GetString($buffer.ToArray())
    $buffer.Dispose()
    if (!(Test-Path -LiteralPath $outputPath) -or
        [System.IO.File]::ReadAllText($outputPath) -cne $content) {
        [System.IO.File]::WriteAllText($outputPath, $content, $utf8)
    }
}
