using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace LifeSaver.Models
{
    public class ConnectionNode: Node
    {
        #region Declarations/Accessors
        public enum ConnectionTypeEnum { Door, RoomBoundary }

        public ConnectionTypeEnum ConnectionType { get; set; }
        public ElementId Room2 { get; set; }

        public Boolean IsEgress { get; set; }
        #endregion

        #region Constructor
        public ConnectionNode()
            : base()
        {
            IsEgress = false;
        }
        #endregion

        public override string ToString()
        {
            return base.ToString() + (IsEgress ? "(EGRESS)":"");
        }
    }
}
