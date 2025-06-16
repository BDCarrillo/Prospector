using Sandbox.ModAPI;
using VRage.Game.Components;
using System;
using VRage.Serialization;
using System.IO;
using Sandbox.Definitions;
using VRageMath;
using VRage.Voxels;
using Draygo.API;
using System.Text;


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

        private void DigiMode2()
        {
            if (boundsScan == null || boundsScan.MarkedForClose || boundsScan.Closed)
                return;
            /*
            switch (boundsScan.Storage.Size.X)
            {
                case 64:
                    LOD = 0;
                    break;
                case 128:
                    LOD = 0;
                    break;
                case 256:
                    LOD = 0;
                    break;
                case 512:
                    LOD = 0;
                    break;
                case 1024:
                    LOD = 1;
                    break;
                case 2048:
                    LOD = 2;
                    break;
            }
            */
            int LOD = 3;

            Vector3I start = boundsScan.StorageMin >> LOD;
            Vector3I end = (boundsScan.StorageMax >> LOD) - 1;
            int cellSize = (1 << LOD);
            float cellSizeHalf = cellSize * 0.5f;

            MyStorageData temp = new MyStorageData(MyStorageDataTypeFlags.ContentAndMaterial);
            temp.Resize(start, end);

            boundsScan.Storage.ReadRange(temp, MyStorageDataTypeFlags.ContentAndMaterial, LOD, start, end);

            bool bbValid = false;
            BoundingBox contentBB = BoundingBox.CreateInvalid();

            for (int i = 0; i < temp.SizeLinear; i++)
            {
                var content = temp[MyStorageDataTypeEnum.Content][i];
                
                Vector3I pos;
                temp.ComputePosition(i, out pos);
                pos += start;
                pos <<= LOD; // turn into real voxel coords

                Vector3 localPos = pos;
                localPos -= boundsScan.SizeInMetresHalf; // worldmatrix.Translation is boundingbox center, shift positions accordingly
                localPos += cellSizeHalf; // shift to center of the cell

                if (content > MyVoxelConstants.VOXEL_ISO_LEVEL)
                {
                    contentBB.Include(localPos);
                    bbValid = true;
                    var material = temp[MyStorageDataTypeEnum.Material][i];
                    var voxelDef = MyDefinitionManager.Static.GetVoxelMaterialDefinition(material);
                    if (voxelDef != null && voxelDef.MinedOre != "Stone")
                    {
                        Log.Line($"{voxelDef.MinedOre}");
                        //fun draws
                        float contentRatio = content / 255f;
                        Vector3D world = Vector3D.Transform(localPos, boundsScan.WorldMatrix);
                        float radius = cellSizeHalf * 10;
                        //MyTransparentGeometry.AddPointBillboard(solidCircle, Color.Red * 3f, world, radius, 0f, -1, MyBillboard.BlendTypeEnum.AdditiveTop);

                        var camMat = Session.Camera.WorldMatrix;
                        var ore = new HudAPIv2.SpaceMessage(new StringBuilder($"<color=255,0,0> {voxelDef.MinedOre}"), world, camMat.Up, camMat.Left, 50);
                    }
                }
            }
            
            if (bbValid)
            {
                Vector3D min = Vector3D.Transform(contentBB.Min, boundsScan.WorldMatrix);
                Vector3D worldCenter = Vector3D.Transform(contentBB.Center, boundsScan.WorldMatrix);
                var radius = Vector3D.Distance(worldCenter, min);
            }
        }

        private void ProcessBoundsDigified()
        {
            //Thanks to Digi (once again) for this
            int LOD = 6;
            switch (boundsScan.Storage.Size.X)
            {
                case 64:
                    LOD = 2;
                    break;
                case 128:
                    LOD = 3;
                    break;
                case 256:
                    LOD = 4;
                    break;
                case 512:
                    LOD = 5;
                    break;
                case 1024:
                    LOD = 6;
                    break;
                case 2048:
                    LOD = 6;
                    break;
            }
            Vector3I start = boundsScan.StorageMin >> LOD;
            Vector3I end = (boundsScan.StorageMax >> LOD) - 1;
            int cellSize = (1 << LOD);
            float cellSizeHalf = cellSize * 0.5f;

            MyStorageData temp = new MyStorageData(MyStorageDataTypeFlags.Content);
            temp.Resize(start, end);

            boundsScan.Storage.ReadRange(temp, MyStorageDataTypeFlags.Content, LOD, start, end);
            byte[] voxelContent = temp[MyStorageDataTypeEnum.Content];

            bool bbValid = false;
            BoundingBox contentBB = BoundingBox.CreateInvalid();

            for (int i = 0; i < voxelContent.Length; i++)
            {
                byte content = voxelContent[i];
                if (content > MyVoxelConstants.VOXEL_ISO_LEVEL)
                {
                    Vector3I pos;
                    temp.ComputePosition(i, out pos);
                    pos += start;
                    pos <<= LOD; // turn into real voxel coords
                    Vector3 localPos = pos;
                    localPos -= boundsScan.SizeInMetresHalf; // worldmatrix.Translation is boundingbox center, shift positions accordingly
                    localPos += cellSizeHalf; // shift to center of the cell
                    contentBB.Include(localPos);
                    bbValid = true;
                }
            }

            if (bbValid && voxelScans.Dictionary.ContainsKey(boundsScan))
            {
                Vector3D min = Vector3D.Transform(contentBB.Min, boundsScan.WorldMatrix);
                Vector3D worldCenter = Vector3D.Transform(contentBB.Center, boundsScan.WorldMatrix);
                var radius = Vector3D.Distance(worldCenter, min) * 0.75f;

                voxelScans[boundsScan].actualCenter = worldCenter;
                voxelScans[boundsScan].actualSize = (int)radius;
            }
            processingBounds = false;
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

