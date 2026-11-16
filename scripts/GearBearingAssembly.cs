using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

public static class GearBearingBuilder {
    // Millimetres; all parts share a Z axis normal to the first standard reference plane.
    const double ShaftRadius = 6, ShoulderRadius = 8.5, BoreRadius = 6.025;
    const double BearingEnd = 16, GearStart = 22, GearWidth = 12, ShaftLength = 44;
    const int Teeth = 24;
    const double Module = 2;

    static double M(double mm) { return mm / 1000; }

    static ISldWorks Connect() {
        const string progId = "SldWorks.Application";
        try { return (ISldWorks)Marshal.GetActiveObject(progId); }
        catch (COMException ex) {
            if (ex.ErrorCode != unchecked((int)0x800401E3)) throw;
            return (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID(progId, true));
        }
    }

    static void Circle(ISketchManager sketch, double r) {
        if (sketch.CreateCircleByRadius(0, 0, 0, M(r)) == null) throw new Exception("Circle creation failed.");
    }

    static void SketchBoss(IModelDoc2 part, double start, double length, Action<ISketchManager> draw) {
        IFeature plane = (IFeature)part.FirstFeature();
        while (plane != null && plane.GetTypeName2() != "RefPlane") plane = (IFeature)plane.GetNextFeature();
        part.ClearSelection2(true);
        if (plane == null || !plane.Select2(false, 0)) throw new Exception("Could not select the part reference plane.");
        ISketchManager sketch = part.SketchManager;
        sketch.InsertSketch(true);
        IFeature profile = (IFeature)sketch.ActiveSketch;
        sketch.AddToDB = true;
        try { draw(sketch); } finally { sketch.AddToDB = false; }
        sketch.InsertSketch(true);
        part.ClearSelection2(true);
        if (!profile.Select2(false, 0)) throw new Exception("Could not select the boss profile.");
        IFeature boss = part.FeatureManager.FeatureExtrusion2(true, false, false, 0, 0, M(length), 0,
            false, false, false, false, 0, 0, false, false, false, false, true, true, true,
            start == 0 ? 0 : 3, M(start), false);
        part.ClearSelection2(true);
        if (boss == null || boss.GetErrorCode() != 0) throw new Exception("Boss creation failed: start " + start);
    }

    static void Point(List<double[]> points, double radius, double angle) {
        points.Add(new[] { M(radius * Math.Cos(angle)), M(radius * Math.Sin(angle)) });
    }

    static double Involute(double a) { return Math.Tan(a) - a; }

    static void GearProfile(ISketchManager sketch) {
        // Standard spur gear proportions, with sampled straight segments and radial root transitions.
        double pressure = 20 * Math.PI / 180, step = 2 * Math.PI / Teeth;
        double pitchR = Teeth * Module / 2, baseR = pitchR * Math.Cos(pressure);
        double rootR = pitchR - 1.25 * Module, tipR = pitchR + Module;
        double rootHalf = step / 4 + Involute(pressure);
        double tipHalf = rootHalf - Involute(Math.Acos(baseR / tipR));
        List<double[]> points = new List<double[]>();
        const int samples = 6;
        for (int tooth = 0; tooth < Teeth; tooth++) {
            double center = tooth * step;
            Point(points, rootR, center - rootHalf);
            for (int j = 0; j <= samples; j++) {
                double r = baseR + (tipR - baseR) * j / samples;
                Point(points, r, center - rootHalf + Involute(Math.Acos(baseR / r)));
            }
            Point(points, tipR, center);
            for (int j = samples; j >= 0; j--) {
                double r = baseR + (tipR - baseR) * j / samples;
                Point(points, r, center + rootHalf - Involute(Math.Acos(baseR / r)));
            }
            Point(points, rootR, center + rootHalf);
            Point(points, rootR, center + step / 2);
        }
        if (tipHalf <= 0 || rootR >= baseR) throw new Exception("Invalid tooth proportions.");
        for (int i = 0; i < points.Count; i++) {
            double[] a = points[i], b = points[(i + 1) % points.Count];
            if (sketch.CreateLine(a[0], a[1], 0, b[0], b[1], 0) == null)
                throw new Exception("Tooth outline creation failed.");
        }
        Circle(sketch, ShaftRadius);
    }

