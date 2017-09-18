using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeSaver.Models
{
    internal class Route
    {
        internal IList<Node> Nodes { get; set; }
        internal double TotalDistance { get; set; }

        internal Route()
        {
            Nodes = new List<Node>();
        }

        public override string ToString()
        {
            return "Nodes: " + Nodes.Count + " Total Distance: " + TotalDistance;
        }
    }
}
