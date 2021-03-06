﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LifeSaver.Utilities
{
    public static class Utility
    {
        public static IntPtr GetMainWindowHandle()
        {

            System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess();
            return p.MainWindowHandle;
        }

        /// <summary>
        /// Validate that the given path is acceptable
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsValidPath(string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);

                if (info != null) return true;
            }
            catch (Exception)
            {

            }
            return false;
        }

        public static bool CanWriteToFolder(string folder)
        {
            try
            {
                string file = System.IO.Path.Combine(folder, "Test" + DateTime.Now.Ticks.ToString() + ".log");
                System.IO.File.WriteAllText(file, "This is a test. Should be deleted.");
                System.Threading.Thread.Sleep(100);
                System.IO.File.Delete(file);
                return true;
            }
            catch
            {

            }
            return false;
        }

        internal static void GetExtents(Autodesk.Revit.UI.UIApplication uiApp, out int x, out int y)
        {

#if REVIT2016 || REVIT2015
            try
            {
                x = uiApp.DrawingAreaExtents.Left;
                y = uiApp.DrawingAreaExtents.Top;
            }
            catch
            {
                x = 10;
                y = 10;
            }
#else
            //2017+
            try
            {
                x = uiApp.DrawingAreaExtents.Left;
                y = uiApp.DrawingAreaExtents.Top;
            }
            catch
            {
                x = 10;
                y = 10;
            }
#endif
        }
    }
}