    static void Save(IModelDoc2 doc, string path) {
        int errors = 0, warnings = 0;
        if (!doc.Extension.SaveAs(path, 0, 1, null, ref errors, ref warnings) || errors != 0 || !File.Exists(path))
            throw new Exception("Save failed (" + errors + "): " + path);
        if (warnings != 0) Console.WriteLine("Save warning {0}: {1}", warnings, path);
    }

    static string MakePart(ISldWorks app, string template, string path, Action<IModelDoc2> build, List<IModelDoc2> opened) {
        IModelDoc2 part = String.IsNullOrEmpty(template) ?
            (IModelDoc2)app.NewPart() : (IModelDoc2)app.NewDocument(template, 0, 0, 0);
        if (part == null) throw new Exception("Could not create part. Check -PartTemplate.");
        opened.Add(part);
        build(part);
        if (!part.ForceRebuild3(false)) throw new Exception("Part rebuild failed: " + path);
        object[] bodies = (object[])((IPartDoc)part).GetBodies2(0, false);
        if (bodies == null || bodies.Length != 1 || !(((IMassProperty)part.Extension.CreateMassProperty()).Volume > 0))
            throw new Exception("Expected one solid body: " + path);
        part.ShowNamedView2("", 7);
        part.ViewZoomtofit2();
        Save(part, path);
        return path;
    }

    static IEntity Face(IComponent2 component, double value, bool cylindrical) {
        IPartDoc part = (IPartDoc)component.GetModelDoc2();
        object[] bodies = (object[])part.GetBodies2(0, false);
        foreach (object item in (object[])((IBody2)bodies[0]).GetFaces()) {
            IFace2 face = (IFace2)item;
            ISurface surface = (ISurface)face.GetSurface();
            if (cylindrical) {
                if (!surface.IsCylinder()) continue;
                double[] cylinder = (double[])surface.CylinderParams;
                if (Math.Abs(cylinder[6] - M(value)) > 1e-7) continue;
            } else {
                if (!surface.IsPlane()) continue;
                double[] box = (double[])face.GetBox();
                if (Math.Abs(box[2] - box[5]) > 1e-7 || Math.Abs(Math.Abs(box[2]) - M(value)) > 1e-7)
                    continue;
            }
            IEntity entity = (IEntity)component.GetCorrespondingEntity(face);
            if (entity == null) throw new Exception("Could not map part face into assembly.");
            return entity;
        }
        throw new Exception("Missing " + (cylindrical ? "cylinder R" : "axial face at ") + value + ": " + component.Name2);
    }

    static void Mate(IModelDoc2 doc, IAssemblyDoc assembly, IEntity first, IEntity second, bool concentric, bool lockRotation, string name) {
        doc.ClearSelection2(true);
        if (!first.Select4(false, null) || !second.Select4(true, null))
            throw new Exception("Could not select both mate faces: " + name);
        int status;
        IMate2 mate = assembly.AddMate5(concentric ? 1 : 0, concentric ? 2 : 1, false, 0, 0, 0,
            0, 0, 0, 0, 0, false, lockRotation, 0, out status);
        doc.ClearSelection2(true);
        if (mate == null || status != 1) throw new Exception("Mate failed (" + status + "): " + name);
        IFeature feature = (IFeature)mate;
        feature.Name = name;
        if (feature.GetErrorCode() != 0) throw new Exception("Mate has a feature error: " + name);
    }

    static IComponent2 Insert(IAssemblyDoc assembly, string file) {
        IComponent2 component = assembly.AddComponent5(file, 0, "", false, "", 0, 0, 0);
        if (component == null) throw new Exception("Component insertion failed: " + file);
        return component;
    }

