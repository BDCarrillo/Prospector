using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using VRage.Utils;
using System;
using Draygo.API;
using System.Text;
using VRage.Game.ModAPI;
using System.Collections.Generic;
using Sandbox.Game.Entities;

namespace Prospector2
{
    public partial class Session : MySessionComponentBase
    {
        public override void Draw()
        {
            tick++;
            if (client)
                try
                {
                    var s = Settings.Instance;
                    var sessBool = Session.Camera == null || Session.Player == null;
                    var scannerBool = currentScanner == null || !currentScanner.IsFunctional || !currentScanner.Enabled || !currentScannerActive;
                    if (sessBool || scannerBool || s.hideAsteroids || !hudAPI.Heartbeat || planetSuppress) return;
                    if (controlledGrid != null && !controlledGrid.MarkedForClose && !controlledGrid.IsStatic)
                    {
                        var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix * 1.1;
                        var camMat = Session.Camera.WorldMatrix;
                        var FirstPersonView = Session.IsCameraControlledObject && Session.CameraController.IsInFirstPersonView;
                        var entBlock = Session.Player.Controller.ControlledEntity.Entity as IMyCubeBlock;
                        var scanCenter = controlledGrid.GridIntegerToWorld(entBlock.Position + entBlock.PositionComp.LocalMatrixRef.Up * entBlock.CubeGrid.LocalVolume.Radius * 0.5f / controlledGrid.GridSize);                        
                        Vector3D scanCenterScreenCoords = FirstPersonView ? Vector3D.Zero : Vector3D.Transform(scanCenter, viewProjectionMat);
                        scanRing.Offset = new Vector2D(scanCenterScreenCoords.X, scanCenterScreenCoords.Y);
                        var viewRay = FirstPersonView ? new RayD(Session.Camera.Position, Session.Camera.WorldMatrix.Forward) :
                            new RayD(Session.Camera.Position, Vector3D.Normalize(scanCenter - Session.Camera.Position));

                        if (expandedMode)
                        {
                            if (scanLine.Offset.X >= ctrOffset * 2) //Hit end, reset
                            {
                                scanLine.Offset = Vector2D.Zero;
                                scanLine.Visible = false;
                            }
                            else if (scanLine.Visible)
                                scanLine.Offset = new Vector2D(scanLine.Offset.X + 0.004f, 0);
                            else if (!scanLine.Visible && tick % 600 == 0)
                                scanLine.Visible = true;

                            var topRightDraw = new Vector2D(ctrOffset, ctrOffset);
                            var foundOre = 0;
                            var volume = 0d;
                            double minDist = double.MaxValue;
                            double maxDist = 0;
                            var rollupList = new Dictionary<string, int>();
                            int count = 0;
                            Vector3D totalPos = Vector3D.Zero;
                            KeyValuePair<MyVoxelBase, VoxelScan> lastFound = new KeyValuePair<MyVoxelBase, VoxelScan>();
                            foreach (var keyValuePair in voxelScans.Dictionary)
                            {
                                var voxel = keyValuePair.Key;
                                var scanData = keyValuePair.Value;
                                var position = voxel.PositionComp.WorldAABB.Center;
                                var screenCoords = Vector3D.Transform(position, viewProjectionMat);
                                var offscreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1.000001;
                                var dist = Vector3D.DistanceSquared(position, controlledGrid.PositionComp.WorldAABB.Center);
                                if (offscreen || dist > maxCheckDist) continue;
                                var drawColor = scanData.scanPercent == 1 ? s.finishedColor : scanData.scanPercent == 0 ? s.obsColor : s.scanColor;
                                if (screenCoords.X < ctrOffset && screenCoords.X > -ctrOffset && screenCoords.Y < ctrOffset && screenCoords.Y > -ctrOffset)
                                {
                                    var ctrSymbolObj = new HudAPIv2.BillBoardHUDMessage(solidCircle, new Vector2D(screenCoords.X, screenCoords.Y), drawColor, Width: symbolWidth * .75f, Height: symbolHeight * .75f, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                                    if (queueReScan)
                                        ResetData(ref scanData, ref voxel);
                                    else
                                    {
                                        totalPos += position;
                                        count++;
                                        if (dist < minDist)
                                            minDist = dist;
                                        if (dist > maxDist)
                                            maxDist = dist;

                                        foundOre += scanData.foundore;
                                        volume += scanData.foundore * scanData.scanSpacing * scanData.scanSpacing * scanData.scanSpacing / 1000000d;
                                        foreach (var ore in scanData.ore.Dictionary)
                                        {
                                            if (rollupList.ContainsKey(ore.Key))
                                                rollupList[ore.Key] += ore.Value;
                                            else
                                                rollupList[ore.Key] = ore.Value;
                                        }
                                        lastFound = keyValuePair;
                                    }
                                }
                                else
                                {
                                    var ctrSymbolObj = new HudAPIv2.BillBoardHUDMessage(hollowCircle, new Vector2D(screenCoords.X, screenCoords.Y), drawColor, Width: symbolWidth * 1.25f, Height: symbolHeight * 1.25f, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                                    var ctrSymbolObj2 = new HudAPIv2.BillBoardHUDMessage(hollowCircle, new Vector2D(screenCoords.X, screenCoords.Y), drawColor, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2, Rotation: 0, HideHud: true, Shadowing: true);
                                }
                            }
                            var textList = new List<string>();
                            if (rollupList.Count > 0)
                            {
                                foreach (var ore in rollupList)
                                {
                                    var amount = Math.Round((double)ore.Value / foundOre * 100, 2);
                                    var info = $"  {ore.Key} {(amount > 0.00d ? amount + " %" : "- Trace")}";
                                    textList.Add(info);
                                }
                            }
                            var finalText = new StringBuilder();
                            var stringVol = volume.ToString("0.00") + " km^3";
                            if (textList.Count > 0)
                            {
                                textList.Sort();
                                finalText.AppendLine($"  {stringVol}");
                                foreach (var entry in textList)
                                    finalText.Append(entry + "\n");
                                message.Message = finalText;
                            }
                            else
                                finalText.Append("  No Data\n");

                            if (maxDist != 0)
                            {
                                var min = Math.Sqrt(minDist);
                                var max = Math.Sqrt(maxDist);
                                //TODO Consider reworking to distance to center of cluster with +/- from there
                                finalText.AppendLine($"  {(min > 1000 ? (min / 1000).ToString("0.0") + " km" : (int)min + " m")} {(min != max ? (max > 1000 ? "- " + (max / 1000).ToString("0.0") + " km" : "- " + (int)max + " m") : "")}");
                            }
                            message.Message = finalText;
                            if (maxDist != 0 && queueGPSTag)
                            {
                                if (count == 1)
                                    GPSTagSingle(lastFound.Key.PositionComp.WorldAABB.Center, lastFound.Value, lastFound.Key.EntityId);
                                else
                                {
                                    var pos = totalPos / count;
                                    var dispersion = Math.Sqrt(maxDist) - Math.Sqrt(minDist);
                                    GPSTagMultiple(textList, count, dispersion, pos, rollupList, stringVol);
                                }
                            }
                        }
                        else
                        {
                            scanRing2.Scale += 0.005f;
                            if (scanRing2.Scale >= 2.5)
                                scanRing2.Scale = 1;

                            foreach (var keyValuePair in voxelScans.Dictionary)
                            {
                                var voxel = keyValuePair.Key;
                                var scanData = keyValuePair.Value;
                                var position = voxel.PositionComp.WorldAABB.Center;
                                var screenCoords = Vector3D.Transform(position, viewProjectionMat);
                                var offscreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1.000001;
                                var dist = Vector3D.DistanceSquared(position, controlledGrid.PositionComp.WorldAABB.Center);
                                if (offscreen || dist > maxCheckDist) continue;

                                var obsSize = voxel.PositionComp.LocalVolume.Radius;
                                obsSize *= 0.5f; //Since 'roid LocalVolumes can be massive.  Unsure if there's a more accurate source of size or center point of actual voxel material                                
                                var topRightScreen = Vector3D.Transform(position + camMat.Up * obsSize + camMat.Right * obsSize, viewProjectionMat);
                                var inScanRange = dist <= currentScannerConfig.scanDistance * currentScannerConfig.scanDistance;

                                bool scanning = false;
                                if (inScanRange && Vector3D.Dot(Session.Camera.WorldMatrix.Forward, Vector3D.Normalize(position - Session.Camera.Position)) >= currentScannerFOVLimit)
                                {
                                    scanning = true;
                                    if (currentScannerConfig.scanSpacing < scanData.scanSpacing) //Reset data to use a more precise scanner
                                        ResetData(ref scanData, ref voxel);
                                    var offset = voxel.PositionComp.WorldVolume.Center - voxel.PositionLeftBottomCorner; //Pushes the storage checks to bottom left corner as all storage is positive, world matrix refs center
                                    if (scanData.scanPercent < 1)
                                    {
                                        for (int i = 0; i < currentScannerConfig.scansPerTick; i++) //Iterate spaces and check for ore
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
                                                if (material.IsRare)
                                                {
                                                    if (!scanData.ore.Dictionary.ContainsKey(material.MinedOre))
                                                        scanData.ore.Dictionary.Add(material.MinedOre, 1);
                                                    else
                                                        scanData.ore[material.MinedOre]++;
                                                }
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
                                if (s.enableSymbols)
                                    if (scanData.scanPercent == 1)
                                        DrawFrame(topRightScreen, screenCoords, s.finishedColor.ToVector4());
                                    else if (scanning)
                                    {
                                        if ((tick + 15) % 60 <= 20)
                                            DrawFrame(topRightScreen, screenCoords, s.scanColor.ToVector4());
                                    }
                                    else if (inScanRange)
                                        DrawFrame(topRightScreen, screenCoords, s.scanColor.ToVector4());
                                    else
                                        DrawFrame(topRightScreen, screenCoords, s.obsColor.ToVector4());
                                if (queueReScan && viewRay.Intersects(voxel.PositionComp.WorldAABB) != null)
                                    ResetData(ref scanData, ref voxel);

                                if (s.enableLabels && (viewRay.Intersects(voxel.PositionComp.WorldAABB) != null))
                                {
                                    if (queueGPSTag)
                                        GPSTagSingle(position, scanData, voxel.EntityId);
                                    if (scanData.scanPercent == 1)
                                        DrawOreLabel(position, obsSize, s.finishedColor, scanData, true, false, dist);
                                    else if (inScanRange)
                                        DrawOreLabel(position, obsSize, s.scanColor, scanData, inScanRange, scanning, dist);
                                    else
                                        DrawOreLabel(position, obsSize, s.obsColor, scanData, inScanRange, false, dist);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowNotification($"{modName} Draw exception {e}");
                    MyLog.Default.WriteLineAndConsole($"{modName} Error while trying to draw {e}");
                }
            queueReScan = false;
            queueGPSTag = false;
        }

        private void ResetData(ref VoxelScan scanData, ref MyVoxelBase voxel)
        {
            scanData.foundore = 0;
            scanData.nextScanPosX = 0;
            scanData.nextScanPosY = 0;
            scanData.nextScanPosZ = 0;
            scanData.ore.Dictionary.Clear();
            scanData.scanned = 0;
            scanData.scanPercent = 0;
            scanData.scanSpacing = currentScannerConfig.scanSpacing;
            scanData.size = (voxel.StorageMax.X / currentScannerConfig.scanSpacing + 1) * (voxel.StorageMax.Y / currentScannerConfig.scanSpacing + 1) * (voxel.StorageMax.Z / currentScannerConfig.scanSpacing + 1);
        }
        private void GPSTagSingle(Vector3D position, VoxelScan scanData, long id)
        {
            var idStr = id.ToString();
            var gpsName = "A-" + idStr.Substring(idStr.Length - 4, 4);
            var gpsPos = position;
            var info = "";
            foreach (var ore in scanData.ore.Dictionary)
            {
                var amount = Math.Round((double)ore.Value / scanData.foundore * 100, 2);
                var text = amount > 0.00d ? amount + " %" : "Trace";
                info += $"{text} {ore.Key}\n";
                gpsName += " " + oreTagMap[ore.Key];
            }
            var volume = (scanData.foundore * scanData.scanSpacing * scanData.scanSpacing * scanData.scanSpacing / 1000000d).ToString("0.00") + " km^3";
            if (Settings.Instance.gpsIncludeVol) gpsName += " " + volume;
            info += $"Scanned material: {volume}";
            var gps = MyAPIGateway.Session.GPS.Create(gpsName, info, gpsPos, true);
            MyAPIGateway.Session.GPS.AddGps(Session.Player.IdentityId, gps);
        }
        private void GPSTagMultiple(List<string> ores, int count, double dispersion, Vector3D position, Dictionary<string, int> rollup, string volume)
        {
            //This goofy naming ID thing should give repeatable results for the same cluster.  Is that necessary? Probably not.
            var x = ((int)position.X).ToString();
            var y = ((int)position.Y).ToString();
            var z = ((int)position.Z).ToString();
            var a = count.ToString();
            var gpsName = "C-" + x.Substring(x.Length - 1, 1) + y.Substring(y.Length - 1, 1) + z.Substring(z.Length -1, 1) + a.Substring(a.Length -1, 1);
            var info = "Cluster of " + count + " asteroids\n";
            foreach (var ore in ores)
                info += ore + "\n";
            foreach(var ore in rollup.Keys)
            {
                gpsName += " " + oreTagMap[ore];
            }
            info += (dispersion > 1000 ? (dispersion / 1000).ToString("0.0") + " km" : (int)dispersion + " m") + " dispersion\n";
            if (Settings.Instance.gpsIncludeVol) gpsName += " " + volume;
            info += $"Scanned material: {volume}"; 
            var gps = MyAPIGateway.Session.GPS.Create(gpsName, info, position, true);
            MyAPIGateway.Session.GPS.AddGps(Session.Player.IdentityId, gps);
        }
        private void DrawOreLabel(Vector3D position, float size, Color color, VoxelScan scanData, bool inRange, bool scanning, double distSqr)
        {           
            var topRightPos = position + Session.Camera.WorldMatrix.Up * size + Session.Camera.WorldMatrix.Right * size;
            var screenCoords = Session.Camera.WorldToScreen(ref topRightPos);
            var info = new StringBuilder();
            var distance = Math.Sqrt(distSqr);
            info.AppendLine($"  {(distance > 1000 ? (distance / 1000).ToString("0.0") + " km" : (int)distance + " m")}");
            if (scanData.scanPercent < 1) info.AppendLine($"  {Math.Round(scanData.scanPercent * 100, 0)}% {(scanning ? "Scanning" : "Scanned" )}");
            if (scanData.scanPercent < 1 && !inRange) info.AppendLine($"  {(tick % 60 <= 30 ? "Out Of Range": "")}");
            info.AppendLine($"  {scanData.scanSpacing}m Scan");
            if (scanData.scanPercent == 1)
            {
                var volume = scanData.foundore * scanData.scanSpacing * scanData.scanSpacing * scanData.scanSpacing / 1000000d;
                info.AppendLine($"  {volume.ToString("0.00")} km^3");
            }
            foreach (var ore in scanData.ore.Dictionary)
            {
                var amount = Math.Round((double)ore.Value / scanData.foundore * 100, 2);
                var text = amount > 0.00d ? amount + " %" : "Trace";
                info.AppendLine($"  {text} {ore.Key}");
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

