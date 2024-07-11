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
using System.Reflection;
using VRage.Game.Entity;
using VRage.Voxels;
using VRage.ModAPI;
using VRage.Game;
using System.Runtime.CompilerServices;

namespace Prospector
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
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
                voxelScanMemory.scans = new SerializableDictionary<long, VoxelScan>();
                LoadScans();
                hudAPI = new HudAPIv2(InitMenu);
                maxCheckDist = (int)(Math.Max(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance) * 0.95f);
            }
            if (server)
            {
                LoadConfigs();
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            }
            if (client && server)
                rcvdSettings = true;
        }

        public override void LoadData()
        {
            mpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            server = (mpActive && MyAPIGateway.Multiplayer.IsServer) || !mpActive;
            client = (mpActive && !MyAPIGateway.Multiplayer.IsServer) || !mpActive;
            if (client)
                MyEntities.OnEntityCreate += OnEntityCreate;
        }
        private void UpdateLists()
        {
            try
            {
                if (controlledGrid != null)
                { 
                    //Check if in grav (mostly to suppress symbology through the planet)
                    if (controlledGrid.NaturalGravity.LengthSquared() > 4)
                    {
                        var oldPlanetSuppress = planetSuppress;
                        if (!oldPlanetSuppress && !(Settings.Instance.hideAsteroids || currentScanner.Item1 == null))
                            MyAPIGateway.Utilities.ShowNotification("Prospector shutting down due to gravity > 0.2");
                        planetSuppress = true;
                    }
                    else
                        planetSuppress = false;

                    //Update active scanner
                    //TODO this is crap for multiple scanners on a grid
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
                    gridMaxDisplayDistSqr = gridMaxDisplayDist * gridMaxDisplayDist;

                    var gridPos = controlledGrid.PositionComp.WorldAABB.Center;
                    if (!Settings.Instance.hideAsteroids && currentScanner.Item1 != null && currentScanner.Item1.IsWorking)
                    {
                        foreach(var objRoid in newRoids.Keys) 
                        {
                            if(voxelScans.Dictionary.ContainsKey(objRoid))
                            {
                                continue;
                            }
                            else if (voxelScanMemory.scans.Dictionary.ContainsKey(objRoid.EntityId))
                            {
                                voxelScans.Dictionary.Add(objRoid, voxelScanMemory.scans.Dictionary[objRoid.EntityId]);
                            }
                            else if (Vector3D.DistanceSquared(gridPos, objRoid.PositionComp.WorldAABB.Center) < gridMaxDisplayDistSqr)
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
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[Prospector]  Well something went wrong in Update {e}");
                MyLog.Default.WriteLineAndConsole($"[Prospector] Well something went wrong in Update {e}");
            }
        }

        private void ValidateList()
        {
            //SaveScans(false);
            //var gridPos = controlledGrid.PositionComp.WorldAABB.Center;
            //var dispDistSqr = gridMaxDisplayDist * gridMaxDisplayDist;
            //foreach (var objRoid in newRoids)
            //{
                //return;
                /*
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
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Well something went wrong in Validate List {e}");
                }
                */
            //}
        }

        public override void UpdateBeforeSimulation()
        {
            if (client)
            {
                if (symbolHeight == 0)
                {
                    aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                    symbolHeight = symbolWidth * aspectRatio;
                }

                if (!registeredController)
                {
                    try
                    {
                        Session.Player.Controller.ControlledEntityChanged += GridChange;
                        registeredController = true;
                        GridChange(null, Session.Player.Controller.ControlledEntity);
                        MyLog.Default.WriteLineAndConsole($"Prospector: Registered controller");
                    }
                    catch
                    {
                        registeredController = false;
                    }
                }

                if (showConfigQueued)
                {
                    if (MyAPIGateway.Gui.ChatEntryVisible)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Hit Enter to close chat and display Prospector block configs", 16, "Red");
                    }
                    else
                    {
                        showConfigQueued = false;
                        string d = "";
                        foreach (var scanner in scannerTypes)
                        {
                            var s = scanner.Value;
                            d += "Block SubType: " + s.subTypeID + "\n" +
                                "  Display Distance:" + s.displayDistance + "m\n" +
                                "  Scan Distance:" + s.scanDistance + "m\n" +
                                "  Scan FOV:" + s.scanFOV + "\n" +
                                "  Scan Spacing:" + s.scanSpacing + "m\n" +
                                "  Scans per Tick:" + s.scansPerTick + "\n \n";
                        }
                        MyAPIGateway.Utilities.ShowMissionScreen("Prospector Configs", "", "", d, null, "Close");
                    }
                }

                if (tick % 300 == 0)
                {
                    UpdateLists();
                }
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
            Clean();
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
                        voxelScanMemory.scans.Dictionary[temp.Key.EntityId] = temp.Value;
                    else
                        voxelScanMemory.scans.Dictionary.Add(temp.Key.EntityId, temp.Value);
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

