using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Utils;

namespace Prospector2
{
    public partial class Session
    {
        private List<IMyTerminalControl> ProspectorControls = new List<IMyTerminalControl>();
        internal readonly HashSet<IMyTerminalAction> ProspectorActions = new HashSet<IMyTerminalAction>();

        internal void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions)
        {
            if (!(block is IMyOreDetector))
                return;
        }
        internal void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if (!(block is IMyOreDetector))
                return;

            foreach (var newControl in ProspectorControls)
                controls.Add(newControl);
        }
        internal void CreateTerminalControls<T>() where T : IMyOreDetector
        {
            ProspectorControls.Add(Separator<T>());
            ProspectorControls.Add(AddOnOff<T>("scannerOnOff", "Active Scanning", "", "On", "Off", GetActivated, SetActivated, VisibleTrue, VisibleTrue));
            ProspectorActions.Add(CreateActiveScanAction<T>());
            ProspectorActions.Add(CreateDataViewAction<T>());
            ProspectorActions.Add(CreateResetScanAction<T>());
            ProspectorActions.Add(CreateGPSTagAction<T>());

            foreach (var action in ProspectorActions)
                MyAPIGateway.TerminalControls.AddAction<T>(action);
        }

        internal IMyTerminalControlSeparator Separator<T>() where T : IMyOreDetector
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>("Prospector_Separator");
            c.Enabled = IsTrue;
            c.Visible = IsTrue;
            return c;
        }
        internal bool IsTrue(IMyTerminalBlock block)
        {
            return true;
        }
        public static bool VisibleTrue(IMyTerminalBlock block)
        {
            return scannerTypes.ContainsKey(block.BlockDefinition.SubtypeId);
        }
        internal bool GetActivated(IMyTerminalBlock block)
        {
            return block == currentScanner && currentScannerActive;
        }
        internal void SetActivated(IMyTerminalBlock block, bool activated)
        {
            UpdateScannerState(block);
            return;
        }
        private bool CheckMode(IMyTerminalBlock block)
        {
            return block == currentScanner && currentScannerActive;
        }
        internal static IMyTerminalControlOnOffSwitch AddOnOff<T>(string name, string title, string tooltip, string onText, string offText, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter, Func<IMyTerminalBlock, bool> enabledGetter = null, Func<IMyTerminalBlock, bool> visibleGetter = null) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>(name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Tooltip = MyStringId.GetOrCompute(tooltip);
            c.OnText = MyStringId.GetOrCompute(onText);
            c.OffText = MyStringId.GetOrCompute(offText);
            c.Enabled = enabledGetter;
            c.Visible = visibleGetter;
            c.Getter = getter;
            c.Setter = setter;
            return c;
        }

        internal IMyTerminalAction CreateDataViewAction<T>() where T : IMyOreDetector
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("DataView");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder("Toggle data review mode");
            action.Action = ToggleDataView;
            action.Writer = DataViewActionWriter;
            action.Enabled = VisibleTrue;
            return action;
        }
        internal void DataViewActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Data View");
        }
        internal void ToggleDataView(IMyTerminalBlock block)
        {
            if (block == currentScanner && currentScannerActive)
            {
                expandedMode = !expandedMode;
                HudCycleVisibility(expandedMode);
            }
        }
        internal IMyTerminalAction CreateResetScanAction<T>() where T : IMyOreDetector
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("ResetScan");
            action.Icon = @"Textures\GUI\Icons\Actions\Reset.dds";
            action.Name = new StringBuilder("Reset scan data for asteroids at aim point");
            action.Action = ResetScanData;
            action.Writer = ResetScanActionWriter;
            action.Enabled = VisibleTrue;
            return action;
        }
        internal void ResetScanActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Re-Scan");
        }
        internal void ResetScanData(IMyTerminalBlock block)
        {
            queueReScan = true;
        }
        internal IMyTerminalAction CreateActiveScanAction<T>() where T : IMyOreDetector
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("ActiveScanning");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder("Toggle active scanning");
            action.Action = ToggleActiveScan;
            action.Writer = ActiveScanActionWriter;
            action.Enabled = VisibleTrue;
            return action;
        }
        internal void ActiveScanActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Scan " + (block == currentScanner && currentScannerActive ? "On" : "Off"));
        }
        internal void ToggleActiveScan(IMyTerminalBlock block)
        {
            UpdateScannerState(block);
        }
        internal IMyTerminalAction CreateGPSTagAction<T>() where T : IMyOreDetector
        {
            var action = MyAPIGateway.TerminalControls.CreateAction<T>("GPSTag");
            action.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
            action.Name = new StringBuilder("Add GPS for asteroid at aim point");
            action.Action = GPSTagAction;
            action.Writer = GPSTagActionWriter;
            action.Enabled = VisibleTrue;
            return action;
        }
        internal void GPSTagActionWriter(IMyTerminalBlock block, StringBuilder builder)
        {
            builder.Append("Mark GPS");
        }
        internal void GPSTagAction(IMyTerminalBlock block)
        {
            queueGPSTag = true;
        }
        internal void UpdateScannerState(IMyTerminalBlock block)
        {
            if (currentScanner != block)
            {
                var detector = (IMyOreDetector)block;
                currentScanner = detector;
                currentScannerConfig = scannerTypes[block.BlockDefinition.SubtypeId];
                detector.IsWorkingChanged += OreDetector_IsWorkingChanged;
                detector.OnMarkForClose += Detector_OnMarkForClose;
                //Calc FOV limit to something usable for DOT comparison
                currentScannerFOVLimit = Math.Cos(currentScannerConfig.scanFOV * 0.01745329251);
            }
            currentScannerActive = !currentScannerActive;
            if (!currentScannerActive)
            {
                expandedMode = false;
                HudCycleVisibility(expandedMode);
            }
            scanRing.Visible = currentScannerActive;
            scanRing2.Visible = currentScannerActive;
            UpdateLists();
        }
    }
}
