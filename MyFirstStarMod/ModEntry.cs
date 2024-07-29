using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MyFirstStarMod.Movement;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Pathfinding;

namespace MyFirstStarMod
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static Dictionary<Farmer, Dictionary<NPC, NpcMovementController>> following_npcs = new Dictionary<Farmer, Dictionary<NPC, NpcMovementController>>();
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            var harmony = new Harmony(ModManifest.UniqueID);
            
            this.Monitor.Log("Hello world!", LogLevel.Debug);
            // patch the game
            ObjectPatches.Initialize(this.Monitor);
            // harmony.Patch(
            //     original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.canBePlacedHere)),
            //     prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.CanBePlacedHere_Prefix))
            // );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.TryLoadSchedule)),
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.TryLoadSchedule_Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.NPC_checkAction_Prefix))
            );
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }
        static void set_follow_schedule(NPC npc, Farmer farmer)
        {
            npc.temporaryController = null;
            npc.controller = null;
            npc.followSchedule = false;
            // npc.speed = farmer.speed;
        }
        
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            if (e.IsMultipleOf(15))
            {
                foreach (var pair in following_npcs)
                {
                    Farmer farmer = pair.Key;
                    foreach (var npcAndControllorPair in pair.Value)
                    {
                        NPC npc = npcAndControllorPair.Key;
                        NpcMovementController controller = npcAndControllorPair.Value;
                        if (npc.currentLocation != farmer.currentLocation)
                        {
                            Game1.warpCharacter(npc, farmer.currentLocation, farmer.TilePoint.ToVector2());
                        }
                        controller.AcquireTarget(farmer.TilePoint.ToVector2());
                        controller.Update(e);
                    }
                }
            }
        }
        
        internal class ObjectPatches
        {
            private static IMonitor Monitor;

            // call this method from your Entry class
            internal static void Initialize(IMonitor monitor)
            {
                Monitor = monitor;
            }

            // patches need to be static!

            internal static bool TryLoadSchedule_Prefix(NPC __instance)
            {
                int dayOfMonth = Game1.dayOfMonth;
                string key = Game1.shortDayNameFromDayOfSeason(dayOfMonth);
                try {
                    Monitor.Log($"Called TryLoadSchedule for {__instance.Name}.", LogLevel.Debug);
                    return true;
                } catch (Exception ex) {
                    Monitor.Log($"Failed in {nameof(TryLoadSchedule_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
            

            internal static bool NPC_checkAction_Prefix(NPC __instance, Farmer who, GameLocation l)
            {
                try
                {
                    Monitor.Log($"Called checkAction for {__instance.Name}. With Farmer {who.Name} at Location: {l.Name} ", LogLevel.Debug);
                    Monitor.Log($"CurrentPlayer active object is ({who.ActiveObject?.name})[{who.ActiveObject?.displayName}] ID: {who.ActiveObject?.QualifiedItemId}", LogLevel.Debug);
                    switch (who.ActiveObject?.QualifiedItemId)
                    {
                        case "(O)szzrain.CPMyFirstMod.debug.dog":
                            Monitor.Log("Dog object use detected.", LogLevel.Info);
                            __instance.doEmote(20); 
                            if (ModEntry.following_npcs.ContainsKey(who))
                            {
                                if (!ModEntry.following_npcs[who].ContainsKey(__instance))
                                {
                                    ModEntry.following_npcs[who].Add(__instance, new NpcMovementController(__instance, new PathFinder(l, __instance, who)));
                                    set_follow_schedule(__instance, who);
                                } 
                                else 
                                {
                                    ModEntry.following_npcs[who].Remove(__instance);
                                }
                            } 
                            else 
                            {
                                ModEntry.following_npcs.Add(who, new Dictionary<NPC, NpcMovementController>() { { __instance, new NpcMovementController(__instance, new PathFinder(l, __instance, who)) } });
                                set_follow_schedule(__instance, who);
                            }
                            
                            return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in {nameof(NPC_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            
            }

        }

        /*********
         ** Private methods
         *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;

            // print button presses to the console window
            this.Monitor.Log($"{Game1.player.Name} pressed {e.Button}.", LogLevel.Debug);
        }
    }
}