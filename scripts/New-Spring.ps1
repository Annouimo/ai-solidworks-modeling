[CmdletBinding()]
param(
    [ValidateRange(1, 1000)][int]$Turns = 10,
    [ValidateRange(0.1, 10000.0)][double]$MeanDiameterMm = 30,
    [ValidateRange(0.1, 10000.0)][double]$WireDiameterMm = 3,
    [ValidateRange(0.1, 10000.0)][double]$PitchMm = 8,
    [string]$OutputDirectory,
    [string]$InteropPath,
    [string]$PartTemplate
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run this script with Windows PowerShell (powershell.exe).'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path (Split-Path -Parent $PSCommandPath | Split-Path -Parent) 'results\spring' }
if ($WireDiameterMm -ge $MeanDiameterMm -or $WireDiameterMm -ge $PitchMm) {
    throw 'The wire diameter must be smaller than both the mean coil diameter and the pitch.'
}
if (-not $InteropPath) {
    $relativeDll = 'api\redist\SolidWorks.Interop.sldworks.dll'
    $key = 'HKLM:\SOFTWARE\Classes\SldWorks.Application\CLSID'
    $clsid = if (Test-Path $key) { (Get-Item $key).GetValue('') }
    $serverKey = "HKLM:\SOFTWARE\Classes\CLSID\$clsid\LocalServer32"
    $server = if ($clsid -and (Test-Path $serverKey)) { (Get-Item $serverKey).GetValue('') }
    $candidates = @(
        if ($server -match '^\s*"?(.+?\.exe)') { Join-Path (Split-Path $Matches[1]) $relativeDll }
        Join-Path "$env:ProgramFiles\SOLIDWORKS Corp\SOLIDWORKS" $relativeDll
        Join-Path 'E:\sw\SOLIDWORKS' $relativeDll
        Join-Path 'D:\Programs\SolidWorks\SOLIDWORKS Corp\SOLIDWORKS' $relativeDll
    )
    $InteropPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $InteropPath) {
    throw 'SolidWorks API library not found. Supply -InteropPath with the full path to SolidWorks.Interop.sldworks.dll.'
}
$InteropPath = (Resolve-Path -LiteralPath $InteropPath).ProviderPath
$defaultTemplate = Join-Path $env:ProgramData 'SOLIDWORKS\SOLIDWORKS 2025\templates\gb_part.prtdot'
if (-not $PartTemplate -and (Test-Path -LiteralPath $defaultTemplate)) { $PartTemplate = $defaultTemplate }
if ($PartTemplate) { $PartTemplate = (Resolve-Path -LiteralPath $PartTemplate).ProviderPath }
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)

Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies $InteropPath -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

public static class SpringBuilder {
    public static string Run(string folder, string template, int turns, double mean, double wire, double pitch) {
        const string progId = "SldWorks.Application";
        ISldWorks app;
        try { app = (ISldWorks)Marshal.GetActiveObject(progId); }
        catch (COMException ex) {
            if (ex.ErrorCode != unchecked((int)0x800401E3)) throw;
            Type server = Type.GetTypeFromProgID(progId, true);
            app = (ISldWorks)Activator.CreateInstance(server);
        }

        // SolidWorks API lengths are in metres; input and output dimensions are in mm.
        double radius = mean / 2000.0, diameter = wire / 1000.0, lead = pitch / 1000.0;
        IModelDoc2 model = String.IsNullOrEmpty(template) ?
            (IModelDoc2)app.NewPart() : (IModelDoc2)app.NewDocument(template, 0, 0, 0);
        if (model == null) throw new Exception("Could not create a part. Check the default template or supply -PartTemplate.");
        try {
            IFeature plane = (IFeature)model.FirstFeature();
            while (plane != null && plane.GetTypeName2() != "RefPlane") plane = (IFeature)plane.GetNextFeature();
            if (plane == null || !plane.Select2(false, 0)) throw new Exception("Could not select a reference plane.");
            ISketchManager sketch = model.SketchManager;
            sketch.InsertSketch(true);
            IFeature circle = (IFeature)sketch.ActiveSketch;
            if (sketch.CreateCircleByRadius(0, 0, 0, radius) == null) throw new Exception("Could not sketch the coil circle.");
            sketch.InsertSketch(true);
            model.ClearSelection2(true);
            if (!circle.Select2(false, 0)) throw new Exception("Could not select the coil circle.");

            // 0 = pitch and revolutions. Counterclockwise, with no taper.
            model.InsertHelix(false, false, false, false, 0, lead * turns, lead, turns, 0, 0);
            IFeature helix = null;
            for (IFeature f = (IFeature)model.FirstFeature(); f != null; f = (IFeature)f.GetNextFeature()) {
                if (f.GetTypeName2() == "Helix") helix = f;
            }
            if (helix == null || helix.GetErrorCode() != 0) throw new Exception("Helix creation failed.");
            IHelixFeatureData helixData = (IHelixFeatureData)helix.GetDefinition();
            if (Math.Abs(helixData.Revolution - turns) > 1e-8 || Math.Abs(helixData.Pitch - lead) > 1e-8)
                throw new Exception("The helix does not match the requested turns and pitch.");

            model.ClearSelection2(true);
            // Mark 4 identifies the sweep path; circular profile avoids a separate wire sketch.
            if (!helix.Select2(false, 4)) throw new Exception("Could not select the helix path.");
            IFeature sweep = model.FeatureManager.InsertProtrusionSwept4(
                false, false, 0, false, false, 0, 0, false, 0, 0, 0, 0,
                true, false, true, 0, true, true, diameter, 0);
            model.ClearSelection2(true);
            if (sweep == null || !model.ForceRebuild3(false) || sweep.GetErrorCode() != 0)
                throw new Exception("Circular sweep failed to rebuild.");
            object[] bodies = (object[])((IPartDoc)model).GetBodies2(0, false);
            double volume = ((IMassProperty)model.Extension.CreateMassProperty()).Volume;
            double length = turns * Math.Sqrt(Math.Pow(2 * Math.PI * radius, 2) + lead * lead);
            double expected = Math.PI * Math.Pow(diameter / 2, 2) * length;
            if (bodies == null || bodies.Length != 1 || !(Math.Abs(volume - expected) <= expected * 0.01))
                throw new Exception("Expected one solid with the volume of a round-wire spring.");

            Directory.CreateDirectory(folder);
            string stem = "Spring_" + turns + "T";
            string file = Path.Combine(folder, stem + ".SLDPRT");
            if (File.Exists(file))
                file = Path.Combine(folder, stem + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".SLDPRT");
            model.ShowNamedView2("", 7);
            model.ViewZoomtofit2();
            int errors = 0, warnings = 0;
            if (!model.Extension.SaveAs(file, 0, 1, null, ref errors, ref warnings) || errors != 0 || !File.Exists(file))
                throw new Exception("Part save failed: " + errors);
            if (warnings != 0) Console.WriteLine("Save warning: " + warnings);
            Console.WriteLine("Turns: {0}; solid bodies: 1; volume: {1:F2} mm^3", turns, volume * 1e9);
            return file;
        } finally {
            // Close only the document created above, including when a build fails.
            app.CloseDoc(model.GetTitle());
        }
    }
}
'@

[SpringBuilder]::Run($OutputDirectory, $PartTemplate, $Turns, $MeanDiameterMm, $WireDiameterMm, $PitchMm)
