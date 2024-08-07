using Digi.NetworkProtobufProspector;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Prospector2
{
    public partial class Session : MySessionComponentBase
    {
        private void OnEntityCreate(MyEntity entity)
        {
            if(entity is MyVoxelBase && !(entity is MyPlanet))
            {
                var roid = entity as MyVoxelBase;
                if (roid.BoulderInfo != null)
                    return;
                
                roid.OnMarkForClose += Roid_OnMarkForClose;
                newRoids.TryAdd(roid, 0);
            }
            else if (entity is IMyOreDetector && !controlInit)
            {
                controlInit = true;
                CreateTerminalControls<IMyOreDetector>();
            }
        }

        private void Roid_OnMarkForClose(MyEntity obj)
        {
            var roid = obj as MyVoxelBase;
            roid.OnMarkForClose -= Roid_OnMarkForClose;

            //If it was never pulled into the actively scanned list but exited sync
            byte val;
            newRoids.TryRemove(roid, out val);

            //If it was actively tracked, update storage and pull from active tracking
            if (voxelScans.Dictionary.ContainsKey(roid))
            {
                var scan = voxelScans.Dictionary[roid];
                if (voxelScanMemory.scans.Dictionary.ContainsKey(roid.EntityId))
                    voxelScanMemory.scans.Dictionary[roid.EntityId] = scan;
                else
                    voxelScanMemory.scans.Dictionary.Add(roid.EntityId, scan);
                voxelScans.Dictionary.Remove(roid);
            }
        }

        private void Detector_OnMarkForClose(IMyEntity obj)
        {
            var scanner = obj as IMyOreDetector;
            scanner.OnMarkForClose -= Detector_OnMarkForClose;
            //TODO Verify
            SaveScans(false);
            voxelScans.Dictionary.Clear();
            HudCycleVisibility(false);
            expandedMode = false;
            scanRing.Visible = false;
            currentScanner = null;
            currentScannerActive = false;
            currentScannerConfig = null;
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
                HudCycleVisibility(false);
                expandedMode = false;
                scanRing.Visible = false;
                if(currentScanner != null)
                    currentScanner.OnMarkForClose -= Detector_OnMarkForClose;
                currentScanner = null;
                currentScannerActive = false;
                currentScannerConfig = null;
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
            var steamId = MyAPIGateway.Players.TryGetSteamId(id);
            Networking.SendToPlayer(new PacketSettings(serverList, oreTagMapCustom, serverName), steamId);
            MyLog.Default.WriteLineAndConsole($"{modName} Player connected, settings sent to {steamId}");
        }
        private void OreDetector_IsWorkingChanged(IMyCubeBlock obj)
        {
            var detector = (IMyOreDetector)obj;
            if (!detector.IsWorking || !detector.Enabled)
            {
                expandedMode = false;
                HudCycleVisibility(expandedMode);
                scanRing.Visible = false;
                currentScannerActive = false;
            }
        }
    }
}
