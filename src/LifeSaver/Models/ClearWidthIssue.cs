using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeSaver.Models
{
    public class ClearWidth
    {
        public String Name { get; set; }
        public double Width { get; set; }
        public ElementId Id { get; set; }

        public double OccupancyLoad { get; set; }
        public double RequiredWidth { get; set; }

        public bool IsOK {  get { return Width >= RequiredWidth; } }

    }
}
