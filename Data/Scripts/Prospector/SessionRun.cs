using Sandbox.ModAPI;
using VRage.Game.Components;
using Sandbox.Game.Entities;
using VRage.Utils;
using System;
using Draygo.API;
using VRage.Serialization;
using System.IO;
using Digi.NetworkProtobufProspector;
using Sandbox.Game;
using Sandbox.Definitions;
using System.Linq;


namespace Prospector2
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register(this);
            if (server)
            {
                LoadConfigs();
                LoadCustomOreTags();
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                serverName = string.Concat(Session.Name.Split(Path.GetInvalidFileNameChars()));
            }
            if (client)
            {
                InitConfig();
                if(serverName.Length > 0)
                    LoadScans();
                hudAPI = new HudAPIv2(InitMenu);
                maxCheckDist = (uint)Math.Max(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
                maxCheckDist *= maxCheckDist;
                LoadOreTags();
            }
            if (client && server)
                rcvdSettings = true; //SP workaround
        }

        public override void LoadData()
        {
            mpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            server = (mpActive && MyAPIGateway.Multiplayer.IsServer) || !mpActive;
            client = (mpActive && !MyAPIGateway.Utilities.IsDedicated) || !mpActive;
            if (client)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
            }
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
                        if (!oldPlanetSuppress && !(Settings.Instance.hideAsteroids || currentScanner == null))
                            MyAPIGateway.Utilities.ShowNotification("Prospector2 shutting down due to gravity > 0.2");
                        planetSuppress = true;
                    }
                    else
                        planetSuppress = false;

                    //Pull in new 'roid data
                    if (!Settings.Instance.hideAsteroids && currentScanner != null && currentScannerActive)
                    {
                        foreach(var objRoid in newRoids.Keys) 
                        {
                            if(voxelScans.Dictionary.ContainsKey(objRoid))
                                continue;
                            else if (voxelScanMemory.scans.Dictionary.ContainsKey(objRoid.EntityId))
                                voxelScans.Dictionary.Add(objRoid, voxelScanMemory.scans.Dictionary[objRoid.EntityId]);
                            else
                            {
                                var scan = new VoxelScan();
                                scan.size = (objRoid.StorageMax.X / currentScannerConfig.scanSpacing + 1) * (objRoid.StorageMax.Y / currentScannerConfig.scanSpacing + 1) * (objRoid.StorageMax.Z / currentScannerConfig.scanSpacing + 1);
                                scan.nextScanPosX = objRoid.StorageMin.X;
                                scan.nextScanPosY = objRoid.StorageMin.Y;
                                scan.nextScanPosZ = objRoid.StorageMin.Z;
                                scan.scanSpacing = currentScannerConfig.scanSpacing;
                                scan.ore = new SerializableDictionary<string, int>();
                                voxelScans.Dictionary.Add(objRoid, scan);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"{modName}  Well something went wrong in Update {e}");
                MyLog.Default.WriteLineAndConsole($"{modName} Well something went wrong in Update {e}");
            }
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
                        GridChange(null, Session.Player.Controller.ControlledEntity);
                        MyLog.Default.WriteLineAndConsole($"{modName} Registered controller");
                        registeredController = true;
                    }
                    catch { }
                }

                if (showConfigQueued)
                {
                    if (MyAPIGateway.Gui.ChatEntryVisible)
                    {
                        MyAPIGateway.Utilities.ShowNotification("Hit Enter to close chat and display Prospector2 block configs", 16, "Red");
                    }
                    else
                    {
                        showConfigQueued = false;
                        string d = $"Max Data Display Distance: {Math.Sqrt(maxCheckDist)} \n" +
                            $"Sync Distance: {Session.SessionSettings.SyncDistance}\n" +
                            $"View Distance: {Session.SessionSettings.ViewDistance}\n";

                        foreach (var scanner in scannerTypes)
                        {
                            var s = scanner.Value;
                            d += "Block SubType: " + s.subTypeID + "\n" +
                                "  Scan Distance: " + s.scanDistance + "m\n" +
                                "  Scan FOV: " + s.scanFOV + "\n" +
                                "  Scan Spacing: " + s.scanSpacing + "m\n" +
                                "  Scans per Tick: " + s.scansPerTick + "\n \n";
                        }
                        MyAPIGateway.Utilities.ShowMissionScreen("Prospector2 Configs", "", "", d, null, "Close");
                    }
                }
                if (tick % 300 == 0)
                    UpdateLists();
            }
        }

        protected override void UnloadData()
        {
            Clean();
            if(client)
            {
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            }
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
                    voxelScanMemory.scans.Dictionary[temp.Key.EntityId] = temp.Value;
                if (writeFile)
                {
                    TextWriter writer;
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict));
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(voxelScanMemory));
                    writer.Close();
                    MyLog.Default.WriteLineAndConsole($"{modName} Saved scan data: " + scanDataSaveFile);
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{modName} Failed to save scan data {e}");
            }
        }

        internal void LoadScans()
        {
            scanDataSaveFile = "ScanData" + serverName + ".scn";
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(scanDataSaveFile, typeof(VoxelScanDict));
                    var rawData = reader.ReadToEnd();
                    reader.Close();
                    voxelScanMemory = MyAPIGateway.Utilities.SerializeFromXML<VoxelScanDict>(rawData);
                    MyLog.Default.WriteLineAndConsole($"{modName} Loaded scan data: " + scanDataSaveFile);
                }
                else
                    MyLog.Default.WriteLineAndConsole($"{modName} No existing scan data found, will create new file on first save named: " + scanDataSaveFile);
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{modName} Failed to load scan data {e}");
                MyAPIGateway.Utilities.ShowMessage($"{modName}", $"Error loading saved info");
            }
        }      

        private void LoadOreTags()
        {
            MyLog.Default.WriteLine($"{modName} LoadOreTags started");
            foreach (var matDef in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                if (matDef == null || !matDef.CanBeHarvested || !matDef.SpawnsInAsteroids || matDef.MinedOre == null || !matDef.IsRare)
                    continue;
                if (!oreTagMap.ContainsKey(matDef.MinedOre))
                {
                    var formattedName = "";
                    //Better Stone formatting
                    if (matDef.MinedOre.Contains("(") && matDef.MinedOre.Contains(")"))
                    {
                        var trimmed = matDef.MinedOre.Remove(0, matDef.MinedOre.IndexOf('(') + 1);
                        var final = trimmed.TrimEnd(new char[] { ')', ' ' });
                        formattedName = final;
                    }
                    else if (oreDefaults.ContainsKey(matDef.MinedOre))
                        formattedName = oreDefaults[matDef.MinedOre]; 
                    else
                    {
                        MyLog.Default.WriteLine($"{modName} Asteroid spawnable ore type found without a linked shorthand for {matDef.MinedOre}");
                        formattedName = matDef.MinedOre;
                    }
                    oreTagMap.Add(matDef.MinedOre, formattedName);
                }
            }
        }
    }
}

