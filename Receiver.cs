using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class Receiver
        {
            // Program reference (for access to GridTerminalSystem, Echo, etc.)
            Program _program;

            // Broadcast listener
            IMyBroadcastListener _broadcastListener;

            public Receiver(Program program)
            {
                // Set program reference
                _program = program;

                // Initialize receiver
                Initialize();
            }

            private void Initialize()
            {
                // Register message handler
                RegisterListener();
            }

            private void RegisterListener()
            {
                // Register message handler
                _broadcastListener = _program.IGC.RegisterBroadcastListener(_program.TRANSMIT_RECEIVER_TAG);
                _broadcastListener.SetMessageCallback(_program.TRANSMIT_RECEIVER_TAG);
            }

            // <summary>
            // Listen for new messages from transmitter
            // Also prints data to console and LCD panel
            // </summary> 
            public void Receive()
            {
                // Check for new messages
                while (_broadcastListener.HasPendingMessage)
                {
                    // Get message
                    var acceptedMessage = _broadcastListener.AcceptMessage();

                    // Check if message has correct broadcast tag (abort if not)
                    if (acceptedMessage.Tag != _program.TRANSMIT_RECEIVER_TAG) continue;

                    // Parse message
                    CSVParser(acceptedMessage.Data.ToString());
                }
            }

            private void CSVParser(string message)
            {
                // message format:
                // counter
                // status
                // altitude
                // velocity
                // acceleration
                // gravity
                // thrust
                // batterypower
                // tankcapacity
                // mass

                // Split message into array of strings
                string[] data = message.Split(',');

                // String builder for printing to console
                StringBuilder sb = new StringBuilder();

                sb.Append("Counter:" + data[0]);
                sb.Append("\nStatus:" + data[1]);
                sb.Append("\nAltitude:" + data[2]);
                sb.Append("\nVelocity:" + data[3]);
                sb.Append("\nAcceleration:" + data[4]);
                sb.Append("\nGravity:" + data[5]);
                sb.Append("\nThrust:" + data[6]);
                sb.Append("\nBattery Power:" + data[7]);
                sb.Append("\nTank Capacity:" + data[8]);
                sb.Append("\nMass:" + data[9]);

                // get custom data (name of LCD panel)
                string lcdPanelGraphCustomData = _program.Me.CustomData;

                // Get LCD panel using that custom data name
                IMyTextPanel lcdPanelGraph = (IMyTextPanel)_program.GridTerminalSystem.GetBlockWithName(lcdPanelGraphCustomData);

                // Print data to LCD panel
                lcdPanelGraph.WritePublicTitle("BSP-Autolander");
                lcdPanelGraph.WriteText(sb.ToString());
            }
        }
    }
}
