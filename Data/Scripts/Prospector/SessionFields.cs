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
using VRage.Voxels;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        HudAPIv2 hudAPI;
        internal bool client;
        internal int tick = -300;
        internal int gridMaxDisplayDist = 0;
        internal long gridMaxDisplayDistSqr = 0;

        internal MyStringId corner = MyStringId.GetOrCompute("SharpEdge"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId missileOutline = MyStringId.GetOrCompute("MissileOutline"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId frameCorner = MyStringId.GetOrCompute("FrameCorner"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal MyStringId solidCircle = MyStringId.GetOrCompute("RoidCircle");
        internal MyStringId hollowCircle = MyStringId.GetOrCompute("RoidCircleHollow");

        internal VRageRender.MyBillboard.BlendTypeEnum cornerBlend = VRageRender.MyBillboard.BlendTypeEnum.Standard;
        internal SerializableDictionary<MyVoxelBase, VoxelScan> voxelScans = new SerializableDictionary<MyVoxelBase, VoxelScan>();
        public static Dictionary<MyStringHash, ScannerConfig> scannerTypes = new Dictionary<MyStringHash, ScannerConfig>();
        internal MyCubeGrid controlledGrid;
        internal MyTuple<MyCubeBlock, ScannerConfig> currentScanner = new MyTuple<MyCubeBlock, ScannerConfig>();
        internal double currentScannerFOVLimit = 0;
        internal VoxelScanDict voxelScanMemory = new VoxelScanDict();
        internal string scannerCfg = "ScannerConfigs.cfg";
        public static Networking Networking = new Networking(5860);
        public static ScannerConfigList serverList = new ScannerConfigList();
        public static bool rcvdSettings = false;
        internal bool registeredController = false;
        internal int clientAttempts = 0;
        internal bool server;
        internal bool mpActive;
        internal ulong serverID;
        internal string scanDataSaveFile = "ScanData";
        internal bool planetSuppress = true;
        internal float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float aspectRatio = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal float symbolWidth = 0.03f;
        internal int maxCheckDist = 10000;
        internal bool showConfigQueued = false;
        internal ConcurrentDictionary<MyVoxelBase, byte> newRoids = new ConcurrentDictionary<MyVoxelBase, byte>();
        private void Clean()
        {
            if (client)
            {
                SaveScans(true);
                controlledGrid = null;
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
                Save(Settings.Instance);
                if (hudAPI != null)
                    hudAPI.Unload();
                voxelScans.Dictionary.Clear();
                voxelScanMemory.scans.Dictionary.Clear();
                scannerTypes.Clear();
                if (registeredController)
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
                MyEntities.OnEntityCreate -= OnEntityCreate;
                newRoids.Clear();
            }
            if (server)
            {
                serverList.Clear();
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            }
            Networking?.Unregister();
            Networking = null;
        }
    }
}
