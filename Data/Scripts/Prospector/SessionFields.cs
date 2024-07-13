using Digi.NetworkProtobufProspector;
using Draygo.API;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Serialization;
using VRage.Utils;
using VRage;
using Sandbox.ModAPI;
using Sandbox.Game;
using System.Collections.Concurrent;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        HudAPIv2 hudAPI;
        public static Dictionary<MyStringHash, ScannerConfig> scannerTypes = new Dictionary<MyStringHash, ScannerConfig>();
        public static Networking Networking = new Networking(5860);
        public static ScannerConfigList serverList = new ScannerConfigList() { cfgList = new List<ScannerConfig>() };
        public static bool rcvdSettings = false;

        internal Dictionary<string, string> oreTagMap = new Dictionary<string, string>();
        internal ConcurrentDictionary<MyVoxelBase, byte> newRoids = new ConcurrentDictionary<MyVoxelBase, byte>();
        internal SerializableDictionary<MyVoxelBase, VoxelScan> voxelScans = new SerializableDictionary<MyVoxelBase, VoxelScan>();
        internal MyTuple<MyCubeBlock, ScannerConfig> currentScanner = new MyTuple<MyCubeBlock, ScannerConfig>();
        internal VoxelScanDict voxelScanMemory = new VoxelScanDict() { scans = new SerializableDictionary<long, VoxelScan>() };
        internal MyCubeGrid controlledGrid;
        internal double currentScannerFOVLimit = 0;
        internal ulong serverID;
        internal string scanDataSaveFile = "ScanData";
        internal string scannerCfg = "ScannerConfigs.cfg";
        internal float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float symbolWidth = 0.03f;
        internal int maxCheckDist = 10000;
        internal int tick = -300;
        internal bool showConfigQueued = false;
        internal bool controlInit = false;
        internal bool queueReScan = false;
        internal bool queueGPSTag = false;
        internal bool planetSuppress = true;
        internal bool registeredController = false;
        internal bool server;
        internal bool client;
        internal bool mpActive;

        //Client AV Vars
        internal MyStringId corner = MyStringId.GetOrCompute("SharpEdge"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId missileOutline = MyStringId.GetOrCompute("MissileOutline"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId frameCorner = MyStringId.GetOrCompute("FrameCorner"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId solidCircle = MyStringId.GetOrCompute("RoidCircle");
        internal MyStringId hollowCircle = MyStringId.GetOrCompute("RoidCircleHollow");
        internal bool expandedMode = false;
        internal bool hudObjectsRegistered = false;
        internal HudAPIv2.BillBoardHUDMessage topLeft = null;
        internal HudAPIv2.BillBoardHUDMessage topRight = null;
        internal HudAPIv2.BillBoardHUDMessage botLeft = null;
        internal HudAPIv2.BillBoardHUDMessage botRight = null;
        internal HudAPIv2.BillBoardHUDMessage scanRing = null;
        internal HudAPIv2.HUDMessage message = null;
        internal HudAPIv2.HUDMessage title = null;
        internal double ctrOffset = 0.25;

        //Hardcoded material names
        internal Dictionary<string, string> oreDefaults = new Dictionary<string, string>()
        {
            {"Ice", "Ice"},
            {"Nickel", "Ni"},
            {"Stone", "Stone"},
            {"Cobalt", "Co"},
            {"Magnesium", "Mg"},
            {"Silicon", "Si"},
            {"Silver", "Ag"},
            {"Gold", "Au"},
            {"Platinum", "Pt"},
            {"Uranium", "U"},
            
            //IO
            {"Sulfur", "S"},
            {"Coal", "C"},
            {"Copper", "Cu"},
            {"Lithium", "Li"},
            {"Bauxite", "Al"},
            {"Titanium", "Ti"},
            {"Tantalum", "Ta"},
        };

        private void Clean()
        {
            if (client)
            {
                SaveScans(true);
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
                if (hudAPI != null)
                    hudAPI.Unload();

                if (registeredController)
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
                MyEntities.OnEntityCreate -= OnEntityCreate;
            }
            if (server)
            {
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            }
            Networking?.Unregister();
            Networking = null;

            //Data purge
            scannerTypes.Clear();
            serverList.Clear();
            oreTagMap.Clear();
            newRoids.Clear();
            voxelScans.Dictionary.Clear();
            voxelScanMemory.scans.Dictionary.Clear();
            controlledGrid = null;
        }
    }
}
