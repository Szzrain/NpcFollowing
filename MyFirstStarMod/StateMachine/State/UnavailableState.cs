using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyFirstStarMod.StateMachine;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace MyFirstStarMod.StateMachine.State
{
    class UnavailableState : CompanionState
    {
        public UnavailableState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor) : base(stateMachine, events, monitor)
        {
        }
    }
}
