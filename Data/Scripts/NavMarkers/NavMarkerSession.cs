using Draygo.API;
using NavMarkers.Data.Scripts.NavMarkers;
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

using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace NavMarkers
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class NavMarkerSession : MySessionComponentBase
    {
        public const string Keyword = "/nav";
        public const string ModName = "NavMarkers";

        public static NavMarkerSession Instance { get; private set; }
        public static bool ControlsInit = false;

        public int counter = 0;
        public int retryCount = 0;
        public bool ready = false;
        public string SessionName = "";
        public string NavMarkerFile = "NavMarkers";
        public bool NavMarkerState = false;
        public bool SaveQueued = false;
        public NavMarkerData NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
        public HudAPIv2 HudAPI;

        private bool shouldLog = false;

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
        }

        private void LoadConfig()
        {
            NavMarkerConfig.InitConfig();
            HudAPI = new HudAPIv2(NavMarkerConfig.Instance.InitMenu);
            if (HudAPI == null)
                MyAPIGateway.Utilities.ShowMessage("NavMarker", "TextHudAPI failed to register");
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
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(NavMarkerFile, typeof (NavMarkerData)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(NavMarkerFile, typeof( NavMarkerData));
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
            ready = true;
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
                actions.Add(TerminalControls.NavIntersectMarkerAction);
            }
        }

        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            TerminalControls.Create();
            if (block is IMyCockpit || block is IMyShipController)
            {
                controls.AddOrInsert(TerminalControls.Separator<IMyShipController>(), 5);
                controls.AddOrInsert(TerminalControls.NavToggleButton, 6);
                controls.AddOrInsert(TerminalControls.NavListbox, 7);
                controls.AddOrInsert(TerminalControls.NavAddButton, 8);
                controls.AddOrInsert(TerminalControls.NavActiveMarkerListbox, 9);
                controls.AddOrInsert(TerminalControls.NavRemoveButton, 10);
                controls.AddOrInsert(TerminalControls.Separator<IMyShipController>(), 11);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (Tools.IsDedicatedServer)
            {
                return;
            }
            if (retryCount > 10)
            {
                // Give up, we dead.
                return;
            }

            if (!ready)
            {
                counter++;
                if (counter > 100)
                {
                    counter = 0;
                    retryCount++;
                }
            }

            if (NavMarkers == null || NavMarkers.Markers == null || NavMarkers.Markers.Dictionary == null)
            {
                NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
                NavMarkerState = false;
            }

            IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
            if (player == null)
            {
                Tools.Log($"{ModName}: Local player is null despite this not being a dedicated server. Will try again to see if this happens during loading. Has tried {retryCount} times.");
                ready = false;
                counter = 0;
                return;
            }

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
                    if (shouldLog)
                    {
                        shouldLog = false;
                        MyAPIGateway.Utilities.ShowMessage("NavMarkers", $"segments = {wireSegments}, width= {wireframeWidth}, distanceFromEdge= {distanceFromEdge}, sizeMultiplier= {sizeMultiplier}");
                    }

                    // Set to wireframe based on config
                    if (config.enableSolidRender == false)
                    {
                        rasterMode = MySimpleObjectRasterizer.Wireframe;
                        // Allow overriding of blend mode based on config
                        blendType = config.renderAfterPostProcess ? BlendType.PostPP : BlendType.AdditiveBottom;
                    }

                    MySimpleObjectDraw.DrawTransparentSphere(
                        ref matrix,
                        marker.Radius,
                        ref color,
                        rasterMode,
                        wireSegments,
                        MyStringId.GetOrCompute("NavMarkerTransparency"),
                        MyStringId.GetOrCompute("NavMarkerLines"),
                        wireframeWidth * config.wireframeWidth,
                        -1,
                        null,
                        blendType,
                        config.bloomIntensity
                    );
                }
            }

            if (SaveQueued)
            {
                SaveQueued = false;
                SaveMarkers(true);
            }
        }

        public override void Draw()
        {
            if (Tools.IsClient == false || HudAPI == null || MyAPIGateway.Session.Config.HudState == 0)
            {
                return;
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
    }
}
