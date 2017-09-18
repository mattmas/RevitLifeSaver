using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using LifeSaver.Models;
using Autodesk.Revit.UI;

namespace LifeSaver
{
    public class Controller
    {
        #region Declarations/Accessors
        private List<Node> _allNodes = new List<Node>();
        private Document _doc;
        private View _view;
        private Phase _phase;
        private Dictionary<string, Models.Edge> _edgeDictionary = new Dictionary<string,Models.Edge>();
        private Dictionary<int, Node> _nodeDictionary = new Dictionary<int, Node>();
        private List<Models.Edge> _allEdges = new List<Models.Edge>();

        internal IFeedback View { get; set; }

        public List<Node> AllNodes { get { return _allNodes; } }
        public String EgressParameter { get; set; }
        #endregion

        #region Constructor
        public Controller(Document doc, View v)
        {
            _doc = doc;
            _view = v;

            Parameter phaseParm = v.get_Parameter(BuiltInParameter.VIEW_PHASE);
            if (phaseParm == null) throw new ApplicationException("Invalid View: Must contain phase information!");

            ElementId phaseId = phaseParm.AsElementId();

            if (phaseId.IntegerValue < 0) throw new ApplicationException("Invalid View: Must contain phase information!");

            _phase = _doc.GetElement(phaseId) as Phase;
        }
        #endregion

        #region PublicMethods

        public List<Room> GetRooms()
        {
            return getRooms();
        }

        public List<FamilyInstance> GetDoors()
        {
            FilteredElementCollector coll = new FilteredElementCollector(_doc, _view.Id);
            var allDoors = coll.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Doors).ToElements().Cast<FamilyInstance>().ToList();

            return allDoors;
            
        }

        public IList<Node> GetRoomNodes()
        {
            return _allNodes.Where(n => n.NodeType == Node.NodeTypeEnum.Room).ToList();

        }

        public IList<Node> GetEgressNodes()
        {
            return _allNodes.Where(n => n.NodeType == Node.NodeTypeEnum.Door).Cast<ConnectionNode>().Where(c => c.IsEgress).Cast<Node>().ToList();
        }

        public List<FamilyInstance> GetEgressDoors(string egressParam)
        {
            var doors = GetDoors();
            List<FamilyInstance> egress = new List<FamilyInstance>();
            foreach( var door in doors )
            {
                Parameter p = door.GetParameters(egressParam).FirstOrDefault();

                if (p != null)
                {
                    if (p.AsInteger() == 1) egress.Add(door);
                }
            }

            return egress;
        }

        public void DrawAll()
        {
            createLines(_allEdges);
             
        }

        internal void Show(ElementId id)
        {
            UIDocument uiDoc = new UIDocument(_doc);
            uiDoc.ShowElements(new ElementId[] { id});
        }

