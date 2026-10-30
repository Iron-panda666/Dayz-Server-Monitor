param(
    [string]$DotNet = "dotnet",
    [string]$OutputDirectory = "",
    [switch]$SkipTests,
    [switch]$SkipStreamDeck
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
if ($DotNet -eq "dotnet" -and (Test-Path ".tools\dotnet\dotnet.exe")) {
    $DotNet = (Resolve-Path ".tools\dotnet\dotnet.exe").Path
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot ".tools\cli-home"
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot ".tools\nuget"
}
function Invoke-DotNet([string[]]$Arguments) {
    & $DotNet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE)" }
}
Invoke-DotNet -Arguments @("build", "DayzServerMonitor.sln", "-c", "Release", "--configfile", "NuGet.Config")
if (-not $SkipTests) {
    Invoke-DotNet -Arguments @("run", "--project", "tests\MagDeer.Tests", "-c", "Release", "--no-build")
    Invoke-DotNet -Arguments @("run", "--project", "tests\MagDeer.ThemeTests", "-c", "Release", "--no-build", "--", "--render", "artifacts\previews\theme-comparison.png")
}
if (-not $SkipStreamDeck) {
    Push-Location "streamdeck"
    try {
        & npm.cmd ci --ignore-scripts --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw "Plugin dependency install failed" }
        & npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw "Plugin build failed" }
        if (-not $SkipTests) {
            & npm.cmd test
            if ($LASTEXITCODE -ne 0) { throw "Plugin tests failed" }
        }
        & .\node_modules\.bin\streamdeck.cmd validate com.iron-panda666.dayz-server-monitor.sdPlugin --no-update-check
        if ($LASTEXITCODE -ne 0) { throw "Elgato validation failed" }
        & .\node_modules\.bin\streamdeck.cmd pack com.iron-panda666.dayz-server-monitor.sdPlugin --output ..\artifacts --force --no-update-check --no-file-list
        if ($LASTEXITCODE -ne 0) { throw "Elgato packaging failed" }
    }
    finally { Pop-Location }
}
foreach ($preview in @("theme-comparison.png", "world-clocks.png", "query-chart.png", "layout-desktop.png", "layout-wide.png", "layout-bar-79.png", "layout-bar-88.png", "layout-bar-119.png", "layout-portrait.png", "layout-mini.png", "layout-ribbon.png")) {
    $path = Join-Path "artifacts\previews" $preview
    if (Test-Path -LiteralPath $path) {
        New-Item -ItemType Directory -Path "docs\images" -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination (Join-Path "docs\images" $preview) -Force
    }
}
$version = ([xml](Get-Content -LiteralPath "src\MagDeer.App\MagDeer.App.csproj" -Raw)).Project.PropertyGroup.Version
$pluginPackage = Join-Path $PSScriptRoot "artifacts\Dayz-Server-Monitor-$version.streamDeckPlugin"
if (-not $SkipStreamDeck) {
    Move-Item -LiteralPath "artifacts\com.iron-panda666.dayz-server-monitor.streamDeckPlugin" -Destination $pluginPackage -Force
}
$publish = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot "artifacts\release-$version" }
Invoke-DotNet -Arguments @("publish", "src\MagDeer.App", "-c", "Release", "-p:PublishProfile=Portable", "--configfile", "NuGet.Config", "--output", $publish)
Copy-Item -LiteralPath "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "config.example.json" -Destination $publish -Force
Copy-Item -LiteralPath "docs" -Destination $publish -Recurse -Force
$portableZip = Join-Path $PSScriptRoot "artifacts\Dayz-Server-Monitor-$version-win-x64.zip"
$portableFiles = @("DayzServerMonitor.exe", "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "config.example.json", "docs") | ForEach-Object { Join-Path $publish $_ }
if (-not $SkipStreamDeck) {
    $pluginCopy = Join-Path $publish ([IO.Path]::GetFileName($pluginPackage))
    Copy-Item -LiteralPath $pluginPackage -Destination $pluginCopy -Force
    $portableFiles += $pluginCopy
}
if (Test-Path -LiteralPath "artifacts\previews\theme-comparison.png") {
    $previewCopy = Join-Path $publish "theme-comparison.png"
    Copy-Item -LiteralPath "artifacts\previews\theme-comparison.png" -Destination $previewCopy -Force
    $portableFiles += $previewCopy
}
if (Test-Path -LiteralPath "artifacts\previews\world-clocks.png") {
    $clockPreviewCopy = Join-Path $publish "world-clocks.png"
    Copy-Item -LiteralPath "artifacts\previews\world-clocks.png" -Destination $clockPreviewCopy -Force
    $portableFiles += $clockPreviewCopy
}
if (Test-Path -LiteralPath "artifacts\previews\query-chart.png") {
    $chartPreviewCopy = Join-Path $publish "query-chart.png"
    Copy-Item -LiteralPath "artifacts\previews\query-chart.png" -Destination $chartPreviewCopy -Force
    $portableFiles += $chartPreviewCopy
}
Compress-Archive -LiteralPath $portableFiles -DestinationPath $portableZip -Force
$sourceStage = Join-Path $PSScriptRoot ("artifacts\source-stage-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
Copy-Item -LiteralPath "DayzServerMonitor.sln", "Directory.Build.props", "global.json", "NuGet.Config", ".gitignore", "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "build.ps1", "config.example.json" -Destination $sourceStage
foreach ($folder in @("src", "tests", "docs", "tools", "streamdeck")) {
    Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|node_modules|logs)[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
        $destination = Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
}
New-Item -ItemType Directory -Path (Join-Path $sourceStage "ReleasePackages") -Force | Out-Null
Copy-Item -LiteralPath "ReleasePackages\README.md" -Destination (Join-Path $sourceStage "ReleasePackages")
$sourceZip = Join-Path $PSScriptRoot "artifacts\Dayz-Server-Monitor-$version-source.zip"
Compress-Archive -Path (Join-Path $sourceStage "*") -DestinationPath $sourceZip -Force
Get-FileHash -LiteralPath $portableZip, $sourceZip -Algorithm SHA256 | Format-Table Hash, Path -AutoSize
Write-Host "Ready: $portableZip"
Write-Host "Source: $sourceZip"
$hashFiles = @($portableZip, $sourceZip, (Join-Path $publish "DayzServerMonitor.exe"))
if (-not $SkipStreamDeck) {
    $hashFiles += $pluginPackage
}
$checksumPath = Join-Path $PSScriptRoot "artifacts\SHA256SUMS-$version.txt"
$hashFiles | ForEach-Object { $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256; $hash.Hash.ToLowerInvariant() + "  " + [IO.Path]::GetFileName($_) } | Set-Content -LiteralPath $checksumPath -Encoding utf8
$packageArgs = @{ Version = $version; PortableZip = $portableZip; SourceZip = $sourceZip; ExecutablePath = (Join-Path $publish "DayzServerMonitor.exe") }
if (-not $SkipStreamDeck) { $packageArgs.PluginPackage = $pluginPackage }
& (Join-Path $PSScriptRoot "tools\New-ReleasePackages.ps1") @packageArgs
# Never launch the desktop app or install the plugin during packaging.
