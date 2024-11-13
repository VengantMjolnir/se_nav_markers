using Draygo.API;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using VRage.Utils;

namespace NavMarkers.Data.Scripts.NavMarkers
{
    [ProtoContract]
    public class NavMarkerConfig
    {
        public static NavMarkerConfig Instance = new NavMarkerConfig();

        private HudAPIv2 HudApi;

        #region Config Settings
        public static readonly NavMarkerConfig Default = new NavMarkerConfig()
        {
            EnableSolidRender = false,
            RenderAfterPostProcess = false,
            AlphaValue = 60,
            BloomIntensity = 0.5f,
            WireframeWidth = 1.0f,
            CloseOnlyDistance = 100000,
            PartialLineDistance = 175000
        };

        [ProtoMember(1)]
        public bool EnableSolidRender { get; set; } = false;
        [ProtoMember(2)]
        public bool RenderAfterPostProcess { get; set; } = false;
        [ProtoMember(3)]
        public int AlphaValue { get; set; } = 60;
        [ProtoMember(4)]
        public float BloomIntensity { get; set; } = 0.5f;
        [ProtoMember(5)]
        public float WireframeWidth { get; set; } = 1.0f;
        [ProtoMember(6)]
        public int CloseOnlyDistance { get; set; } = 100000;
        [ProtoMember(7)]
        public int PartialLineDistance { get; set; } = 175000;
        #endregion

        #region HudAPI fields
        private HudAPIv2.MenuRootCategory SettingsMenu;
        private HudAPIv2.MenuItem EnableSolidRenderItem, RenderAfterPostProcessItem;
        private HudAPIv2.MenuSliderInput AlphaValueSlider, BloomIntensitySlider, WireframeWidthSlider;

        #endregion

        public static void InitConfig()
        {
            string Filename = "NavMarkerConfig.cfg";
            try
            {
                var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(NavMarkerConfig));
                if (!Tools.IsDedicatedServer && localFileExists) //client already has an established cfg
                {
                    MyLog.Default.WriteLineAndConsole($"NavMarkers: Starting config. Local exists: {localFileExists}");
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(NavMarkerConfig));
                    string text = reader.ReadToEnd();
                    reader.Close();

                    if (text.Length == 0) //Corner case catch of a blank Config
                    {
                        MyAPIGateway.Utilities.ShowMessage("NavMarkers", "Error with config file, overwriting with default.");
                        MyLog.Default.Error($"NavMarkers: Error with config file, overwriting with default");
                        Save(NavMarkerConfig.Default);
                    }
                    else
                    {
                        NavMarkerConfig config = MyAPIGateway.Utilities.SerializeFromXML<NavMarkerConfig>(text);
                        Save(config);
                    }
                }
                else //Default/initial client cfg
                {
                    MyLog.Default.WriteLineAndConsole($"NavMarkers: Local config doesn't exist. Creating default");
                    Save(NavMarkerConfig.Default);
                }
            }
            catch (Exception ex)
            {
                Save(NavMarkerConfig.Default);
                MyAPIGateway.Utilities.ShowMessage("NavMarkers", "Error with config file, overwriting with default." + ex);
                MyLog.Default.Error($"NavMarkers: Error with config file, overwriting with default {ex}");
            }
        }

        public static void Save(NavMarkerConfig config)
        {
            string Filename = "NavMarkerConfig.cfg";
            try
            {
                if (!Tools.IsDedicatedServer)
                {
                    MyLog.Default.WriteLineAndConsole($"NavMarkers: Saving config.");
                    TextWriter writer;
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(NavMarkerConfig));
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(config));
                    writer.Close();
                }
                NavMarkerConfig.Instance = config;
            }
            catch (Exception ex)
            {
                MyLog.Default.Error($"NavMarkers: Error saving config file {ex}");
            }
        }

        // HudAPIv2 callback
        public void InitMenu()
        {
            SettingsMenu = new HudAPIv2.MenuRootCategory("Nav Markers", HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Nav Markers Settings");
            EnableSolidRenderItem = new HudAPIv2.MenuItem($"Enable Solid Render Mode: {EnableSolidRender}", SettingsMenu, ShowEnableSolidRender);
            RenderAfterPostProcessItem = new HudAPIv2.MenuItem($"Wireframe Blend Mode: {(RenderAfterPostProcess ? "After PostProcess" : "Alpha")}", SettingsMenu, ShowRenderMode);
            AlphaValueSlider = new HudAPIv2.MenuSliderInput($"Alpha Value: {AlphaValue}", SettingsMenu, (float)(AlphaValue / 255f), "Adjust Slider to modify sphere transparency", ChangeAlphaValue, GetAlphaValue);
            BloomIntensitySlider = new HudAPIv2.MenuSliderInput($"Bloom Intensity: {BloomIntensity}", SettingsMenu, BloomIntensity, "Adjust Slider to modify wireframe line intensity", ChangeBloomIntensity, GetBloomIntensity);
            WireframeWidthSlider = new HudAPIv2.MenuSliderInput($"Wireframe Width: {WireframeWidth}", SettingsMenu, WireframeWidth - 0.5f, "Adjust Slider to modify wireframe width", ChangeWireframeWidth, GetWireframeWidth);
        }

        private void ShowEnableSolidRender()
        {
            EnableSolidRender = !EnableSolidRender;
            EnableSolidRenderItem.Text = $"Enable Solid Render Mode: {EnableSolidRender}";
            Save(this);
        }

        private void ShowRenderMode()
        {
            RenderAfterPostProcess = !RenderAfterPostProcess;
            RenderAfterPostProcessItem.Text = $"Wireframe Blend Mode: {(RenderAfterPostProcess ? "After PostProcess" : "Alpha")}";
            Save(this);
        }

        private void ChangeAlphaValue(float input)
        {
            AlphaValue = (int)(input * 255);
            AlphaValueSlider.Text = $"Alpha Value: {AlphaValue}";
            AlphaValueSlider.InitialPercent = (float)(AlphaValue / 255f);
            Save(this);
        }

        private object GetAlphaValue(float input)
        {
            return (int)(input * 255);
        }

        private void ChangeBloomIntensity(float input)
        {
            BloomIntensity = input;
            BloomIntensitySlider.Text = $"Bloom Intensity: {BloomIntensity}";
            BloomIntensitySlider.InitialPercent = BloomIntensity;
            Save(this);
        }

        private object GetBloomIntensity(float input)
        {
            return input;
        }

        private void ChangeWireframeWidth(float input)
        {
            WireframeWidth = input + 0.5f;
            WireframeWidthSlider.Text = $"Wireframe Width: {WireframeWidth}";
            WireframeWidthSlider.InitialPercent = WireframeWidth - 0.5f;
            Save(this);
        }
        
        private object GetWireframeWidth(float input)
        {
            return input + 0.5f;
        }
    }
}