        internal void DrawRoute(Route r)
        {
            Transaction t = null;
            if (_doc.IsModifiable == false)
            {
                t = new Transaction(_doc, "Draw Route");
                t.Start();
            }
            Utilities.GeometryUtility.DrawLines(_doc, r.Nodes.Select(n => n.Location).ToList());
            
            if (t != null)
            {
                t.Commit();
            }
        }
        internal IList<ClearWidth> CheckClearWidthIssues(IList<Route> routes, double inchesPerOcc)
        {

            // go through the routes, building up the requirements via Occupancy Load.
            Dictionary<ElementId, ClearWidth> dict = new Dictionary<ElementId, ClearWidth>();
            foreach( var route in routes )
            {
                int runningTotal = 0;
                var backwards = route.Nodes.Reverse<Node>().ToList();
                foreach( var node in backwards ) // start at the room
                {
                    if (node.NodeType == Node.NodeTypeEnum.Room)
                    {
                        // what is the occupancy load?
                        Element e = _doc.GetElement(node.RoomId);
                        Parameter occ = e.GetParameters("Occupancy Load").FirstOrDefault();
                        if (occ == null) throw new ApplicationException("The 'occupancy load' parameter is not defined!");

                        int val = occ.AsInteger();
                        runningTotal = runningTotal + val;
                    }
                    if (node.NodeType == Node.NodeTypeEnum.Door)
                    {
                        // see if we've seen it before.
                        if (dict.ContainsKey(node.RevitId) == false)
                        {
                            var cw = new ClearWidth() { Id = node.RevitId, Name = node.Name };
                            Element inst = _doc.GetElement(node.RevitId);
                            Element typ = _doc.GetElement(inst.GetTypeId());

                            Parameter typParam = typ.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                            if (typParam != null)
                            {
                                cw.Width = typParam.AsDouble();
                            }
                            else
                            {
                                // check for instance door width, just in case.
                                Parameter instParam = inst.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                                if (instParam != null)
                                {
                                    cw.Width = instParam.AsDouble();
                                }
                                else
                                {
                                    throw new ApplicationException("The door family: " + typ.Name + " has no width parameter found?");
                                }
                            }
                            dict[node.RevitId] = cw;
                        }
                        // add the stuff to the total.
                        dict[node.RevitId].OccupancyLoad += (double)runningTotal;
                    }
                }

            }

            // ok, at the end of this, let's see how we did?

            foreach( var pair in dict )
            {
                pair.Value.RequiredWidth = inchesPerOcc / 12.0 * pair.Value.OccupancyLoad;
            }

            return dict.Values.ToList();


        }
        public int BuildNetwork()
        {
            log("Retrieving Rooms");
            List<Room> rooms = getRooms();

            if ((rooms == null) || (rooms.Count == 0)) return 0;  // no rooms

            Dictionary<ElementId, Models.RoomWithTransitLines> transitRooms = getRoomsWithTransitLines();

            log("Determining Room Nodes");
            // we need to find all of the connection between rooms, as determined by doors and room boundary lines.
            List<Node> roomNodes = getRoomNodes(rooms, transitRooms);

            log("Retrieving related doors");
            List<ConnectionNode> relatedDoors = getRelatedDoors(rooms, transitRooms);
            List<ConnectionNode> relatedRoomSep = getRoomSeparations(rooms, transitRooms);

            List<ConnectionNode> allConnections = new List<ConnectionNode>();
            allConnections.AddRange(relatedDoors);
            allConnections.AddRange(relatedRoomSep);

            //temp cleanup.
            foreach( var n in allConnections)
            {
                if (_allNodes.Count(a => a.Id == n.Id) > 0) continue;
                _allNodes.Add(n);
            }
            //_allNodes.AddRange(allConnections);
           

            foreach (Node room in roomNodes)
             {
                List<ConnectionNode> relevantDoors =
                    allConnections.Where(d => (d.RoomId == room.RevitId) || (d.Room2 == room.RevitId)).ToList();
                addNode(room);

                foreach (ConnectionNode node in relevantDoors)
                {
                    addNode(node);
                    makeEdge(room, node);

                }
            }

            // now we need to go through any transit rooms and connect those to any doors/separation lines...
            foreach (KeyValuePair<ElementId, RoomWithTransitLines> pair in transitRooms)
            {
                List<ConnectionNode> relevantDoors =
                    allConnections.Where(d => (d.RoomId == pair.Key) || (d.Room2 == pair.Key)).ToList();

                foreach (ConnectionNode node in relevantDoors)
                {
                    // connect the node to the nearest way to attach to a transit line...
                    Node connectNode = null;
                    TransitLine tl = null;
                    XYZ projection = pair.Value.GetNearestProjection(node.Location, out tl);

                    if (projection == null)
                    {
                        // get the nearest endpoint
                        connectNode = pair.Value.GetNearestEndPoint(node.Location);
                        // see if the node already exists.
                        var existing = lookupByXYZ(connectNode.Location);
                        if (existing != null) connectNode = existing;

                    }
                    else
                    {
                        // make a new node for the projection
                        connectNode = new Node() { LevelId = node.LevelId, Location = projection, NodeType = Node.NodeTypeEnum.TransitPoint, RoomId = pair.Key };
                        // see if the node already exists.
                        var existing = lookupByXYZ(connectNode.Location);
                        if (existing != null) connectNode = existing;

                        

                    }

                    addNode(connectNode);
                    makeEdge(connectNode, node);

                    // we need to add edges to the two nodes on the ends of the line.
                    if (tl != null)
                    {
                        if (connectNode.Location.DistanceTo(tl.End1.Location) > _doc.Application.ShortCurveTolerance) makeEdge(connectNode, tl.End1);
                        if (connectNode.Location.DistanceTo(tl.End2.Location) > _doc.Application.ShortCurveTolerance) makeEdge(connectNode, tl.End2);
                    }

                }
            }

            // final processing of rooms with transit lines.
            processRoomsWithTransit(transitRooms);

            drawAllConnections(allConnections.Cast<Node>().ToList());
            //createLines(_allEdges);
            return _allEdges.Count;
            
        }

