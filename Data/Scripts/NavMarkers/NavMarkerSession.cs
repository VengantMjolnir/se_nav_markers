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
        public bool SaveQueued = false;
        public NavMarkerData NavMarkers = new NavMarkerData() { Enabled = false, Markers = new VRage.Serialization.SerializableDictionary<string, NavMarker>() };
        public HudAPIv2 HudAPI;
        

        private bool shouldLog = false;

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
            
            if (NavMarkerState)
            {
                foreach (NavMarker marker in NavMarkers.Markers.Dictionary.Values)
                {
                    IMyPlayer player = MyAPIGateway.Session.LocalHumanPlayer;
                    double distance = Vector3D.Distance(player.GetPosition(), marker.Position);
                    double distanceFromEdge = Math.Abs(distance - marker.Radius);
                    double sizeMultiplier = marker.Radius / 100000.0;

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
    }
}
