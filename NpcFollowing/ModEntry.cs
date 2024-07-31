using System;
using System.Xml.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using NpcFollowing.Driver;
using NpcFollowing.Model;
using NpcFollowing.Movement;
using NpcFollowing.Objects;
using NpcFollowing.Utils;
using NpcFollowing.StateMachine;
using NpcFollowing.StateMachine.State;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Pathfinding;

namespace NpcFollowing
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        public static CompanionManager companionManager;
        public static IModHelper ModHelper;
        private StuffDriver StuffDriver { get; set; }
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            var harmony = new Harmony(ModManifest.UniqueID);
            ModHelper = helper;
            
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
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.NPC_checkAction_Prefix)),
                postfix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.NPC_checkAction_Postfix))
            );
            companionManager = new CompanionManager(this.Monitor);
            helper.Events.GameLoop.DayStarted += this.GameLoop_DayStarted;
            helper.Events.GameLoop.DayEnding += this.GameLoop_DayEnding;
            StuffDriver = new StuffDriver(helper.Data, this.Monitor);
            StuffDriver.RegisterEvents(helper.Events);
        }
        
        public static void setNpcStateMachine(NPC npc, CompanionManager companionManager, IModEvents gameEvents, IMonitor monitor)
        {
            // CompanionStateMachine csm = new CompanionStateMachine(npcName, this, new CompanionMetaData(dispositions[npcName]), loader, reflection, this.monitor);
            CompanionStateMachine csm = new CompanionStateMachine(ModEntry.companionManager, npc, monitor);
            Dictionary<CompanionStateMachine.StateFlag, ICompanionState> stateHandlers = new Dictionary<CompanionStateMachine.StateFlag, ICompanionState>()
            {
                [CompanionStateMachine.StateFlag.RESET] = new ResetState(csm, gameEvents, monitor),
                [CompanionStateMachine.StateFlag.AVAILABLE] = new AvailableState(csm, gameEvents, monitor),
                [CompanionStateMachine.StateFlag.RECRUITED] = new RecruitedState(csm, gameEvents, monitor),
                [CompanionStateMachine.StateFlag.UNAVAILABLE] = new UnavailableState(csm, gameEvents, monitor),
            };

            csm.Setup(stateHandlers);
            companionManager.PossibleCompanions.Add(npc.Name, csm);
        }
        
        private void GameLoop_DayStarted(object? sender, DayStartedEventArgs e)
        {
            if (Context.IsMultiplayer)
                return;
            companionManager.NewDaySetup();
        }

        private void GameLoop_DayEnding(object? sender, DayEndingEventArgs e)
        {
            if (Context.IsMultiplayer)
                return;

            companionManager.ResetStateMachines();
            companionManager.DumpCompanionNonEmptyBags();
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
                            if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                            {
                                if (companionManager.PossibleCompanions[__instance.Name].CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                                {
                                    Monitor.Log($"Removed {__instance.Name} from possible companions.", LogLevel.Info);
                                    companionManager.PossibleCompanions[__instance.Name].Dismiss();
                                }
                                else
                                {
                                    Monitor.Log($"Added {__instance.Name} to possible companions.", LogLevel.Info);
                                    __instance.ClearSchedule();
                                    __instance.followSchedule = false;
                                    if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                                    {
                                        Monitor.Log($"Companion already exists in possible companions, activating.", LogLevel.Info);
                                    }
                                    else
                                    {
                                        setNpcStateMachine(__instance, ModEntry.companionManager, ModEntry.ModHelper.Events, Monitor);
                                    }
                                    companionManager.PossibleCompanions[__instance.Name].Recruit();
                                }
                            } 
                            else 
                            {
                                Monitor.Log($"Added {__instance.Name} to possible companions.", LogLevel.Info);
                                __instance.ClearSchedule();
                                __instance.followSchedule = false;
                                setNpcStateMachine(__instance, ModEntry.companionManager, ModEntry.ModHelper.Events, Monitor);
                                companionManager.PossibleCompanions[__instance.Name].Recruit();
                            }
                            return false;
                    }

                    if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                    {
                        Monitor.Log($"Found Available Companions", LogLevel.Info);
                        CompanionStateMachine s = companionManager.PossibleCompanions[__instance.Name];
                        Monitor.Log($"Current State: {s.CurrentStateFlag}", LogLevel.Info);
                    } 
                    else 
                    {
                        Monitor.Log($"Not Available Companions", LogLevel.Info);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in {nameof(NPC_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            }

            internal static void NPC_checkAction_Postfix(NPC __instance, Farmer who, GameLocation l)
            {
                if (who.ActiveObject?.QualifiedItemId == "(O)szzrain.CPMyFirstMod.debug.dog")
                {
                    return;
                }
                try
                {
                    if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                    {
                        Monitor.Log("Check action: NPC is following player.", LogLevel.Info);
                        if (__instance.isMarried() && __instance.getSpouse() == who && !__instance.hasBeenKissedToday.Value)
                        {
                            Monitor.Log($"Kissing {__instance.Name}.", LogLevel.Info);
                        }
                        else
                        {
                            companionManager.CheckAction(who, __instance, l);
                        }
                    }
                }
                catch (Exception e)
                {
                    Monitor.Log($"Failed in {nameof(NPC_checkAction_Postfix)}:\n{e}", LogLevel.Error);
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