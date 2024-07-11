using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using VRage.Utils;
using System;
using Draygo.API;
using System.Text;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics;

namespace Prospector
{
    public partial class Session : MySessionComponentBase
    {
        private void ProcessDraws()
        {
            try
            {
                var s = Settings.Instance;
                if (s.hideAsteroids || currentScanner.Item1 == null || !currentScanner.Item1.IsWorking) return;
                if (controlledGrid != null && !controlledGrid.MarkedForClose && !controlledGrid.IsStatic && !planetSuppress)
                {
                    if (Session == null || Session.Player == null)
                    {
                        MyAPIGateway.Utilities.ShowNotification($"[Prospector] Draw Session or player is null");
                        MyLog.Default.WriteLineAndConsole($"[Prospector] Draw Session or player is null");
                        return;
                    }
                    var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                    var playerPos = controlledGrid.PositionComp.WorldAABB.Center;
                    var camMat = Session.Camera.WorldMatrix;
                    var PlayerCamera = MyAPIGateway.Session.IsCameraControlledObject;
                    var cameraController = MyAPIGateway.Session.CameraController;
                    var FirstPersonView = PlayerCamera && cameraController.IsInFirstPersonView;
                    var entBlock = Session.Player.Controller.ControlledEntity.Entity as IMyCubeBlock;
                    var crossHairPos = controlledGrid.GridIntegerToWorld(entBlock.Position + entBlock.PositionComp.LocalMatrixRef.Forward * 1000 / controlledGrid.GridSize);

                    var viewRay = FirstPersonView ? new RayD(Session.Camera.Position, Session.Camera.WorldMatrix.Forward) :
                        new RayD(Session.Camera.Position, Vector3D.Normalize(crossHairPos - Session.Camera.Position));


                    if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.LeftShift))
                    {
                        //TODO onscreen label "Data Review - Scanner Paused" or similar
                        var ctrOffset = 0.25;
                        var sizeMult = 0.75f;
                        var topRightDraw = new Vector2D(ctrOffset, ctrOffset);
                        var topLeftDraw = new Vector2D(-ctrOffset, ctrOffset);
                        var botRightDraw = new Vector2D(ctrOffset, -ctrOffset);
                        var botLeftDraw = new Vector2D(-ctrOffset, -ctrOffset);
                        var topLeftSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, topLeftDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                        var topRightSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, topRightDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, TimeToLive: 2, Rotation: 1.5708f, HideHud: true, Shadowing: true);
                        var botRightSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, botRightDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, TimeToLive: 2, Rotation: 3.14159f, HideHud: true, Shadowing: true);
                        var botLeftSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, botLeftDraw, s.expandedColor, Width: symbolWidth * sizeMult, Height: symbolHeight * sizeMult, TimeToLive: 2, Rotation: -1.5708f, HideHud: true, Shadowing: true);
                        var inbox = 0;
                        var foundOre = 0;

