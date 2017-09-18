using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace LifeSaver.Utilities
{
    public class GeometryUtility
    {
        public static List<Element> DrawLines(Document doc, IEnumerable<Line> lines)
        {
            List<Element> modelLines = new List<Element>();

            SubTransaction st = new SubTransaction(doc);
            st.Start();

            XYZ lastNormal = new XYZ(999, 992, 200); // random

            Plane p = null;
            SketchPlane sp = null;
            bool useLines = false;

            FilteredElementCollector coll = new FilteredElementCollector(doc);
            var fs = coll.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_DetailComponents).Cast<FamilySymbol>().Where(f => f.Name.ToUpper().Replace(" ","") == "EGRESSPATH").FirstOrDefault();

            if (fs == null)
            {
                //ugly to do this here.
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Egress Path");
                td.MainContent = "The 'EgressPath' Line-based detail family is not loaded. Model Lines will be shown instead.";
                td.Show();
                useLines = true;
            }


            foreach (Line ln in lines)
            {
                
                if (ln.Length < (1.0 / 24.0 / 12.0)) continue; // too short for Revit!
                // see what the plane is
                XYZ vector = ln.Direction;
                XYZ normal = null;
                if (vector.Normalize().IsAlmostEqualTo(XYZ.BasisZ) == false)
                {
                    normal = vector.CrossProduct(XYZ.BasisZ);
                }
                else
                {
                    normal = vector.CrossProduct(XYZ.BasisX);
                }

                if (lastNormal.IsAlmostEqualTo(normal) == false)
                {
                    p = Plane.CreateByNormalAndOrigin(normal, ln.GetEndPoint(0));
                    sp = SketchPlane.Create(doc, p);
                    normal = lastNormal;
                }

                if (!useLines)
                {
                    if (fs.IsActive == false) fs.Activate();
                    FamilyInstance fi = doc.Create.NewFamilyInstance(ln, fs, doc.ActiveView);
                    modelLines.Add(fi);
                 }
                else
                {

                    ModelCurve curve = doc.Create.NewModelCurve(ln, sp);
                    modelLines.Add(curve as ModelLine);
                }
            }

            st.Commit();

            return modelLines;
        }

        public static List<Element> DrawLines(Document doc, IList<XYZ> points)
        {
            List<Line> lines = new List<Line>();
         
            for (int i=1;i<points.Count;i++)
            {
                if (points[i].DistanceTo(points[i - 1]) < doc.Application.ShortCurveTolerance) continue;
                lines.Add(Line.CreateBound(points[i], points[i - 1]));
            }

           

           return DrawLines(doc, lines);
        }

        public static ModelCurve DrawCircle(Document doc, Plane p, SketchPlane sp, double radius)
        {
            SubTransaction st = new SubTransaction(doc);
            st.Start();

            
            Curve circle = Arc.Create(p, radius, 0, Math.PI * 2.0 - 0.5);
            
            ModelCurve curve = doc.Create.NewModelCurve(circle, sp);

            st.Commit();
            return curve as ModelLine;

        }

        

    }
}
