Add-Type -AssemblyName System.Drawing

function Add-RoundedRectPath {
    param($path, [float]$x, [float]$y, [float]$w, [float]$h, [float]$radius)
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

function New-RobotBitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $black = [System.Drawing.Color]::FromArgb(255, 15, 15, 15)
    $yellow = [System.Drawing.Color]::FromArgb(255, 255, 216, 0)
    $blackBrush = New-Object System.Drawing.SolidBrush $black
    $yellowBrush = New-Object System.Drawing.SolidBrush $yellow

    # Background: black rounded square
    $bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRectPath $bgPath 0 0 $size $size ($size * 0.20)
    $g.FillPath($blackBrush, $bgPath)

    # Antenna
    $cx = $size * 0.5
    $penW = [Math]::Max(1.0, $size * 0.035)
    $pen = New-Object System.Drawing.Pen $yellow, $penW
    $g.DrawLine($pen, $cx, $size*0.10, $cx, $size*0.20)
    $g.FillEllipse($yellowBrush, $cx-$size*0.035, $size*0.055, $size*0.07, $size*0.07)

    # Head (yellow rounded rect)
    $headX = $size * 0.20
    $headY = $size * 0.22
    $headW = $size * 0.60
    $headH = $size * 0.56
    $headPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRectPath $headPath $headX $headY $headW $headH ($size * 0.10)
    $g.FillPath($yellowBrush, $headPath)

    # Eyes (black)
    $eyeW = $size * 0.11
    $eyeH = $size * 0.16
    $eyeY = $headY + $headH * 0.22
    $g.FillEllipse($blackBrush, $headX + $headW*0.20, $eyeY, $eyeW, $eyeH)
    $g.FillEllipse($blackBrush, $headX + $headW*0.80 - $eyeW, $eyeY, $eyeW, $eyeH)

    # Mouth grille (only for larger sizes, keeps small sizes clean)
    if ($size -ge 48) {
        $barH = $size * 0.045
        $barY = $headY + $headH * 0.62
        $barW = $headW * 0.62
        $barX = $headX + ($headW - $barW) / 2
        for ($i = 0; $i -lt 3; $i++) {
            $g.FillRectangle($blackBrush, $barX, $barY + $i * ($barH * 1.6), $barW, $barH)
        }
    }
    else {
        $barH = $size * 0.07
        $barY = $headY + $headH * 0.68
        $barW = $headW * 0.55
        $barX = $headX + ($headW - $barW) / 2
        $g.FillRectangle($blackBrush, $barX, $barY, $barW, $barH)
    }

    # Side antennae/ears (small yellow nubs)
    $earW = $size * 0.07
    $earH = $size * 0.18
    $earY = $headY + $headH * 0.30
    $g.FillRectangle($yellowBrush, $headX - $earW*0.6, $earY, $earW, $earH)
    $g.FillRectangle($yellowBrush, $headX + $headW - $earW*0.4, $earY, $earW, $earH)

    $g.Dispose()
    return $bmp
}

$sizes = 16, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { New-RobotBitmap $s }

$pngDataList = New-Object System.Collections.Generic.List[byte[]]
foreach ($img in $images) {
    $pngStream = New-Object System.IO.MemoryStream
    $img.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList.Add($pngStream.ToArray())
    $pngStream.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$images.Count)

$offset = 6 + (16 * $images.Count)
for ($i = 0; $i -lt $images.Count; $i++) {
    $img = $images[$i]
    $w = if ($img.Width -ge 256) { 0 } else { $img.Width }
    $h = if ($img.Height -ge 256) { 0 } else { $img.Height }
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngDataList[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngDataList[$i].Length
}
foreach ($data in $pngDataList) { $bw.Write($data) }
$bw.Flush()

$outDir = 'C:\Users\tlangfe\source\BitPlusTool\Resources'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
[System.IO.File]::WriteAllBytes("$outDir\AppIcon.ico", $ms.ToArray())

$previewImg = $images[$images.Count - 1]
$previewImg.Save('C:\Users\tlangfe\source\BitPlusTool\icon_preview.png', [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($img in $images) { $img.Dispose() }
$bw.Dispose()
$ms.Dispose()

Write-Output "Icon geschrieben: $outDir\AppIcon.ico"
