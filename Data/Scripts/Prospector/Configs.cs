using Sandbox.ModAPI;
using System.Collections.Generic;
using ProtoBuf;
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
                WriteDefaults(true);
            }
        }

        private void WriteDefaults(bool error = false)
        {
            scannerTypes.Clear();
            serverList.cfgList.Clear();
            var defaultList = new List<ScannerConfig>();
            var largeScanner = new ScannerConfig() {
                scansPerTick = 400,
                scanDistance = 10000,
                scanSpacing = 8,
                displayDistance = 10000,
                subTypeID = "LargeOreDetector",
                scanFOV = 15};
            defaultList.Add(largeScanner);

            var smallScanner = new ScannerConfig() {
                scansPerTick = 200,
                scanDistance = 5000,
                scanSpacing = 12,
                displayDistance = 5000,
                subTypeID = "SmallBlockOreDetector",
                scanFOV = 5};
            defaultList.Add(smallScanner);

            var largeDish = new ScannerConfig() {
                scansPerTick = 0, 
                scanDistance = 0, 
                scanSpacing = 100, 
                displayDistance = 25000, 
                subTypeID = "LargeBlockRadioAntennaDish", 
                scanFOV = 45};
            defaultList.Add(largeDish);

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
    }
}

