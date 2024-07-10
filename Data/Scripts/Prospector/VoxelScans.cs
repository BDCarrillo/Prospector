using ProtoBuf;
using VRage.Serialization;

namespace Prospector
{
    [ProtoContract]
    public class VoxelScanDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<long, VoxelScan> scans { get; set; }
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
}
