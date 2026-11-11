# Erzeugt monitor.ico (16-256 px): Graphit-Plakette mit oranger Pulskurve (Symbol des Systemmonitors).
Add-Type -AssemblyName System.Drawing
$out = Join-Path (Split-Path $PSScriptRoot) 'monitor.ico'
$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $s / 32.0
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = [single](8 * $k); $x0 = [single](1 * $k); $w = [single](30 * $k) - $x0
    $p.AddArc($x0, $x0, $r, $r, 180, 90); $p.AddArc($x0 + $w - $r, $x0, $r, $r, 270, 90)
    $p.AddArc($x0 + $w - $r, $x0 + $w - $r, $r, $r, 0, 90); $p.AddArc($x0, $x0 + $w - $r, $r, $r, 90, 90); $p.CloseFigure()
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect,
        [System.Drawing.Color]::FromArgb(78, 82, 88), [System.Drawing.Color]::FromArgb(26, 28, 31), [single]45)
    $g.FillPath($bg, $p)
    # Pulskurve wie im Monitor: 3,12 7,12 10,5 14,19 17,12 21,12 (in 24er-Raster) -> auf 32 skaliert
    $pts = @(@(4, 16), @(9.3, 16), @(13.3, 6.7), @(18.7, 25.3), @(22.7, 16), @(28, 16)) | ForEach-Object {
        New-Object System.Drawing.PointF ([single]($_[0] * $k)), ([single]($_[1] * $k)) }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 122, 26)), ([single]([Math]::Max(1.6, 2.6 * $k)))
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]$pts)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    ,$ms.ToArray()
}
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Close()
"Icon geschrieben: $out"
