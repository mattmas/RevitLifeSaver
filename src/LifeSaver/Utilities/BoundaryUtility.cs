using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace LifeSaver.Utilities
{
    internal static class BoundaryUtility
    {
        private static double TOLERANCE = 0.0001;

        // get the longer distance around a list of boundary segments (return the point XYZs).
        internal static IList<XYZ> getLongerDistanceAround( Document doc, XYZ from, XYZ to, Curve fromCurve, IList<BoundarySegment> segments )
        {
            // from: a point on the curves to start from (can be in the middle)
            // to: point which is a vertex.
            // fromCurve: curve which the from is located on.


            
            XYZ lastPoint = null;
            List<XYZ> allPoints = new List<XYZ>();

            for (int i=0; i<segments.Count; i++)
            {
                var crv = segments[i].GetCurve();
                Curve toAdd = crv;
                
                 
                // goes from p1 to p2
                XYZ p1 = crv.GetEndPoint(0);
                XYZ p2 = crv.GetEndPoint(1);

                var crvPoints = crv.Tessellate();
                bool inverted = false;
                // special cases.
                if (lastPoint != null)
                {
                    if (lastPoint.DistanceTo(p2) < lastPoint.DistanceTo(p1))
                    {
                        inverted = true;
                        // in this case, swap p1 and p2.
                        XYZ tmp = p1;
                        p1 = p2;
                        p2 = tmp;
                        crvPoints = crvPoints.Reverse<XYZ>().ToList();
                    }
                   
                }

               

                // special case, where it is the curve that we know the point is on.
                if (crv.Evaluate(0.5,true).DistanceTo(fromCurve.Evaluate(0.5,true)) < TOLERANCE)
                {
                    crvPoints = new List<XYZ>();
                    crvPoints.Add(p1);
                    if ((p1.DistanceTo(from) > 0) && (p2.DistanceTo(from)>0)) crvPoints.Add(from);
                    crvPoints.Add(p2);
                    if (inverted) crvPoints = crvPoints.Reverse<XYZ>().ToList();
                }

                
                for (int j=0;j<crvPoints.Count;j++) // skip p1, because it should be the same as the last p2 (if there is one).
                {
                    if ((j == 0) && (!(lastPoint == null))) continue;
                    allPoints.Add(crvPoints[j]);
                }

                lastPoint = crvPoints.Last();
            }

            
            // now we have the ordered list of all points.

            double distForward = 0;
            // forward.
            bool keepGoing = true;
            int k = 0;
            int fromIndex = -1;
            List<XYZ> forwardPoints = new List<XYZ>();
            while (keepGoing)
            {
                // ugh, inefficient.
                if (allPoints[k].DistanceTo(from) < TOLERANCE)
                {
                    // this is the from point.
                    fromIndex = k;
                    forwardPoints.Add(allPoints[k]);
                }

                if ((fromIndex >= 0)&&(fromIndex!=k)) // we've already got the from.
                {
                    int prevPoint = k - 1;
                    if (k <= 0) prevPoint = allPoints.Count - 1;

                    distForward += allPoints[k].DistanceTo(allPoints[prevPoint]);
                    forwardPoints.Add(allPoints[k]);

                    // if it's the endpoint.

                    if (allPoints[k].DistanceTo(to) < TOLERANCE)
                    {
                        keepGoing = false;
                        break;
                    }
                }

                if (k == (allPoints.Count-1))
                {
                    k = 0; // reset
                }
                else
                {
                    k++;
                }
               
            }

            // now go backwards.
            allPoints.Reverse();
            double distBack = 0;
            // forward.
            keepGoing = true;
            k = 0;
            fromIndex = -1;
            List<XYZ> backwardPoints = new List<XYZ>();
            while (keepGoing)
            {
                // ugh, inefficient.
                if (allPoints[k].DistanceTo(from) < TOLERANCE)
                {
                    // this is the from point.
                    fromIndex = k;
                    backwardPoints.Add(allPoints[k]);
                }

                if ((fromIndex >= 0) && (fromIndex != k)) // we've already got the from.
                {
                    int prevPoint = k - 1;
                    if (k <= 0) prevPoint = allPoints.Count - 1;

                    distBack += allPoints[k].DistanceTo(allPoints[prevPoint]);
                    backwardPoints.Add(allPoints[k]);

                    // if it's the endpoint.

                    if (allPoints[k].DistanceTo(to) < TOLERANCE)
                    {
                        keepGoing = false;
                        break;
                    }
                }

                if (k == (allPoints.Count - 1))
                {
                    k = 0; // reset
                }
                else
                {
                    k++;
                }

            }

            // now, return the longer of the two.

            IList<XYZ> returnPoints = (distForward > distBack) ? forwardPoints : backwardPoints;

            return returnPoints;
        }
    }
}
