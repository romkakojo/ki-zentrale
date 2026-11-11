# Erzeugt kiz.ico (16/24/32/48/64/256 px, PNG-komprimiert): Graphit-Plakette mit Strahlen und orangem Punkt.
Add-Type -AssemblyName System.Drawing
$out = Join-Path (Split-Path $PSScriptRoot) 'kiz.ico'
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
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(225, 229, 231, 235)), ([single](2.2 * $k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $c = 16 * $k
    foreach ($a in 0, 45, 90, 135, 180, 225, 270, 315) {
        $rad = $a * [Math]::PI / 180
        $g.DrawLine($pen, [single]($c + 7.5 * $k * [Math]::Cos($rad)), [single]($c + 7.5 * $k * [Math]::Sin($rad)),
                          [single]($c + 11 * $k * [Math]::Cos($rad)), [single]($c + 11 * $k * [Math]::Sin($rad)))
    }
    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 122, 26))
    $g.FillEllipse($dot, [single]($c - 5 * $k), [single]($c - 5 * $k), [single](10 * $k), [single](10 * $k))
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