        private void processRoomsWithTransit(Dictionary<ElementId, RoomWithTransitLines> transitRooms)
        {
            var transitPoints = _allNodes.Where(n => n.NodeType == Node.NodeTypeEnum.TransitPoint).ToList();


            int k = 0;
            foreach( var pair in transitRooms)
            {
                foreach (var curve in pair.Value.Lines)
                {
                    if (k == 0)
                    {
                        // the first point in the list should be converted to a room point.
                        Node node = lookupByXYZ(curve.Curve.GetEndPoint(0));
                        if (node != null)
                        {
                            if (node.NodeType == Node.NodeTypeEnum.TransitPoint)
                            {
                                node.NodeType = Node.NodeTypeEnum.Room;
                                node.Name = pair.Value.Name;
                            }
                        }
                        else
                        {
                            addNode(new Node() { NodeType = Node.NodeTypeEnum.Room, LevelId = pair.Value.LevelId, Location = curve.Curve.GetEndPoint(0), RevitId = pair.Key, RoomId = pair.Key, Name = pair.Value.Name });
                        }
                    }
                    k++;

                    XYZ p1 = curve.Curve.GetEndPoint(0);
                    XYZ p2 = curve.Curve.GetEndPoint(1);

                    // now we need to make sure that there are edges between each thing, and in particular we need to look for transit points that are
                    List<Tuple<double,Node>> intermediates = new List<Tuple<double,Node>>();
                    foreach ( var pt in transitPoints.Where( p => p.RoomId == pair.Key))
                    {
                        var intRes = curve.Curve.Project(pt.Location);
                        if (intRes != null)
                        {
                            if (intRes.XYZPoint.DistanceTo(pt.Location) < 0.01)
                            {

                                // not too close to the end.
                                if ((p1.DistanceTo(pt.Location) > 0.1) && (p2.DistanceTo(pt.Location) > 0.1))
                                {
                                    // this is on the line.
                                    double normParam = curve.Curve.ComputeNormalizedParameter(intRes.Parameter);
                                    intermediates.Add(new Tuple<double, Node>(normParam, pt));
                                }
                            }
                        }
                    }
                    intermediates.Add(new Tuple<double, Node>(0.0, curve.End1));
                    intermediates.Add(new Tuple<double, Node>(1.0, curve.End2));

                    
                    intermediates = intermediates.OrderBy(n => n.Item1).ToList();

                    // create an edge.
                    for (int i=1; i< intermediates.Count;i++)
                    {
                        makeEdge(intermediates[i - 1].Item2, intermediates[i].Item2);
                    }
                    
                }
            }
        }
        private void drawAllConnections(IList<Node> nodes)
        {
            Transaction t = null;
            if (_doc.IsModifiable == false)
            {
                t = new Transaction(_doc, "Make circles");
                t.Start();
            }
            foreach (var node in nodes)
            {
                Plane p = Plane.CreateByNormalAndOrigin(_doc.ActiveView.ViewDirection, node.Location);
                Utilities.GeometryUtility.DrawCircle(_doc, p, _doc.ActiveView.SketchPlane, 0.5);
            }

            if (t != null) t.Commit();
        }

        private Node lookupByXYZ(XYZ location)
        {
            foreach( Node n1 in _allNodes)
            {
                if (n1.Location.DistanceTo(location) < 0.001) return n1;
            }
            return null;
        }

