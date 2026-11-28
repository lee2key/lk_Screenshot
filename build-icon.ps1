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
    $tile = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tile.AddArc(0,0,14,14,180,90); $tile.AddArc(50,0,14,14,270,90)
    $tile.AddArc(50,50,14,14,0,90); $tile.AddArc(0,50,14,14,90,90)
    $tile.CloseFigure()
    $g.FillPath($background,$tile)
    foreach ($points in @(@(24,12,12,12,12,24), @(40,12,52,12,52,24), @(12,40,12,52,24,52), @(52,40,52,52,40,52))) {
        $g.DrawLine($line, $points[0], $points[1], $points[2], $points[3])
        $g.DrawLine($line, $points[2], $points[3], $points[4], $points[5])
    }
    $screen = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $screen.AddArc(19,22,6,6,180,90); $screen.AddArc(39,22,6,6,270,90)
    $screen.AddArc(39,36,6,6,0,90); $screen.AddArc(19,36,6,6,90,90); $screen.CloseFigure()
    $g.FillPath($accent,$screen)
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $output = [System.Drawing.Graphics]::FromImage($bitmap)
    $output.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $output.DrawImage($large,0,0,$size,$size)
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    $stream.Dispose(); $output.Dispose(); $bitmap.Dispose(); $g.Dispose(); $large.Dispose()
    $tile.Dispose(); $screen.Dispose(); $line.Dispose(); $background.Dispose(); $accent.Dispose()
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
