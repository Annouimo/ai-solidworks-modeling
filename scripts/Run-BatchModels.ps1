param([string]$Ids = '')
$ErrorActionPreference='Stop'
$interop='D:\Programs\SolidWorks\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
Add-Type -Path $interop
Add-Type -ReferencedAssemblies $interop -Path (Join-Path $PSScriptRoot 'BatchModels.cs')
[BatchModels]::Run((Split-Path -Parent $PSScriptRoot),$Ids)
