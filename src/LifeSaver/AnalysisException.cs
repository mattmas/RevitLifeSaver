using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace LifeSaver
{
    [Serializable]
    public class AnalysisException : Exception
    {
        public ElementId Element { get; set; }

        public AnalysisException() { }
        public AnalysisException(string message) : base(message) { }
        public AnalysisException(string message, Exception inner) : base(message, inner) { }
        protected AnalysisException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
