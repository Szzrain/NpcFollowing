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
        public static ModEntry Instance { get; private set; }
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
            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.returnHomeFromFarmPosition)),
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.returnHomeFromFarmPosition_Prefix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.warpCharacter), new Type[] { typeof(NPC), typeof(GameLocation), typeof(Vector2) }),
                prefix: new HarmonyMethod(typeof(ObjectPatches), nameof(ObjectPatches.Warp_Prefix))
            );
            companionManager = new CompanionManager(helper, this.Monitor);
            Instance = this;
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
                    Monitor.Log($"Called TryLoadSchedule for {__instance.Name} at {key}", LogLevel.Debug);
                    return true;
                } catch (Exception ex) {
                    Monitor.Log($"Failed in {nameof(TryLoadSchedule_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
            
            internal static bool returnHomeFromFarmPosition_Prefix(NPC __instance, Farm farm)
            {
                try
                {
                    Monitor.Log($"Called returnHomeFromFarm for {__instance.Name}. With Location: {farm.Name}", LogLevel.Debug);
                    if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                    {
                        CompanionStateMachine s = companionManager.PossibleCompanions[__instance.Name];
                        if (s.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                        {
                            Monitor.Log($"Trying to warp {__instance.Name} but it's following a player, skipping", LogLevel.Info);
                            return false;
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in {nameof(returnHomeFromFarmPosition_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
            
            internal static bool Warp_Prefix(NPC character, GameLocation targetLocation, Vector2 position)
            {
                try
                {
                    Monitor.Log($"Called Warp for {character.Name}. With Location: {targetLocation.Name} at Position: {position.ToString()}", LogLevel.Debug);
                    return true;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in {nameof(Warp_Prefix)}:\n{ex}", LogLevel.Error);
                    return true;
                }
            }

            internal static bool NPC_checkAction_Prefix(NPC __instance, Farmer who, GameLocation l, ref bool __result)
            {
                try
                {
                    Monitor.Log($"Called checkAction for {__instance.Name}. With Farmer {who.Name} at Location: {l.Name} ", LogLevel.Debug);
                    Monitor.Log($"CurrentPlayer active object is ({who.ActiveObject?.name})[{who.ActiveObject?.displayName}] ID: {who.ActiveObject?.QualifiedItemId} Category: {who.ActiveObject?.category.Value}", LogLevel.Debug);
                    switch (who.ActiveObject?.QualifiedItemId)
                    {
                        case "(O)szzrain.NpcFollowing.object.debug.stick":
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
                        s.IsKissedEarly = __instance.hasBeenKissedToday.Value;
                        Monitor.Log($"Current State: {s.CurrentStateFlag}", LogLevel.Info);
                    } 
                    else 
                    {
                        Monitor.Log($"Not Available Companions, adding", LogLevel.Info);
                        setNpcStateMachine(__instance, ModEntry.companionManager, ModEntry.ModHelper.Events, Monitor);
                        companionManager.PossibleCompanions[__instance.Name].ChangeState(CompanionStateMachine.StateFlag.AVAILABLE);
                    }
                    if (ModEntry.Instance.Helper.Input.IsDown(SButton.LeftShift) && companionManager.CheckAction(who, __instance, l))
                    {
                        __result = true;
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

            internal static void NPC_checkAction_Postfix(NPC __instance, Farmer who, GameLocation l, ref bool __result, ref bool __state)
            {
                if (who.ActiveObject?.QualifiedItemId == "(O)szzrain.NpcFollowing.object.debug.stick")
                {
                    return;
                }
                // try
                // {
                //     if (companionManager.PossibleCompanions.ContainsKey(__instance.Name))
                //     {
                //         CompanionStateMachine s = companionManager.PossibleCompanions[__instance.Name];
                //         Monitor.Log("Check action: NPC can following player.", LogLevel.Info);
                //         if (__result || (__instance.isMarried() && __instance.getSpouse() == who 
                //             && (!__instance.hasBeenKissedToday.Value) || ( __instance.hasBeenKissedToday.Value && !s.IsKissedEarly)))
                //         {
                //             s.IsKissedEarly = false;
                //             Monitor.Log($"Interacting with {__instance.Name}, skip action check", LogLevel.Info);
                //         }
                //         else if (s.CurrentStateFlag == CompanionStateMachine.StateFlag.RECRUITED)
                //         {
                //             companionManager.CheckAction(who, __instance, l);
                //         }
                //     }
                // }
                // catch (Exception e)
                // {
                //     Monitor.Log($"Failed in {nameof(NPC_checkAction_Postfix)}:\n{e}", LogLevel.Error);
                // }
                try
                {
                    if (__instance.CurrentDialogue.Count > 0)
                    {
                        Monitor.Log("CurrentDialogue.Count > 0, skip", LogLevel.Debug);
                        return;
                    }
                    // Check our action when vanilla check action don't triggered or spouse was kissed today and farmer try to kiss again
                    if (!__result || (__result && __state && who.FarmerSprite.CurrentFrame == 101))
                    {
                        __result = companionManager.CheckAction(who, __instance, l);

                        // Cancel kiss when spouse was kissed today and we did our action
                        if (__result && who.FarmerSprite.CurrentFrame == 101)
                        {
                            who.completelyStopAnimatingOrDoingAction();
                            __instance.IsEmoting = false;
                            __instance.Halt();
                            __instance.facePlayer(who);
                        }
                    }
                } catch (Exception ex)
                {
                    Monitor.Log($"Failed in {nameof(NPC_checkAction_Postfix)}:\n{ex}", LogLevel.Error);
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