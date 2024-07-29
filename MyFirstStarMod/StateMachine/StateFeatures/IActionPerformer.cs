using StardewValley;

namespace MyFirstStarMod.StateMachine.StateFeatures;

interface IActionPerformer
{
    bool CanPerformAction { get; }
    bool PerformAction(Farmer who, GameLocation location);
}