[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$InteropPath,
    [string]$PartTemplate,
    [string]$AssemblyTemplate
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell (powershell.exe).' }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path -Parent $PSCommandPath | Split-Path -Parent) 'results\gear-bearing'
}
$source = Join-Path (Split-Path -Parent $PSCommandPath) 'GearBearingAssembly.cs'
if (-not $InteropPath) {
    $clsidKey = 'HKLM:\SOFTWARE\Classes\SldWorks.Application\CLSID'
    if (Test-Path $clsidKey) {
        $clsid = (Get-Item $clsidKey).GetValue('')
        $serverKey = "HKLM:\SOFTWARE\Classes\CLSID\$clsid\LocalServer32"
        if (Test-Path $serverKey) {
            $server = (Get-Item $serverKey).GetValue('')
            if ($server -match '^\s*"?(.+?\.exe)') {
                $InteropPath = Join-Path (Split-Path $Matches[1]) 'api\redist\SolidWorks.Interop.sldworks.dll'
            }
        }
    }
}
if (-not $InteropPath) { throw 'SolidWorks API library not found. Supply -InteropPath.' }
$InteropPath = (Resolve-Path -LiteralPath $InteropPath).ProviderPath
$defaultTemplates = Join-Path $env:ProgramData 'SOLIDWORKS\SOLIDWORKS 2025\templates'
if (-not $PartTemplate -and (Test-Path (Join-Path $defaultTemplates 'gb_part.prtdot'))) {
    $PartTemplate = Join-Path $defaultTemplates 'gb_part.prtdot'
}
if (-not $AssemblyTemplate -and (Test-Path (Join-Path $defaultTemplates 'gb_assembly.asmdot'))) {
    $AssemblyTemplate = Join-Path $defaultTemplates 'gb_assembly.asmdot'
}
if ($PartTemplate) { $PartTemplate = (Resolve-Path -LiteralPath $PartTemplate).ProviderPath }
if ($AssemblyTemplate) { $AssemblyTemplate = (Resolve-Path -LiteralPath $AssemblyTemplate).ProviderPath }
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies $InteropPath -Path $source
[GearBearingBuilder]::Run($OutputDirectory, $PartTemplate, $AssemblyTemplate)
