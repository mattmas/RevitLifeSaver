

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LifeSaver.UI
{
    public partial class LifeSafetyResults : System.Windows.Forms.Form, IFeedback
    {
        private Autodesk.Revit.UI.UIApplication _uiApp;
        private Controller _controller;
        private enum ActionEnum { None, Run, Show, GetAllRoutes, DrawRoute, GetRoutes, DrawAll};
        private ActionEnum _action = ActionEnum.None;

        private double _maxTravel = 100.0;
        private double _inchPerOcc = 0.2;

        public LifeSafetyResults(Autodesk.Revit.UI.UIApplication uiApp, Controller c)
        {
            InitializeComponent();
            _controller = c;
            _controller.View = this; // set the view.
            _uiApp = uiApp;

            // register for idling callback.
            _uiApp.Idling += _uiApp_Idling;
            _action = ActionEnum.Run;

        }

        private bool updateMaxTravel()
        {
            double tmp = -1;
            if (Double.TryParse(tbMaxTravel.Text, out tmp))
            {
                tbMaxTravel.BackColor = Color.White;
                _maxTravel = tmp;
                toolTip1.SetToolTip(tbMaxTravel, "Max Travel Distance to check against");
                return true;
            }
            else
            {
                tbMaxTravel.BackColor = Color.HotPink;
                toolTip1.SetToolTip(tbMaxTravel, "Please enter a valid number");
                return false;
            }
        }

        private bool updateClearWidth()
        {
            double tmp = -1;
            if (Double.TryParse(tbClearWidth.Text, out tmp))
            {
                tbClearWidth.BackColor = Color.White;
                _inchPerOcc = tmp;
                toolTip1.SetToolTip(tbClearWidth, "Door Clear Width to check against");
                return true;
            }
            else
            {
                tbClearWidth.BackColor = Color.HotPink;
                toolTip1.SetToolTip(tbClearWidth, "Please enter a valid number");
                return false;
            }
        }

        private void _uiApp_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            ActionEnum tmp = _action;
            _action = ActionEnum.None; // reset;

            switch (tmp)
            {
                case ActionEnum.Run:
                    performRun();
                    break;

                case ActionEnum.DrawRoute:
                    performDrawRoute();
                    break;

                case ActionEnum.Show:
                    performShow();
                    break;
               

                case ActionEnum.GetAllRoutes:
                    performGetAllRoutes();
                    break;

                case ActionEnum.DrawAll:
                    performDrawAll();
                    break;
            }
        }

        public void UpdateStatus(string msg)
        {
            toolStripStatusLabel1.Text = msg;
            Application.DoEvents(); // this is not good!

        }
        private void performGetAllRoutes()
        {
            try
            {
                DateTime start = DateTime.Now;
                var egresses = _controller.GetEgressNodes();
                double total = (double)treeView1.Nodes[0].Nodes.Count;
                UpdateStatus("Calculating Egress Routes to " + egresses.Count + " egress locations...");
                double count = 0;
                double percent = 0.0;
                
                foreach ( TreeNode child in treeView1.Nodes[0].Nodes)
                {
                    percent = count / total * 100.0;
                    count++;
                    UpdateStatus("Calculating Egress Routes to " + egresses.Count + " egress locations...(" + percent.ToString("F0") +"%)");
                    child.Nodes.Clear();
                    Models.Node roomNode = child.Tag as Models.Node;
                    foreach( var egress in egresses )
                    {
                        ExternalApp.Log("PathFinding " + roomNode + " to " + egress);
                        try
                        {

                            Models.Route r = Utilities.GraphUtility.FindShortest(roomNode, egress);
                            if (r != null)
                            {
                                addRouteNode(child, egress, r);
                            }
                        }
                        catch (Exception ex)
                        {
                            ExternalApp.Log("Exception while pathfinding:  " + ex.GetType().Name + ": " + ex.Message);
                            ExternalApp.Log(ex.StackTrace);
                        }
                    }
                    


                }
                reCheckMaxPath();
                reCheckDoorCW();
                TimeSpan analysis = DateTime.Now - start;
                UpdateStatus("Analysis Completed in " + analysis);

            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Error");
                td.MainContent = "Unexpected error: " + ex.GetType().Name + ":  " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();
            }
        }

        private void reCheckDoorCW()
        {
            if (treeView1.Nodes.Count == 0) return;
            UpdateStatus("Checking Door Clear Width");

            List<TreeNode> toRemove = new List<TreeNode>();
            foreach (TreeNode door in treeView1.Nodes[0].Nodes)
            {
                Models.ClearWidth cw = door.Tag as Models.ClearWidth;
                if ((cw != null))
                {
                    toRemove.Add(door);
                }
            }
            foreach (TreeNode tmp in toRemove) tmp.Remove();

            /////////////////////////
            var cws = _controller.CheckClearWidthIssues(getBestRoutes(), _inchPerOcc);

            cws = cws.OrderBy(c => c.IsOK).ToList();

            foreach( var c in cws )
            {
                TreeNode cwTree = treeView1.Nodes[0].Nodes.Add("Door: " + c.Name + ": width: " + (c.Width * 12.0).ToString("F1") + ": required: " + (c.RequiredWidth * 12.0).ToString("F1"));
                cwTree.Tag = c;
                if (c.IsOK)
                {
                    cwTree.BackColor = Color.White;
                }
                else
                {
                    cwTree.BackColor = Color.Orange;
                }
                
            }

        }

        private void reCheckMaxPath()
        {
            if (treeView1.Nodes.Count == 0) return;

            UpdateStatus("Checking max travel...");
            foreach( TreeNode room in treeView1.Nodes[0].Nodes)
            {
                Models.Node roomNode = room.Tag as Models.Node;
                if (roomNode == null) continue;

                // find the minimum
                double min = 9999;
                int childRoutes = 0;
                foreach( TreeNode child in room.Nodes)
                {
                    Models.Route r = child.Tag as Models.Route;
                    if (r != null)
                    {
                        childRoutes++;
                        if (r.TotalDistance < min) min = r.TotalDistance;

                        if (r.TotalDistance > _maxTravel)
                        {
                            child.BackColor = System.Drawing.Color.Yellow;
                        }
                        else
                        {
                            child.BackColor = Color.White;
                        }
                    }
                }

                if ((childRoutes>0 ))
                {
                    room.Text = roomNode.Name + " Max Travel Dist: " + min.ToString("F1");
                    if (min > _maxTravel)
                    {
                        room.BackColor = Color.Orange;
                    }
                    else
                    {
                        room.BackColor = Color.White;
                    }
                    
                }
            }
        }

        //private void updateDoorWidthCalcs()
        //{

        //    //reset:
        //    var best = getBestRoutes();
        //    _controller.CheckClearWidthIssues(best, _inchPerOcc);
        //}
        /// <summary>
        /// retrieve the best route for each room that has a route.
        /// </summary>
        /// <returns></returns>
        private IList<Models.Route> getBestRoutes()
        {
            // so there might be 
            if (treeView1.Nodes.Count == 0) return new List<Models.Route>();

            List<Models.Route> best = new List<Models.Route>();
            foreach ( TreeNode roomNode in treeView1.Nodes[0].Nodes)
            {

                // for every one, get the shortest route
                Models.Route bestRoute = null;
                double shortest = 99999;
                foreach ( TreeNode routeNode in roomNode.Nodes)
                {
                    Models.Route r = routeNode.Tag as Models.Route;
                    if (r != null)
                    {
                        if (r.TotalDistance < shortest)
                        {
                            shortest = r.TotalDistance;
                            bestRoute = r;
                        }
                    }
                    
                }
                if (bestRoute != null) best.Add(bestRoute);
            }

            return best;
        }
        private void addRouteNode(TreeNode parent, Models.Node egress, Models.Route route)
        {
            TreeNode routeNode = parent.Nodes.Add("Route to " + egress.NodeType + ": " + egress.Name);
            routeNode.Tag = route;
            if (route != null)
            {
                routeNode.Text += ": " + route.TotalDistance.ToString("F2") + "ft.";

                //_controller.DrawRoute(route);
                StringBuilder sb = new StringBuilder();
                foreach (var node in route.Nodes) sb.Append(node.Name + " - ");
                routeNode.ToolTipText = sb.ToString();
            }
        }
        private void performDrawAll()
        {
            try
            {
                _controller.DrawAll();
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Error");
                td.MainContent = "Unexpected error: " + ex.GetType().Name + ":  " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();
            }
        }

        private void performShow()
        {
            try
            {
                Autodesk.Revit.DB.ElementId toShow = Autodesk.Revit.DB.ElementId.InvalidElementId;

                Models.ClearWidth cw = treeView1.SelectedNode.Tag as Models.ClearWidth;
                if (cw != null) toShow = cw.Id;

                Models.Node node = treeView1.SelectedNode.Tag as Models.Node;
                if (node != null)
                { toShow = node.RoomId; }

                if (toShow == Autodesk.Revit.DB.ElementId.InvalidElementId) return;
                _controller.Show(toShow);

            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Error");
                td.MainContent = "Unexpected error: " + ex.GetType().Name + ":  " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();
            }
        }

        private void performDrawRoute()
        {
            try
            {
                Models.Route  r = treeView1.SelectedNode.Tag as Models.Route;

                if (r == null) return;

                _controller.DrawRoute(r);
                
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Error");
                td.MainContent = "Unexpected error: " + ex.GetType().Name + ":  " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();
            }
        }
        private void performRun()
        {
            try
            {
                _controller.BuildNetwork();

                var rooms = _controller.GetRoomNodes();
                renderTree(rooms);

                _action = ActionEnum.GetAllRoutes;

                //var ids = _uiApp.ActiveUIDocument.Selection.GetElementIds();

                //if (ids.Count==2)
                //{
                //    Models.Node n1 = _controller.GetNodeByElementId(ids.First());
                //    Models.Node n2 = _controller.GetNodeByElementId(ids.Last());

                //    if ((n1 != null) && (n2 != null))
                //    {
                //        Models.Route r = Utilities.GraphUtility.FindShortest(n1, n2);

                //        MessageBox.Show("Route from: " + n1.Id + " to " + n2.Id + " Route: " + r);

                //    }
                //    else
                //    {
                //        MessageBox.Show("Both nodes were not found!");
                //    }
                //}
               
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("Error");
                td.MainContent = "An unexpected error occurred: " + ex.GetType().Name + ": " + ex.Message;
                td.ExpandedContent = ex.StackTrace;
                td.Show();
            }
        }

        private void renderTree(IList<Models.Node> rooms)
        {
            treeView1.Nodes.Clear();
            TreeNode root = treeView1.Nodes.Add("Rooms: " + rooms.Count);

            foreach( var node in rooms)
            {
                TreeNode roomNode = root.Nodes.Add(node.Name);
                roomNode.Tag = node;
            }
            root.Expand();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void onAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treeView1.SelectedNode.Tag is Models.Route)
            {
                _action = ActionEnum.DrawRoute;
            }
            if (treeView1.SelectedNode.Tag is Models.Node)
            {
                _action = ActionEnum.Show;
            }
            if (treeView1.SelectedNode.Tag is Models.ClearWidth)
            {
                _action = ActionEnum.Show;
            }
        }

        private void btnDrawAll_Click(object sender, EventArgs e)
        {
            _action = ActionEnum.DrawAll;
        }



        private void onMaxDistChange(object sender, EventArgs e)
        {
            if (updateMaxTravel())
            {
                reCheckMaxPath();
            }

        }

        private void onClearWidthChange(object sender, EventArgs e)
        {
            if (updateClearWidth())
            {
                reCheckDoorCW();
            }
        }
    }
}
