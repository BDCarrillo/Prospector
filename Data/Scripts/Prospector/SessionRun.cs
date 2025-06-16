using Sandbox.ModAPI;
using VRage.Game.Components;
using Sandbox.Game.Entities;
using System;
using Draygo.API;
using System.IO;
using Digi.NetworkProtobufProspector;
using Sandbox.Game;
using VRageMath;


namespace Prospector2
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Log.InitLogs();
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
                        Log.Line($"{modName} Registered controller");
                        registeredController = true;
                    }
                    catch { }
                }

                if (!hudObjectsRegistered && hudAPI.Heartbeat)
                    HudRegisterObjects();

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

                //Background task to figure actual bounds/center
                if (!processingBounds)
                {
                    foreach (var scan in voxelScans.Dictionary)
                    {
                        if (scan.Value.actualCenter == Vector3D.Zero && scan.Key.InScene)
                        {
                            boundsScan = scan.Key;
                            processingBounds = true; 
                            //DigiMode2();
                            BGTask = MyAPIGateway.Parallel.StartBackground(ProcessBoundsDigified);
                            break;
                        }
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            if (client)
            {
                //SaveScans(true);
                if (hudAPI != null)
                    hudAPI.Unload();

                if (registeredController)
                    MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
                MyEntities.OnEntityCreate -= OnEntityCreate;
                if (!BGTask.IsComplete)
                    BGTask.Wait();
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
                MyAPIGateway.TerminalControls.CustomActionGetter -= CustomActionGetter;
            }
            if (server)
            {
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            }
            Networking?.Unregister();
            Networking = null;

            //Data purge
            scannerTypes.Clear();
            serverList.cfgList.Clear();
            oreTagMap.Clear();
            newRoids.Clear();
            voxelScans.Dictionary.Clear();
            currentScanner = null;
            voxelScanMemory.scans.Dictionary.Clear();
            controlledGrid = null; 
            Log.Close();
        }
        public override void SaveData()
        {
            if (client)
                SaveScans(true);
        }
    }
}

