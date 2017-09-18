using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace LifeSaver.Models
{
    public class TransitLine
    {
        #region Declarations/Accessors
        public ElementId RevitId { get; set; }
        public Node End1 { get; set; }
        public Node End2 { get; set; }
        public ElementId RoomId { get; set; }
        public Curve Curve { get; set; }

        #endregion
    }
}
