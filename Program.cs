using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using VRageRender;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        enum ScriptState
        {
            STARTED,
            INITIALIZED,
            FREE_FALL,
            MAX_THRUST,
            SOFT_THRUST,
            LANDED,
            IDLE,
        }

        // Set script state to started by default
        ScriptState autoLanderState = ScriptState.INITIALIZED;

        // State booleans for transmitter and receiver
        bool IsTransmitting = false;
        bool IsReceiver = false;

        // Transmitter and Receiver classes and variables
        string TRANSMIT_RECEIVER_TAG = "Autolander";
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

        // Batteries
        List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();

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
                throw new Exception("No controllers found");
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

            // Get SAS
            sas = GridTerminalSystem.GetBlockWithName("SAS") as IMyProgrammableBlock;
            if (sas == null)
            {
                throw new Exception("No SAS found");
            }

            // Asign thrusters to correct orientation
            AsignThrusters();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // Increase counter
            counter++;

            // Parse arguments and set flags accordingly
            ParseArguments(argument);

            // Quick scrtipt status
            ScriptStatusOverview();

            // Receive vessel data
            ReceiveData();

            // Transmit vessel data
            TransmitData();

            // Auto lander
            AutoLander();
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
                            autoLanderState = ScriptState.STARTED;
                            sas.TryRun("retro");
                            break;
                    }
                }
            }
        }

        private void ScriptStatusOverview()

        {
            // Script active animation
            string[] spinnerAnimation = {
                "(=                                                              )",
                "( =                                                             )",
                "(  =                                                            )",
                "(   =                                                           )",
                "(    =                                                          )",
                "(     =                                                         )",
                "(      =                                                        )",
                "(       =                                                       )",
                "(        =                                                      )",
                "(         =                                                     )",
                "(          =                                                    )",
                "(           =                                                   )",
                "(            =                                                  )",
                "(             =                                                 )",
                "(              =                                                )",
                "(               =                                               )",
                "(                =                                              )",
                "(                 =                                             )",
                "(                  =                                            )",
                "(                   =                                           )",
                "(                    =                                          )",
                "(                     =                                         )",
                "(                      =                                        )",
                "(                       =                                       )",
                "(                        =                                      )",
                "(                         =                                     )",
                "(                          =                                    )",
                "(                           =                                   )",
                "(                            =                                  )",
                "(                             =                                 )",
                "(                              =                                )",
                "(                               =                               )",
                "(                                =                              )",
                "(                                 =                             )",
                "(                                  =                            )",
                "(                                   =                           )",
                "(                                    =                          )",
                "(                                     =                         )",
                "(                                      =                        )",
                "(                                       =                       )",
                "(                                        =                      )",
                "(                                         =                     )",
                "(                                          =                    )",
                "(                                           =                   )",
                "(                                            =                  )",
                "(                                             =                 )",
                "(                                              =                )",
                "(                                               =               )",
                "(                                                =              )",
                "(                                                 =             )",
                "(                                                  =            )",
                "(                                                   =           )",
                "(                                                    =          )",
                "(                                                     =         )",
                "(                                                      =        )",
                "(                                                       =       )",
                "(                                                        =      )",
                "(                                                         =     )",
                "(                                                          =    )",
                "(                                                           =   )",
                "(                                                            =  )",
                "(                                                             = )",
                "(                                                              =)",
                "(                                                             = )",
                "(                                                            =  )",
                "(                                                           =   )",
                "(                                                          =    )",
                "(                                                         =     )",
                "(                                                        =      )",
                "(                                                       =       )",
                "(                                                      =        )",
                "(                                                     =         )",
                "(                                                    =          )",
                "(                                                   =           )",
                "(                                                  =            )",
                "(                                                 =             )",
                "(                                                =              )",
                "(                                               =               )",
                "(                                              =                )",
                "(                                             =                 )",
                "(                                            =                  )",
                "(                                           =                   )",
                "(                                          =                    )",
                "(                                         =                     )",
                "(                                        =                      )",
                "(                                       =                       )",
                "(                                      =                        )",
                "(                                     =                         )",
                "(                                    =                          )",
                "(                                   =                           )",
                "(                                  =                            )",
                "(                                 =                             )",
                "(                                =                              )",
                "(                               =                               )",
                "(                              =                                )",
                "(                             =                                 )",
                "(                            =                                  )",
                "(                           =                                   )",
                "(                          =                                    )",
                "(                         =                                     )",
                "(                        =                                      )",
                "(                       =                                       )",
                "(                      =                                        )",
                "(                     =                                         )",
                "(                    =                                          )",
                "(                   =                                           )",
                "(                  =                                            )",
                "(                 =                                             )",
                "(                =                                              )",
                "(               =                                               )",
                "(              =                                                )",
                "(             =                                                 )",
                "(            =                                                  )",
                "(           =                                                   )",
                "(          =                                                    )",
                "(         =                                                     )",
                "(        =                                                      )",
                "(       =                                                       )",
                "(      =                                                        )",
                "(     =                                                         )",
                "(    =                                                          )",
                "(   =                                                           )",
                "(  =                                                            )",
                "( =                                                             )"
            };
            Echo(spinnerAnimation[counter % spinnerAnimation.Length]);

            Echo("--------------=BSP-AutoLander=---------------");

            // Loop trough switch states and set script state accordingly
            switch (autoLanderState)
            {
                case ScriptState.INITIALIZED:
                    Echo("STATE: INITIALIZED");
                    break;
                case ScriptState.STARTED:
                    Echo("STATE: STARTED");
                    break;
                case ScriptState.FREE_FALL:
                    Echo("STATE: FREE FALL");
                    break;
                case ScriptState.MAX_THRUST:
                    Echo("STATE: MAX THRUST");
                    break;
                case ScriptState.SOFT_THRUST:
                    Echo("STATE: SOFT THRUST");
                    break;
                case ScriptState.LANDED:
                    Echo("STATE: LANDED");
                    break;
                case ScriptState.IDLE:
                    Echo("STATE: IDLE");
                    break;
                default:
                    break;
            }

            Echo("TX: " + IsTransmitting.ToString().ToUpper() + " / RX: " + IsReceiver.ToString().ToUpper());
            Echo("-------------------=STATUS=--------------------");
        }
        
        private void ReceiveData()
        {
            // Receive data from transmitter (if enabled)
            if (IsReceiver)
            {
                // Receiver
                _receiver.Receive();
            }
        }

        private void TransmitData()
        {
            // Transmit data to receiver (if enabled) 
            // Also prints data to console and LCD panel
            if (IsTransmitting)
            {
                // String Builder for all data to be transmitted, csv format (time, gravity)
                StringBuilder sb = new StringBuilder();

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

                // Counter
                sb.Append(counter.ToString());
                sb.Append(",");

                // Status of Autolander
                sb.Append(autoLanderState.ToString());
                sb.Append(",");

                // Altitude in m
                mainController.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
                sb.Append(altitude);
                sb.Append(",");

                // Velocity in m/s
                velocity = (double)mainController.GetShipVelocities().LinearVelocity.Length();
                sb.Append(velocity);
                sb.Append(",");

                // Acceleration in m/s^2
                double acceleration = (double)mainController.GetShipVelocities().LinearVelocity.Length();
                sb.Append(acceleration);
                sb.Append(",");

                // Gravity in m/s^2
                gravity = (double)mainController.GetNaturalGravity().Length();
                sb.Append(gravity);
                sb.Append(",");

                // Thrust in N
                maxThrust = thrustersUp.Sum(thruster => thruster.MaxEffectiveThrust);
                sb.Append(maxThrust);
                sb.Append(",");

                // Battery Power in W
                double batteryPower = 0.0;
                batteries.ForEach(battery => batteryPower += battery.MaxOutput);
                sb.Append(batteryPower);
                sb.Append(",");

                // Tank Capacity in L
                double tankCapacity = 0.0;
                tanks.ForEach(tank => tankCapacity += tank.Capacity);
                sb.Append(tankCapacity);
                sb.Append(",");

                // Mass in kg
                mass = (double)mainController.CalculateShipMass().TotalMass;
                sb.Append(mass);

                // Transmitter
                _transmitter.Transmit(sb.ToString());
            }
        }

        private void AutoLander()
        {
            if (
                autoLanderState == ScriptState.STARTED ||
                autoLanderState == ScriptState.MAX_THRUST ||
                autoLanderState == ScriptState.SOFT_THRUST ||
                autoLanderState == ScriptState.FREE_FALL
                )
            {

                // Get gravity, mass, altitude, velocity, and maximum thrust
                gravity = mainController.GetNaturalGravity().Length();
                mass = mainController.CalculateShipMass().PhysicalMass;
                mainController.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);
                velocity = mainController.GetShipVelocities().LinearVelocity.Length();
                maxThrust = thrustersUp.Sum(thruster => thruster.MaxEffectiveThrust);

                // Calculate the force of gravity
                double gravitationalForce = mass * gravity;

                // Calculate the maximum possible deceleration (thrust - gravitational force)
                double maxDeceleration = (maxThrust - gravitationalForce) / mass;

                // Calculate the distance needed to stop from the current velocity and deceleration
                double stoppingDistance = Math.Pow(velocity, 2) / (2 * maxDeceleration);

                // Add a safety margin to the stopping distance to account for delays in thrust application
                double adjustedStoppingDistance = stoppingDistance * CalculateSafetyMargin(mass);

                Echo("Altitude: " + Math.Round(altitude, 2).ToString());
                Echo("velocity: " + Math.Round(velocity, 2).ToString());
                Echo("Burn Altitude: " + Math.Round(adjustedStoppingDistance, 2).ToString());
                Echo("Max Available Thrust: " + Math.Round(maxThrust, 2).ToString());
                Echo("Planet Gravity m/s^2: " + Math.Round(gravity, 2).ToString());

                // Start deceleration burn at the adjusted stopping distance
                if (altitude <= adjustedStoppingDistance && altitude > 50)
                {
                    autoLanderState = ScriptState.MAX_THRUST;
                    // Set the thrust to the maximum available to initiate deceleration
                    SetThrust(maxThrust);
                }

                // Once the rocket is close to the surface and the velocity is low, increase the thrust for a soft landing
                if (altitude >= 5 && altitude <= 50)
                {
                    autoLanderState = ScriptState.SOFT_THRUST;
                    // Calculate the thrust needed to slow down descent gently
                    // Increase the multiplier as needed to provide more thrust
                    double descentControlFactor = 0.3; // Increase this factor to apply more thrust during soft landing
                    double landingThrust = gravitationalForce + (mass * velocity * descentControlFactor);

                    // Ensure the landing thrust does not exceed maximum thrust capacity
                    landingThrust = Math.Min(landingThrust, maxThrust);
                    SetThrust(landingThrust);
                }

                // If the velocity is close to zero and the rocket is very close to the surface, cut the thrust to land
                if (velocity < 2 && thrustersOn)
                {
                    autoLanderState = ScriptState.LANDED;
                    sas.TryRun("off");
                    SetThrust(0);
                }
            }
        }

        private void SetThrust(double thrust)
        {
            Echo("Softlanding Thrust: " + thrust.ToString());
            thrustersUp.ForEach(thruster => thruster.ThrustOverridePercentage = (float)thrust);
            thrustersOn = true;
        }

        private double CalculateSafetyMargin(double shipMass)
        {
            // Calculate the safety margin based on the ship's mass
            double safetyMargin = -0.00001859 * shipMass + 1.3816;

            Echo("SafetyMargin: " + Math.Round(Math.Min(1.0, Math.Max(0.1, safetyMargin)), 2).ToString());

            // Ensure safety margin is between 0.1 and 1.0
            return Math.Min(1.0, Math.Max(0.1, safetyMargin));
        }

        private void AsignThrusters()
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

    }
}
