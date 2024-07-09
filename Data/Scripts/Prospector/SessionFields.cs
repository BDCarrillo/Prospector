using Digi.NetworkProtobufProspector;
using Draygo.API;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Serialization;
using VRage.Utils;
using VRage;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        HudAPIv2 hudAPI;
        internal bool client;
        internal int tick = -300;
        internal int gridMaxDisplayDist = 0;

        internal MyStringId corner = MyStringId.GetOrCompute("SharpEdge"); //Square  GizmoDrawLine  particle_laser ReflectorConeNarrow
        internal List<MyVoxelBase> obsList = new List<MyVoxelBase>();
        internal VRageRender.MyBillboard.BlendTypeEnum cornerBlend = VRageRender.MyBillboard.BlendTypeEnum.Standard;
        internal SerializableDictionary<MyVoxelBase, VoxelScan> voxelScans = new SerializableDictionary<MyVoxelBase, VoxelScan>();
        public static Dictionary<MyStringHash, ScannerConfig> scannerTypes = new Dictionary<MyStringHash, ScannerConfig>();
        internal MyCubeGrid controlledGrid;
        internal MyTuple<MyCubeBlock, ScannerConfig> currentScanner = new MyTuple<MyCubeBlock, ScannerConfig>();
        internal double currentScannerFOVLimit = 0;
        internal VoxelScanDict voxelScanMemory = new VoxelScanDict();
        internal string scannerCfg = "ScannerConfigs.cfg";
        internal MyPlanet closestPlanet;
        public static Networking Networking = new Networking(5860);
        public static ScannerConfigList serverList = new ScannerConfigList();
        public static bool rcvdSettings = false;
        internal bool registeredController = false;
        internal int clientAttempts = 0;
        internal bool server;
        internal ulong serverID;
        internal string scanDataSaveFile = "ScanData";
        internal bool planetSuppress = true;
    }
}