        private void addNode(Node n1)
        {
            if (_nodeDictionary.ContainsKey(n1.Id)) return;
            _nodeDictionary.Add(n1.Id, n1);
            _allNodes.Add(n1);
        }
        internal Route FindPath(Node n1, Node n2)
        {
            return Utilities.GraphUtility.FindShortest(n1, n2);
        }

        internal Node GetNodeByElementId(ElementId id)
        {
            return _allNodes.FirstOrDefault(n => n.RevitId == id);

        }
        internal void GetRoomEgress(Room r)
        {
            var doors = this.getRelatedDoors(new List<Room>() { r }, new Dictionary<ElementId, RoomWithTransitLines>());

            var doorNode = doors.First();
            FamilyInstance fi = _doc.GetElement(doorNode.RevitId) as FamilyInstance;

            this.calcuateRouteOppositeDoor(r, fi);

        }
       
        #endregion

        #region PrivateMethods

        private void log(string msg, bool isPublic = true)
        {
            if (isPublic && (View != null)) View.UpdateStatus(msg);
            _doc.Application.WriteJournalComment(msg, false);
        }

        private IList<XYZ> calcuateRouteOppositeDoor(Room r, FamilyInstance door)
        {
            // given a room and a door into that room,
            // 1. Find the corner furthest away from the door.
            // 2. Find the longer route, along the boundary from that spot to the door

            IList<XYZ> points = null;
            LocationPoint lp = door.Location as LocationPoint;
            if (lp == null) throw new ApplicationException("Unable to figure location of door: " + door.Id.IntegerValue);

            XYZ doorLocation = lp.Point;

            // NOTE: pre-determined that this is a room that has area, boundaries, etc.
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions() { SpatialElementBoundaryLocation =  SpatialElementBoundaryLocation.Finish };
            foreach( var segmentList in r.GetBoundarySegments(options))
            {

                XYZ maxPoint = null;
                double furthest = -1;
                double dist = -1;
                Curve nearestToDoor = null;
                double nearestDist = 9999;
                XYZ doorProjection = null;

                for( int i=0; i< segmentList.Count; i++)
                {
                    var segment = segmentList[i];
                    var crv = segment.GetCurve();
                    XYZ p1 = crv.GetEndPoint(0);
                    XYZ p2 = crv.GetEndPoint(1);

                    dist = doorLocation.DistanceTo(p1);
                    if (dist > furthest)
                    {
                        maxPoint = p1;
                        furthest = dist;
                    }
                    dist = doorLocation.DistanceTo(p2);
                    if (dist > furthest)
                    {
                        maxPoint = p2;
                        furthest = dist;
                    }

                    // test if this curve is the nearest to the door
                    var intResult = crv.Project(doorLocation);
                    if (intResult != null)
                    {
                        if (intResult.Distance < nearestDist)
                        {
                            nearestToDoor = crv;
                            doorProjection = intResult.XYZPoint;
                            nearestDist = intResult.Distance;
                        }
                    }

                }

                Transaction t = new Transaction(_doc);
                t.Start("Make Results");

                // now figure out which direction around was shorter.
                points = 
                    Utilities.BoundaryUtility.getLongerDistanceAround(_doc, doorProjection, maxPoint, nearestToDoor, segmentList);


               
                
                Utilities.GeometryUtility.DrawLines(_doc, points);
                t.Commit();

                break; // we only want the outer loop.

            }

            return points;

        }


        private void makeEdge(Node n1, Node n2)
        {
            
            string tmp1 = n1.Id + "-" + n2.Id;
            string tmp2 = n2.Id + "-" + n1.Id;
            if (_edgeDictionary.ContainsKey(tmp1)) return; // already exists
            if (_edgeDictionary.ContainsKey(tmp2)) return;

            LifeSaver.Models.Edge edge =
                new Models.Edge() { End1 = n1, End2 = n2 };
            edge.Distance = n1.Location.DistanceTo(n2.Location);

            _edgeDictionary.Add(tmp1, edge);
            _edgeDictionary.Add(tmp2, edge);
            n1.Neighbors.Add(edge);
            n2.Neighbors.Add(edge);

            _allEdges.Add(edge);
        }
        private List<Room> getRooms()
        {

            FilteredElementCollector coll = null;
            if (_view != null)
            {
                coll = new FilteredElementCollector(_doc, _view.Id);
            }
            else
            {
                coll = new FilteredElementCollector(_doc);
            }
            coll.OfCategory(BuiltInCategory.OST_Rooms);

            List<Room> rooms = coll.Cast<Room>().ToList();
            if (_view == null)
            {
                // then look at the phase...
                rooms = rooms.Where(r => r.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == _phase.Id).ToList();
            }

            return rooms;
        }

