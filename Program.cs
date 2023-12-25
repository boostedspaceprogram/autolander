﻿using Sandbox.Game.EntityComponents;
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
    partial class Program : MyGridProgram
    {
        // Script state
        enum State
        {
            IDLE,
            APPROACHING,
            LANDING,
            LANDED
        }

        // Transmitter and Receiver classes and variables
        Transmitter _transmitter = null;
        Receiver _receiver = null;
        bool IsReceiver = false;

        // Parse arguments 
        MyCommandLine _commandLine = new MyCommandLine();

        // Antennas
        List<IMyRadioAntenna> antennas = new List<IMyRadioAntenna>();

        // Controllers
        List<IMyShipController> controllers = new List<IMyShipController>();

        // Gas tanks
        List<IMyGasTank> tanks = new List<IMyGasTank>();

        // Thrusters
        List<IMyThrust> thrusters = new List<IMyThrust>();
        List<IMyThrust> thrustersUp = new List<IMyThrust>();
        List<IMyThrust> thrustersDown = new List<IMyThrust>();
        List<IMyThrust> thrustersLeft = new List<IMyThrust>();
        List<IMyThrust> thrustersRight = new List<IMyThrust>();

        IMyShipController mainController = null;
        Matrix mainControllerMatrix;
        List<IMyGyro> gyros = new List<IMyGyro>();

        double gravity = 0.0;
        double mass = 0.0;

        public Program()
        {
            //allow transmitter class to access grid
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            // Get main controller
            GridTerminalSystem.GetBlocksOfType(controllers);
            if (controllers.Count() == 0)
            {
                throw new Exception("No ship controller found");
            }

            // Get main controller (the one that can control the ship)
            mainController = controllers.Find(x => x.CanControlShip);
            if (mainController == null)
            {
                throw new Exception("No main controller found");
            }

            // Get antennas
            GridTerminalSystem.GetBlocksOfType(antennas);
            if (antennas.Count >= 1)
            {
                // Transmitter class
                _transmitter = new Transmitter(this);

                // Receiver class
                _receiver = new Receiver(this);
            } else
            {
                throw new Exception("No antennas found");
            }

            // Save orientation of controller
            mainController.Orientation.GetMatrix(out mainControllerMatrix);

            // Get gyros
            GridTerminalSystem.GetBlocksOfType(gyros);

            // Get thrusters
            updateThrusters();
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means.
            //
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // Parse arguments and set flags accordingly
            ParseArguments(argument);

            if (IsReceiver)
            {
                // Receiver
                _receiver.Receive();

            }

            // TODO: Everything in try-catch block
            if (mainController != null && !IsReceiver)
            {
                // Get gravity and mass
                gravity = (double)mainController.GetNaturalGravity().Length();
                mass = (double)mainController.CalculateShipMass().TotalMass;

                Print("Gravity: " + gravity.ToString() +
                    "\nMass: " + mass.ToString() +
                    "\nTotal thrusters: " + thrusters.Count().ToString() +
                    "\nTotal gyros: " + gyros.Count().ToString());

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Gravity: " + gravity.ToString());
                sb.AppendLine("Mass: " + mass.ToString());
                sb.AppendLine("Total thrusters: " + thrusters.Count().ToString());
                sb.AppendLine("Total gyros: " + gyros.Count().ToString());
                sb.AppendLine("Time:" + DateTime.Now);

                _transmitter.Transmit(sb.ToString());
            }
        }

        private void ParseArguments(string argument)
        {
            if (_commandLine.TryParse(argument))
            {
                if (_commandLine.ArgumentCount > 0)
                {
                    switch (_commandLine.Argument(0))
                    {
                        case "receiver":
                            IsReceiver = true;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void updateThrusters()
        {
            GridTerminalSystem.GetBlocksOfType(thrusters);

            thrusters.ForEach(thruster =>
            {
                Matrix fromThrusterToGrid;
                thruster.Orientation.GetMatrix(out fromThrusterToGrid);

                Vector3 direction = Vector3.Transform(fromThrusterToGrid.Backward, mainControllerMatrix);

                if (direction == mainControllerMatrix.Down)
                {
                    thrustersDown.Add(thruster);
                }
                else if (direction == mainControllerMatrix.Up)
                {
                    thrustersUp.Add(thruster);
                }
                else if (direction == mainControllerMatrix.Left)
                {
                    thrustersLeft.Add(thruster);
                }
                else if (direction == mainControllerMatrix.Right)
                {
                    thrustersRight.Add(thruster);
                }
            });
        }

        private void Print(String message)
        {
            IMyTextSurface mesurface0 = Me.GetSurface(0);
            mesurface0.ContentType = ContentType.TEXT_AND_IMAGE;
            mesurface0.FontSize = 2;
            mesurface0.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
            mesurface0.WriteText(message);
        }
    }
}
