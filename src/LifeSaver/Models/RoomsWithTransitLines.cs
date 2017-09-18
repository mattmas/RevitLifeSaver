using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace LifeSaver.Models
{
    public class RoomWithTransitLines
    {
        #region Declarations/Accessors
        public ElementId RoomId { get; set; }
        public ElementId LevelId { get; set; }
        public String Name { get; set; }
        public List<TransitLine> Lines { get; private set; }
        #endregion

        #region Constructor
        public RoomWithTransitLines(Autodesk.Revit.DB.Architecture.Room room)
        {
            RoomId = room.Id;
            LevelId = room.LevelId;
            Name = room.Number + ": " + room.Name;

            Lines = new List<TransitLine>();
        }
        #endregion

        #region PublicMethods
        public XYZ GetNearestProjection(XYZ pt, out TransitLine tl)
        {
            double dist = 9999999.0;
            tl = null;
            XYZ val = null;
            foreach (TransitLine line in Lines)
            {
                IntersectionResult result = line.Curve.Project(pt);

                if (result.Distance < dist)
                {
                    val = result.XYZPoint;
                    dist = result.Distance;
                    tl = line;
                }
            }

            return val;
        }

        public XYZ GetEndPoint()
        {
            return Lines.First().Curve.GetEndPoint(0);
        }
        public Node GetNearestEndPoint(XYZ pt)
        {
            double dist = 99999999;
            Node n = null;
            foreach (TransitLine line in Lines)
            {
                double test = line.End1.Location.DistanceTo(pt);
                if (test < dist)
                {
                    n = line.End1;
                    dist = test;
                }
                test = line.End2.Location.DistanceTo(pt);
                if (test < dist)
                {
                    n = line.End2;
                    dist = test;
                }
            }

            return n; 
        }
        #endregion
    }
}
