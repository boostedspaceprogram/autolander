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
    partial class Program : MyGridProgram
    {
        // Script state
        bool IsTransmitting = false;
        bool IsReceiver = false;
        bool IsLanding = false;

        // Transmitter and Receiver classes and variables
        Transmitter _transmitter = null;
        Receiver _receiver = null;

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
        IMyProgrammableBlock sas = null;

        double gravity = 0.0;
        double mass = 0.0;
        double altitude = 0.0;
        double velocity = 0.0;
        double maxThrust = 0.0;
        bool thrustersOn = false;

        int counter = 0;

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

            // Get antennas (for transmitter and receiver)
            GridTerminalSystem.GetBlocksOfType(antennas);
            if (antennas.Count() == 0)
            {
                throw new Exception("No antennas found");
            }
            else
            {
                // Transmitter class
                _transmitter = new Transmitter(this);

                // Receiver class
                _receiver = new Receiver(this);
            }

            // Save orientation of controller
            mainController.Orientation.GetMatrix(out mainControllerMatrix);

            // Get gyros
            GridTerminalSystem.GetBlocksOfType(gyros);
            if (gyros.Count() == 0)
            {
                throw new Exception("No gyros found");
            }

            // Get thrusters
            setThrusters();

            // Get SAS
            sas = GridTerminalSystem.GetBlockWithName("SAS") as IMyProgrammableBlock;
            if (sas == null)
            {
                throw new Exception("No SAS found");
            }
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
            counter++;
            // Parse arguments and set flags accordingly
            ParseArguments(argument);

            if (IsReceiver)
            {
                // Receiver
                _receiver.Receive();
            }

            if (IsTransmitting)
            {
                // String Builder for all data to be transmitted, csv format (time, gravity)
                StringBuilder sb = new StringBuilder();

                // Counter
                sb.Append(counter.ToString());
                sb.Append("\n");

                // Gravity in m/s^2
                sb.Append(mainController.GetNaturalGravity().Length());
                sb.Append("\n");

                // Mass in kg
                sb.Append(mainController.CalculateShipMass().TotalMass);
                sb.Append("\n");

                // Altitude in m
                double altitude = 0.0;
                mainController.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
                sb.Append(altitude);
                sb.Append("\n");

                // Velocity in m/s
                sb.Append(mainController.GetShipVelocities().LinearVelocity.Length());
                sb.Append("\n");

                // Max thrust in N
                sb.Append(thrustersUp.Sum(thruster => thruster.MaxEffectiveThrust));
                sb.Append("\n");

                // Transmitter
                _transmitter.Transmit(sb.ToString());
            }

            if (IsLanding)
            {
                LandShip();
            }
        }

        private void ParseArguments(string argument)
        {
            if (_commandLine.TryParse(argument))
            {
                if (_commandLine.ArgumentCount > 0 || _commandLine.Switches.Count() > 0)
                {
                    // Check if (-transmitter) switch is present to enable transmitter
                    IsTransmitting = _commandLine.Switch("transmitter");

                    // Parse script arguments (if any)
                    switch (_commandLine.Argument(0))
                    {
                        case "receiver":
                            IsReceiver = true;
                            break;
                        case "land":
                            IsLanding = true;
                            sas.TryRun("retro");
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void setThrusters()
        {
            GridTerminalSystem.GetBlocksOfType(thrusters);
            if (thrusters.Count() == 0)
            {
                throw new Exception("No thrusters found");
            }

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
            mesurface0.WriteText(message);
        }

        private void LandShip()
        {
            // Get gravity, mass, altitude, velocity, and maximum thrust
            gravity = (double)mainController.GetNaturalGravity().Length();
            mass = (double)mainController.CalculateShipMass().TotalMass;
            mainController.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
            velocity = (double)mainController.GetShipVelocities().LinearVelocity.Length();
            maxThrust = thrustersUp.Sum(thruster => thruster.MaxEffectiveThrust);

            // Subtract the sensor offset from the altitude
            double sensorOffset = 4.0; // Altitude sensor offset in meters
            altitude -= sensorOffset;

            // Calculate the force of gravity
            double gravitationalForce = mass * gravity;

            // Calculate the maximum possible deceleration (thrust - gravitational force)
            double maxDeceleration = (maxThrust - gravitationalForce) / mass;

            // Calculate the distance needed to stop from the current velocity and deceleration
            double stoppingDistance = Math.Pow(velocity, 2) / (2 * maxDeceleration);

            // Add a safety margin to the stopping distance to account for delays in thrust application
            double safetyMargin = 0.7; // 0% safety margin
            double adjustedStoppingDistance = stoppingDistance * safetyMargin;

            Echo("Gravitanional force: " + gravitationalForce.ToString());
            Echo("maxDeceleration: " + maxDeceleration.ToString());
            Echo("stoppingDistance: " + stoppingDistance.ToString());
            Echo("adjustedStoppingDistance: " + adjustedStoppingDistance.ToString());
            Echo("altitude: " + altitude.ToString());
            Echo("velocity: " + velocity.ToString());
            Echo("maxThrust: " + maxThrust.ToString());

            // Start deceleration burn at the adjusted stopping distance
            if (altitude <= adjustedStoppingDistance && altitude >= 50)
            {
                Echo("MAX THRUST");
                // Set the thrust to the maximum available to initiate deceleration
                SetThrust(maxThrust);
            } else
            {
                // If the rocket is not within the adjusted stopping distance, cut the thrust
                SetThrust(0);
            }

            // Once the rocket is close to the surface and the velocity is low, increase the thrust for a soft landing
            if (altitude >= 5 && altitude <= 50)
            {
                // Calculate the thrust needed to slow down descent gently
                // Increase the multiplier as needed to provide more thrust
                double descentControlFactor = 0.4; // Increase this factor to apply more thrust during soft landing
                double landingThrust = gravitationalForce + (mass * velocity * descentControlFactor);

                // Ensure the landing thrust does not exceed maximum thrust capacity
                landingThrust = Math.Min(landingThrust, maxThrust);

                SetThrust(landingThrust);
                Echo("landingThrust: " + landingThrust.ToString());
            }

            // If the velocity is close to zero and the rocket is very close to the surface, cut the thrust to land
            if (velocity < 2 && thrustersOn)
            {
                IsLanding = false;
                sas.TryRun("off");
                SetThrust(0);
                Echo("LANDED");
            }
        }

        private void SetThrust(double thrust)
        {
            thrustersUp.ForEach(thruster => thruster.ThrustOverridePercentage = (float)thrust);
            thrustersOn = true;
        }


    }
}
