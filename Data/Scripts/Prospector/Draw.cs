using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using VRage.Game;
using VRage.Utils;
using System;
using Draygo.API;
using System.Text;

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
                        MyLog.Default.WriteLineAndConsole($"[Prospector] Draw Session or player is null");
                        controlledGrid = null;
                        return;
                    }
                    var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                    var playerPos = controlledGrid.PositionComp.WorldAABB.Center;
                    var camMat = Session.Camera.WorldMatrix;
                    var viewRay = new RayD(Session.Camera.Position, Session.Camera.WorldMatrix.Forward);
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
                                    var worldCoord = Vector3D.Transform(nextScanPos, voxel.PositionComp.WorldMatrixRef);
                                    var material = voxel.GetMaterialAt(ref worldCoord);
                                    //
                                    //var localCoord = new Vector3I(scanData.nextScanPosX, scanData.nextScanPosY, scanData.nextScanPosZ);
                                    //var material2 = voxel.Storage.GetMaterialAt(ref worldCoord);
                                    //
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
                controlledGrid = null;
                MyLog.Default.WriteLineAndConsole($"[Prospector] Error while trying to draw {e}");
            }
        }
        private void DrawOreLabel(Vector3D position, float size, Color color, VoxelScan scanData, bool inRange, bool scanning)
        {
            
            var topRightPos = position + Session.Camera.WorldMatrix.Up * size + Session.Camera.WorldMatrix.Right * size;
            var screenCoords = Session.Camera.WorldToScreen(ref topRightPos);
            var info = new StringBuilder();
            var distance = Vector3D.Distance(position, controlledGrid.PositionComp.WorldAABB.Center); //TODO pull this dist out from the normalize in the ProcessDraws method
            info.AppendLine($"  {(distance > 1000 ? (distance / 1000).ToString("0.0") + " km" : (int)distance + " m")}");
            if (scanData.scanPercent < 1) info.AppendLine($"  {Math.Round(scanData.scanPercent * 100, 0)}% {(scanning ? "Scanning" : "Scanned" )}");
            if (scanData.scanPercent < 1 && !inRange) info.AppendLine($"  {(tick % 60 <= 30 ? "Out Of Range": "")}");
            info.AppendLine($"  {scanData.scanSpacing}m Scan");
            foreach (var ore in scanData.ore.Dictionary)
            {
                if (ore.Key != "Stone")
                info.AppendLine($"  {Math.Round((double)ore.Value / scanData.foundore * 100, 2)}% {ore.Key}");
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

