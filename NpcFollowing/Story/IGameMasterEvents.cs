using StardewValley;
using System;

namespace NpcFollowing.Story
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