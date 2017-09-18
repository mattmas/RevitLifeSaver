using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using LifeSaver.Utilities;

namespace LifeSaver
{
    [Transaction(TransactionMode.Manual), Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDoc = commandData.Application.ActiveUIDocument;
                Controller c = new Controller(commandData.Application.ActiveUIDocument.Document,
                                              commandData.Application.ActiveUIDocument.ActiveGraphicalView);

                c.EgressParameter = "Egress Door";

                //// pick a room.
                //Reference r = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element);

                //Element e = uiDoc.Document.GetElement(r);
                //Autodesk.Revit.DB.Architecture.Room room = e as Autodesk.Revit.DB.Architecture.Room;

                //c.GetRoomEgress(room);

                LifeSafetyStartForm start = new LifeSafetyStartForm(c);
                if (start.ShowDialog() != System.Windows.Forms.DialogResult.OK) return Result.Cancelled;


                UI.LifeSafetyResults res = new UI.LifeSafetyResults(commandData.Application, c);
               int x, y;
                Utility.GetExtents(commandData.Application, out x, out y);
                res.Location = new System.Drawing.Point(x, y);


                IntPtr currentRevitWin = Utility.GetMainWindowHandle();
                if (currentRevitWin != null)
                {
                    Utilities.WindowHandle handle = new Utilities.WindowHandle(currentRevitWin);

                    res.Show(handle);
                }
                else
                {
                    res.Show();
                }


                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog td = new TaskDialog("Error");
                td.MainContent = ex.GetType().Name + ": " + ex.Message;
                td.ExpandedContent = ex.StackTrace;

                td.Show();
            }
            return Result.Failed;
        }
    }
}
