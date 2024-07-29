using StardewValley;
using System;

namespace MyFirstStarMod.AI
{
    public class EventArgsLocationChanged : EventArgs
    {
        public GameLocation PreviousLocation { get; set; }
        public GameLocation CurrentLocation { get; set; }
    }
}