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
            //[----------------------------------]
            //[----- BEGIN CUSTOMIZABLE CODE -----]
            //[----------------------------------]

            const string TRANSMITTER_TAG = "Autolander"; // Transmitter tag (must match receiver's TRANSMITTER_TAG)

            //[--------------------------------]
            //[----- END CUSTOMIZABLE CODE -----]
            //[--------------------------------]

            // Program reference (for access to GridTerminalSystem, Echo, etc.)
            Program _program;

            // Broadcast listener
            IMyBroadcastListener _broadcastListener;

            public Receiver(Program program)
            {
                // Set program reference
                _program = program;
                _program.Echo("[BSP-Autolander]: Receiver initialized (" + TRANSMITTER_TAG + ")");

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
                _broadcastListener = _program.IGC.RegisterBroadcastListener(TRANSMITTER_TAG);
                _broadcastListener.SetMessageCallback(TRANSMITTER_TAG);
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
                    var message = _broadcastListener.AcceptMessage();

                    // Check if message has correct broadcast tag (abort if not)
                    if (message.Tag != TRANSMITTER_TAG) continue;

                    // Parse message
                    _program.Print(message.Data.ToString());
                    _program.Echo(message.Data.ToString());
                }
            }

        }
    }
}
