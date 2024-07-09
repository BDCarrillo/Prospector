using Digi.NetworkProtobufProspector;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        private void OnMessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            messageText.ToLower();
            if (messageText == "/prospector")
            {
                MyAPIGateway.Utilities.ShowMessage("Prospector", "Options can be found in the Mod Settings Menu.  Press F2 with a chat line open and it should appear in the top left of your screen.  '/prospector reset' to restore defaults or '/prospector hide' to show/hide HUD elements");
                sendToOthers = false;
            }
            if (messageText == "/prospector hide")
            {
                Settings.Instance.hideAsteroids = !Settings.Instance.hideAsteroids;
                if (Settings.Instance.hideAsteroids)
                    MyAPIGateway.Utilities.ShowNotification("Prospector hidden, re-enable with '/prospector hide' or '/prospector show' again");
                else
                    MyAPIGateway.Utilities.ShowNotification("Prospector visible");
                sendToOthers = false;
            }
            if (messageText == "/prospector show")
            {
                Settings.Instance.hideAsteroids = !Settings.Instance.hideAsteroids;
                if (Settings.Instance.hideAsteroids)
                    MyAPIGateway.Utilities.ShowNotification("Prospector hidden, re-enable with '/prospector hide' or '/prospector show' again");
                else
                    MyAPIGateway.Utilities.ShowNotification("Prospector visible");
                sendToOthers = false;
            }
            if (messageText == "/prospector reset")
            {
                MyAPIGateway.Utilities.ShowMessage("Prospector", "Options reset to default");
                Settings.Instance = Settings.Default;
                sendToOthers = false;
            }
            return;
        }

        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            var newGrid = newEnt?.Entity?.GetTopMostParent() as MyCubeGrid;
            controlledGrid = newGrid != null ? newGrid : null;
            obsList.Clear();
            SaveScans(false);
            voxelScans.Dictionary.Clear();
            UpdateLists();
        }
        private void PlayerConnected(long id)
        {
            var playerList = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(playerList);
            foreach (var player in playerList)
            {
                if (player.IdentityId == id && !player.IsBot)
                {
                    var steamId = MyAPIGateway.Players.TryGetSteamId(id);
                    Networking.SendToPlayer(new PacketSettings(serverList), steamId);
                    MyLog.Default.WriteLineAndConsole($"Prospector: Sent settings to player " + steamId + serverList.cfgList.Count);
                    return;
                }
            }
        }
    }
}
