using StardewModdingAPI.Events;

namespace NpcFollowing.Internal;

internal interface IUpdateable
{
    void Update(UpdateTickedEventArgs e);
}