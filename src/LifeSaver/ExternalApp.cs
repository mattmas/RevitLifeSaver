using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace LifeSaver
{
    public class ExternalApp : IExternalApplication
    {
        private static UIControlledApplication _App;
        private static bool _Started = false;

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _App = application;
                buildUI(application);
                return Result.Succeeded;
            }
            catch (Exception eX)
            {
                TaskDialog td = new TaskDialog("Error in Setup");
                td.ExpandedContent = eX.GetType().Name + ": " + eX.Message + Environment.NewLine + eX.StackTrace;
                td.Show();
                return Result.Failed;
            }
        }

        public static string GetUserInfo()
        {
            // make a reasonably unique identifier - but pretty anonymous. This is for analytics tracking.
            return (Environment.UserDomainName + "\\" + Environment.UserName).GetHashCode().ToString();
        }

        public static void FirstTimeRun()
        {
            if (_Started) return;
            //otherwise, record the fact that we started, for analytics purposes.
            startup();
        }

        public static void Log(string msg)
        {
            _App.ControlledApplication.WriteJournalComment(msg, false);
        }

        private static void startup()
        {
            _App.ControlledApplication.WriteJournalComment("Starting up Revit LifeSaver...", false);
            _Started = true;
        }

        public static bool AnalyticsOptIn()
        {

            //NOTE: We do collect anonymized analytics with our official binary build (# of times each feature was launched).
            // we do this just to have a sense of whether anyone is using this application.
            // if you would still like to opt out of this, please create an "optout.txt" file in LifeSaver folder.
            // if you have concerns about the analytics, and would like to see the analytical information we collect, please reach out to:
            // mmason (at) rand.com

            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string optOutFile = System.IO.Path.Combine(path, "optout.txt");

            return !System.IO.File.Exists(optOutFile);
        }

        private void buildUI(UIControlledApplication app)
        {
            var panel = app.CreateRibbonPanel(Tab.AddIns, "LifeSaver" + Environment.NewLine + "Life Safety");

            var save = new PushButtonData("LifeSaver", "LifeSaver", System.Reflection.Assembly.GetExecutingAssembly().Location, "LifeSaver.Command");
            save.ToolTip = "Analyze the model for life safety issues";
            save.LongDescription = "Analyze the current model to look at life safety issues";
            save.LargeImage = getImage("LifeSaver.Images.lifesaver-32.png");
            save.Image = getImage("LifeSaver.Images.lifesaver-16.png");

            panel.AddItem(save);

        }

        private System.Windows.Media.ImageSource getImage(string imageFile)
        {
            try
            {
                System.IO.Stream stream = this.GetType().Assembly.GetManifestResourceStream(imageFile);
                if (stream == null) return null;
                PngBitmapDecoder pngDecoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                return pngDecoder.Frames[0];

            }
            catch
            {
                return null; // no image


            }
        }
    }
}
