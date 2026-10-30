param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory = $true)][string]$PortableZip,
    [string]$SourceZip = "",
    [string]$PluginPackage = "",
    [string]$ExecutablePath = "",
    [string]$OutputDirectory = ""
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = Split-Path $PSScriptRoot -Parent
$destination = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot "ReleasePackages\v$Version" }
$portable = (Resolve-Path -LiteralPath $PortableZip).Path
function Get-StreamHash($Stream) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($Stream))).Replace("-", "").ToLowerInvariant() }
    finally { $sha.Dispose() }
}
$archive = [IO.Compression.ZipFile]::OpenRead($portable)
try {
    if (-not $archive.GetEntry("DayzServerMonitor.exe")) { throw "Windows ZIP is missing DayzServerMonitor.exe" }
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '(^|[/\\])config\.json$|^data[/\\]') { throw "Private runtime data in Windows ZIP" }
    }
    if ($PluginPackage) {
        $pluginEntry = $archive.GetEntry([IO.Path]::GetFileName($PluginPackage))
        if (-not $pluginEntry) { throw "Windows ZIP is missing the Stream Deck installer" }
        $stream = $pluginEntry.Open()
        try { $embeddedHash = Get-StreamHash $stream } finally { $stream.Dispose() }
        if ($embeddedHash -ne (Get-FileHash -LiteralPath $PluginPackage -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "Embedded plugin differs from standalone installer" }
    }
    if ($ExecutablePath) {
        $stream = $archive.GetEntry("DayzServerMonitor.exe").Open()
        try { $embeddedHash = Get-StreamHash $stream } finally { $stream.Dispose() }
        if ($embeddedHash -ne (Get-FileHash -LiteralPath $ExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant()) { throw "ZIP executable differs from published executable" }
    }
}
finally { $archive.Dispose() }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$portableName = "Dayz-Server-Monitor-$Version-portable-win-x64.zip"
Copy-Item -LiteralPath $portable -Destination (Join-Path $destination $portableName) -Force
$files = @((Join-Path $destination $portableName))
foreach ($inputFile in @($SourceZip, $PluginPackage)) {
    if ($inputFile) {
        $target = Join-Path $destination ([IO.Path]::GetFileName($inputFile))
        Copy-Item -LiteralPath $inputFile -Destination $target -Force
        $files += $target
    }
}
$manifest = [ordered]@{
    version = $Version
    distribution = "portable"
    installationRequired = $false
    platform = "Windows 10/11 x64"
    streamDeckPluginRequiresInstallation = [bool]$PluginPackage
    files = @($files | ForEach-Object { [ordered]@{ name = [IO.Path]::GetFileName($_); bytes = (Get-Item -LiteralPath $_).Length; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() } })
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination "package.json") -Encoding utf8
$files | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + "  " + [IO.Path]::GetFileName($_) } | Set-Content -LiteralPath (Join-Path $destination "SHA256SUMS.txt") -Encoding utf8
@"
Dayz Server Monitor $Version

Portable build for Windows 10/11 x64. Extract $portableName into a writable folder
and run DayzServerMonitor.exe. The .NET runtime is included; no setup or admin rights needed.

Close the old copy before updating. Keep config.json and data; neither is in the ZIP.

The full Windows ZIP includes the Stream Deck installer. Install that optional plugin
into Elgato Stream Deck. The monitor itself stays portable. The standalone plugin
is the same file as the one in the Windows ZIP.

The source ZIP contains the matching sources. SHA256SUMS.txt lists this folder's checksums.
Packaging does not open the app or an installer.

CZ: Přenosná verze bez instalace. Rozbal ZIP a spusť DayzServerMonitor.exe.
Při aktualizaci ponech config.json a data. Plugin se instaluje do Elgato Stream Deck.
"@ | Set-Content -LiteralPath (Join-Path $destination "README.txt") -Encoding utf8
if ((Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination $portableName) -Algorithm SHA256).Hash) { throw "Portable package copy failed verification" }
Write-Host "Portable packages: $destination"
