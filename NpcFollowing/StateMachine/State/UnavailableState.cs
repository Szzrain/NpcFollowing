using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NpcFollowing.StateMachine;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace NpcFollowing.StateMachine.State
{
    class UnavailableState : CompanionState
    {
        public UnavailableState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor) : base(stateMachine, events, monitor)
        {
        }
    }
}
