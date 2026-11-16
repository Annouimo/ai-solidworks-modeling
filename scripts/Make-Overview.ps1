Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$folder=Join-Path $root 'deliverables\previews'
$prefix=[string][char]0x96f6+[char]0x4ef6
foreach($start in 1,11){
 $sheet=New-Object Drawing.Bitmap 1600,1050
 $g=[Drawing.Graphics]::FromImage($sheet)
 $g.Clear([Drawing.Color]::White)
 $font=New-Object Drawing.Font 'Microsoft YaHei',16
 for($k=0;$k -lt 10;$k++){
  $n=$start+$k;$x=($k%4)*400;$y=[math]::Floor($k/4)*350
  $path=Join-Path $folder ($prefix+$n+'_7.bmp')
  if(Test-Path -LiteralPath $path){$im=[Drawing.Image]::FromFile($path);$g.DrawImage($im,$x,($y+35),400,300);$im.Dispose()}
  $g.DrawString(($prefix+$n),$font,[Drawing.Brushes]::Black,$x+10,$y)
 }
 $sheet.Save((Join-Path $folder ('overview'+$start+'.png')),[Drawing.Imaging.ImageFormat]::Png)
 $font.Dispose();$g.Dispose();$sheet.Dispose()
}
