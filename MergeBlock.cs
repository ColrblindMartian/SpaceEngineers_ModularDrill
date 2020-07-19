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
    /* the only reason we need this is to add a delay to the IsConnected state
     * If the state is set to IsConnected() it acctually isn't.... It takes a bit more time
     * for the block to really connect to the other grid so if we move the piston backward
     * after we get the IsConnected state it could be that we are not connected to the other grid
     * and the module falls off :/
     */ 
    public class MergeBlock
    {
        public MergeBlock(string strName, Program program)
        {
            m_program = program;
            m_MergeBlock = m_program.GridTerminalSystem.GetBlockWithName(strName) as IMyShipMergeBlock;
            Name = strName;

            // We want delay between m_MergeBlock.IsConnected and our IsConnected
            m_delay = 750;
            m_bTimerStarted = false;
        }

        public string Name { get; set; }

        public bool Enabled 
        { 
            get { return m_MergeBlock.Enabled; } 
            set { m_MergeBlock.Enabled = value; } 
        }

        public bool IsConnected
        {
            get { return m_MergeBlock.IsConnected; }
        }

        public bool IsConnectedWithDelay
        {
            get
            {
                // check if the block is connected
                if (m_MergeBlock.IsConnected)
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
                }

                return false;
            }
        }

        ulong m_delay;
        DateTime m_startTime;
        bool m_bTimerStarted;
        Program m_program;
        IMyShipMergeBlock m_MergeBlock;
    }
}
