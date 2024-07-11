using Digi.NetworkProtobufProspector;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.Entity;
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

        private void OnEntityCreate(MyEntity entity)
        {
            if(entity is MyVoxelBase && !(entity is MyPlanet))
            {
                var roid = entity as MyVoxelBase;
                if (roid.BoulderInfo != null)
                    return;

                roid.RemovedFromScene += Roid_RemovedFromScene;
                newRoids.TryAdd(roid, 0);
            }
        }

        private void Roid_RemovedFromScene(MyEntity obj)
        {
            var roid = obj as MyVoxelBase;
            roid.RemovedFromScene -= Roid_RemovedFromScene;

            //If it was never pulled into the actively scanned list but exited sync
            byte val;
            newRoids.TryRemove(roid, out val);
            
            //If it was actively tracked, update storage
            if(voxelScans.Dictionary.ContainsKey(roid))
            {
                var scan = voxelScans.Dictionary[roid];
                if (voxelScanMemory.scans.Dictionary.ContainsKey(roid.EntityId))
                    voxelScanMemory.scans.Dictionary[roid.EntityId] = scan;
                else
                    voxelScanMemory.scans.Dictionary.Add(roid.EntityId, scan);
                voxelScans.Dictionary.Remove(roid);
            }
        }

        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            var newBlock = newEnt as IMyCubeBlock;
            var newGrid = newBlock?.CubeGrid;
            var oldBlock = previousEnt as IMyCubeBlock;
            var oldGrid = oldBlock?.CubeGrid;

            if (newEnt is IMyCharacter)
            {
                controlledGrid = null;
                SaveScans(false);
                voxelScans.Dictionary.Clear();
            }
            else if (newGrid != null)
            {
                controlledGrid = (MyCubeGrid)newGrid;
                if(newGrid != oldGrid)
                {
                    SaveScans(false);
                    voxelScans.Dictionary.Clear();
                }
            }
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