                        var rollupList = new Dictionary<string, int>();
                        foreach (var keyValuePair in voxelScans.Dictionary)
                        {
                            var voxel = keyValuePair.Key;
                            var scanData = keyValuePair.Value;
                            var position = voxel.PositionComp.WorldAABB.Center;
                            var screenCoords = Vector3D.Transform(position, viewProjectionMat);
                            var offscreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;
                            if (offscreen) continue;

                            if (screenCoords.X < ctrOffset && screenCoords.X > -ctrOffset && screenCoords.Y < ctrOffset && screenCoords.Y > -ctrOffset)
                            {
                                var ctrSymbolObj = new HudAPIv2.BillBoardHUDMessage(solidCircle, new Vector2D(screenCoords.X, screenCoords.Y), s.expandedColor, Width: symbolWidth*.75f, Height: symbolHeight*.75f, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                                inbox++;
                                foundOre += scanData.foundore;
                                foreach(var ore in scanData.ore.Dictionary)
                                {
                                    if (ore.Key == "Stone")
                                        continue;
                                    if (rollupList.ContainsKey(ore.Key))
                                        rollupList[ore.Key] += ore.Value;
                                    else
                                        rollupList[ore.Key] = ore.Value;
                                }
                            }
                            else
                            {
                                var ctrSymbolObj = new HudAPIv2.BillBoardHUDMessage(hollowCircle, new Vector2D(screenCoords.X, screenCoords.Y), s.expandedColor, Width: symbolWidth*1.5f, Height: symbolHeight*1.5f, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                            }
                        }
                        var textList = new List<string>();
                        if(rollupList.Count > 0)
                        {
                            foreach(var ore in rollupList)
                            {
                                var amount = Math.Round((double)ore.Value / foundOre * 100, 2);
                                var info = $"  {ore.Key} {(amount > 0.00d ? amount + " %" : "- Trace")}";
                                textList.Add(info);
                            }
                        }

                        textList.Sort();
                        var finalText = new StringBuilder();
                        foreach(var entry in textList)
                            finalText.Append(entry + "\n");

                        var label = new HudAPIv2.HUDMessage(finalText, topRightDraw, new Vector2D(.01, .025), 2, 1, true, true);
                        label.InitialColor = s.expandedColor;
                        label.Visible = true;
                    }
                    else

                    foreach (var keyValuePair in voxelScans.Dictionary)
                    {
                        var voxel = keyValuePair.Key;
                        var scanData = keyValuePair.Value;
                        var position = voxel.PositionComp.WorldAABB.Center;
                        var screenCoords = Vector3D.Transform(position, viewProjectionMat);
                        var offscreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;
                        if (offscreen) continue;

                        var obsSize = voxel.PositionComp.LocalVolume.Radius;
                        obsSize *= 0.5f; //Since 'roid LocalVolumes can be massive.  Unsure if there's a more accurate source of size or center point of actual voxel material                                
                        var topRightScreen = Vector3D.Transform(position + camMat.Up * obsSize + camMat.Right * obsSize, viewProjectionMat);
                        var inScanRange = Vector3D.DistanceSquared(position, controlledGrid.PositionComp.WorldAABB.Center) <= currentScanner.Item2.scanDistance * currentScanner.Item2.scanDistance;

                        bool scanning = false;
                        if (inScanRange && Vector3D.Dot(Session.Camera.WorldMatrix.Forward, Vector3D.Normalize(position - controlledGrid.PositionComp.WorldAABB.Center)) >= currentScannerFOVLimit)//viewRay.Intersects(voxel.PositionComp.WorldAABB) != null)
                        {
                            scanning = true;
                            if (currentScanner.Item2.scanSpacing < scanData.scanSpacing) //Reset data to use a more precise scanner
                            {
                                //TODO rework reset function
                                scanData.foundore = 0;
                                scanData.nextScanPosX = 0;
                                scanData.nextScanPosY = 0;
                                scanData.nextScanPosZ = 0;
                                scanData.ore.Dictionary.Clear();
                                scanData.scanned = 0;
                                scanData.scanPercent = 0;
                                scanData.scanSpacing = currentScanner.Item2.scanSpacing;
                                scanData.size = (voxel.StorageMax.X / currentScanner.Item2.scanSpacing + 1) * (voxel.StorageMax.Y / currentScanner.Item2.scanSpacing + 1) * (voxel.StorageMax.Z / currentScanner.Item2.scanSpacing + 1);
                            }
                            var offset = voxel.PositionComp.WorldVolume.Center - voxel.PositionLeftBottomCorner; //Pushes the storage checks to bottom left corner as all storage is positive, world matrix refs center
                            if (scanData.scanPercent < 1)
                            {
                                for (int i = 0; i < currentScanner.Item2.scansPerTick; i++) //Iterate spaces and check for ore
                                {
                                    var nextScanPos = new Vector3D(scanData.nextScanPosX, scanData.nextScanPosY, scanData.nextScanPosZ);
                                    if ((Vector3I)nextScanPos == voxel.StorageMax)
                                    {
                                        scanData.scanPercent = 1;
                                        break;
                                    }
                                    
                                    var worldCoord = Vector3D.Transform(nextScanPos, voxel.PositionComp.WorldMatrixRef) - offset;
                                    var material = voxel.GetMaterialAt(ref worldCoord);
                                    if (material != null && material.MinedOre != null)
                                    {
                                        if (!scanData.ore.Dictionary.ContainsKey(material.MinedOre))
                                            scanData.ore.Dictionary.Add(material.MinedOre, 1);
                                        else
                                            scanData.ore[material.MinedOre]++;
                                        scanData.foundore++;
                                    }
                                    scanData.scanned++;
                                    scanData.scanPercent = (float)scanData.scanned / scanData.size;
                                    if (scanData.nextScanPosX + scanData.scanSpacing <= voxel.StorageMax.X)
                                        scanData.nextScanPosX += scanData.scanSpacing;
                                    else
                                    {
                                        scanData.nextScanPosX = 0;
                                        if (scanData.nextScanPosY + scanData.scanSpacing <= voxel.StorageMax.Y)
                                            scanData.nextScanPosY += scanData.scanSpacing;
                                        else
                                        {
                                            scanData.nextScanPosY = 0;
                                            if (scanData.nextScanPosZ + scanData.scanSpacing <= voxel.StorageMax.Z)
                                                scanData.nextScanPosZ += scanData.scanSpacing;
                                        }
                                    }
                                }
                            }                            
                        }
                        if (s.enableSymbols && !offscreen && hudAPI.Heartbeat)
                            if (scanData.scanPercent == 1)
                                DrawFrame(topRightScreen, screenCoords, s.finishedColor.ToVector4());
                            else if (scanning)
                            {
                                if((tick + 15) % 60 <= 20)
                                    DrawFrame(topRightScreen, screenCoords, s.scanColor.ToVector4());
                            }
                            else if (inScanRange)
                                DrawFrame(topRightScreen, screenCoords, s.scanColor.ToVector4());
                            else
                                DrawFrame(topRightScreen, screenCoords, s.obsColor.ToVector4());
                        if (s.enableLabels && hudAPI.Heartbeat && (MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Alt) || viewRay.Intersects(voxel.PositionComp.WorldAABB) != null))
                            if(scanData.scanPercent == 1)
                                DrawOreLabel(position, obsSize, s.finishedColor, scanData, true, false);
                            else if (inScanRange)
                                DrawOreLabel(position, obsSize, s.scanColor, scanData, inScanRange, scanning);
                            else
                                DrawOreLabel(position, obsSize, s.obsColor, scanData, inScanRange, false);                       
                    }
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification($"[Prospector] Draw exception");

