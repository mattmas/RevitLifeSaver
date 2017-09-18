using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace LifeSaver.Models
{
    public class Node
    {
        #region Declarations/Accessors
        private static int COUNTER = 0;
        public enum NodeTypeEnum { Room, Door, RoomBoundary, TransitPoint };

        public int Id { get; private set; }
        public String Name { get; set; }

        public NodeTypeEnum NodeType { get; set; }
        public ElementId RevitId { get; set; }
        public ElementId RoomId { get; set; }
        public XYZ Location { get; set; }
        public ElementId LevelId { get; set; }

        
        
        public List<Edge> Neighbors { get; private set; }
        #endregion

        #region Constructor
        public Node()
        {
            COUNTER++;
            Id = COUNTER;
            Neighbors = new List<Edge>();
        }
        #endregion

        public override string ToString()
        {
            return NodeType + ": " + Id + ": " + Name;
        }
    }
}
