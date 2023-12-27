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

            // Altitude tracking
            DateTime[] sampleTimes;
            float[] altitudeValues;
            int historyValueCount = 500;

            public Receiver(Program program)
            {
                // Set program reference
                _program = program;

                // Set up receiver
                _program.IsReceiver = true;

                // Initialize receiver
                Initialize();
            }

            private void Initialize()
            {
                // Register message handler
                RegisterListener();

                // Initialize arrays for altitude tracking
                sampleTimes = new DateTime[historyValueCount];
                altitudeValues = new float[historyValueCount];
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

                // get custom data (name of LCD panel) which has been set like graphLCD=LCD Panel Name
                string lcdPanelGraphCustomData = _program.Me.CustomData.Split('=')[1];

                // Get LCD panel using that custom data name
                IMyTextPanel lcdPanelGraph = (IMyTextPanel)_program.GridTerminalSystem.GetBlockWithName(lcdPanelGraphCustomData);

                // record altitude but round to nearest meter
                RecordAltitude(float.Parse(data[2]), lcdPanelGraph);
            }

            void RecordAltitude(float altitude, IMyTextPanel lcdPanelGraph)
            {
                // Record altitude and time
                AddValueToArray(sampleTimes, DateTime.Now);
                AddValueToArray(altitudeValues, altitude);

                // Update graph on LCD
                DrawAltitudeGraph(lcdPanelGraph);
            }

            void AddValueToArray<T>(T[] array, T value)
            {
                Array.Copy(array, 1, array, 0, array.Length - 1);
                array[array.Length - 1] = value;
            }

            void DrawAltitudeGraph(IMyTextPanel lcdPanel)
            {
                // Ensure the LCD panel is set to write and draw mode
                lcdPanel.ContentType = ContentType.SCRIPT;
                lcdPanel.Script = "";

                // Check if the altitudeValues array has data
                if (altitudeValues == null || altitudeValues.Length == 0)
                {
                    lcdPanel.WriteText("No altitude data available.");
                    return;
                }

                // Prepare the drawing surface
                using (var frame = lcdPanel.DrawFrame())
                {
                    var viewport = new RectangleF((lcdPanel.TextureSize - lcdPanel.SurfaceSize) / 2f, lcdPanel.SurfaceSize);

                    // Calculate scale factors for the graph
                    float xScale = viewport.Width / altitudeValues.Length;
                    float yMax = altitudeValues.Max();
                    float yMin = altitudeValues.Min();
                    float yScale = (yMax - yMin) > 0 ? viewport.Height / (yMax - yMin) : 0; // Adjust scale to include yMin

                    // Draw the graph
                    for (int i = 1; i < altitudeValues.Length; i++)
                    {
                        // Calculate points for the line
                        var startPoint = new Vector2(viewport.X + (i - 1) * xScale, viewport.Y + viewport.Height - ((altitudeValues[i - 1] - yMin) * yScale));
                        var endPoint = new Vector2(viewport.X + i * xScale, viewport.Y + viewport.Height - ((altitudeValues[i] - yMin) * yScale));

                        // Draw the line
                        var line = new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "SquareSimple",
                            Position = (startPoint + endPoint) / 2,
                            Size = new Vector2((endPoint - startPoint).Length(), 1),
                            Color = Color.White,
                            RotationOrScale = (float)Math.Atan2(endPoint.Y - startPoint.Y, endPoint.X - startPoint.X)
                        };

                        frame.Add(line);
                    }
                }
            }
        }
    }
}
