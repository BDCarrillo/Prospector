using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using System;

namespace Prospector2
{
    public partial class Session
    {
        private void LoadConfigs()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(scannerCfg, typeof(ScannerConfig)))//Write config if missing
                {
                    var scannerListTemp = new List<ScannerConfig>();
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(scannerCfg, typeof(ScannerConfig));
                    var rawData = reader.ReadToEnd();
                    reader.Close();
                    scannerListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<ScannerConfig>>(rawData);
                    scannerTypes.Clear();
                    serverList.cfgList.Clear();
                    foreach (var temp in scannerListTemp)
                    {
                        serverList.cfgList.Add(temp);
                        scannerTypes.Add(temp.subTypeID, temp);
                    }
                    rcvdSettings = true;
                    Log.Line($"{modName} {scannerListTemp.Count} block configs loaded");

                }
                else
                    WriteDefaults();
            }
            catch (Exception e)
            {
                Log.Line($"{modName} Error with loading config, writing default to world folder {e}");
                WriteDefaults();
            }
        }
        private void WriteDefaults()
        {
            scannerTypes.Clear();
            serverList.cfgList.Clear();
            var defaultList = new List<ScannerConfig>();
            var largeScanner = new ScannerConfig() {
                scansPerTick = 1200,
                scanDistance = 10000,
                scanSpacing = 4,
                subTypeID = "LargeOreDetector",
                scanFOV = 15};
            defaultList.Add(largeScanner);
            var smallScanner = new ScannerConfig() {
                scansPerTick = 200,
                scanDistance = 5000,
                scanSpacing = 12,
                subTypeID = "SmallBlockOreDetector",
                scanFOV = 5};
            defaultList.Add(smallScanner);
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(scannerCfg, typeof(ScannerConfig));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(defaultList));
            writer.Close();
            foreach (var temp in defaultList)
            {
                serverList.cfgList.Add(temp);
                scannerTypes.Add(temp.subTypeID, temp);
            }
            Log.Line($"{modName} Using newly written default config");

        }
        private void LoadCustomOreTags()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(customOreTags, typeof(OreTags)))
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(customOreTags, typeof(OreTags));
                    var rawData = reader.ReadToEnd();
                    reader.Close();
                    var data = MyAPIGateway.Utilities.SerializeFromXML<List<OreTags>>(rawData);
                    foreach (var tag in data)
                        oreTagMapCustom[tag.minedName] = tag.tag;
                    if(client && server)
                        foreach (var tag in data)
                            oreTagMap[tag.minedName] = tag.tag;
                    Log.Line($"{modName} Loaded {data.Count} custom ore tags");
                }
                else
                    WriteOreDefaults();
            }
            catch (Exception e)
            {
                Log.Line($"{modName} Error with loading custom ore tags, writing default to world folder {e}");
                WriteOreDefaults();
            }
        }
        private void WriteOreDefaults()
        {
            oreTagMapCustom.Clear();
            var tempCfg = new List<OreTags>()
            {
                new OreTags() { minedName = "Element Zero", tag = "eezo" },
                new OreTags() { minedName = "Unobtanium", tag = "Uo" },
                new OreTags() { minedName = "Lynxite", tag = "Lx" },
            };
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(customOreTags, typeof(OreTags));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
        }
    }
}

