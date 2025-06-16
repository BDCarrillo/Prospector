using Sandbox.ModAPI;
using VRage.Game.Components;
using System;
using VRage.Serialization;
using System.IO;
using Sandbox.Definitions;
using VRageMath;
using ParallelTasks;
using VRage.Voxels;
using Sandbox.Game.Entities;


namespace Prospector2
{
    public partial class Session : MySessionComponentBase
    {
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
                        foreach (var objRoid in newRoids.Keys) 
                        {
                            if (voxelScans.Dictionary.ContainsKey(objRoid))
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
                Log.Line($"{modName} Well something went wrong in Update {e}");
            }
        }

        private void ProcessBounds()
        {
            if (BGTask.valid && BGTask.Exceptions != null)
                TaskHasErrors(ref BGTask, "BGTask");

            var voxel = boundsScan;
            var start = Vector3I.Zero;
            var end = new Vector3I(31, 31, 31);
            int LOD = 6;
            var mult = boundsScan.Storage.Size.X / 32;

            switch (boundsScan.Storage.Size.X)
            {
                case 64:
                    LOD = 1;
                    break;
                case 128:
                    LOD = 2;
                    break;
                case 256:
                    LOD = 3;
                    break;
                case 512:
                    LOD = 4;
                    break;
                case 1024:
                    LOD = 5;
                    break;
                case 2048:
                    LOD = 6;
                    break;
            }

            MyStorageData temp = new MyStorageData(MyStorageDataTypeFlags.Content);
            temp.Resize(start, end);

            boundsScan.Storage.ReadRange(temp, MyStorageDataTypeFlags.Content, LOD, start, end);
            Vector3I pos;

            var sumPos = new Vector3D();
            int count = 0;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = 0;
            int maxY = 0;
            int maxZ = 0;

            for (int i = 0; i < temp.SizeLinear; i++)
            {
                var content = temp[0][i];
                if (content > 127)
                {
                    temp.ComputePosition(i, out pos);
                    sumPos += pos;
                    count++;
                    if (pos.X < minX)
                        minX = pos.X;
                    else if (pos.X > maxX)
                        maxX = pos.X;
                    if (pos.Y < minY)
                        minY = pos.Y;
                    else if (pos.Y > maxY)
                        maxY = pos.Y;
                    if (pos.Z < minZ)
                        minZ = pos.Z;
                    else if (pos.Z > maxZ)
                        maxZ = pos.Z;
                }
            }
            var lower = Vector3D.Transform(new Vector3D(minX, minY, minZ) * mult - boundsScan.PositionComp.LocalAABB.HalfExtents, boundsScan.PositionComp.WorldMatrixRef);
            var upper = Vector3D.Transform(new Vector3D(maxX, maxY, maxZ) * mult - boundsScan.PositionComp.LocalAABB.HalfExtents, boundsScan.PositionComp.WorldMatrixRef);

            if (voxelScans.Dictionary.ContainsKey(boundsScan))
            {
                voxelScans[boundsScan].actualCenter = new Vector3D((int)((lower.X + upper.X) / 2), (int)((lower.Y + upper.Y) / 2), (int)((lower.Z + upper.Z) / 2));
                voxelScans[boundsScan].actualSize = (int)(Vector3D.Distance(upper, lower) * 0.35f); //Scaled down from 0.5f for better visual fit
            }
            processingBounds = false;
        }

        public bool TaskHasErrors(ref Task task, string taskName)
        {
            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (var e in task.Exceptions)
                {
                    Log.Line($"{modName} {taskName} thread!\n{e}");
                }
                return true;
            }
            return false;
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
                    Log.Line($"{modName} Saved scan data: " + scanDataSaveFile);
                }
            }
            catch (Exception e)
            {
                Log.Line($"{modName} Failed to save scan data {e}");
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
                    Log.Line($"{modName} Loaded scan data: " + scanDataSaveFile);
                }
                else
                    Log.Line($"{modName} No existing scan data found, will create new file on first save named: " + scanDataSaveFile);
            }
            catch (Exception e)
            {
                Log.Line($"{modName} Failed to load scan data {e}");
                MyAPIGateway.Utilities.ShowMessage($"{modName}", $"Error loading saved info");
            }
        }      

        private void LoadOreTags()
        {
            Log.Line($"{modName} LoadOreTags started");
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
                        Log.Line($"{modName} Asteroid spawnable ore type found without a linked shorthand for {matDef.MinedOre}");
                        formattedName = matDef.MinedOre;
                    }
                    oreTagMap.Add(matDef.MinedOre, formattedName);
                }
            }
        }
    }
}

