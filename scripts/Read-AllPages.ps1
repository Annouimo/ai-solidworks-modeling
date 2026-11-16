$ErrorActionPreference='Stop'
$folder=Join-Path (Split-Path -Parent $PSScriptRoot) 'results\slides'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$ppt=New-Object -ComObject PowerPoint.Application
$deck=$ppt.Presentations.Open((Get-ChildItem -LiteralPath (Split-Path -Parent $PSScriptRoot) -Filter '*.ppt' | Select-Object -First 1 -ExpandProperty FullName),-1,0,0)
try {
 Write-Output ('Slide count: '+$deck.Slides.Count)
 for($i=1;$i -le $deck.Slides.Count;$i++) { $deck.Slides.Item($i).Export((Join-Path $folder ('page'+$i+'.png')),'PNG',1600,1200) }
} finally { $deck.Close() }