        private List<Node> getRoomNodes(List<Room> rooms, Dictionary<ElementId,RoomWithTransitLines> transitRooms)
        {
            List<Node> nodes = new List<Node>();

            foreach (Room room in rooms)
            {
                if (room.Area <= 0) continue;

                if (transitRooms.ContainsKey(room.Id)) continue; // don't make a room node if there are transit lines in it.

                Node roomNode = new Node()
                {
                    RevitId = room.Id,
                    LevelId = room.LevelId,
                    NodeType = Node.NodeTypeEnum.Room,
                    RoomId = room.Id,
                    Location = (room.Location as LocationPoint).Point,
                    Name = room.Number + "-" + room.Name
                };
                nodes.Add(roomNode);
            }

            //foreach( var pair in transitRooms)
            //{
            //    Node roomNode = new Node()
            //    {
            //        RevitId = pair.Key,
            //        LevelId = pair.Value.LevelId,
            //        NodeType = Node.NodeTypeEnum.Room,
            //        RoomId = pair.Key,
            //        Name = pair.Value.Name
            //    };
            //    roomNode.Location = pair.Value.GetEndPoint(); // arbitrary.
            //    nodes.Add(roomNode);
            //}

            return nodes;
        }

        private List<ConnectionNode> getRelatedDoors(List<Room> rooms, Dictionary<ElementId, Models.RoomWithTransitLines> transitRooms)
        {
            // get the doors (either by view or in general)
            List<ConnectionNode> doorNodes = new List<ConnectionNode>();
            FilteredElementCollector coll = null;
            if (_view != null)
            {
                coll = new FilteredElementCollector(_doc, _view.Id);
            }
            else
            {
                coll = new FilteredElementCollector(_doc);
            }

            coll.OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType();

            List<FamilyInstance> doors = coll.Cast<FamilyInstance>().ToList();

            // now, for the phase that's appropriate, figure out what rooms they come to/from.
            if (doors.Count == 0) return doorNodes;

            ElementId phaseId = rooms[0].get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId();
            Phase phase = _doc.GetElement(phaseId) as Phase;

            // get the list of room elements we care about
            List<ElementId> roomIds = rooms.Select(s => s.Id).ToList();
            foreach (var pair in transitRooms) roomIds.Add(pair.Key);

            foreach (FamilyInstance door in doors)
            {
                ElementOnPhaseStatus status = door.GetPhaseStatus(phaseId);
                switch (status)
                {
                    case ElementOnPhaseStatus.Existing:
                    case ElementOnPhaseStatus.New:
                        // do nothing.
                        break;

                    default:
                        continue; // ignore this
                }
                Room from = door.get_FromRoom(phase);
                Room to = door.get_ToRoom(phase);

                if (
                    ((from != null) && roomIds.Contains(from.Id))
                    ||
                    ((to != null) && roomIds.Contains(to.Id))
                    )
                {

                    // we have a room we care about!
                    LocationPoint lp = (door.Location as LocationPoint);
                    XYZ location = null;
                    if ((lp != null) && (lp.Point != null))
                    {
                        location = lp.Point;
                    }
                    else
                    {
                        // fallback!
                        BoundingBoxXYZ box = door.get_BoundingBox(null);
                        Level lev = _doc.GetElement(door.LevelId) as Level;

                        location = new XYZ((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0,
                                             (lev != null) ? (lev.ProjectElevation) : (box.Min.Z));
                    }
                    string mark = "";
                    Parameter markParam = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                    if (markParam != null) mark = markParam.AsString();

                    ConnectionNode node = new ConnectionNode()
                    {
                        ConnectionType = ConnectionNode.ConnectionTypeEnum.Door,
                        NodeType = Node.NodeTypeEnum.Door,
                        RevitId = door.Id,
                        LevelId = door.LevelId,
                        Location = location,
                        Name = mark
                    };
                    if (from != null) node.RoomId = from.Id;
                    if (to != null) node.Room2 = to.Id;
                    if ((from == null) && (to != null)) node.RoomId = to.Id;

                    Parameter egress = door.GetParameters(EgressParameter).FirstOrDefault();
                    if ((egress != null) && (egress.AsInteger() == 1)) node.IsEgress = true;

                    doorNodes.Add(node);
                }

                
            }
            return doorNodes;
        }

        private Dictionary<ElementId, RoomWithTransitLines> getRoomsWithTransitLines()
        {
            Dictionary<ElementId, RoomWithTransitLines> rooms = new Dictionary<ElementId,RoomWithTransitLines>();
            FilteredElementCollector coll = null;
            if (_view != null)
            {
                coll = new FilteredElementCollector(_doc);
            }
            else
            {
                coll = new FilteredElementCollector(_doc, _view.Id);
            }

            coll.OfCategory(BuiltInCategory.OST_Lines).OfClass(typeof(CurveElement)).WhereElementIsNotElementType();

            List<CurveElement> curves = coll.Cast<CurveElement>().Where(c => c.CurveElementType == CurveElementType.ModelCurve).ToList();

            curves = curves.Where(c => (c.LineStyle != null) && (c.LineStyle.Name.ToUpper() == "TRANSITLINE")).ToList();

            if (curves.Count == 0) return rooms;

            
            foreach (ModelCurve mc in curves)
            {
                XYZ end1 = mc.GeometryCurve.GetEndPoint(0);
                XYZ end2 = mc.GeometryCurve.GetEndPoint(1);

                Room r1 = _doc.GetRoomAtPoint(end1, _phase);
                Room r2 = _doc.GetRoomAtPoint(end2, _phase);

                if ((r1 == null) || (r2 == null)) throw new AnalysisException("The Transit Line Id: " + mc.Id.IntegerValue + " is not entirely in a room in phase " + _phase.Name) { Element = mc.Id };

                if ((r1.Id != r2.Id)) throw new AnalysisException("The Transit Line Id:  " + mc.Id.IntegerValue + " is not entirely in the same room in phase " + _phase.Name) { Element = mc.Id };

                // see if it already exists or not...
                if (rooms.ContainsKey(r1.Id) == false)
                {
                    rooms.Add(r1.Id, new RoomWithTransitLines(r1));
                }

                Node node1 = new Node()
                    {
                        LevelId = r1.Id,
                        Location = end1,
                        NodeType = Node.NodeTypeEnum.TransitPoint,
                        RevitId = mc.Id,
                        RoomId = r1.Id
                    };
                Node node2 = new Node()
                    {
                        LevelId = r2.Id,
                        Location = end2,
                        NodeType = Node.NodeTypeEnum.TransitPoint,
                        RevitId = mc.Id,
                        RoomId = r2.Id
                    };
                addNode(node1);
                addNode(node2);

                rooms[r1.Id].Lines.Add(new TransitLine()
                {
                    Curve = mc.GeometryCurve,
                    RevitId = mc.Id,
                    RoomId = r1.Id,
                    End1 = node1,
                    End2 = node2
                });

                // make an edge as well...

                Models.Edge edge = new Models.Edge() { End1 = node1, End2 = node2 };

                _edgeDictionary.Add(node1.Id + "-" + node2.Id, edge);
                _edgeDictionary.Add(node2.Id + "-" + node1.Id, edge);
                _allEdges.Add(edge);

            }

            return rooms;
        }
        private List<ConnectionNode> getRoomSeparations(List<Room> rooms, Dictionary<ElementId,RoomWithTransitLines> transitRooms)
        {
            List<ConnectionNode> nodes = new List<ConnectionNode>();
            SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions() { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center };
            Dictionary<ElementId, ElementId> separationRooms = new Dictionary<ElementId, ElementId>();

            foreach (Room room in rooms)
            {
                if (room.Area <= 0) continue;

                addRoomSepLineFromBoundary(room, nodes, options);
            }
            foreach( var pair in transitRooms)
            {
                // we also need to capture these...
                Room r = _doc.GetElement(pair.Key) as Room;
                addRoomSepLineFromBoundary(r, nodes, options);
            }

            return nodes;
        }

        private void addRoomSepLineFromBoundary(Room room, IList<ConnectionNode> nodes, SpatialElementBoundaryOptions options)
        {
            IList<IList<Autodesk.Revit.DB.BoundarySegment>> outline = room.GetBoundarySegments(options);

            foreach (IList<Autodesk.Revit.DB.BoundarySegment> segments in outline)
            {
                foreach (Autodesk.Revit.DB.BoundarySegment segment in segments)
                {
                    Element segElem = _doc.GetElement(segment.ElementId);

                    if (segElem is ModelCurve)
                    {

                        ElementId otherRoomId = ElementId.InvalidElementId;
                        XYZ location = segment.GetCurve().Evaluate(0.5, true);
                        Room r2 = getOtherRoom(room, segment, location);
                        if (r2 != null) otherRoomId = r2.Id;

                        // see if it already exists.
                        var existing = nodes.FirstOrDefault(n => n.NodeType == Node.NodeTypeEnum.RoomBoundary && (n.Location.DistanceTo(location) < 0.01));

                        if (existing == null)
                        {
                            // we have a connection between rooms
                            ConnectionNode node = new ConnectionNode()
                            {
                                ConnectionType = ConnectionNode.ConnectionTypeEnum.RoomBoundary,
                                NodeType = Node.NodeTypeEnum.RoomBoundary,
                                LevelId = room.LevelId,
                                RoomId = room.Id,
                                Room2 = otherRoomId,
                                RevitId = segment.ElementId,
                                Location = segment.GetCurve().Evaluate(0.5, true)
                            };
                            nodes.Add(node);
                        }


                    }
                }
            }
        }

        private Room getOtherRoom( Room r1, BoundarySegment seg, XYZ point)
        {
            // we already know what one of the rooms is attached to a boundary segment.
            // this attempts to find the other.
            Curve crv = seg.GetCurve();

            XYZ vector = crv.GetEndPoint(1).Subtract(crv.GetEndPoint(0)).Normalize();

            XYZ otherVector = XYZ.BasisZ;
            if (vector.IsAlmostEqualTo(XYZ.BasisZ)) otherVector = XYZ.BasisY;
            XYZ perp = vector.CrossProduct(otherVector).Normalize();

            XYZ testp1 = point.Add(perp.Multiply(0.25));
            XYZ testp2 = point.Add(perp.Negate().Multiply(0.25));
            Parameter phaseParm = r1.get_Parameter(BuiltInParameter.ROOM_PHASE);
            Phase p = _doc.GetElement(phaseParm.AsElementId()) as Phase;

            Room roomP1 = _doc.GetRoomAtPoint(testp1, p);
            Room roomP2 = _doc.GetRoomAtPoint(testp2, p);

            if ((roomP1 != null) && (roomP1.Id != r1.Id)) return roomP1;
            if ((roomP2 != null) && (roomP2.Id != r1.Id)) return roomP2;

            return null;
        }
        private void createLines(List<Models.Edge> edges)
        {
            // now we want to visualize the edges...
            Transaction t = new Transaction(_doc, "Show Edges");
            t.Start();

         
            Plane sketch = _view.SketchPlane.GetPlane();
            
            foreach (Models.Edge edge in edges)
            {
                // sanity check the Z coordinate...
                XYZ p1 = edge.End1.Location;
                XYZ p2 = edge.End2.Location;

                if ((p1.Z != p2.Z) || (p1.Z != sketch.Origin.Z))
                {
                    p1 = new XYZ(p1.X, p1.Y, sketch.Origin.Z);
                    p2 = new XYZ(p2.X, p2.Y, sketch.Origin.Z);
                }
                _doc.Create.NewModelCurve(Line.CreateBound(p1,p2), _view.SketchPlane);
            }
            t.Commit();
        }
        #endregion
    }
}
