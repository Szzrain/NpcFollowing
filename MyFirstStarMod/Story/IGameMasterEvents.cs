using StardewValley;
using System;

namespace MyFirstStarMod.Story
{
    public interface IGameMasterEvents
    {
        event EventHandler<ICheckEventEventArgs> CheckEvent;
    }

    public interface ICheckEventEventArgs
    {
        GameLocation Location { get; }
        Farmer Player { get; }
    }
}