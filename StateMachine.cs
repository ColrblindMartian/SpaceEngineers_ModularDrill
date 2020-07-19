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
    public class States
    {
        // enum does not work in Space Engineers thats why we have this two classes
        // has to be in order of execution
        // I could probably connect this "enums" with the states from the state machine better
        // but I am too lazy doing so ;)
        public static class eDrillState
        {
            public static int eInvalid =            -1;
            public static int eInsertModule =        0; // instert the new module between top and bottom part
            public static int eModuleMoveTop =       1; // move top pistons down to new module
            public static int eAttachModuleTop =     2; // attach top base to new module
            public static int eMoveModuleBottom =    3; // move new module and top base to bottom part of the drill
            public static int eAttachModuleBottom =  4; // connect bottom drill to new module
            public static int ePushModules =         5; // push new module until it reaches bottom holders
            public static int eHoldBottom =          6; // hold bottom part of drill to insert a new module
            public static int ePrepareForNewModule = 7; // move completely up with top part to make place for new module
            public static int eInvalidMax =          8;
        }
        public static class eModuleBuildState
        {
            public static int eInvalid =        -1; 
            public static int ePrepareBuild =    0; // move welder down to start position
            public static int eBuildModule =     1; // welder on and move up
            public static int eCollectBuild =    2; // welder off
            public static int eClearBuildSpace = 3; // grab module
            public static int eInvalidMax =      4;
        }

        // saves the state of the drill
        public class DrillState
        {
            public DrillState() { State = eDrillState.eInvalid; }
            public int State { get; set; }
            public int NextState
            {
                get
                {
                    int nextState = State + 1;
                    if (nextState >= eDrillState.eInvalidMax)
                        nextState = 0;
                    return nextState;
                }
            }
            public int PreviousState
            {
                get
                {
                    int prevState = State - 1;
                    if (prevState <= eDrillState.eInvalid)
                        prevState = eDrillState.eInvalidMax - 1;
                    return prevState;
                }
            }

            //public void IncrementState()
            //{
            //    State++;
            //    if (State >= eDrillState.eInvalidMax)
            //        State = 0;
            //}
        }
        
        // saves only the state of the creation of the module
        // Welding and collecting it with the piston
        public class ModuleBuildState
        {
            public ModuleBuildState() { State = eModuleBuildState.eInvalid; }
            public int State { get; set; }
            public int NextState 
            { 
                get 
                {
                    int nextState = State + 1;
                    if (nextState >= eModuleBuildState.eInvalidMax)
                        nextState = 0;
                    return nextState;
                } 
            }
            public int PreviousState
            {
                get
                {
                    int prevState = State - 1;
                    if (prevState <= eModuleBuildState.eInvalid)
                        prevState = eModuleBuildState.eInvalidMax - 1;
                    return prevState;
                }
            }

            //public void IncrementState()
            //{
            //    State++;
            //    if (State >= eModuleBuildState.eInvalidMax)
            //        State = 0;
            //}
        };
    };

    // states for module build process
    // \todo Add state for finishing drill process returning to a
    // state where we can start again from the beginning to make it 
    // easier moving the drill around on a moving platform
    public class StateMachine 
    {
        public StateMachine(Program program) { m_program = program; }

        // base class for new states
        public abstract class BaseState
        {
            // name of the State
            public abstract string Name { get; }

            // should return true if conditions are met to enter this state
            public abstract bool Condition(Program program);

            // this function should contain the commands for this state
            public abstract void Perform(Program program);
        };

        // get state with index
        // this defines the order of execution
        public BaseState GetStateModuleBuild(int index)
        {
            switch(index)
            {
                default:
                case (-1):
                    throw new Exception("invalid index");
                        
                case 0: return new StatePrepareBuild();
                case 1: return new StateBuild();
                case 2: return new StateCollectModule();
                case 3: return new StateClearBuildSpace();
            }
        }
        public BaseState GetStateDrill(int index)
        {
            switch(index)
            {
                default:
                case (-1):
                    throw new Exception("invalid index");

                case 0: return new StateModuleInsert();
                case 1: return new StateModuleMoveTop();
                case 2: return new StateModuleAttachTop();
                case 3: return new StateModuleMoveBottom();
                case 4: return new StateModuleAttachBottom();
                case 5: return new StateModulePush();
                case 6: return new StateHoldBottom();
                case 7: return new StatePrepareForNewModule();
            }
        }

        // ------ Module Build states -------------

        public class StatePrepareBuild : BaseState
        {
            public override string Name
            {
                get { return "ModuleBuild:StatePrepareBuild"; }
            }
            
            public override bool Condition(Program program)
            {
                // Merge block still connected?
                if (program.MergeBlocks["Merge_module_creation"].IsConnected) return false;
                if (program.MergeBlocks["Merge_module_grabber_front"].IsConnected) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                // welder off just to be shure
                program.Welder.Enabled = false;

                // move welder to starting position
                program.Pistons["Piston_module_creation_top_1"].MoveToPos(Piston.PistonSpeeds.fastest, 7f);
                program.Pistons["Piston_module_creation_top_2"].MoveToPos(Piston.PistonSpeeds.fastest, 7f);

                // turn on merge block
                program.MergeBlocks["Merge_module_creation"].Enabled = true;
            }
        }

        public class StateBuild : BaseState
        {
            public override string Name
            {
                get { return "ModuleBuild:StateBuild"; }
            }

            public override bool Condition(Program program)
            {
                // Pistons ready?
                if (program.Pistons["Piston_module_creation_top_1"].Distance < 6.5f) return false;
                if (program.Pistons["Piston_module_creation_top_2"].Distance < 6.5f) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                // welder on
                program.Welder.Enabled = true;

                // retract pistons
                program.Pistons["Piston_module_creation_top_1"].MoveToPos(Piston.PistonSpeeds.slow, 0f);
                program.Pistons["Piston_module_creation_top_2"].MoveToPos(Piston.PistonSpeeds.slow, 0f);
            }
        }

        public class StateCollectModule : BaseState
        {
            public override string Name
            {
                get { return "ModuleBuild:StateCollectModule"; }
            }

            public override bool Condition(Program program)
            {
                // Pistions retracted ?
                if (program.Pistons["Piston_module_creation_top_1"].Distance > 0f) return false;
                if (program.Pistons["Piston_module_creation_top_2"].Distance > 0f) return false;

                // Module grabber free ?
                if (program.MergeBlocks["Merge_module_grabber_front"].IsConnected) return false;

                // this merge block should definetly be connected after the module finished building
                if(!program.MergeBlocks["Merge_module_creation"].IsConnected)
                {
                    program.Echo("Error in State: " + Name + " : module could not be created");
                    program.Echo("trying again... ");
                    program.moduleBuildState.State = 0;
                    return false;
                }

                return true;
            }

            public override void Perform(Program program)
            {
                // Welder off
                program.Welder.Enabled = false;

                // collect module
                // move sideway piston faster to make shure we come directly from the front
                program.MergeBlocks["Merge_module_grabber_front"].Enabled = true;
                program.Pistons["Piston_module_grabber_side"].MoveToPos(Piston.PistonSpeeds.fastest, 5f);
                program.Pistons["Piston_module_grabber_front"].MoveToPos(Piston.PistonSpeeds.fast, 5f);
            }
        }

        public class StateClearBuildSpace : BaseState
        {
            public override string Name
            {
                get { return "ModuleBuild:StateClearBuildSpace"; }
            }

            public override bool Condition(Program program)
            {

                if (program.Pistons["Piston_module_grabber_side"].Distance < 4.9f) return false;
                if (program.Pistons["Piston_module_grabber_front"].Distance < 4.9f) return false;

                // Merge block connected ?
                if (!program.MergeBlocks["Merge_module_grabber_front"].IsConnectedWithDelay) return false;
                

                return true;
            }

            public override void Perform(Program program)
            {
                // disconnect merge block
                program.MergeBlocks["Merge_module_creation"].Enabled = false;

                // Piston back
                program.Pistons["Piston_module_grabber_front"].MoveToPos(Piston.PistonSpeeds.medium, 0f);
            }
        }

        // ------- Drill States -------------------

        public class StateModuleInsert : BaseState
        {
            public override string Name
            {
                get{ return "DrillState:StateModuleInsert"; }
            }

            public override bool Condition(Program program)
            {
                // module available ? 
                if (program.moduleBuildState.State != States.eModuleBuildState.eClearBuildSpace) return false;
                
                // bottom part of drill stabilized
                if (!program.MergeBlocks["Merge_holder_front"].IsConnectedWithDelay) return false;
                if (!program.MergeBlocks["Merge_holder_back"].IsConnectedWithDelay) return false;
                
                // top pistons retracted ?
                if (program.Pistons["Piston_top_up"].Distance < 9.8) return false;
                if (program.Pistons["Piston_top_down"].Distance > 0.2) return false;
                
                // connector disconnected
                if (program.connectorDrillTop.Status == MyShipConnectorStatus.Connected) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                // move new module to place
                program.Pistons["Piston_module_grabber_side"].MoveToPos(Piston.PistonSpeeds.fast, 0f);
                program.Pistons["Piston_module_grabber_front"].MoveToPos(Piston.PistonSpeeds.fast, 2.4f);

                // move back piston to stabilize module
                // slower to make shure it reaches position after front 
                program.MergeBlocks["Merge_module_grabber_back"].Enabled = true;
                program.Pistons["Piston_module_grabber_back"].MoveToPos(Piston.PistonSpeeds.slow, 2.5f);
            }
        }

        public class StateModuleMoveTop : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateModuleAttachTop"; }
            }

            public override bool Condition(Program program)
            {
                // Module stabilized below top connector?
                if (!program.MergeBlocks["Merge_module_grabber_front"].IsConnectedWithDelay) return false;
                if (!program.MergeBlocks["Merge_module_grabber_back"].IsConnectedWithDelay) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                // Move top piston down
                program.Pistons["Piston_top_down"].MoveToPos(Piston.PistonSpeeds.slow, 2.45f);
            }
        }

        public class StateModuleAttachTop : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateModuleAttachTop"; }
            }

            public override bool Condition(Program program)
            {
                // can we connect ?
                // check with a delay because it looks better if the connectors are 
                // connected and acctually physically touch each other
                if (!program.connectorDrillTop.IsConnectableWithDelay) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                // connect it!
                program.connectorDrillTop.Connect();
            }
        }

        public class StateModuleMoveBottom : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateModuleMoveBottom"; }
            }

            public override bool Condition(Program program)
            {
                // is it connected ? if not we did something wrong
                if (program.connectorDrillTop.Status != MyShipConnectorStatus.Connected)
                    throw new Exception("Error in Status " + Name + ": Top connector should be connected by now");

                return true;
            }

            public override void Perform(Program program)
            {
                // move grabber pistons back
                program.Pistons["Piston_module_grabber_front"].MoveToPos(Piston.PistonSpeeds.fast, 0f);
                program.Pistons["Piston_module_grabber_back"].MoveToPos(Piston.PistonSpeeds.fast, 0f);
                
                program.MergeBlocks["Merge_module_grabber_front"].Enabled = false;
                program.MergeBlocks["Merge_module_grabber_back"].Enabled = false;

                // move whole module down to connect with bottom part
                program.Pistons["Piston_top_down"].MoveToPos(Piston.PistonSpeeds.slow, 2.5f);
                program.Pistons["Piston_top_up"].MoveToPos(Piston.PistonSpeeds.slow, 7.5f);
            }
        }

        public class StateModuleAttachBottom : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateModuleAttachBottom"; }
            }

            public override bool Condition(Program program)
            {
                if (!program.connectorDrillBottom.IsConnectableWithDelay) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                program.connectorDrillBottom.Connect();
            }
        }

        public class StateModulePush : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateModulePush"; }
            }

            public override bool Condition(Program program)
            {
                // if this is not connected we did something wrong!
                if (program.connectorDrillBottom.Status != MyShipConnectorStatus.Connected)
                    throw new Exception("Error in Status " + Name + ": Top connector should be connected by now");
                
                return true;
            }

            public override void Perform(Program program)
            {
                // turn on drills and rotor
                for(int i = 0; i < program.Drills.Count; i++)
                {
                    program.Drills[i].Enabled = true;
                }
                try
                {
                    program.Rotor.Enabled = true;
                    program.Rotor.TargetVelocityRPM = 4f;
                }
                catch
                {
                    program.Echo("Rotor was not found, continuing without rotation");
                }

                // move newly inserted module to the bottom holders to prepare for the new module
                program.Pistons["Piston_top_up"].MoveToPos(Piston.PistonSpeeds.slowest, 0f);
                program.Pistons["Piston_top_down"].MoveToPos(Piston.PistonSpeeds.slowest, 10f);
                program.Pistons["Piston_holder_front"].MoveToPos(Piston.PistonSpeeds.fastest, 0f);
                program.Pistons["Piston_holder_back"].MoveToPos(Piston.PistonSpeeds.fastest, 0f);
                program.MergeBlocks["Merge_holder_front"].Enabled = false;
                program.MergeBlocks["Merge_holder_back"].Enabled = false;
            }
        }

        public class StateHoldBottom : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StateHoldBottom"; }
            }

            public override bool Condition(Program program)
            {
                // reached bottom position
                if (program.Pistons["Piston_top_up"].Distance > 0.1f) return false;
                if (program.Pistons["Piston_top_down"].Distance < 9.9f) return false;

                return true;
            }

            public override void Perform(Program program)
            {
                //connect bottom drill part
                program.MergeBlocks["Merge_holder_front"].Enabled = true;
                program.MergeBlocks["Merge_holder_back"].Enabled = true;
                // move at different speeds to make shure both get connected correctly
                program.Pistons["Piston_holder_front"].MoveToPos(Piston.PistonSpeeds.fast, 2.4f);
                program.Pistons["Piston_holder_back"].MoveToPos(Piston.PistonSpeeds.medium, 2.5f);
            }
        }

        public class StatePrepareForNewModule : BaseState
        {
            public override string Name
            {
                get { return "DrillState:StatePrepareforNewModule"; }
            }

            public override bool Condition(Program program)
            {
                // bottom connected?
                if (!program.MergeBlocks["Merge_holder_front"].IsConnectedWithDelay) return false;
                if (!program.MergeBlocks["Merge_holder_back"].IsConnectedWithDelay) return false;

                return true;
            }

            public override void Perform(Program program)
            {   
                // here we set the new bottom connecter
                // we just check what connecter lost it's connection after we disconnect
                // the top connector, than we know the new bottom connector that connects to the
                // new module
                List<IMyShipConnector> listMyShipConnectors = new List<IMyShipConnector>();
                List<Connector> listConnectors = new List<Connector>();
                // only keep connected connectors in the list and also remove the top connector from the list
                program.GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(listMyShipConnectors, connetor => connetor.Status == MyShipConnectorStatus.Connected);
                // copy everything into my list, because why not
                listConnectors = listMyShipConnectors.Select(connector => new Connector(connector.EntityId, program)).ToList();
                
                for (int i = listConnectors.Count -1; i >= 0; i--)
                {
                    if(listConnectors[i].EntityId == program.connectorDrillTop.EntityId)
                    {
                        //program.Echo("removing connecotor <" + listConnectors[i].Name + "> from list because it's top connector");
                        listConnectors.RemoveAt(i);
                        break;
                    }
                }


                // now we disconnect the top connector
                program.connectorDrillTop.Disconnect();
                // after disconnecting initialize it again because somehow it changed?
                
                // remove old connector
                program.connectorDrillBottom = new Connector("", program);

                // the remaining connector in the list that is also disconnected has
                // to be the one that was just connected to the top connector
                for(int i = 0; i < listConnectors.Count; i++)
                {
                    if(listConnectors[i].Status != MyShipConnectorStatus.Connected)
                    {
                        program.connectorDrillBottom = listConnectors[i];
                        break;
                    }
                }
                // make shure we found it
                if (!program.connectorDrillBottom.isValid)
                    throw new Exception("Error in State " + Name + ": could not find bottom connector; name: " + program.connectorDrillBottom.Name);


                // move top piston up to make space for new module
                program.Pistons["Piston_top_up"].MoveToPos(Piston.PistonSpeeds.fastest, 10f);
                program.Pistons["Piston_top_down"].MoveToPos(Piston.PistonSpeeds.fastest, 0f);
            }
        }

        public Program m_program;
    }
}
