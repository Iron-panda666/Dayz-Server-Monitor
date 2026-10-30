# Regenerate the original T/R icon with Windows System.Drawing. No desktop windows.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$asset = Join-Path $root 'src\MagDeer.App\Assets'
$sizes = @(16,24,32,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap($size,$size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/256.0,$size/256.0)
    $bg = New-Object Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(3,3,100,100,180,90); $bg.AddArc(153,3,100,100,270,90)
    $bg.AddArc(153,153,100,100,0,90); $bg.AddArc(3,153,100,100,90,90); $bg.CloseFigure()
    $fill = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#14222b'))
    $edge = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#31515c'),4)
    $g.FillPath($fill,$bg); $g.DrawPath($edge,$bg)
    $white = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#eaf4f7'),16)
    $orange = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#ffb15c'),12)
    $blue = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#90e8ff'),14)
    foreach ($pen in @($white,$orange,$blue)) { $pen.StartCap='Round'; $pen.EndCap='Round'; $pen.LineJoin='Round' }
    $g.DrawLine($white,32,76,96,76); $g.DrawLine($white,64,76,64,180)
    $g.DrawLine($orange,110,196,148,60)
    $r = New-Object Drawing.Drawing2D.GraphicsPath
    $r.AddLine(172,180,172,76); $r.AddLine(172,76,194,76)
    $r.AddBezier(194,76,226,76,226,128,194,128); $r.AddLine(194,128,172,128)
    $r.StartFigure(); $r.AddLine(194,128,226,180); $g.DrawPath($blue,$r)
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$stream.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $root 'docs\images\app-icon.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $bg.Dispose(); $r.Dispose()
    foreach ($item in @($fill,$edge,$white,$orange,$blue)) { $item.Dispose() }
}
$file = [IO.File]::Create((Join-Path $asset 'app.ico'))
$writer = New-Object IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'Generated app.ico (16–256 px) and app-icon.png.'
