using Draygo.API;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.IO;
using VRageMath;

namespace Prospector
{
    [ProtoContract]
    public class Settings
    {
        public static Settings Instance;
        public static readonly Settings Default = new Settings()
        {
            hideAsteroids = false,
            enableLabels = true,
            enableSymbols = true,
            obsColor = Color.Goldenrod,
            finishedColor = Color.LawnGreen,
            scanColor = Color.Yellow,
        };

        [ProtoMember(1)]
        public bool hideAsteroids { get; set; } = false;
        [ProtoMember(2)]
        public bool enableLabels { get; set; } = true;
        [ProtoMember(3)]
        public bool enableSymbols { get; set; } = true;
        [ProtoMember(4)]
        public Color obsColor { get; set; } = Color.Goldenrod;
        [ProtoMember(5)]
        public Color finishedColor { get; set; } = Color.LawnGreen;
        [ProtoMember(6)]
        public Color scanColor { get; set; } = Color.Yellow;
        [ProtoMember(7)]
        public Color expandedColor { get; set; } = Color.PowderBlue;
    }
    public partial class Session
    {
        private void InitConfig()
        {
            Settings s = Settings.Default;

            var Filename = "UserSettings.cfg";
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    if (text.Length == 0)
                        s = Settings.Default;
                    else
                        s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Save(s);
                }
                else
                {
                    Save(Settings.Default);
                }
            }
            catch (Exception e)
            {
                Settings.Instance = Settings.Default;
                s = Settings.Default;
                Save(s);
                MyAPIGateway.Utilities.ShowNotification("Prospector: Error with config file, overwriting with default." + e);
            }
        }
        public static void Save(Settings settings)
        {
            var Filename = "UserSettings.cfg";
            try
            {
                TextWriter writer;
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
                writer.Close();
                Settings.Instance = settings;
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage("Prospector", "Error saving cfg file");
            }
        }

        HudAPIv2.MenuRootCategory SettingsMenu;
        HudAPIv2.MenuItem AsteroidEnable, SymbolEnableObs, LabelEnableObs, ShowConfigs;
        HudAPIv2.MenuColorPickerInput ObsColor, FinishColor, ScanColor, ExpandedColor;
        private void InitMenu()
        {
            SettingsMenu = new HudAPIv2.MenuRootCategory("Prospector", HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Prospector Settings");
            AsteroidEnable = new HudAPIv2.MenuItem("Hide asteroid display: " + Settings.Instance.hideAsteroids, SettingsMenu, ShowAsteroids);
            SymbolEnableObs = new HudAPIv2.MenuItem("Show bounding box: " + Settings.Instance.enableSymbols, SettingsMenu, ShowSymbolsObs);
            LabelEnableObs = new HudAPIv2.MenuItem("Show labels: " + Settings.Instance.enableLabels, SettingsMenu, ShowLabelsObs);
            ObsColor = new HudAPIv2.MenuColorPickerInput("Set out of range asteroid symbol/text color >", SettingsMenu, Settings.Instance.obsColor, "Select color", ChangeObsColor);
            ScanColor = new HudAPIv2.MenuColorPickerInput("Set in range but unscanned asteroid symbol/text color >", SettingsMenu, Settings.Instance.scanColor, "Select color", ChangeScanColor);
            FinishColor = new HudAPIv2.MenuColorPickerInput("Set scanned symbol/text color >", SettingsMenu, Settings.Instance.finishedColor, "Select color", ChangeFinishedColor);
            ExpandedColor = new HudAPIv2.MenuColorPickerInput("Set data review mode color >", SettingsMenu, Settings.Instance.expandedColor, "Select color", ChangeExpandedColor);
            ShowConfigs = new HudAPIv2.MenuItem("Display configs (click here then hit enter to see info) >>", SettingsMenu, ShowCfgs);
            HudRegisterObjects();
        }

        private void ShowCfgs()
        {
            showConfigQueued = true;
        }
        private void ChangeExpandedColor(Color obj)
        {
            Settings.Instance.expandedColor = obj;
            HudUpdateColor();
            Save(Settings.Instance);
        }
        private void ChangeObsColor(Color obj)
        {
            Settings.Instance.obsColor = obj;
            Save(Settings.Instance);
        }
        private void ChangeFinishedColor(Color obj)
        {
            Settings.Instance.finishedColor = obj;
            Save(Settings.Instance);
        }
        private void ChangeScanColor(Color obj)
        {
            Settings.Instance.scanColor = obj;
            Save(Settings.Instance);
        }
        private void ShowSymbolsObs()
        {
            Settings.Instance.enableSymbols = !Settings.Instance.enableSymbols;
            SymbolEnableObs.Text = "Show bounding box: " + Settings.Instance.enableSymbols;
            Save(Settings.Instance);
        }
        private void ShowAsteroids()
        {
            Settings.Instance.hideAsteroids = !Settings.Instance.hideAsteroids;
            AsteroidEnable.Text = "Hide asteroid display: " + Settings.Instance.hideAsteroids;
            Save(Settings.Instance);
        }
        private void ShowLabelsObs()
        {
            Settings.Instance.enableLabels = !Settings.Instance.enableLabels;
            LabelEnableObs.Text = "Show labels: " + Settings.Instance.enableLabels;
            Save(Settings.Instance);
        }
    }
}

