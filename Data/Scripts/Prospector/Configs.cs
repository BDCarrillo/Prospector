using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;
using System;

namespace Prospector
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
                    scannerListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<ScannerConfig>>(reader.ReadToEnd());
                    reader.Close();
                    scannerTypes.Clear();
                    serverList.cfgList.Clear();
                    foreach (var temp in scannerListTemp)
                    {
                        serverList.cfgList.Add(temp);
                        scannerTypes.Add(MyStringHash.GetOrCompute(temp.subTypeID), temp);
                    }
                    rcvdSettings = true;
                }
                else
                {
                    WriteDefaults();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"[Prospector] Error with loading config, writing default to world folder {e}");
                WriteDefaults();
            }
        }
        private void WriteDefaults()
        {
            scannerTypes.Clear();
            serverList.cfgList.Clear();
            var defaultList = new List<ScannerConfig>();
            var largeScanner = new ScannerConfig() {
                scansPerTick = 400,
                scanDistance = 10000,
                scanSpacing = 8,
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
                scannerTypes.Add(MyStringHash.GetOrCompute(temp.subTypeID), temp);
            }
        }
        private void LoadCustomOreTags()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(customOreTags, typeof(OreTags)))//Write config if missing
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(customOreTags, typeof(OreTags));
                    var data = MyAPIGateway.Utilities.SerializeFromXML<List<OreTags>>(reader.ReadToEnd());
                    reader.Close();
                    foreach (var tag in data)
                        oreTagMapCustom[tag.minedName] = tag.tag;
                    if(client && server)
                        foreach (var tag in data)
                            oreTagMap[tag.minedName] = tag.tag;
                    MyLog.Default.WriteLineAndConsole($"[Prospector] Loaded {data.Count} custom ore tags");
                }
                else
                {
                    WriteOreDefaults();
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"[Prospector] Error with loading custom ore tags, writing default to world folder {e}");
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