    public static string Run(string directory, string partTemplate, string assemblyTemplate) {
        if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length > 0)
            directory = Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 5));
        Directory.CreateDirectory(directory);
        ISldWorks app = Connect();
        List<IModelDoc2> opened = new List<IModelDoc2>();
        try {
            string bearingFile = MakePart(app, partTemplate, Path.Combine(directory, "FlangedPlainBearing.SLDPRT"), p => {
                SketchBoss(p, 0, BearingEnd, s => { Circle(s, 14); Circle(s, BoreRadius); });
                SketchBoss(p, 0, 3, s => { Circle(s, 17.5); Circle(s, BoreRadius); });
            }, opened);
            string shaftFile = MakePart(app, partTemplate, Path.Combine(directory, "ShoulderedShaft.SLDPRT"), p => {
                SketchBoss(p, 0, ShaftLength, s => Circle(s, ShaftRadius));
                SketchBoss(p, BearingEnd, GearStart - BearingEnd, s => Circle(s, ShoulderRadius));
            }, opened);
            string gearFile = MakePart(app, partTemplate, Path.Combine(directory, "SpurGear_24T.SLDPRT"), p => {
                SketchBoss(p, GearStart, GearWidth, GearProfile);
            }, opened);

            IModelDoc2 doc = String.IsNullOrEmpty(assemblyTemplate) ?
                (IModelDoc2)app.NewAssembly() : (IModelDoc2)app.NewDocument(assemblyTemplate, 0, 0, 0);
            if (doc == null) throw new Exception("Could not create assembly. Check -AssemblyTemplate.");
            opened.Add(doc);
            IAssemblyDoc assembly = (IAssemblyDoc)doc;
            IComponent2 bearing = Insert(assembly, bearingFile);
            IComponent2 shaft = Insert(assembly, shaftFile);
            IComponent2 gear = Insert(assembly, gearFile);
            if (!bearing.IsFixed() || shaft.IsFixed() || gear.IsFixed())
                throw new Exception("Expected only the bearing to be fixed.");
            IMathUtility math = (IMathUtility)app.GetMathUtility();
            MathTransform origin = (MathTransform)math.CreateTransform(new double[] {
                1,0,0, 0,1,0, 0,0,1, 0,0,0, 1,0,0,0 });
            // Start in the intended orientation, then fully constrain the axial positions with real contact faces.
            shaft.Transform2 = origin;
            gear.Transform2 = origin;
            Mate(doc, assembly, Face(bearing, BoreRadius, true), Face(shaft, ShaftRadius, true), true, false, "Bearing to shaft - free rotation");
            Mate(doc, assembly, Face(bearing, BearingEnd, false), Face(shaft, BearingEnd, false), false, false, "Bearing against shaft shoulder");
            Mate(doc, assembly, Face(shaft, ShaftRadius, true), Face(gear, ShaftRadius, true), true, true, "Gear to shaft - rotation locked");
            Mate(doc, assembly, Face(shaft, GearStart, false), Face(gear, GearStart, false), false, false, "Gear against shaft shoulder");

            if (!doc.ForceRebuild3(false) || assembly.GetComponentCount(true) != 3)
                throw new Exception("Assembly rebuild or component count check failed.");
            double[] anchor = (double[])bearing.Transform2.ArrayData;
            foreach (IComponent2 component in new[] { shaft, gear }) {
                double[] t = (double[])component.Transform2.ArrayData;
                for (int i = 9; i <= 11; i++)
                    if (Math.Abs(t[i] - anchor[i]) > 1e-6) throw new Exception("Unexpected component position: " + component.Name2);
                if (t[8] < 0.999) throw new Exception("Unexpected component orientation: " + component.Name2);
            }
            IInterferenceDetectionMgr manager = assembly.InterferenceDetectionManager;
            manager.TreatCoincidenceAsInterference = false;
            try {
                object[] interferences = (object[])manager.GetInterferences();
                if (interferences != null && interferences.Length != 0)
                    throw new Exception("Unexpected interference count: " + interferences.Length);
            } finally { manager.Done(); }
            doc.ShowNamedView2("", 7);
            doc.ViewZoomtofit2();
            string file = Path.Combine(directory, "GearBearingAssembly.SLDASM");
            Save(doc, file);
            if (!doc.SaveBMP(Path.Combine(directory, "GearBearingAssembly.bmp"), 1200, 900))
                Console.WriteLine("Preview could not be saved.");
            Console.WriteLine("3 components, 4 mates, no solid interferences.");
            return file;
        } finally {
            for (int i = opened.Count - 1; i >= 0; i--) {
                try { app.CloseDoc(opened[i].GetTitle()); } catch (COMException) { }
            }
        }
    }
}
