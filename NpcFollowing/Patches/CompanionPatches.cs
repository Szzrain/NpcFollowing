using NpcFollowing.StateMachine;
using StardewModdingAPI;
using StardewValley;
using CompanionHelper = NpcFollowing.Utils.Helper;

namespace NpcFollowing.Patches;

/// <summary>Interception only: registration and game-session ownership belong to the manager.</summary>
internal static class CompanionPatches
{
    private const string DebugStickId = "(O)szzrain.NpcFollowing.object.debug.stick";
    [ThreadStatic] private static int UseToolInteractionDepth;
    private static CompanionManager manager = null!;
    private static IModHelper helper = null!;
    private static IMonitor monitor = null!;

    public static void Initialize(CompanionManager companions, IModHelper modHelper, IMonitor modMonitor)
    {
        manager = companions;
        helper = modHelper;
        monitor = modMonitor;
    }

    public static bool CheckActionPrefix(NPC __instance, Farmer who, GameLocation l, ref bool __result, out bool __state)
    {
        __state = false;
        if (UseToolInteractionDepth > 0 || !manager.CanRecruit())
            return true;
        try
        {
            if (who.ActiveObject?.QualifiedItemId == DebugStickId)
            {
                if (!manager.ToggleDebugCompanion(__instance))
                    return true;
                __instance.doEmote(20);
                __result = true;
                return false;
            }

            if (!manager.IsCandidate(__instance))
                return true;

            // Shift deliberately bypasses ordinary queued dialogue, while gifts and unavailable actors keep vanilla behavior.
            if (helper.Input.IsDown(SButton.LeftShift) && Context.IsPlayerFree && !Game1.eventUp
                && who.ActiveObject?.canBeGivenAsGift() != true && !__instance.IsInvisible
                && !__instance.isSleeping.Value && !who.isRidingHorse()
                && manager.CheckAction(who, __instance, l))
            {
                __result = true;
                return false;
            }
            __state = __instance.hasBeenKissedToday.Value;
        }
        catch (Exception ex)
        {
            monitor.Log($"Failed to handle companion interaction for '{__instance.Name}': {ex}", LogLevel.Error);
        }
        return true;
    }

    public static void CheckActionPostfix(NPC __instance, Farmer who, GameLocation l, ref bool __result, bool __state)
    {
        if (UseToolInteractionDepth > 0 || !manager.CanRecruit() || who.ActiveObject?.QualifiedItemId == DebugStickId
            || __instance.CurrentDialogue.Count > 0 || Game1.activeClickableMenu != null)
            return;
        try
        {
            if ((!__result || (__state && who.FarmerSprite.CurrentFrame == 101))
                && CompanionHelper.CanRequestDialog(who, __instance, true))
            {
                bool handled = manager.CheckAction(who, __instance, l);
                if (!handled)
                    return;
                __result = true;
                if (who.FarmerSprite.CurrentFrame == 101)
                {
                    who.completelyStopAnimatingOrDoingAction();
                    __instance.IsEmoting = false;
                    __instance.Halt();
                    __instance.facePlayer(who);
                }
            }
        }
        catch (Exception ex)
        {
            monitor.Log($"Failed to handle companion interaction for '{__instance.Name}': {ex}", LogLevel.Error);
        }
    }

    public static void UseToolInteractionPrefix(out bool __state)
    {
        // Vanilla may route tool input through pressActionButton/checkAction.
        // Scope by the call path so remapped keys and controllers work too.
        __state = true;
        UseToolInteractionDepth++;
    }

    public static void UseToolInteractionFinalizer(bool __state)
    {
        // Unwind nested calls, early returns and exceptions. A skipped prefix
        // must not decrement an enclosing tool interaction's depth.
        if (__state)
            UseToolInteractionDepth--;
    }

    public static bool ReturnHomePrefix(NPC __instance) => !manager.CanRecruit()
        || !manager.PossibleCompanions.TryGetValue(__instance.Name, out var state)
        || !ReferenceEquals(state.Companion, __instance)
        || state.CurrentStateFlag != CompanionStateMachine.StateFlag.RECRUITED;

    // Preserve the workspace's existing wedding-day compatibility fix.
    public static bool NewDayAfterFadePrefix(Action after)
    {
        if (!Context.IsWorldReady || Context.IsMultiplayer)
            return true;
        try
        {
            if (Game1.player.spouse != null && Game1.player.isEngaged() && Game1.weddingsToday.Contains(Game1.player.UniqueMultiplayerID))
            {
                Friendship friendship = Game1.player.friendshipData[Game1.player.spouse];
                friendship.Status = FriendshipStatus.Married;
                friendship.WeddingDate = new WorldDate(Game1.Date);
                Game1.prepareSpouseForWedding(Game1.player);
                Game1.weddingsToday.Remove(Game1.player.UniqueMultiplayerID);
            }
        }
        catch (Exception ex)
        {
            monitor.Log($"Failed to apply wedding-day compatibility fix: {ex}", LogLevel.Error);
        }
        return true;
    }
}
