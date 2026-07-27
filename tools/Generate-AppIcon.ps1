Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeIcon {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

$output = Join-Path $PSScriptRoot '..\src\ScreenshotTranslation.App\Assets\AppIcon.ico'
New-Item -ItemType Directory -Force -Path (Split-Path $output -Parent) | Out-Null
function New-RoundedRectanglePath([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$bitmap = [System.Drawing.Bitmap]::new(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$backgroundPath = New-RoundedRectanglePath 1 1 62 62 12
$backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(124, 58, 237))
$graphics.FillPath($backgroundBrush, $backgroundPath)
$pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 4)
$graphics.DrawLine($pen, 12, 24, 12, 12)
$graphics.DrawLine($pen, 12, 12, 24, 12)
$graphics.DrawLine($pen, 40, 12, 52, 12)
$graphics.DrawLine($pen, 52, 12, 52, 24)
$graphics.DrawLine($pen, 12, 40, 12, 52)
$graphics.DrawLine($pen, 12, 52, 24, 52)
$graphics.DrawLine($pen, 40, 52, 52, 52)
$graphics.DrawLine($pen, 52, 40, 52, 52)
$bubblePath = New-RoundedRectanglePath 20 23 24 17 5
$graphics.FillPath([System.Drawing.Brushes]::White, $bubblePath)
$graphics.FillPolygon([System.Drawing.Brushes]::White, @(
    [System.Drawing.Point]::new(25, 39),
    [System.Drawing.Point]::new(25, 46),
    [System.Drawing.Point]::new(32, 39)))
$handle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($handle)
$stream = [System.IO.File]::Create($output)
$icon.Save($stream)
$stream.Dispose()
$icon.Dispose()
[NativeIcon]::DestroyIcon($handle) | Out-Null
$bubblePath.Dispose()
$backgroundBrush.Dispose()
$backgroundPath.Dispose()
$pen.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
