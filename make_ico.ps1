Add-Type -AssemblyName System.Drawing
$root = "C:/Users/Administrator/WorkBuddy/AI/ClasslandCE"
$srcPath = Join-Path $root "ClassIsland/Assets/AppLogo.png"
$dstPath = Join-Path $root "ClassIsland/Assets/AppLogo.ico"

$src = [System.Drawing.Image]::FromFile($srcPath)
$sizes = 16,32,48,64,128,256
$pngs = @()
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.DrawImage($src, 0, 0, $s, $s)
  $g.Dispose()
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs += @{ size=$s; data=$ms.ToArray() }
  $ms.Dispose()
  $bmp.Dispose()
}
$src.Dispose()

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
  $w = if ($p.size -ge 256) { 0 } else { $p.size }
  $bw.Write([byte]$w)
  $bw.Write([byte]$w)
  $bw.Write([byte]0)
  $bw.Write([byte]0)
  $bw.Write([uint16]1)
  $bw.Write([uint16]32)
  $bw.Write([uint32]$p.data.Length)
  $bw.Write([uint32]$offset)
  $offset += $p.data.Length
}
foreach ($p in $pngs) {
  $bw.Write($p.data)
}
$bw.Flush()
[System.IO.File]::WriteAllBytes($dstPath, $ms.ToArray())
$ms.Dispose()
Write-Output ("ICO_DONE size=" + [math]::Round((Get-Item $dstPath).Length/1KB,1) + "KB")