                MyLog.Default.WriteLineAndConsole($"[Prospector] Error while trying to draw {e}");
            }
        }
        private void DrawOreLabel(Vector3D position, float size, Color color, VoxelScan scanData, bool inRange, bool scanning)
        {
            
            var topRightPos = position + Session.Camera.WorldMatrix.Up * size + Session.Camera.WorldMatrix.Right * size;
            var screenCoords = Session.Camera.WorldToScreen(ref topRightPos);
            var info = new StringBuilder();
            var distance = Vector3D.Distance(position, controlledGrid.PositionComp.WorldAABB.Center);
            info.AppendLine($"  {(distance > 1000 ? (distance / 1000).ToString("0.0") + " km" : (int)distance + " m")}");
            if (scanData.scanPercent < 1) info.AppendLine($"  {Math.Round(scanData.scanPercent * 100, 0)}% {(scanning ? "Scanning" : "Scanned" )}");
            if (scanData.scanPercent < 1 && !inRange) info.AppendLine($"  {(tick % 60 <= 30 ? "Out Of Range": "")}");
            info.AppendLine($"  {scanData.scanSpacing}m Scan");
            foreach (var ore in scanData.ore.Dictionary)
            {
                if (ore.Key != "Stone")
                {
                    var amount = Math.Round((double)ore.Value / scanData.foundore * 100, 2);
                    var text = amount > 0.00d ? amount + " %" : "Trace";
                    info.AppendLine($"  {text} {ore.Key}");
                }
            }
            var labelposition = new Vector2D(screenCoords.X, screenCoords.Y);
            var shadowOffset = new Vector2D(0.003, -0.003);
            var shadow = new HudAPIv2.HUDMessage(info, labelposition, shadowOffset, 2, 1, true); //Delete shadow?  Is it worth displaying?
            shadow.InitialColor = Color.Black;
            shadow.Visible = true;
            var label = new HudAPIv2.HUDMessage(info, labelposition, null, 2, 1, true);
            label.InitialColor = color;
            label.Visible = true;
        }

        private void DrawFrame(Vector3D topRight, Vector3D center, Vector4 color)
        {
            var offsetX = topRight.X - center.X;
            if (offsetX > symbolWidth * 0.55f)
            {
                var offsetY = topRight.Y - center.Y;
                var symHalfX = symbolWidth * 0.25f;
                var symHalfY = symbolHeight * 0.25f;
                var topRightDraw = new Vector2D(topRight.X - symHalfX, topRight.Y - symHalfY);
                var topLeftDraw = new Vector2D(center.X - offsetX + symHalfX, center.Y + offsetY - symHalfY);
                var botRightDraw = new Vector2D(center.X + offsetX - symHalfX, center.Y - offsetY + symHalfY);
                var botLeftDraw = new Vector2D(center.X - offsetX + symHalfX, center.Y - offsetY + symHalfY);

                var topLeftSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, topLeftDraw, color, Width: symbolWidth * 0.5f, Height: symbolHeight * 0.5f, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                var topRightSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, topRightDraw, color, Width: symbolWidth * 0.5f, Height: symbolHeight * 0.5f, TimeToLive: 2, Rotation: 1.5708f, HideHud: true, Shadowing: true);
                var botRightSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, botRightDraw, color, Width: symbolWidth * 0.5f, Height: symbolHeight * 0.5f, TimeToLive: 2, Rotation: 3.14159f, HideHud: true, Shadowing: true);
                var botLeftSymbolObj = new HudAPIv2.BillBoardHUDMessage(frameCorner, botLeftDraw, color, Width: symbolWidth * 0.5f, Height: symbolHeight * 0.5f, TimeToLive: 2, Rotation: -1.5708f, HideHud: true, Shadowing: true);
            }
            else
            {
                var ctrSymbolObj = new HudAPIv2.BillBoardHUDMessage(missileOutline, new Vector2D(center.X, center.Y), color, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
            }
        }
    }
}

