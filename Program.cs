using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        // global variables
        // Pistons
        public Dictionary<string, Piston> Pistons = new Dictionary<string, Piston>();
        // Merge blocks
        public Dictionary<string, MergeBlock> MergeBlocks = new Dictionary<string, MergeBlock>();
        // Connectors
        public Connector connectorDrillTop;
        public Connector connectorDrillBottom;
        // Welder
        public IMyShipWelder Welder;
        // Drills
        public List<IMyShipDrill> Drills = new List<IMyShipDrill>();
        // Rotor
        public IMyMotorStator Rotor;

        // States
        States.DrillState drillState;
        public States.ModuleBuildState moduleBuildState;

        // State Machine
        StateMachine stateMachine;


        // test
        //string nameOfPiston = "Piston_module_grabber_side";
        int ticks = 0;

        public Program()
        {
            // fill pistons dictionary
            {
                string[] strPistonNames = new string[9];
                strPistonNames[0] = "Piston_top_down";
                strPistonNames[1] = "Piston_top_up";
                strPistonNames[2] = "Piston_module_creation_top_1";
                strPistonNames[3] = "Piston_module_creation_top_2";
                strPistonNames[4] = "Piston_module_grabber_side";
                strPistonNames[5] = "Piston_module_grabber_front";
                strPistonNames[6] = "Piston_module_grabber_back";
                strPistonNames[7] = "Piston_holder_front";
                strPistonNames[8] = "Piston_holder_back";

                for (int i = 0; i < strPistonNames.Length; i++)
                {
                    if((GridTerminalSystem.GetBlockWithName(strPistonNames[i]) as IMyPistonBase) == null)
                    {
                        throw new Exception("Error - Piston with name: " + strPistonNames[i] + " does not exist!");
                    }
                    Pistons.Add(strPistonNames[i], new Piston(strPistonNames[i], this));
                }
            }
            
            // fill merge blocks dictionary
            {
                string[] strMergeBlocks = new string[5];
                strMergeBlocks[0] = "Merge_module_creation";
                strMergeBlocks[1] = "Merge_module_grabber_front";
                strMergeBlocks[2] = "Merge_module_grabber_back";
                strMergeBlocks[3] = "Merge_holder_front";
                strMergeBlocks[4] = "Merge_holder_back";

                for (int i = 0; i < strMergeBlocks.Length; i++)
                {
                    if((GridTerminalSystem.GetBlockWithName(strMergeBlocks[i]) as IMyShipMergeBlock) == null)
                    {
                        throw new Exception("Error - Merge Block with name: " + strMergeBlocks[i] + " does not exist!");
                    }
                    MergeBlocks.Add(strMergeBlocks[i], new MergeBlock(strMergeBlocks[i], this));
                }
            }

            // fill drills list
            GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(Drills);
            if (Drills.Count == 0)
                throw new Exception("No attached drills detected");

            // initialize connectors
            {
                string connectorTopName = "Connector_base";
                connectorDrillTop = new Connector(connectorTopName, this);
                if (!connectorDrillTop.isValid)
                    throw new Exception("Connector with the name " + connectorTopName + " does not exist");

                // drill connector only needed for first module
                // after first module this connector should be replaced with the top connector
                // of the last added module - see StateMachine.StatePrepareForNewModule.Perform()
                string connectorBottomName = "Connector_drill";
                connectorDrillBottom = new Connector(connectorBottomName, this);
                if (!connectorDrillBottom.isValid)
                    throw new Exception("Connector with the name " + connectorBottomName + " does not exist");
            }

            // initialize welder
            {
                Welder = GridTerminalSystem.GetBlockWithName("Welder_module_creation") as IMyShipWelder;
                if (Welder == null)
                    throw new Exception("Welder with the name Welder_module_creation does not exist");
            }

            // initialize Rotor
            {
                string rotorName = "Rotor_drills";
                Rotor = GridTerminalSystem.GetBlockWithName(rotorName) as IMyMotorStator;
                if (Rotor == null)
                    Echo("Rotor with the name " + rotorName + " not found");
            }

            // initialize states
            drillState = new States.DrillState();
            moduleBuildState = new States.ModuleBuildState();

            // initialize state machine
            stateMachine = new StateMachine(this);


            // load data
            if (!Load()) // don't load for testing
            {
                Echo("could not load saved data - set state to default values");
                // first start of script
                drillState.State = States.eDrillState.eInvalidMax;
                moduleBuildState.State = States.eModuleBuildState.eInvalidMax;
                connectorDrillBottom = new Connector("Connector_drill", this);
            }
        }

        public void Save()
        {
            // saves The States and the name of the bottom connector
            // because this one will change every time a new module gets added to the drill
            //string strStateDrill = drillState.State.ToString();
            //string strStateModuleBuild = moduleBuildState.State.ToString();

            if (drillState == null) return;
            if (moduleBuildState == null) return;
            if (!connectorDrillBottom.isValid) return;

            Storage = string.Join(";",
                drillState.State.ToString(),
                moduleBuildState.State.ToString(),
                connectorDrillBottom.Name);
        }

        public bool Load()
        {
            // check if something was saved
            string[] storedData = Storage.Split(';');

            if (storedData[0] == "" || storedData[1] == "" || storedData[2] == "") return false;

            if (storedData.Length >= 3)
            {
                int i;
                if (int.TryParse(storedData[0], out i))
                    drillState.State = i;
                else
                    return false;

                if (int.TryParse(storedData[1], out i))
                    moduleBuildState.State = i;
                else
                    return false;

                long id;
                if (long.TryParse(storedData[2], out id))
                    connectorDrillBottom = new Connector(id, this);
                else
                    return false;
            }
            else
                return false;

            Echo("loaded saved data");
            return true;
        }


        public void EnableDrill(bool bEnable)
        {
            // turn everything off
            Echo("turning everything " + (bEnable ? "on" : "off"));

            //Welder
            Welder.Enabled = bEnable;

            //pistons
            // foreach does not work in Space engineers?
            for (int i = 0; i < Pistons.Count; i++)
            {
                Pistons.ElementAt(i).Value.Enabled = bEnable;
            }
        }


        public void Main(string argument, UpdateType updateSource)
        {
            Echo("tiks: " + ticks);
            ticks++;

            // check if arguments are given
            if (argument.Length != 0)
            {
                // look for commands
                // should come in this format: toggle=on;
                string[] arguments = argument.Split(';');

                for (int i = 0;  i < arguments.Length; i++)
                {
                    string[] command = arguments[i].Split('=');

                    // toggle everything on/off: toggle=off or toggle=on
                    if(command[0] == "toggle")
                    {
                        // toggle on
                        if(command[1] == "on")
                        {
                            Echo("toggle on received");
                            // we want to run the script every 100 Tiks, 100 Tiks enought?
                            Runtime.UpdateFrequency = UpdateFrequency.Update10;
                            EnableDrill(true);
                        }
                        // toggle off
                        else 
                        {
                            Echo("toggle off received");
                            Runtime.UpdateFrequency = UpdateFrequency.None;
                            EnableDrill(false);
                        }
                    }

                    if(command[0] == "restart")
                    {
                        drillState.State = States.eDrillState.eInvalidMax;
                        moduleBuildState.State = States.eModuleBuildState.eInvalidMax;
                        connectorDrillBottom = new Connector("Connector_drill", this);
                    }
                }
            }

            // perform module build states
            Echo("current build state: " + stateMachine.GetStateModuleBuild(moduleBuildState.State).Name);
            StateMachine.BaseState nextBuildState = stateMachine.GetStateModuleBuild(moduleBuildState.NextState);
            // can we enter the next state?
            Echo("checking condition: " + nextBuildState.Name);
            if(nextBuildState.Condition(this))
            {
                //Log
                Echo("Performing State: " + nextBuildState.Name);

                // perform commands
                nextBuildState.Perform(this);
                // set current state to next state
                moduleBuildState.State = moduleBuildState.NextState;
            }

            // perform drill states
            Echo("current drill state: " + stateMachine.GetStateDrill(drillState.State).Name);
            StateMachine.BaseState nextDrillState = stateMachine.GetStateDrill(drillState.NextState);
            // can we enter the next state?
            Echo("checking condition: " + nextDrillState.Name);
            if (nextDrillState.Condition(this))
            {
                //Log
                Echo("Performing State: " + nextDrillState.Name);

                // perform commands
                nextDrillState.Perform(this);
                // set current state to next state
                drillState.State = drillState.NextState;
            }

        }
    }
}
