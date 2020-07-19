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
    /* The reason why we have this class is because every time a connector gets connected
     * or disconnected we have to initialize the connector again because the old block
     * reference gets invalid ... only god knows why
     * 
     * Also I now have a reason to implement the connectable check with a delay :)
     */
    public class Connector
    {
        public Connector(string name, Program program)
        {
            this.Name = name;
            try
            {
                this.EntityId = (program.GridTerminalSystem.GetBlockWithName(name) as IMyShipConnector).EntityId;
            }
            catch
            {
                this.Name = "";
            }
            m_program = program;
        }

        public Connector(long entityId, Program program) 
        {
            this.Name = (program.GridTerminalSystem.GetBlockWithId(entityId) as IMyShipConnector).Name;
            this.EntityId = entityId;
            m_program = program;
        }

        // only save name and not the reference, because we have to get the block reference
        // everytime to make shure it is still valid
        public string Name { get; set; }

        // to be save because the connectors get created with the welder
        // that means the names will be given out automatically and there
        // can be duplicates
        public long EntityId { get; set; }

        public bool isValid 
        { 
            get 
            {
                IMyShipConnector conn = m_program.GridTerminalSystem.GetBlockWithId(EntityId) as IMyShipConnector;
                return (conn != null);
            }
        }
        public MyShipConnectorStatus Status 
        { 
            get { return (m_program.GridTerminalSystem.GetBlockWithId(EntityId) as IMyShipConnector).Status; } 
        }

        public bool IsConnectableWithDelay
        {
            get
            {
                IMyShipConnector connector = m_program.GridTerminalSystem.GetBlockWithId(EntityId) as IMyShipConnector;
                // check if the block is connected
                if (connector.Status == MyShipConnectorStatus.Connectable)
                {
                    // set the timer if not already set
                    if (!m_bTimerStarted)
                    {
                        m_startTime = DateTime.UtcNow;
                        m_bTimerStarted = true;
                    }
                    else
                    {
                        // delay passed ?
                        if (DateTime.UtcNow.Subtract(m_startTime).TotalMilliseconds > m_delay)
                        {
                            // reset timer
                            m_bTimerStarted = false;
                            // now we should be really connected
                            return true;
                        }
                    }
                    return false;
                }
                // reset the timer
                m_bTimerStarted = false;
                return false;
            }
        }

        public void Connect()
        {
            (m_program.GridTerminalSystem.GetBlockWithId(EntityId) as IMyShipConnector).Connect();
        }

        public void Disconnect()
        {
            (m_program.GridTerminalSystem.GetBlockWithId(EntityId) as IMyShipConnector).Disconnect();
        }

        bool m_bTimerStarted = false;
        DateTime m_startTime = new DateTime();
        // we want 1s delay between the real connection and our connection
        ulong m_delay = 750;
        Program m_program;
    }
}
