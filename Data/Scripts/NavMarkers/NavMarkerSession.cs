using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace NavMarkers
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NavMarkerSession : MySessionComponentBase
    {
        public const string Keyword = "/nav";
        public const string ModName = "NavMarkers";
        public const string NavMarkerConfig = "NavMarkers.cfg";

        public static NavMarkerSession Instance { get; private set; }
        public static bool ControlsInit = false;

        public string SessionName = "";
        public string NavMarkerFile = "NavMarkers";
        public bool NavMarkerState = false;
        public bool CloseOnly = false;
        public bool PartialDisplay = false;
        public bool SaveQueued = false;
        public NavMarkerData NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
        public HudAPIv2 HudAPI;


        private bool shouldLog = false;
        private double _closeRange = 100000d;
        private double _lineRange = 175000d;
        private Dictionary<NavMarker, List<Segment>> _navMarkerSegments = new Dictionary<NavMarker, List<Segment>>();

        public override void LoadData()
        {
            if (Tools.IsDedicatedServer)
            {
                MyAPIGateway.Utilities.ShowMessage("NavMarker", "Dedicated Server, not initializing");
                return;
            }
            MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEntered;

            Instance = this;
        }

        public override void BeforeStart()
        {
            SessionName = string.Concat(Session.Name.Split(Path.GetInvalidFileNameChars()));
            if (Tools.IsDedicatedServer)
            {
                MyAPIGateway.Utilities.ShowMessage("NavMarker", "Dedicated Server, not initializing");
                return;
            }

            LoadConfig();
            LoadMarkers();

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
        }

        private void LoadConfig()
        {

        }


        public override void SaveData()
        {
            if (Tools.IsDedicatedServer == false)
            {
                SaveMarkers(true);
            }
        }

        protected override void UnloadData()
        {
            if (Tools.IsDedicatedServer == false)
            {
                SaveMarkers(true);
            }
        }

        private void SaveMarkers(bool writeFile)
        {
            try
            {
                NavMarkers.Enabled = NavMarkerState;
                if (writeFile)
                {
                    TextWriter writer;
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(NavMarkerFile, typeof(NavMarkerData));
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(NavMarkers));
                    writer.Close();
                    Tools.Log($"{ModName} Saved nav markers: {NavMarkerFile}");
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"{ModName}: Failed to save marker data");

            }
        }

        private void LoadMarkers()
        {
            NavMarkerFile = "NavMarkers" + SessionName + ".dat";
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(NavMarkerFile, typeof(NavMarkerData)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(NavMarkerFile, typeof(NavMarkerData));
                    var data = reader.ReadToEnd();
                    reader.Close();
                    NavMarkers = MyAPIGateway.Utilities.SerializeFromXML<NavMarkerData>(data);
                    NavMarkerState = NavMarkers.Enabled;
                    Tools.Log($"{ModName} Loaded nav markers: {NavMarkerFile}");
                }
                else
                {
                    Tools.Log($"{ModName}: No existing nav marker data. Will create new file on first save named: {NavMarkerFile}");
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"{ModName}: Failed to load marker data {ex}");
                MyAPIGateway.Utilities.ShowMessage($"{ModName}", $"Error loading saved info");
            }
        }

        private void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
        {
            var message = messageText.ToLower();
            if (message.Contains(Keyword))
            {
                shouldLog = true;
            }
        }

        private void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            TerminalControls.Create();
            if (block is IMyShipController)
            {
                actions.Add(TerminalControls.NavToggleAction);
                actions.Add(TerminalControls.NavToggleCloseOnlyAction);
                actions.Add(TerminalControls.NavTogglePartialDisplayAction);
            }
        }

        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            TerminalControls.Create();
            if (block is IMyCockpit || block is IMyShipController)
            {
                controls.AddOrInsert(TerminalControls.Separator<IMyShipController>(), 5);
                controls.AddOrInsert(TerminalControls.NavToggleButton, 6);
                controls.AddOrInsert(TerminalControls.NavToggleCloseOnlyButton, 7);
                controls.AddOrInsert(TerminalControls.NavTogglePartialDisplayButton, 8);
                controls.AddOrInsert(TerminalControls.NavListbox, 9);
                controls.AddOrInsert(TerminalControls.NavAddButton, 10);
                controls.AddOrInsert(TerminalControls.NavActiveMarkerListbox, 11);
                controls.AddOrInsert(TerminalControls.NavRemoveButton, 12);
                controls.AddOrInsert(TerminalControls.Separator<IMyShipController>(), 13);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (Tools.IsDedicatedServer)
            {
                return;
            }

            if (NavMarkerState)
            {
                IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
                if (player == null) { return; }

                foreach (NavMarker marker in NavMarkers.Markers.Dictionary.Values)
                {
                    double distance = Vector3D.Distance(player.GetPosition(), marker.Position);
                    double distanceFromEdge = Math.Abs(distance - marker.Radius);
                    double sizeMultiplier = marker.Radius / 100000.0;

                    // Only show if within X km of the border
                    if (CloseOnly && distanceFromEdge > _closeRange) { continue; }

                    Color color = marker.Color;
                    MatrixD matrix = MatrixD.Identity;
                    matrix.Translation = marker.Position;
                    color.A = (byte)60;
                    float radius = (float)marker.Radius / 1000.0f;

                    int wireSegments = Math.Max(2, (int)(marker.Radius * 3 / distanceFromEdge)) * 12;
                    MySimpleObjectRasterizer rasterMode = MySimpleObjectRasterizer.SolidAndWireframe;
                    VRageRender.MyBillboard.BlendTypeEnum blendType = VRageRender.MyBillboard.BlendTypeEnum.Standard;
                    if (distance < marker.Radius)
                    {
                        distanceFromEdge = Math.Abs(marker.Radius - distance);
                        rasterMode = MySimpleObjectRasterizer.Wireframe;
                        wireSegments = radius > 100 ? 36 : 24;
                        blendType = VRageRender.MyBillboard.BlendTypeEnum.AdditiveBottom;
                    }

                    float wireframeWidth = (float)((distanceFromEdge / marker.Radius) * 250f * sizeMultiplier);
                    wireSegments = Math.Min(36, wireSegments);
                    wireframeWidth = Math.Max(radius, Math.Min(150.0f * (float)sizeMultiplier, wireframeWidth));
                    if (shouldLog)
                    {
                        shouldLog = false;
                        MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"segments = {wireSegments}, width= {wireframeWidth}, distanceFromEdge= {distanceFromEdge}, sizeMultiplier= {sizeMultiplier}");
                    }

                    rasterMode = MySimpleObjectRasterizer.Wireframe;
                    blendType = VRageRender.MyBillboard.BlendTypeEnum.AdditiveBottom;

                    if (!PartialDisplay)
                    {
                        //ref matrix, Radius.Value, ref color, MySimpleObjectRasterizer.Solid, 20, null, MyStringId.GetOrCompute("KothTransparency"), 0.12f, -1, null);
                        MySimpleObjectDraw.DrawTransparentSphere(
                            ref matrix,
                            marker.Radius,
                            ref color,
                            rasterMode,
                            wireSegments,
                            MyStringId.GetOrCompute("NavMarkerTransparency"),
                            MyStringId.GetOrCompute("NavMarkerLines"),
                            wireframeWidth,
                            -1,
                            null,
                            blendType,
                            0.5f
                        );
                    }
                    else
                    {
                        // Get all the line segments
                        var material = MyStringId.GetOrCompute("NavMarkerLines");
                        var vectorColor = color.ToVector4();

                        var segments = GetNavMarkerSegments(marker, wireSegments, wireSegments);
                        MyLog.Default.Log(MyLogSeverity.Info, "NavMarker - Found {0} segments", segments.Count);
                        foreach (var segment in segments)
                        {
                            var distanceStart = Vector3D.Distance(player.GetPosition(), segment.Start);
                            var distanceEnd = Vector3D.Distance(player.GetPosition(), segment.End);
                            if (distanceStart > _lineRange && distanceEnd > _lineRange) { continue; } // Don't show segments if far away
                            MySimpleObjectDraw.DrawLine(segment.Start, segment.End, material, ref vectorColor, 200f, blendType); // Thickness might need to be auto set? 200 seems good when ~100km away
                        }
                    }
                }
            }

            if (SaveQueued)
            {
                SaveQueued = false;
                SaveMarkers(true);
            }

        }

        public void AddNavMarker(string name, Vector3D coords, double radius, Color color)
        {
            if (NavMarkers.Markers.Dictionary.ContainsKey(name))
            {
                return;
            }

            NavMarker marker = new NavMarker();
            marker.Name = name;
            marker.Position = coords;
            marker.Radius = (float)radius;
            marker.Color = color;

            NavMarkerSession.Instance.NavMarkers.Markers.Dictionary.Add(name, marker);
            SaveQueued = true;
        }

        public void RemoveNavMarker(string name)
        {
            if (NavMarkers.Markers.Dictionary.ContainsKey(name) == false)
            {
                return;
            }
            NavMarkerSession.Instance.NavMarkers.Markers.Dictionary.Remove(name);
            SaveQueued = true;
        }

        private List<Segment> GetNavMarkerSegments(NavMarker marker, int phiCount, int thetaCount)
        {
            if (_navMarkerSegments.ContainsKey(marker)) { return _navMarkerSegments[marker]; }
            var segments = GetSegments(marker.Position.X, marker.Position.Y, marker.Position.Z, marker.Radius, phiCount, thetaCount);
            _navMarkerSegments.Add(marker, segments);
            return segments;
        }

        private List<Segment> GetSegments(double centerX, double centerY, double centerZ, double radius, int phiCount, int thetaCount)
        {

            var segments = new List<Segment>();

            // Make center lines
            //var minX = centerX - radius;
            //var maxX = centerX + radius;
            //var minY = centerY - radius;
            //var maxY = centerY + radius;
            //var minZ = centerZ - radius;
            //var maxZ = centerZ + radius;
            //segments.Add(new Segment(minX, centerY, centerZ, maxX, centerY, centerZ)); 
            //segments.Add(new Segment(centerX, minY, centerZ, centerX, maxY, centerZ));
            //segments.Add(new Segment(centerX, centerY, minZ, centerX, centerY, maxZ));

            // Make outside lines
            double phi0, theta0;
            double dphi = Math.PI / phiCount;
            double dtheta = 2 * Math.PI / thetaCount;

            phi0 = 0;
            double z0 = radius * Math.Cos(phi0);
            double r0 = radius * Math.Sin(phi0);
            for (int i = 0; i < phiCount; i++)
            {
                double phi1 = phi0 + dphi;
                double z1 = radius * Math.Cos(phi1);
                double r1 = radius * Math.Sin(phi1);

                // Point ptAB has phi value A and theta value B.
                // For example, pt01 has phi = phi0 and theta = theta1.
                // Find the points with theta = theta0.
                theta0 = 0;
                Vector3 pt00 = new Vector3(
                    centerX + r0 * Math.Cos(theta0),
                    centerY + r0 * Math.Sin(theta0),
                    centerZ + z0);
                Vector3 pt10 = new Vector3(
                    centerX + r1 * Math.Cos(theta0),
                    centerY + r1 * Math.Sin(theta0),
                    centerZ + z1);

                segments.Add(new Segment(pt00, pt10));
                for (int j = 0; j < thetaCount; j++)
                {
                    // Find the points with theta = theta1.
                    double theta1 = theta0 + dtheta;
                    Vector3 pt01 = new Vector3(
                        centerX + r0 * Math.Cos(theta1),
                        centerY + r0 * Math.Sin(theta1),
                        centerZ + z0);
                    Vector3 pt11 = new Vector3(
                        centerX + r1 * Math.Cos(theta1),
                        centerY + r1 * Math.Sin(theta1),
                        centerZ + z1);

                    segments.Add(new Segment(pt01, pt11));

                    // Add segments between the current outer lines
                    segments.Add(new Segment(pt00, pt01));
                    segments.Add(new Segment(pt10, pt11));

                    // Move to the next value of theta.
                    theta0 = theta1;
                    pt00 = pt01;
                    pt10 = pt11;
                }

                // Move to the next value of phi.
                phi0 = phi1;
                z0 = z1;
                r0 = r1;
            }

            return segments;
        }

        private class Segment
        {
            public double StartX { get; set; }
            public double StartY { get; set; }
            public double StartZ { get; set; }
            public double EndX { get; set; }
            public double EndY { get; set; }
            public double EndZ { get; set; }

            public Vector3 Start => new Vector3(StartX, StartY, StartZ);
            public Vector3 End => new Vector3(EndX, EndY, EndZ);

            public Segment(double startX, double startY, double startZ, double endX, double endY, double endZ)
            {
                StartX = startX;
                StartY = startY;
                StartZ = startZ;
                EndX = endX;
                EndY = endY;
                EndZ = endZ;
            }

            public Segment(Vector3 start, Vector3 end)
            {
                StartX = start.X;
                StartY = start.Y;
                StartZ = start.Z;
                EndX = end.X;
                EndY = end.Y;
                EndZ = end.Z;
            }
        }
    }
}
