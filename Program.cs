using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Input;
using VRageMath;
using VRageRender;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private const double DEPLOY_LANDING_GEARS_ALTITUDE = 0.8;
        private const double DEPLOY_LANDING_GEARS_THRESHOLD = 30.0;
        private const float INVENTORY_MULTIPLIER = 10.0f;
        private const float EXTEND_PISTON_SPEED = 0.3f;
        private const float EXTEND_HINGE_SPEED = 3.0f;

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
        double brakeDistance = 0.0;
        double burnTime = 0.0;
        bool thrustersOn = false;

        int counter = 0;

        // Landing gear 
        List<IMyMotorStator> hinges = new List<IMyMotorStator>();
        List<IMyPistonBase> pistons = new List<IMyPistonBase>();
        bool isLandingGearExtended = false;
        bool isExtendingLandingGear = false;
        bool isRetractingLandingGear = false;

        public Program()
        {
            //allow transmitter class to access grid
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            // Check custom data for transmitter and receiver
            CheckCustomData();

            // Get antennas (for transmitter and receiver)
            GridTerminalSystem.GetBlocksOfType(antennas);
            if (antennas.Count() == 0 && (IsTransmitting || IsReceiver))
            {
                throw new Exception("No antennas found");
            }

            // Get main controller
            GridTerminalSystem.GetBlocksOfType(controllers);
            if (controllers.Count() == 0 && !IsReceiver)
            {
                throw new Exception("No controllers found");
            }

            // Get main controller (the one that can control the ship)
            mainController = controllers.Find(x => x.CanControlShip);
            if (mainController == null && !IsReceiver)
            {
                throw new Exception("No main controller found");
            }

            // Save orientation of controller
            mainController.Orientation.GetMatrix(out mainControllerMatrix);

            // Get gyros
            GridTerminalSystem.GetBlocksOfType(gyros);
            if (gyros.Count() == 0 && !IsReceiver)
            {
                throw new Exception("No gyros found");
            }

            // Get SAS
            sas = GridTerminalSystem.GetBlockWithName("SAS") as IMyProgrammableBlock;
            if (sas == null && !IsReceiver)
            {
                throw new Exception("No SAS found");
            }

            // Asign thrusters to correct orientation
            AsignThrusters();

            // Get all hinges by custom group name 
            GridTerminalSystem.GetBlockGroupWithName("LandingGearHinges").GetBlocksOfType(hinges);
            GridTerminalSystem.GetBlockGroupWithName("LandingGearPistons").GetBlocksOfType(pistons);

            if (hinges.Count() > 0 && pistons.Count() > 0)
            {
                CheckLandingGearStatus();
            }
        }

        private void CheckCustomData()
        {
            // Get customdata for TX and RX
            string customData = Me.CustomData;

            // Parse every line of customdata
            string[] customDataLines = customData.Split('\n');

            // Loop trough all lines
            foreach (string customDataLine in customDataLines)
            {
                // Parse every line on empty spaces
                string[] customDataLineArguments = customDataLine.Split(' ');

                // Check if line contains arguments
                if (customDataLineArguments.Length > 0)
                {
                    switch (customDataLineArguments[0].ToLower())
                    {
                        case "transmitter":
                            _transmitter = new Transmitter(this);
                            break;
                        case "receiver":
                            _receiver = new Receiver(this);
                            break;
                        default:
                            break;
                    }
                }
            }
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

            if (isExtendingLandingGear)
            {
                ExtendLandingGears();
            }

            if (isRetractingLandingGear)
            {
                RetractLandingGears();
            }   
        }

        private void ParseArguments(string argument)
        {
            if (_commandLine.TryParse(argument))
            {
                if (_commandLine.ArgumentCount > 0)
                {
                    // Parse commandLine arguments on empty spaces
                    string[] ParsedArguments = argument.Split(' ');

                    // loop trough all arguments and set flags accordingly
                    foreach (string ParsedArgument in ParsedArguments)
                    {
                        switch (ParsedArgument.ToLower())
                        {
                            case "land":
                                autoLanderState = ScriptState.STARTED;
                                sas.TryRun("retro");
                                break;
                            case "toggle-landing-gears":
                                ActivateLandingGears();
                                break;
                            case "retract-landing-gears":
                                isRetractingLandingGear = true;
                                isExtendingLandingGear = false;
                                RetractLandingGears();
                                break;
                            default:
                                break;
                        }
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
            ScriptState[] allowedStates = { ScriptState.STARTED, ScriptState.MAX_THRUST, ScriptState.SOFT_THRUST, ScriptState.FREE_FALL };
            if (allowedStates.Contains(autoLanderState))
            {
                mainController.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude);

                Vector3D gravity = mainController.GetNaturalGravity();
                double gMs = Math.Sqrt(Math.Pow(gravity.X, 2) + Math.Pow(gravity.Y, 2) + Math.Pow(gravity.Z, 2));

                double totalMass = mainController.CalculateShipMass().TotalMass;
                double baseMass = mainController.CalculateShipMass().BaseMass;
                double cargoMass = totalMass - baseMass;
                double actualMass = baseMass + (cargoMass / INVENTORY_MULTIPLIER);
                double shipWeight = actualMass * gMs;
                double G = gMs / 9.81;
                double margin = CalculateMargin(G);

                velocity = mainController.GetShipSpeed();
                maxThrust = thrustersUp.Sum(thruster => thruster.MaxEffectiveThrust);
                brakeDistance = calculateBrakeDistance(gMs, actualMass, maxThrust, velocity);
                brakeDistance = brakeDistance + margin;

                Echo("Altitude: " + Math.Round(altitude, 2).ToString());
                Echo("velocity: " + Math.Round(velocity, 2).ToString());
                Echo("Burn Altitude: " + Math.Round(brakeDistance, 2).ToString());
                Echo("Max Available Thrust: " + Math.Round(maxThrust, 2).ToString());
                Echo("Planet Gravity m/s^2: " + Math.Round(gMs, 2).ToString());
                Echo("Burn time: " + Math.Round(burnTime, 4));
                Echo("Margin: " + Math.Round(margin, 2).ToString());

                if (altitude <= brakeDistance)
                {
                    autoLanderState = ScriptState.MAX_THRUST;
                    SetThrust(maxThrust);
                }

                if (
                    (altitude <= (brakeDistance * DEPLOY_LANDING_GEARS_ALTITUDE)
                    || altitude <= DEPLOY_LANDING_GEARS_THRESHOLD) && thrustersOn
                ) {
                    isExtendingLandingGear = true;
                    ExtendLandingGears();
                }

                // If the velocity is close to zero and the rocket is very close to the surface, cut the thrust to land
                if ((velocity < 1 || altitude < 5) && thrustersOn)
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
            thrustersUp.ForEach(thruster => thruster.ThrustOverride = (float)thrust);
            thrustersOn = true;
        }

        private double calculateBrakeDistance(double gMs, double actualMass, double maxthrust, double speed)
        {
            double shipWeight = actualMass * gMs;

            double brakeForce = maxthrust - shipWeight;
            double deceleration = brakeForce / actualMass;

            burnTime = velocity / deceleration;

            return (speed * speed) / (2 * deceleration);
        }

        static double CalculateMargin(double G)
        {
            double slope = -2.67;
            double intercept = 3.67;

            // Calculating the margin
            double margin = slope * G + intercept;
            return margin;
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

        private void CheckLandingGearStatus()
        {
            // Check if landing gear is extended
            isLandingGearExtended = hinges[0].Angle < 1;
        }

        private void ActivateLandingGears()
        {
            // Validation for the required blocks
            if (pistons == null || hinges == null)
            {
                return;
            }

            if (pistons.Count() == 0 || hinges.Count() == 0)
            {
                return;
            }

            if (isLandingGearExtended)
            {
                // Extend landing gear
                isRetractingLandingGear = true;
                RetractLandingGears();
            } else
            {
                // Retract landing gear
                isExtendingLandingGear = true;
                ExtendLandingGears();
            }
        }

        private void ExtendLandingGears()
        {
            // Set safety restrictions
            hinges.ForEach(hinge =>
            {
                hinge.RotorLock = false;
                hinge.TargetVelocityRPM = EXTEND_HINGE_SPEED;
            });

            if (Math.Round(hinges[0].Angle, 2) >= DegreesToRadians(300.0f))
            {
                // Extending pistons
                if (pistons[0].Velocity != EXTEND_PISTON_SPEED)
                {
                    pistons.ForEach(piston =>
                    {
                        piston.Enabled = true;
                        piston.Velocity = EXTEND_PISTON_SPEED;
                    });
                }
            } else if (Math.Round(hinges[0].Angle, 2) == DegreesToRadians(50.0f))
            {
                // Done extending
                LockLandingGears();
                isExtendingLandingGear = false;
                isLandingGearExtended = true;
            }
        }

        private void RetractLandingGears()
        {
            // Retract hinges
            hinges.ForEach(hinge =>
            {
                hinge.RotorLock = false;
                hinge.TargetVelocityRPM = -EXTEND_HINGE_SPEED;
            });

            // Retract pistons
            if (pistons[0].Velocity != -EXTEND_PISTON_SPEED)
            {
                pistons.ForEach(piston =>
                {
                    piston.Enabled = true;
                    piston.Velocity = -EXTEND_PISTON_SPEED;
                });
            }

            // Done retracting
            if (Math.Round(hinges[0].Angle, 2) == DegreesToRadians(269.0f))
            {
                LockLandingGears();
                isRetractingLandingGear = false;
                isLandingGearExtended = false;
            }
        }

        private void LockLandingGears()
        {
            hinges.ForEach(hinge =>
            {
                hinge.TargetVelocityRPM = 0.0f;
                hinge.RotorLock = true;
            });
            pistons.ForEach(piston =>
            {
                piston.Velocity = 0.0f;
                piston.Enabled = false;
            });
        }

        private double DegreesToRadians(float degrees)
        {
            return Math.Round(degrees * (float)(Math.PI / 180.0f), 2);
        }
    }
}
