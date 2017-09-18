using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeSaver.Models
{
    public class Edge
    {
        #region Declarations/Accessors
        public Node End1 { get; set; }
        public Node End2 { get; set; }
        public double Distance { get; set; }
        #endregion

        public Node GetOther(Node n1)
        {
            if (End1.Id == n1.Id) return End2;
            if (End2.Id == n1.Id) return End1;

            throw new ApplicationException("Edge does not contain this node: " + n1.Id + " it is: " + End1.Id + "-" + End2.Id);
        }
    }
}
