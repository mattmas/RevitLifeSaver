using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LifeSaver.Models;

namespace LifeSaver.Utilities
{
    internal static class GraphUtility
    {
        internal static Route FindShortest(Node from, Node to)
        {
            Stack<Node> path = new Stack<Node>();
            path.Push(from);

            double total = 0;
            bool success = recurse(from, to, path, ref total);

            if (success)
            {
                Route r = new Route() { TotalDistance = total };

                r.Nodes = path.ToList();
                
                return r;
            }

            return null;
            
        }

        private static bool recurse(Node current, Node to, Stack<Node> visited, ref double dist)
        {
            List<Tuple<double, Edge>> closest = new List<Tuple<double, Edge>>();
            foreach( var neighborEdge in current.Neighbors)
            {
                var neighbor = neighborEdge.GetOther(current);
                if (visited.Contains(neighbor)) continue; // doesn't count!

                if (neighbor.Id == to.Id) // success
                {
                    dist = dist + neighborEdge.Distance;
                    visited.Push(to);
                    return true; // found!
                }
                double distToTarget = neighbor.Location.DistanceTo(to.Location);
                closest.Add(new Tuple<double, Edge>(distToTarget, neighborEdge));
            }

            closest = closest.OrderBy(n => n.Item1).ToList();

            // go to each neighbor, in order.
            foreach( var tuple in closest )
            {
                var neighbor = tuple.Item2.GetOther(current);
                dist += tuple.Item2.Distance;
                visited.Push(neighbor);
                if (recurse(neighbor, to, visited, ref dist))
                {
                    // found it.
                    return true;
                }
                else
                {
                    // didn't find it.
                    visited.Pop(); // pop this one off.
                    dist -= tuple.Item2.Distance;
                }
            }

            // if we got here, no joy.
            return false;
        }
    }
}
