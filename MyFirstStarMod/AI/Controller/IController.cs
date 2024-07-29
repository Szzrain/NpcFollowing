using MyFirstStarMod.Internal;
using StardewModdingAPI.Events;
using StardewValley.Monsters;

namespace MyFirstStarMod.AI.Controller
{
    internal interface IController : IUpdateable
    {
        bool IsIdle { get; }
        void Activate();
        void Deactivate();
        void SideUpdate(UpdateTickedEventArgs e);
    }
}