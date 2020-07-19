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
    public class Piston
    {
        // default speeds
        // enums are not allowed in SE so I use this static class instead
        public static class PistonSpeeds
        {
            public static float slowest =  0.15f;
            public static float slow =     0.5f;
            public static float medium =   1f;
            public static float fast =     2.5f;
            public static float fastest =  5f;
        }

        public Piston(string strName, Program program)
        {
            m_program = program;
            m_piston = m_program.GridTerminalSystem.GetBlockWithName(strName) as IMyPistonBase;

            // default velocity
            Velocity = 0.1f;
        }

        // --- Properties ---

        public float Distance
        {
            get
            {
                return GetDistance();
            }
            set
            {
                MoveToPos(value);
            }
        }

        public float Velocity { get; set; }
        public bool Enabled 
        { 
            get { return m_piston.Enabled; } 
            set { m_piston.Enabled = value; }
        }

        // --- Functions ---

        // get acctual distance of piston
        private float GetDistance()
        {
            // DetailedInfo contains the string of the text form the right hand pane
            // of the control panel
            string[] pistInfArr = m_piston.DetailedInfo.Split(':');
            return float.Parse(pistInfArr[1].Split('m')[0]);
        }

        public void MoveToPos(float velocity, float distance)
        {
            Velocity = velocity;
            // will call MoveToPos(float distance)
            Distance = distance;
        }

        private void MoveToPos(float distance)
        {
            bool bExtend = (distance > Distance);

            if (bExtend)
            {
                m_piston.Velocity = Math.Abs(Velocity);
                m_piston.MaxLimit = distance;
            }
            else
            {
                m_piston.Velocity = Math.Abs(Velocity) * -1;
                m_piston.MinLimit = distance;
            }

            m_program.Echo("MoveToPos: bExtend = " + bExtend);
        }

        Program m_program;
        IMyPistonBase m_piston;
    }
}
