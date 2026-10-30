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
Dayz Server Monitor $Version - PORTABLE / NO INSTALLATION

1. Extract $portableName into a writable folder.
2. Run DayzServerMonitor.exe. No setup wizard, administrator rights or separate .NET install is needed.
3. Keep config.json and the data folder when updating. They are not included in the ZIP.

The Windows ZIP includes the monitor and, in a full build, the Stream Deck plugin installer.
The .streamDeckPlugin is OPTIONAL and must be installed in the Elgato Stream Deck application.
Installing that plugin does not install the monitor. The monitor remains portable.
The standalone plugin is a convenience copy of the same installer inside the Windows ZIP.
The source ZIP is for developers. SHA256SUMS.txt verifies the files in this folder.
Nothing is launched or installed by packaging.

CZ: Prenosna verze bez instalace. Rozbal Windows ZIP a spust DayzServerMonitor.exe.
Pri aktualizaci ponech config.json a data. Volitelny plugin pro Stream Deck se instaluje samostatne.
"@ | Set-Content -LiteralPath (Join-Path $destination "README.txt") -Encoding utf8
if ((Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination $portableName) -Algorithm SHA256).Hash) { throw "Portable package copy failed verification" }
Write-Host "Portable packages: $destination"
