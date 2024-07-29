using StardewModdingAPI.Events;

namespace MyFirstStarMod.Internal;

internal interface IUpdateable
{
    void Update(UpdateTickedEventArgs e);
}