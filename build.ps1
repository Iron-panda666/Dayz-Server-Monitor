param(
    [string]$DotNet = "dotnet",
    [string]$OutputDirectory = "",
    [switch]$SkipTests
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
Invoke-DotNet -Arguments @("build", "MagDeerMonitor.sln", "-c", "Release", "--configfile", "NuGet.Config")
if (-not $SkipTests) {
    Invoke-DotNet -Arguments @("run", "--project", "tests\MagDeer.Tests", "-c", "Release", "--no-build")
    Invoke-DotNet -Arguments @("run", "--project", "tests\MagDeer.ThemeTests", "-c", "Release", "--no-build", "--", "--render", "artifacts\previews\theme-comparison.png")
}
$version = ([xml](Get-Content -LiteralPath "src\MagDeer.App\MagDeer.App.csproj" -Raw)).Project.PropertyGroup.Version
$publish = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $PSScriptRoot "artifacts\release-$version" }
Invoke-DotNet -Arguments @("publish", "src\MagDeer.App", "-c", "Release", "-p:PublishProfile=Portable", "--configfile", "NuGet.Config", "--output", $publish)
Copy-Item -LiteralPath "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "config.example.json" -Destination $publish -Force
Copy-Item -LiteralPath "docs" -Destination $publish -Recurse -Force
$portableZip = Join-Path $PSScriptRoot "artifacts\MAG-DEER-Server-Monitor-$version-win-x64.zip"
$portableFiles = @("MagDeerMonitor.exe", "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "config.example.json", "docs") | ForEach-Object { Join-Path $publish $_ }
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
Copy-Item -LiteralPath "MagDeerMonitor.sln", "Directory.Build.props", "global.json", "NuGet.Config", ".gitignore", "README.md", "README.cs.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "build.ps1", "config.example.json" -Destination $sourceStage
foreach ($folder in @("src", "tests", "docs", "tools")) {
    Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
        $relative = $_.FullName.Substring($PSScriptRoot.Length + 1)
        $destination = Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    }
}
$sourceZip = Join-Path $PSScriptRoot "artifacts\MAG-DEER-Server-Monitor-$version-source.zip"
Compress-Archive -Path (Join-Path $sourceStage "*") -DestinationPath $sourceZip -Force
Get-FileHash -LiteralPath $portableZip, $sourceZip -Algorithm SHA256 | Format-Table Hash, Path -AutoSize
Write-Host "Ready: $portableZip"
Write-Host "Source: $sourceZip"
# Deliberately never launch the desktop app, change foreground windows or modify Windows startup.
