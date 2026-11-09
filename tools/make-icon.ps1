# Tạo Assets/MacDock.ico bằng cách vẽ vector với System.Drawing (không dùng tài nguyên bên ngoài).
param([string]$Out = (Join-Path $PSScriptRoot "..\src\MacDock\Assets\MacDock.ico"))
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $s = $size / 128.0
    $g.ScaleTransform($s, $s)
    $bg = New-RoundedPath 4 4 120 120 27
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0, 4), (New-Object System.Drawing.PointF 0, 124), ([System.Drawing.Color]::FromArgb(255, 0x4F, 0xA8, 0xFF)), ([System.Drawing.Color]::FromArgb(255, 0x6A, 0x5C, 0xFF)))
    $g.FillPath($br, $bg)
    # thanh dock
    $bar = New-RoundedPath 20 84 88 22 11
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(204, 255, 255, 255))), $bar)
    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x5B, 0x7C, 0xFF))
    for ($i = 0; $i -lt 4; $i++) { $g.FillPath($dot, (New-RoundedPath (28 + $i * 19) 88 14 14 4)) }
    # logo hoa bốn cánh
    $white = [System.Drawing.Brushes]::White
    $cx = 64; $cy = 44
    for ($i = 0; $i -lt 4; $i++) {
        $st = $g.Save()
        $g.TranslateTransform($cx, $cy)
        $g.RotateTransform($i * 90 + 45)
        $g.FillEllipse($white, -10.9, -30.1, 21.8, 30.2)
        $g.Restore($st)
    }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0x5D, 0x82, 0xFF))), $cx - 7, $cy - 7, 14, 14)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @(); foreach ($sz in $sizes) { $pngs += ,(New-IconPng $sz) }
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $pngs[$i]
    $b = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($d in $pngs) { $w.Write($d) }
$w.Close()
Write-Host "Đã tạo $Out"
