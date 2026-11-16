$ErrorActionPreference='Stop'
$interop='D:\Programs\SolidWorks\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
Add-Type -Path $interop
Add-Type -ReferencedAssemblies $interop -TypeDefinition @"
using System;using System.IO;using System.Text;using System.Globalization;using System.Runtime.InteropServices;using SolidWorks.Interop.sldworks;
public static class NativeAudit {
public static string Run(string root){
ISldWorks app=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");StringBuilder report=new StringBuilder("id,solid_bodies,volume_mm3,features,status\n");
for(int id=1;id<=20;id++){
string name="\u96f6\u4ef6"+id;
string path=Path.Combine(root,"SolidWorks",name+".SLDPRT");
string temp=Path.Combine(root,"checks","native_check_"+id+"_"+Guid.NewGuid().ToString("N")+".SLDPRT");File.Copy(path,temp);
IModelDoc2 m=null;
try{int e=0,w=0;m=(IModelDoc2)app.OpenDoc6(temp,1,3,"",ref e,ref w);if(m==null)throw new Exception("Open failed "+id+": "+e);
object[] bodies=(object[])((IPartDoc)m).GetBodies2(0,false);if(bodies==null||bodies.Length!=1)throw new Exception("Body count "+id);
double v=((IMassProperty)m.Extension.CreateMassProperty()).Volume*1e9;
string check=File.ReadAllText(Path.Combine(root,"checks",name+".txt"));double expected=0;
foreach(string line in check.Split('\n'))if(line.StartsWith("volume_mm3="))expected=double.Parse(line.Substring(11),CultureInfo.InvariantCulture);
if(Math.Abs(v-expected)>Math.Max(.1,expected*1e-5))throw new Exception("Saved volume mismatch "+id+": "+v+" vs "+expected);
int count=0;IFeature f=(IFeature)m.FirstFeature();while(f!=null){if(f.GetErrorCode()!=0)throw new Exception("Feature error "+id);string t=f.GetTypeName2();if(t.Contains("Blend")||t.Contains("Loft"))count++;f=(IFeature)f.GetNextFeature();}
report.AppendLine(id+",1,"+v.ToString(CultureInfo.InvariantCulture)+","+count+",passed");Console.WriteLine("Saved native verified "+id);
}finally{if(m!=null)app.CloseDoc(temp);File.Delete(temp);}
}
File.WriteAllText(Path.Combine(root,"checks","native-audit.csv"),report.ToString(),Encoding.UTF8);return "20 saved native parts verified.";
}
}
"@
[NativeAudit]::Run((Join-Path (Split-Path -Parent $PSScriptRoot) 'deliverables'))
