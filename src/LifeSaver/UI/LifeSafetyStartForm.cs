using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LifeSaver
{
    public partial class LifeSafetyStartForm : Form
    {
        private Controller _controller;
        public LifeSafetyStartForm(Controller c)
        {
            InitializeComponent();
            _controller = c;
            updateInfo();

        }

        private void updateInfo()
        {
            try
            {
                lblRooms.Text = "# of Rooms: " + _controller.GetRooms().Count;
                lblDoors.Text = "# of Doors: " + _controller.GetDoors().Count;
                lblEgress.Text = "# of Egress: " + _controller.GetEgressDoors("Egress Door").Count;
            }
            catch (Exception ex)
            {
                TaskDialog td = new TaskDialog("Error");
                td.MainContent = "An unexpected error occurred: " + ex.GetType().Name + ": " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();

            }
        }

        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            //validate
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
