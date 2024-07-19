using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using Prospector;

namespace Digi.NetworkProtobufProspector
{
    public class Networking
    {
        public readonly ushort ChannelId;
        public static Session session;

        public Networking(ushort channelId)
        {
            ChannelId = channelId;
        }

        public void Register(Session s)
        {
            session = s;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(ChannelId, ReceivedPacket);
        }

        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(ChannelId, ReceivedPacket);
            session = null;
        }

        private void ReceivedPacket(byte[] rawData) // executed when a packet is received on this machine
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
                HandlePacket(packet, rawData);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");
                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[Prospector ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author]", 10000, MyFontEnum.Red);
            }
        }

        private void HandlePacket(PacketBase packet, byte[] rawData = null)
        {
            var relay = packet.Received();
        }
        public void SendToPlayer(PacketSettings packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, bytes, steamId);
        }
    }

    [ProtoInclude(1000, typeof(PacketSettings))]

    [ProtoContract]
    public abstract class PacketBase
    {
        [ProtoMember(1)]
        public ulong SenderId;

        public PacketBase()
        {
            SenderId = MyAPIGateway.Multiplayer.MyId;
        }
        public abstract bool Received();
    }
    [ProtoContract]
    public partial class PacketSettings : PacketBase
    {
        [ProtoMember(2)]
        public ScannerConfigList ScannerConfig;
        [ProtoMember(3)]
        public Dictionary<string, string> OreTags;
        [ProtoMember(4)]
        public string ServerName;


        public PacketSettings() { } // Empty constructor required for deserialization

        public PacketSettings(ScannerConfigList scannerConfig, Dictionary<string, string> oreTags, string serverName)
        {
            ScannerConfig = scannerConfig;
            OreTags = oreTags;
            ServerName = serverName;
        }

        public override bool Received()
        {
            if (!MyAPIGateway.Utilities.IsDedicated) //client crap
            {
                MyLog.Default.WriteLineAndConsole($"[Prospector] Received packet");

                try
                {
                    Session.serverName = ServerName;
                    Networking.session.LoadScans();
                    if (ScannerConfig.cfgList.Count > 0)
                    {
                        Session.scannerTypes.Clear();
                        foreach (var scanner in ScannerConfig.cfgList)
                        {
                            Session.scannerTypes.Add(scanner.subTypeID, scanner);
                        }
                        Session.rcvdSettings = true;
                        MyLog.Default.WriteLineAndConsole($"[Prospector] Received {ScannerConfig.cfgList.Count} block settings from server");
                    }
                    if (OreTags.Count > 0)
                    {
                        foreach (var type in OreTags)
                        {
                            Session.oreTagMap[type.Key] = type.Value;
                        }
                        MyLog.Default.WriteLineAndConsole($"[Prospector] Received {OreTags.Count} custom ore tags from server");
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Failed to process packet {e}");
                }
            }
            return false; // relay packet to other clients (only works if server receives it)
        }
    }
}