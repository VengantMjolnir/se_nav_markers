using Draygo.API;
using NavMarkers.Data.Scripts.NavMarkers;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace NavMarkers
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NavMarkerSession : MySessionComponentBase
    {
        public const string Keyword = "/nav";
        public const string ModName = "NavMarkers";

        public static NavMarkerSession Instance { get; private set; }

        public string SessionName = "";
        public string NavMarkerFile = "NavMarkers";
        public bool NavMarkerState = false;
        public bool CloseOnly = false;
        public bool PartialDisplay = false;
        public bool SaveQueued = false;
        public NavMarkerData NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
        public HudAPIv2 HudAPI;

        private Dictionary<string, ChatCommand> ChatCommands;

        private double _closeRange = 100000d;
        private double _lineRange = 175000d;
        private Dictionary<NavMarker, List<Segment>> _navMarkerSegments = new Dictionary<NavMarker, List<Segment>>();

        public override void LoadData()
        {
            if (Tools.IsDedicatedServer)
            {
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
                return;
            }

            LoadConfig();
            LoadMarkers();

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;

            ChatCommands = new Dictionary<string, ChatCommand>()
            {
                { "help", new ChatCommand() { command = "help", callback = ChatCommand_Help } },
                { "add", new ChatCommand() { command = "add", callback = ChatCommand_Add } },
                { "remove", new ChatCommand() { command = "remove", callback = ChatCommand_Remove } },
                { "list", new ChatCommand() { command = "list", callback = ChatCommand_List } }
            };
        }


        public override void SaveData()
        {
            if (Tools.IsDedicatedServer == false)
            {
                SaveMarkers(true);
                NavMarkerConfig.Save(NavMarkerConfig.Instance);
            }
        }

        protected override void UnloadData()
        {
            if (Tools.IsDedicatedServer == false)
            {
                SaveMarkers(true);
                NavMarkerConfig.Save(NavMarkerConfig.Instance);
            }
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEntered;
        }

        public override void UpdateBeforeSimulation()
        {
            if (SaveQueued)
            {
                SaveQueued = false;
                SaveMarkers(true);
            }

            // Sanity checks
            if (Tools.IsDedicatedServer)
            {
                return;
            }
            IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
            if (player == null)
            {
                return;
            }

            if (NavMarkers == null || NavMarkers.Markers == null || NavMarkers.Markers.Dictionary == null)
            {
                NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
                NavMarkerState = false;
            }

            // Drawing logic
            if (NavMarkerState)
            {
                NavMarkerConfig config = NavMarkerConfig.Instance;
                foreach (NavMarker marker in NavMarkers.Markers.Dictionary.Values)
                {
                    if (marker == null)
                        continue; // Huh? Nothing in the lifetime should produce this, but without line numbers for the null reference I'm being extra careful on this one
                    double distance = Vector3D.Distance(player.GetPosition(), marker.Position);
                    double distanceFromEdge = Math.Abs(distance - marker.Radius);
                    double sizeMultiplier = marker.Radius / 100000.0;

                    // Only show if within X km of the border
                    if (CloseOnly && distanceFromEdge > _closeRange) { continue; }

                    Color color = marker.Color;
                    MatrixD matrix = MatrixD.Identity;
                    matrix.Translation = marker.Position;
                    color.A = (byte)config.alphaValue;
                    float radius = (float)marker.Radius / 1000.0f;

                    int wireSegments = Math.Max(2, (int)(marker.Radius * 3 / distanceFromEdge)) * 12;
                    // Render 'mode' is solid, but can be changed by config
                    MySimpleObjectRasterizer rasterMode = MySimpleObjectRasterizer.SolidAndWireframe;
                    BlendType blendType = BlendType.Standard;

                    if (distance < marker.Radius)
                    {
                        // If inside the sphere then render mode changes to wireframe
                        rasterMode = MySimpleObjectRasterizer.Wireframe;
                        blendType = BlendType.AdditiveBottom;
                        distanceFromEdge = Math.Abs(marker.Radius - distance);
                        wireSegments = radius > 100 ? 36 : 24;
                    }

                    // Calculate wireframe, increasing it with distance to offset aliasing
                    float wireframeWidth = (float)((distanceFromEdge / marker.Radius) * 250f * sizeMultiplier);
                    wireSegments = Math.Min(36, wireSegments);
                    wireframeWidth = Math.Max(radius, Math.Min(150.0f * (float)sizeMultiplier, wireframeWidth));

                    // Set to wireframe based on config
                    if (config.enableSolidRender == false)
                    {
                        rasterMode = MySimpleObjectRasterizer.Wireframe;
                        // Allow overriding of blend mode based on config
                        blendType = config.renderAfterPostProcess ? BlendType.PostPP : BlendType.AdditiveBottom;
                    }

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
        }

        public override void Draw()
        {
            if (Tools.IsClient == false || HudAPI == null || MyAPIGateway.Session.Config.HudState == 0)
            {
                return;
            }
        }

        private void LoadConfig()
        {
            NavMarkerConfig.InitConfig();
            HudAPI = new HudAPIv2(NavMarkerConfig.Instance.InitMenu);
            if (HudAPI == null)
                MyAPIGateway.Utilities.ShowMessage("NavMarker", "TextHudAPI failed to register");
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
                NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
                NavMarkerState = false;
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
                    Tools.Log($"{ModName}: Saved nav markers: {NavMarkerFile}");
                }
            }
            catch (Exception ex)
            {
                Tools.Log($"{ModName}: Failed to save marker data");

            }
        }

        private void OnMessageEntered(ulong sender, string messageText, ref bool sendToOthers)
        {
            var message = messageText.ToLower();
            if (message.StartsWith(Keyword) == false)
            {
                return;
            }

            message = messageText.Substring(Keyword.Length).Trim(' ');

            string scanPattern = "[^\\s\"']+|\"([^\"]*)\"|'([^']*)'";
            MatchCollection matches = Regex.Matches(message, scanPattern);
            sendToOthers = false;
            StringBuilder sb = new StringBuilder();
            int index = 0;
            string[] args = new string[matches.Count];
            foreach (var match in matches)
            {
                args[index++] = match.ToString();
            }
            //MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Args: {string.Join(",",args)}");
            if (ChatCommands.ContainsKey(args[0].ToLower()))
            {
                ChatCommands[args[0]].callback(args);
            }
        }

        #region Chat Commands
        private void ChatCommand_Help(string[] args)
        {
            MyAPIGateway.Utilities.ShowMissionScreen("Nav Markers Help", "Test", "Testing", "This is the description");
        }

        private void ChatCommand_Add(string[] args)
        {
            if (args.Length < 3)
            {
                MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"'/nav add' requires at least two arguments: <range> and <gps_name>");
                return;
            }
            string name = args[2];
            double radius;
            if (!double.TryParse(args[1], out radius))
            {
                Tools.Log($"{ModName}: Failed to parse double from '{args[1]}', trying args in opposite order.");
                name = args[1];
                if (!double.TryParse(args[2], out radius))
                {
                    Tools.Log($"{ModName}: Failed to parse double from '{args[2]}' as well. No marker added");
                    return;
                }
            }
            name = name.Trim('"');
            List<IMyGps> gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
            foreach (IMyGps gps in gpsList)
            {
                if (gps.Name.CompareTo(name) == 0)
                {
                    AddNavMarker(name, gps.Coords, radius * 1000.0, gps.GPSColor);
                    return;
                }
            }

            MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Add Marker failed, no GPS with name exists: Name = {name}");
        }

        private void ChatCommand_Remove(string[] args)
        {
            if (args.Length < 2)
            {
                MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"'/nav remove' requires at least one argument: <name>");
                return;
            }
            string name = args[1].Trim('"');
            var markers = NavMarkers.Markers.Dictionary.Values;
            foreach (NavMarker marker in markers)
            {
                if (marker.Name.CompareTo(name) == 0)
                {
                    RemoveNavMarker(name);
                    return;
                }
            }
            MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Remove Marker failed, no marker with name exists: Name = {name}.");
        }

        private void ChatCommand_List(string[] args)
        {
            StringBuilder sb = new StringBuilder();
            var markers = NavMarkers.Markers.Dictionary.Values;
            sb.AppendLine("Active Nav Markers:");
            int index = 1;
            foreach (NavMarker marker in markers)
            {
                sb.AppendLine($"{index}. {marker.Name}");
            }
            MyAPIGateway.Utilities.ShowMessage("NavMarkers", sb.ToString());
        }
        #endregion

        private void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            TerminalControls.Create();
            if (block is IMyShipController)
            {
                actions.Add(TerminalControls.NavToggleAction);
                actions.Add(TerminalControls.NavIntersectMarkerAction);
                actions.Add(TerminalControls.NavToggleAction);
                actions.Add(TerminalControls.NavIntersectMarkerAction);
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

        #region Public interface
        public void AddNavMarker(string name, Vector3D coords, double radius, Color color, bool forceAdd = false)
        {
            if (NavMarkers.Markers.Dictionary.ContainsKey(name))
            {
                MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Add Marker failed, Marker already exists: Name = {name}");
                return;
            }
            MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Added a new marker: Radius = {radius}, Name = {name}");

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
            MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"Removed nav marker: Name = {name}");
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
        public void TryIntersectMarkers()
        {
            IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
            Vector3D position = player.GetPosition();
            RayD playerLookRay = new RayD(position, MyAPIGateway.Session.Camera.WorldMatrix.Forward);
            BoundingSphereD markerSphere = new BoundingSphereD();
            double tMin = 0.0;
            double tMax = 0.0;
            double distance = double.MaxValue;
            NavMarker closestMarker = null;
            foreach (NavMarker marker in NavMarkers.Markers.Dictionary.Values)
            {
                markerSphere.Center = marker.Position;
                markerSphere.Radius = marker.Radius;
                if (markerSphere.IntersectRaySphere(playerLookRay, out tMin, out tMax))
                {
                    if (tMin < distance)
                    {
                        closestMarker = marker;
                        distance = tMin;
                    }
                }
            }

            if (closestMarker != null)
            {
                string name = $"Intercept ({closestMarker.Name})";
                position += playerLookRay.Direction * distance;
                IMyGps gps = MyAPIGateway.Session.GPS.Create(name, $"Intercept point of view direction with closest Nav Marker: {closestMarker.Name}", position, true, false);
                gps.GPSColor = closestMarker.Color;
                MyAPIGateway.Session.GPS.AddGps(player.IdentityId, gps);
            }
        }
        #endregion
    }
}
