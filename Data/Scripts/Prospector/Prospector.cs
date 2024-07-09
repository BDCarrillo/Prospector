using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using Sandbox.Game.Entities;
using VRage.Utils;
using System;
using System.Collections.Generic;
using Draygo.API;
using VRage.Serialization;
using System.IO;
using VRage;
using Digi.NetworkProtobufProspector;
using Sandbox.Game;

namespace Prospector
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            var IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            var MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            var DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            var IsClient = !IsServer && !DedicatedServer && MpActive;
            var IsHost = IsServer && !DedicatedServer && MpActive;
            client = IsHost || IsClient || !MpActive;
            server = IsServer || IsHost || !MpActive || DedicatedServer; 
            Networking.Register();
            serverList.cfgList = new List<ScannerConfig>();
            serverID = MyAPIGateway.Multiplayer.ServerId;
            if (serverID > 0)
                scanDataSaveFile += serverID;
            scanDataSaveFile += ".scn";
            if (client)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
                InitConfig();
                if(!MpActive || IsHost) LoadConfigs();
                voxelScanMemory.scans = new SerializableDictionary<long, VoxelScan>();
                LoadScans();
                hudAPI = new HudAPIv2(InitMenu);
                try
                {
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += GridChange;
                }
                catch
                {
                    registeredController = false;
                }
                if (hudAPI == null)
                {
                    MyAPIGateway.Utilities.ShowMessage("Prospector", "TextHudAPI failed to register");
                    MyLog.Default.WriteLineAndConsole($"[Prospector] TextHudAPI failed to register");
                }
                else
                    MyAPIGateway.Utilities.ShowMessage("Prospector", "overlay options can be found by hitting enter and F2");
            }
            if (server)
            {
                LoadConfigs(true);
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            }
            if (client && server)
                rcvdSettings = true;
        }

        private void UpdateLists()
        {
            try
            {
                if ((Session?.Player?.Character == null || Session?.Player?.Controller == null) && controlledGrid != null)
                {
                    controlledGrid = null;
                    return;
                }
                var entity = Session.Player.Controller.ControlledEntity as MyCubeBlock;
                if (entity == null)
                {
                    controlledGrid = null;
                    return;
                }
                else
                {
                    controlledGrid = entity.CubeGrid;
                    if (controlledGrid.NaturalGravity.LengthSquared() > 4)
                    {
                        var oldPlanetSuppress = planetSuppress;
                        if (!oldPlanetSuppress && !(Settings.Instance.hideAsteroids || currentScanner.Item1 == null))
                            MyAPIGateway.Utilities.ShowNotification("Prospector shutting down due to gravity > 0.2");
                        planetSuppress = true;
                    }
                    else
                        planetSuppress = false;
                    var blockList = controlledGrid.GetFatBlocks();
                    foreach(var block in blockList)
                    {
                        if (scannerTypes.ContainsKey(block.BlockDefinition.Id.SubtypeId) && block.IsWorking)
                        {
                            var dispDist = scannerTypes[block.BlockDefinition.Id.SubtypeId].displayDistance;
                            if (scannerTypes[block.BlockDefinition.Id.SubtypeId].scanDistance > 0)
                            {
                                currentScanner = new MyTuple<MyCubeBlock, ScannerConfig>(block, scannerTypes[block.BlockDefinition.Id.SubtypeId]);
                                currentScannerFOVLimit = Math.Cos(MathHelper.ToRadians(currentScanner.Item2.scanFOV));
                            }
                                
                            if(gridMaxDisplayDist < dispDist)
                                gridMaxDisplayDist = dispDist;
                        }
                    }

                    var gridPos = controlledGrid.PositionComp.WorldAABB.Center;
                    if (!Settings.Instance.hideAsteroids && currentScanner.Item1 != null && currentScanner.Item1.IsWorking)
                    {
                        obsList.Clear();
                        var tempSphere = new BoundingSphereD(gridPos, 25000); //TODO tie in to world settings, lesser of sync or render
                        //TODO consider reworking to a OnEntityCreate hook, this is probably garbo for performance
                        MyGamePruningStructure.GetAllVoxelMapsInSphere(ref tempSphere, obsList);
                        ValidateList(obsList);
                    }
                }
            }
            catch (Exception e)
            {
                controlledGrid = null;
                MyLog.Default.WriteLineAndConsole($"[Prospector] Well something went wrong in Update {e}");
            }
        }

        private void ValidateList(List<MyVoxelBase> list)
        {
            SaveScans(false);
            voxelScans.Dictionary.Clear();
            bool planetFound = false;
            long planetDist = long.MaxValue;
            var gridPos = controlledGrid.PositionComp.WorldAABB.Center;
            var dispDistSqr = gridMaxDisplayDist * gridMaxDisplayDist;
            foreach (var objRoid in list)
            {
                try
                {
                    var planet = objRoid as MyPlanet;
                    var boulder = objRoid.BoulderInfo != null;
                    if (planet == null && !boulder)//Should only suppress boulders
                    {
                        if (voxelScanMemory.scans.Dictionary.ContainsKey(objRoid.EntityId))
                        {
                            voxelScans.Dictionary.Add(objRoid, voxelScanMemory.scans.Dictionary[objRoid.EntityId]);
                        }
                        else if (Vector3D.DistanceSquared(gridPos, objRoid.PositionComp.WorldAABB.Center) < dispDistSqr)
                        {
                            var scan = new VoxelScan();
                            scan.size = (objRoid.StorageMax.X / currentScanner.Item2.scanSpacing + 1) * (objRoid.StorageMax.Y / currentScanner.Item2.scanSpacing + 1) * (objRoid.StorageMax.Z / currentScanner.Item2.scanSpacing + 1);
                            scan.nextScanPosX = objRoid.StorageMin.X;
                            scan.nextScanPosY = objRoid.StorageMin.Y;
                            scan.nextScanPosZ = objRoid.StorageMin.Z;
                            scan.scanSpacing = currentScanner.Item2.scanSpacing;
                            scan.ore = new SerializableDictionary<string, int>();
                            voxelScans.Dictionary.Add(objRoid, scan);
                        }
                    }
                    else if(boulder)
                    {
                        //TODO boulder stuff?
                    }
                    else if (planet != null)
                    {
                        planetFound = true;
                        var distToPlanet = (long)Vector3D.DistanceSquared(controlledGrid.PositionComp.WorldAABB.Center, planet.PositionComp.WorldAABB.Center);
                        if (distToPlanet < planetDist)
                        {
                            planetDist = distToPlanet;
                            closestPlanet = planet;
                        }
                    }

                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Well something went wrong in Validate List {e}");
                }
            }
            if (planetFound = false && closestPlanet != null)
                closestPlanet = null;
            //Planet processing?  18,014,398,509,481,984 possible storage spaces
        }

        public override void UpdateBeforeSimulation()
        {
            if (client && tick % 300 == 0)
            {
                if (!rcvdSettings && tick > 300 && tick % 120 == 0)
                {
                    Networking.SendToServer(new RequestSettings(MyAPIGateway.Multiplayer.MyId));
                    clientAttempts++;
                    if (clientAttempts >= 5)
                    {
                        rcvdSettings = true;
                        MyAPIGateway.Utilities.ShowMessage("Prospector", "Failed to get block configs after 5 attempts, using defaults.");
                        LoadConfigs();
                    }
                }
                if (!registeredController)
                {
                    try
                    {
                        MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += GridChange;
                        registeredController = true;
                        MyLog.Default.WriteLineAndConsole($"Prospector: Registered controller");
                    }
                    catch
                    {
                        registeredController = false;
                    }
                }
                UpdateLists();
            }
        }
        public override void Draw()
        {
            if (client)
            {
                ProcessDraws();
                tick++;
            }
        }

        protected override void UnloadData()
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
                if(registeredController)
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
            }
            if(server)
            {
                serverList.Clear();
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            }
            Networking?.Unregister();
            Networking = null;
        }
        public override void SaveData()
        {
            if(client)
                SaveScans(true);
        }

        private void SaveScans(bool writeFile)
        {
            try
            {
                foreach (var temp in voxelScans.Dictionary)
                {
                    if (voxelScanMemory.scans.Dictionary.ContainsKey(temp.Key.EntityId))
                    {
                        voxelScanMemory.scans.Dictionary[temp.Key.EntityId] = temp.Value;
                    }
                    else
                    {
                        voxelScanMemory.scans.Dictionary.Add(temp.Key.EntityId, temp.Value);
                    }
                }
                if (writeFile)
                {
                    TextWriter writer;
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict));
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(voxelScanMemory));
                    writer.Close();
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Saved scan data: " + scanDataSaveFile);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"[Prospector] Failed to save scan data {e}");
            }
        }

        private void LoadScans()
        {
            try
            {

                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict));
                    voxelScanMemory = MyAPIGateway.Utilities.SerializeFromXML<VoxelScanDict>(reader.ReadToEnd());
                    reader.Close();
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Loaded scan data: " + scanDataSaveFile);

                }
                else
                    MyLog.Default.WriteLineAndConsole($"[Prospector] No existing scan data found, creating new file: " + scanDataSaveFile);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"[Prospector] Failed to load scan data {e}");
                MyAPIGateway.Utilities.ShowMessage("Prospector", $"Error loading saved info");
            }
        }        
        
    }

}

