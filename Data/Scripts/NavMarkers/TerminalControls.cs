using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace NavMarkers
{
    public static class TerminalControls
    {
        private static bool Done = false;

        public static IMyTerminalControlOnOffSwitch NavToggleButton;
        public static IMyTerminalAction NavToggleAction;
        public static IMyTerminalControlListbox NavListbox;
        public static IMyTerminalControlButton NavAddButton;
        public static IMyTerminalControlListbox NavActiveMarkerListbox;
        public static IMyTerminalControlButton NavRemoveButton;

        private static int SelectedGps = -1;
        private static string SelectedMarker = "";

        public static void Create()
        {
            if (Done)
            {
                return;
            }
            Done = true;

            CreateControls();
            CreateActions();
        }

        internal static IMyTerminalControlSeparator Separator<T>() where T : IMyShipController
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>("NavMarker_Separator");
            c.Enabled = IsTrue;
            c.Visible = IsTrue;
            return c;
        }

        internal static bool IsTrue(IMyTerminalBlock block)
        {
            return true;
        }

        private static bool IsControlVisible(IMyTerminalBlock block) => Tools.IsValid(block) && block is IMyShipController;

        private static void CreateActions()
        {
            {
                NavToggleAction = MyAPIGateway.TerminalControls.CreateAction<IMyShipController>("NavMarkers_Toggle");
                NavToggleAction.Enabled = IsControlVisible;
                NavToggleAction.Name = new StringBuilder("Toggle Nav Markers");
                NavToggleAction.Icon = @"Textures\GUI\Icons\Actions\Reverse.dds";
                NavToggleAction.ValidForGroups = false;
                NavToggleAction.InvalidToolbarTypes = new List<MyToolbarType>
                {
                    MyToolbarType.Character,
                    MyToolbarType.ButtonPanel,
                    MyToolbarType.Seat
                };
                NavToggleAction.Action = NavAction_Toggle;
            }
        }

        private static void CreateControls()
        {
            {
                NavToggleButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyShipController>("NavMarkers_ToggleButton");
                NavToggleButton.Title = MyStringId.GetOrCompute("Toggle Nav Markers");
                NavToggleButton.Tooltip = MyStringId.GetOrCompute("Toggle Nav Markers visible/invisible");
                NavToggleButton.SupportsMultipleBlocks = false;
                NavToggleButton.Visible = IsControlVisible;
                NavToggleButton.OnText = MyStringId.GetOrCompute("On");
                NavToggleButton.OffText = MyStringId.GetOrCompute("Off");
                NavToggleButton.Getter = NavToggle_Getter;
                NavToggleButton.Setter = NavToggle_Setter;
            }
            {

                NavListbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipController>("NavMarkers_List");
                NavListbox.Enabled = IsControlVisible;
                NavListbox.Visible = IsControlVisible;
                NavListbox.Multiselect = false;
                NavListbox.SupportsMultipleBlocks = false;
                NavListbox.Title = MyStringId.GetOrCompute("Available GPS Markers");
                NavListbox.VisibleRowsCount = 5;
                NavListbox.ListContent = FillGPSList;
                NavListbox.ItemSelected = NavGPSList_Selected;
            }
            {
                NavAddButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("NavMarker_AddButton");
                NavAddButton.Enabled = IsControlVisible;
                NavAddButton.Visible = IsControlVisible;
                NavAddButton.SupportsMultipleBlocks = false;
                NavAddButton.Title = MyStringId.GetOrCompute("Create From GPS");
                NavAddButton.Action = NavAddButton_Action;
            }
            {
                NavActiveMarkerListbox = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyShipController>("NavMarkers_ActiveList");
                NavActiveMarkerListbox.Enabled = IsControlVisible;
                NavActiveMarkerListbox.Visible = IsControlVisible;
                NavActiveMarkerListbox.Multiselect = false;
                NavActiveMarkerListbox.SupportsMultipleBlocks = false;
                NavActiveMarkerListbox.Title = MyStringId.GetOrCompute("Active Nav Markers");
                NavActiveMarkerListbox.VisibleRowsCount = 5;
                NavActiveMarkerListbox.ListContent = FillMarkerList;
                NavActiveMarkerListbox.ItemSelected = NavMarkersList_Selected;
            }
            {
                NavRemoveButton = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyShipController>("NavMarker_RemoveButton");
                NavRemoveButton.Enabled = IsControlVisible;
                NavRemoveButton.Visible = IsControlVisible;
                NavRemoveButton.SupportsMultipleBlocks = false;
                NavRemoveButton.Title = MyStringId.GetOrCompute("Remove Marker");
                NavRemoveButton.Action = NavRemoveButton_Action;
            }
        }
        private static void UpdateControls()
        {
            List<IMyTerminalControl> controls = new List<IMyTerminalControl>();
            
            MyAPIGateway.TerminalControls.GetControls<IMyShipController>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                MyLog.Default.WriteLineAndConsole($"Updating visual for control: {control.Id}");
                //control.RedrawControl();
                control.UpdateVisual();
            }
            NavActiveMarkerListbox.UpdateVisual();
            NavListbox.UpdateVisual();
        }

        private static void NavAddButton_Action(IMyTerminalBlock block)
        {
            MyLog.Default.WriteLineAndConsole($"Adding GPS from list {SelectedGps}");
            if (SelectedGps < 0)
            {
                return;
            }
            try
            {
                List<IMyGps> gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);
                int index = SelectedGps;
                IMyGps gps = gpsList[index];

                string scanPattern = ".*\\(R-(\\d+)\\)";
                Match match = Regex.Match(gps.Name, scanPattern);
                MyLog.Default.WriteLineAndConsole($"Input string: {gps.Name} Regex result: {match.Success} Regex result: {match.Groups[1].Value}");

                double radius = match.Success ? int.Parse(match.Groups[1].Value) * 1000 : 1000;
                NavMarkerSession.Instance.AddNavMarker(gps.Name, gps.Coords, radius, gps.GPSColor);
                SelectedGps = -1;
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"Error when creating new marker from GPS: {SelectedGps}. {ex}");
            }
            UpdateControls();
        }

        private static void NavRemoveButton_Action(IMyTerminalBlock block)
        {
            MyLog.Default.WriteLineAndConsole($"Removing Marker from list {SelectedMarker}");
            if (SelectedMarker == "")
            {
                return;
            }
            try
            {
                NavMarkerSession.Instance.NavMarkers.markers.Dictionary.Remove(SelectedMarker);
                SelectedMarker = "";
            }
            catch ( Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"Error when removing marker from active list: {SelectedMarker}. {ex}");

            }
            UpdateControls();
        }
        
        private static void NavGPSList_Selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selectedItems)
        {
            SelectedGps = (int)selectedItems[0].UserData;
            MyLog.Default.WriteLineAndConsole($"Selected GPS from list {SelectedGps}");
            UpdateControls();
        }

        private static void NavMarkersList_Selected(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selectedItems)
        {
            SelectedMarker = (string)selectedItems[0].UserData;
            MyLog.Default.WriteLineAndConsole($"Selected Marker from list {SelectedMarker}");
            UpdateControls();
        }

        private static bool NavToggle_Getter(IMyTerminalBlock block)
        {
            return NavMarkerSession.Instance.NavMarkerState;
        }
        private static void NavToggle_Setter(IMyTerminalBlock block, bool activated)
        {
            UpdateNavMarkerState(block, activated);
        }

        private static void NavAction_Toggle(IMyTerminalBlock block)
        {
            UpdateNavMarkerState(block, !NavMarkerSession.Instance.NavMarkerState);
        }

        private static void UpdateNavMarkerState(IMyTerminalBlock block, bool activated)
        {
            NavMarkerSession.Instance.NavMarkerState = activated;
        }

        private static void FillGPSList(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> list, List<MyTerminalControlListBoxItem> selected)
        {
            List<IMyGps> gpsList = MyAPIGateway.Session.GPS.GetGpsList(MyAPIGateway.Session.LocalHumanPlayer.IdentityId);

            int index = 0;
			foreach (IMyGps gps in gpsList)
            {
                string scanPattern = ".*\\(R-(\\d+)\\)";
                Match match = Regex.Match(gps.Name, scanPattern);
                if (match.Success)
                {
                    list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(gps.Name), MyStringId.GetOrCompute(gps.Description), index));
                }
                index++;
            }
            
        }

        private static void FillMarkerList(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> list, List<MyTerminalControlListBoxItem> selected)
        {
            foreach (NavMarker marker in NavMarkerSession.Instance.NavMarkers.markers.Dictionary.Values)
            {
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(marker.Name), MyStringId.NullOrEmpty, marker.Name));
            }
        }
    }
}
