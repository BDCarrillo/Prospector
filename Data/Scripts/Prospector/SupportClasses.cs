using ProtoBuf;
using System.Collections.Generic;
using VRage.Serialization;

namespace Prospector
{
    [ProtoContract]
    public class VoxelScanDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<long, VoxelScan> scans { get; set; }
        [ProtoMember(2)]
        public SerializableDictionary<string, string> oreTags { get; set; }
    }

    [ProtoContract]
    public class VoxelScan
    {
        [ProtoMember(1)]
        public float scanPercent { get; set; }
        [ProtoMember(2)]
        public SerializableDictionary<string, int> ore { get; set; }
        [ProtoMember(3)]
        public int scanned { get; set; }
        [ProtoMember(4)]
        public int foundore { get; set; }
        [ProtoMember(5)]
        public int size { get; set; }
        [ProtoMember(6)]
        public int scanSpacing { get; set; }
        [ProtoMember(7)]
        public int nextScanPosX { get; set; }
        [ProtoMember(8)]
        public int nextScanPosY { get; set; }
        [ProtoMember(9)]
        public int nextScanPosZ { get; set; }
    }

    [ProtoContract]
    public class ScannerConfigList
    {
        [ProtoMember(1)]
        public List<ScannerConfig> cfgList { get; set; }
    }

    [ProtoContract]
    public class ScannerConfig
    {
        [ProtoMember(1)]
        public int scansPerTick { get; set; }
        [ProtoMember(2)]
        public int scanDistance { get; set; }
        [ProtoMember(3)]
        public int scanSpacing { get; set; }
        [ProtoMember(4)]
        public string subTypeID { get; set; }
        [ProtoMember(5)]
        public int scanFOV { get; set; }
    }

    [ProtoContract]
    public class OreTags
    {
        [ProtoMember(1)]
        public string minedName { get; set; }
        [ProtoMember(2)]
        public string tag { get; set; }
    }
}
