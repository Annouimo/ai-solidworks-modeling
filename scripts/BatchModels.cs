using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

public static class BatchModels {
 static ISldWorks app;
 static IModelDoc2 model;
 static string root, log;
 static void Note(string s) { Console.WriteLine(s); File.AppendAllText(log,s+System.Environment.NewLine,Encoding.UTF8); }
 static double[,] P(params double[] values) { double[,] r=new double[values.Length/2,2]; for(int i=0;i<values.Length/2;i++){r[i,0]=values[2*i];r[i,1]=values[2*i+1];}return r; }
 // Coordinates used in this script: X width, Y depth (rear to front), H height.
 // SolidWorks coordinates are (X,H,Y), in metres.
 static double[,] Section(string axis,double offset,double[,] polygon) {
  double[,] r=new double[polygon.GetLength(0),3];
  for(int i=0;i<polygon.GetLength(0);i++) {
   double a=polygon[i,0],b=polygon[i,1];
   if(axis=="x"){r[i,0]=offset;r[i,1]=b;r[i,2]=a;} // polygon Y,H
   if(axis=="y"){r[i,0]=a;r[i,1]=b;r[i,2]=offset;} // polygon X,H
   if(axis=="h"){r[i,0]=a;r[i,1]=offset;r[i,2]=b;} // polygon X,Y
  }return r;
 }
 static IFeature Sketch(double[,] points) {
  model.ClearSelection2(true);
  ISketchManager sm=model.SketchManager;
  sm.AddToDB=false; sm.Insert3DSketch(true); ISketch sk=sm.ActiveSketch; sm.AddToDB=true;
  for(int i=0;i<points.GetLength(0);i++) {
   int j=(i+1)%points.GetLength(0);
   if(sm.CreateLine(points[i,0]/1000,points[i,1]/1000,points[i,2]/1000,points[j,0]/1000,points[j,1]/1000,points[j,2]/1000)==null)throw new Exception("Failed to create line");
  }
  sm.AddToDB=false; sm.Insert3DSketch(true);
  return (IFeature)sk;
 }
 static void Blend(double[,] first,double[,] last,bool cut) {
  Note("  feature "+(cut?"cut":"add")+" points="+first.GetLength(0));
  IFeature a=Sketch(first),b=Sketch(last);
  model.ClearSelection2(true);
  if(!a.Select2(false,1)||!b.Select2(true,1))throw new Exception("Profile selection failed");
  IFeature f=null;
  {
   // Explicit vertex guides prevent automatic profile rotation on symmetric outlines.
   List<IFeature> guides=new List<IFeature>();
   for(int i=0;i<first.GetLength(0);i++) {
    model.ClearSelection2(true); ISketchManager sm=model.SketchManager;
    sm.Insert3DSketch(true);ISketch gs=sm.ActiveSketch;sm.AddToDB=true;
    sm.CreateLine(first[i,0]/1000,first[i,1]/1000,first[i,2]/1000,last[i,0]/1000,last[i,1]/1000,last[i,2]/1000);
    sm.AddToDB=false;sm.Insert3DSketch(true);guides.Add((IFeature)gs);
   }
   model.ClearSelection2(true);a.Select2(false,1);b.Select2(true,1);
   foreach(IFeature g in guides)g.Select2(true,2);
   f=cut ? (IFeature)model.FeatureManager.InsertCutBlend(false,false,false,1,0,0,false,0,0,0,true,true) :
    (IFeature)model.FeatureManager.InsertProtrusionBlend2(false,false,false,1,0,0,1,1,false,false,false,0,0,0,true,true,true,0);
  }
  if(f==null)throw new Exception(cut?"Cut loft failed":"Solid loft failed");
  int error=f.GetErrorCode();
  if(error!=0)throw new Exception("Feature error: "+error);
  model.ClearSelection2(true);
 }
 static void Prism(string axis,double begin,double end,double[,] polygon,bool cut=false) {Blend(Section(axis,begin,polygon),Section(axis,end,polygon),cut);}
 static void Box(double x1,double x2,double y1,double y2,double h1,double h2,bool cut=false) {Prism("h",h1,h2,P(x1,y1,x2,y1,x2,y2,x1,y2),cut);}
 static void SlopingTop(double[,] plan,Func<double,double,double> height) {
  double[,] bottom=Section("h",0,plan),top=Section("h",0,plan);
  for(int i=0;i<plan.GetLength(0);i++)top[i,1]=height(plan[i,0],plan[i,1]);
  Blend(bottom,top,false);
 }
 static void Page2() {
  Prism("x",0,100,P(0,0,0,80,18,80,18,20,60,20,60,0));
  Prism("x",82,100,P(18,20,18,70,60,40,60,20));
 }
 static void Page3A() {
  Prism("h",0,80,P(0,0,100,0,100,60,75,60,0,20));
  Box(-5,105,40,70,36,100,true);
 }
 static void Page3B() {
  Prism("x",0,100,P(0,0,0,80,28,80,60,0));
  Box(33,67,-5,70,36,100,true);
 }
 static void Page3C() {
  Box(0,100,0,60,0,16);
  Prism("y",0,60,P(0,16,30,16,30,41.6));
  Prism("y",20,40,P(0,16,100,16,100,80,75,80));
 }
 static void Page4() {
  Blend(Section("y",0,P(0,0,100,0,100,80,36,80,26,64,0,64)),Section("y",60,P(0,0,100,0,100,80,80,80,50,32,0,32)),false);
 }
 static void Page5() {
  Prism("h",0,40,P(0,0,20,0,20,20,50.1,20,50.1,40,20,40,20,60,0,60));
  Prism("h",0,80,P(50,20,80,20,80,0,100,0,100,60,80,60,80,40,50,40));
 } static void Page6() {
  Prism("h",0,54,P(0,0,100,0,100,60,66,60,0,20));
  Box(-10,110,36,70,18,36,true);
 }
 static void Page7() {
  double[] heights={80,60,36};
  for(int i=0;i<3;i++) {
   double h=heights[i],x=(h-24)*66/56;
   Prism("y",i*20,(i+1)*20,P(0,0,100,0,100,h,x,h,0,24));
  }
 }
 static void Page8() {
  SlopingTop(P(0,0,50,0,50,60,25,60,0,30),(x,y)=>40+0.8*x);
  Box(50,100,0,60,0,80);
 }
 static void Page9() {
  Prism("y",0,60,P(0,0,100,0,75,60,25,60));
  Prism("x",-10,110,P(0,-10,0,60,30,0,30,-10),true);
  Prism("x",-10,110,P(30,-10,30,0,60,60,60,-10),true);
 }
 static void Page11() {
  Box(0,100,0,80,0,18);
  Prism("h",18,80,P(0,0,100,0,100,70,0,20));
  Box(28,72,-10,90,46,100,true);
 }
 static void Page12() {
  SlopingTop(P(0,24,50,0,62,0,62,80,50,80,0,56),(x,y)=>40+40*x/62);
  Box(62,100,0,80,0,80);
  Box(-10,110,24,56,58,100,true);
 }
 static void Page13() {
  Prism("x",0,100,P(0,0,0,80,28,80,70,28,70,0));
  Box(0,16,54,80,-10,100,true);
  Box(84,100,54,80,-10,100,true);
  Box(34,66,-10,80,54,100,true);
 }
 static void Page14() {
  Box(0,100,0,70,0,20);
  Prism("y",0,70,P(30,20,100,20,100,80,70,80));
  Prism("x",-10,110,P(30,80,70,36,80,25,80,110,30,110),true);
  Box(0,46,0,30,20,46);
 }
 static void Page15() {
  Box(0,100,0,70,0,24);
  Prism("y",0,70,P(30,24,100,24,100,80,68,80));
  Prism("h",-10,100,P(-5,27.333333333,30,18,30,52,-5,42.666666667),true);
  Prism("x",-10,110,P(26,58,44,58,52,80,58,110,12,110,18,80),true);
 }
 static void Page16() {
  double[,] profile=P(0,0,100,0,100,40,75,40,75,80,25,80,25,40,0,40);
  double[,] a=Section("y",0,profile),b=Section("y",50,profile);
  for(int i=0;i<profile.GetLength(0);i++){a[i,2]+=profile[i,0]*0.25;b[i,2]+=profile[i,0]*0.25;}
  Blend(a,b,false);
 }
 static void Page17() {
  Blend(Section("y",0,P(0,0,100,0,100,80,48,80,28,24,0,24)),Section("y",70,P(0,0,100,0,100,80,83,80,63,24,0,24)),false);
  Prism("h",-10,100,P(-5,27.333333333,18,15,18,60,-5,44.666666667),true);
 }
 static void Page18() {
  Prism("h",0,80,P(0,20,27,0,73,0,100,20,100,50,73,70,27,70,0,50));
  Prism("y",-10,80,P(-10,47.111111,27,80,27,110,-10,110),true);
  Prism("y",-10,80,P(73,80,110,47.111111,110,110,73,110),true);
  Prism("x",-10,110,P(0,80,20,56,50,56,70,80,70,110,0,110),true);
  Box(34,66,15,55,-10,110,true);
  Prism("x",-10,110,P(-10,-10,80,-10,80,27.2,-10,-3.4),true);
 }
 static void Page19() {
  Page18();
  Prism("y",-10,80,P(-10,-19.12,110,69.08,110,120,-10,120),true);
 }
 static void Build(int id) {
  switch(id){
   case 1:Page2();break;case 2:Page3A();break;case 3:Page3B();break;case 4:Page3C();break;
   case 5:Page4();break;case 6:Page5();break;case 7:Page6();break;case 8:Page7();break;
   case 9:Page8();break;case 10:Page9();break;case 11:Page6();break;case 12:Page11();break;
   case 13:Page12();break;case 14:Page13();break;case 15:Page14();break;case 16:Page15();break;
   case 17:Page16();break;case 18:Page17();break;case 19:Page18();break;case 20:Page19();break;
   default:throw new Exception("Unknown model");
  }
 }
 static int Page(int id){return id==1?2:(id<=4?3:id-1);}
 static double Volume() {return ((IMassProperty)model.Extension.CreateMassProperty()).Volume*1e9;}
 static void Verify() {
  model.ForceRebuild3(false);
  object[] bodies=(object[])((IPartDoc)model).GetBodies2(0,false);
  if(bodies==null||bodies.Length!=1)throw new Exception("Expected one solid body; got "+(bodies==null?0:bodies.Length));
  if(Volume()<=0)throw new Exception("Invalid solid volume");
  IFeature f=(IFeature)model.FirstFeature();
  while(f!=null){int error=f.GetErrorCode();if(error!=0)throw new Exception("Rebuild feature error "+f.Name+": "+error);f=(IFeature)f.GetNextFeature();}
 }
 public static string Run(string project,string ids) {
  CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
  root=Path.Combine(project,"deliverables");
  foreach(string d in new string[]{"SolidWorks","STEP","previews","checks"})Directory.CreateDirectory(Path.Combine(root,d));
  log=Path.Combine(root,"checks","batch.log");
  app=(ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
  List<int> work=new List<int>();
  if(String.IsNullOrEmpty(ids)){for(int i=1;i<=20;i++)work.Add(i);}else foreach(string s in ids.Split(','))work.Add(int.Parse(s));
  List<string> failures=new List<string>();
  foreach(int id in work) {
   string name="\u96f6\u4ef6"+id;
   string title=null;
   try {
    Note("START "+id+" page "+Page(id));
    model=(IModelDoc2)app.NewPart();if(model==null)throw new Exception("NewPart failed");model.SetTitle2("API_BUILD_"+Guid.NewGuid().ToString("N"));title=model.GetTitle();
    Build(id);Verify();
    double vol=Volume();
    string part=Path.Combine(root,"SolidWorks",name+".SLDPRT"),step=Path.Combine(root,"STEP",name+".STEP");
    int e=0,w=0;
    if(!model.Extension.SaveAs(part,0,1,null,ref e,ref w)||e!=0)throw new Exception("Part save failed: "+e);
    title=model.GetTitle();
    Note("PART "+id+" volume_mm3="+vol+" save_warning="+w);
    int[] views={1,5,4,7};
    foreach(int v in views){model.ShowNamedView2("",v);model.ViewZoomtofit2();if(!model.SaveBMP(Path.Combine(root,"previews",name+"_"+v+".bmp"),800,600))throw new Exception("Preview failed "+v);}
    int activationError=0;app.ActivateDoc3(part,false,0,ref activationError);
    if(activationError!=0)Note("ACTIVATION "+activationError);
    e=0;w=0;
    if(!model.Extension.SaveAs(step,0,1,null,ref e,ref w)||e!=0)throw new Exception("STEP export failed: "+e);
    Note("STEP "+id+" warning="+w);
    // Reopen the neutral export to check that it contains one solid and preserves volume.
    IModelDoc2 original=model;
    string verifyPath=Path.Combine(root,"checks","verify_"+id+"_"+Guid.NewGuid().ToString("N")+".STEP");
    File.Copy(step,verifyPath);
    int ie=0;IModelDoc2 imported=(IModelDoc2)app.LoadFile4(verifyPath,"",null,ref ie);
    if(imported==null)throw new Exception("STEP verification import failed: "+ie);
    imported.SetTitle2("STEP_CHECK_"+Guid.NewGuid().ToString("N"));
    try {
     object[] ib=(object[])((IPartDoc)imported).GetBodies2(0,false);
     double iv=((IMassProperty)imported.Extension.CreateMassProperty()).Volume*1e9;
     if(ib==null||ib.Length!=1||Math.Abs(iv-vol)>Math.Max(0.1,vol*1e-5))throw new Exception("STEP validation mismatch: "+iv);
    }finally{app.CloseDoc(imported.GetTitle());model=original;File.Delete(verifyPath);}
    File.WriteAllText(Path.Combine(root,"checks",name+".txt"),"id="+id+"\npage="+Page(id)+"\nsolid_bodies=1\nvolume_mm3="+vol+"\nstep_roundtrip=passed\n",Encoding.UTF8);
    Note("OK "+id);
   }catch(Exception ex){failures.Add(id+": "+ex.Message);Note("FAILED "+id+": "+ex.ToString());}
   finally{if(model!=null){try{string closeName=model.GetPathName();app.CloseDoc(String.IsNullOrEmpty(closeName)?model.GetTitle():closeName);}catch{}model=null;}}
  }
  return failures.Count==0?"All requested models passed.":"Failures: "+String.Join("; ",failures.ToArray());
 }
}
