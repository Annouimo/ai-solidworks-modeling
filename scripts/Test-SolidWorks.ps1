$ErrorActionPreference = 'Stop'
$interop = 'D:\Programs\SolidWorks\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
Add-Type -Path $interop
Add-Type -ReferencedAssemblies $interop -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
public static class SolidWorksProbe {
    public static string Read() {
        ISldWorks app = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
        string revision = app.RevisionNumber();
        IModelDoc2 doc = app.IActiveDoc2;
        return "Connected SOLIDWORKS: " + revision + System.Environment.NewLine +
            "Active document: " + (doc == null ? "(none)" : doc.GetTitle());
    }
}
"@
[SolidWorksProbe]::Read()
