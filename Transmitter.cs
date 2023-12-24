using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class Transmitter
        {
            //[----------------------------------]
            //[----- BEGIN CUSTOMIZABLE CODE -----]
            //[----------------------------------]

            const bool TRANSMIT = true; // Set to false to disable transmission
            const string RECEIVER_TAG = "Autolander"; // Receiver tag (must match transmitter's TRANSMITTER_TAG)

            //[--------------------------------]
            //[----- END CUSTOMIZABLE CODE -----]
            //[--------------------------------]

            // Program reference (for access to GridTerminalSystem, Echo, etc.)
            Program _program;

            public Transmitter(Program program)
            {
                // Set program reference
                _program = program;
                _program.Echo("[BSP-Autolander]: Transmitter initialized (" + RECEIVER_TAG + ")");
            }

            // <summary>
            // Transmit data to receiver (if enabled) 
            // Also prints data to console and LCD panel
            // </summary>
            public void Transmit(string message)
            {
                if (!TRANSMIT) return; // Abort if transmission is disabled

                // Transmit message
                _program.IGC.SendBroadcastMessage(RECEIVER_TAG, message);
            }
        }
    }
}
