using VRage.Game.Components;
using VRage.Utils;
using Digi.NetworkProtobufProspector;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        public static void ServerSendRequested(ulong playerID)
        {
            MyLog.Default.WriteLineAndConsole($"Prospector: Client requested settings");
            Networking.SendToPlayer(new PacketSettings(serverList), playerID);
        }
    }
}