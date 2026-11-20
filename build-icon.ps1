$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    # Draw vector geometry at 4x resolution, then downsample for clean tray sizes.
    $large = [System.Drawing.Bitmap]::new($size * 4, $size * 4)
    $g = [System.Drawing.Graphics]::FromImage($large)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size / 16.0, $size / 16.0)
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#2E2E2E'))
    $accent = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#4772B3'))
    $line = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#E0E0E0'), 4.5)
    $line.StartCap = $line.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $line.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.FillEllipse($background, 1, 1, 62, 62)
    foreach ($points in @(@(25,18,17,18,17,26), @(39,18,47,18,47,26), @(17,38,17,46,25,46), @(47,38,47,46,39,46))) {
        $g.DrawLine($line, $points[0], $points[1], $points[2], $points[3])
        $g.DrawLine($line, $points[2], $points[3], $points[4], $points[5])
    }
    $screen = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $screen.AddArc(23,25,6,6,180,90); $screen.AddArc(35,25,6,6,270,90)
    $screen.AddArc(35,33,6,6,0,90); $screen.AddArc(23,33,6,6,90,90); $screen.CloseFigure()
    $g.FillPath($accent,$screen)
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $output = [System.Drawing.Graphics]::FromImage($bitmap)
    $output.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $output.DrawImage($large,0,0,$size,$size)
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $PSScriptRoot 'assets\logo.png')) }
    if ($size -eq 16) { $bitmap.Save((Join-Path $PSScriptRoot 'assets\tray-16.png')) }
    $stream.Dispose(); $output.Dispose(); $bitmap.Dispose(); $g.Dispose(); $large.Dispose()
    $screen.Dispose(); $line.Dispose(); $background.Dispose(); $accent.Dispose()
}
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $PSScriptRoot 'assets\app.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
} finally { $writer.Dispose() }
