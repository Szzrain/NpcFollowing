using System.Reflection;
using Microsoft.Xna.Framework;
using NpcFollowing.StateMachine.StateFeatures;
using NpcFollowing.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Pathfinding;

namespace NpcFollowing.StateMachine.State;

/// <summary>Returns a dismissed companion to the game's current schedule.</summary>
internal class ResetState : CompanionState, ICompanionIntegrator
{
    private static readonly FieldInfo? ReturningToEndPointField = typeof(NPC).GetField(
        "returningToEndPoint", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CurrentEndOfRouteAnimationField = typeof(NPC).GetField(
        "currentlyDoingEndOfRouteAnimation", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? FinishEndOfRouteAnimationMethod = typeof(NPC).GetMethod(
        "routeEndAnimationFinished", BindingFlags.Instance | BindingFlags.NonPublic);

    public ResetState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor)
        : base(stateMachine, events, monitor)
    {
    }

    internal static void StopScheduleBehavior(NPC companion)
    {
        companion.IsWalkingInSquare = false;
        companion.EndActivityRouteEndBehavior();
        // An outro normally invokes this callback later. Recruitment cancels that animation,
        // so finish the native cleanup now to release freezeMotion and restore sprite dimensions.
        if (companion.doingEndOfRouteAnimation.Value)
        {
            MethodInfo finish = FinishEndOfRouteAnimationMethod
                ?? throw new MissingMethodException(typeof(NPC).FullName, "routeEndAnimationFinished");
            finish.Invoke(companion, new object?[] { null });
        }
        CurrentEndOfRouteAnimationField?.SetValue(companion, false);
        companion.doingEndOfRouteAnimation.Value = false;
        companion.goingToDoEndOfRouteAnimation.Value = false;
        // The public route-ending API leaves this protected return-to-square flag set.
        if (companion.IsReturningToEndPoint())
            ReturningToEndPointField?.SetValue(companion, false);
        companion.Sprite.StopAnimation();
        NpcSpriteHelper.KeepValidStaticFrame(companion.Sprite);
        companion.Halt();
        companion.controller = null;
        companion.temporaryController = null;
        companion.DirectionsToNewLocation = null;
        companion.queuedSchedulePaths.Clear();
        companion.movementPause = 0;
        companion.faceTowardFarmerTimer = 0;
        companion.xVelocity = 0;
        companion.yVelocity = 0;
    }

    public void ReintegrateCompanionNPC()
    {
        if (this.StateMachine.CurrentStateFlag != CompanionStateMachine.StateFlag.RESET
            || this.StateMachine.Companion == null)
        {
            return;
        }

        NPC companion = this.StateMachine.Companion;
        try
        {
            StopScheduleBehavior(companion);
            companion.ignoreScheduleToday = this.StateMachine.BackedUpIgnoreScheduleToday;
            companion.farmerPassesThrough = this.StateMachine.BackedUpFarmerPassesThrough;
            companion.EventActor = this.StateMachine.BackedUpEventActor;
            companion.swimming.Value = false;
            companion.wearNormalClothes();

            // A stationary debug actor may have no schedule or configured home at all.
            // Return the exact clicked instance to its original spot without creating a schedule.
            if (this.StateMachine.IsDebugSession && this.StateMachine.BackedupSchedule == null)
            {
                companion.ClearSchedule();
                this.ReturnToRecruitmentPosition(companion);
                companion.ignoreScheduleToday = this.StateMachine.BackedUpIgnoreScheduleToday;
                companion.followSchedule = this.StateMachine.BackedUpFollowSchedule;
                return;
            }

            // Preserve the schedule chosen before recruitment, including island and rain variants.
            if (this.StateMachine.BackedupSchedule != null)
                companion.TryLoadSchedule(this.StateMachine.BackedUpScheduleKey ?? "", this.StateMachine.BackedupSchedule);
            else
                companion.TryLoadSchedule();

            SchedulePathDescription? currentStop = companion.ignoreScheduleToday
                ? null
                : companion.Schedule?.Where(entry => entry.Key <= Game1.timeOfDay)
                    .OrderByDescending(entry => entry.Key)
                    .Select(entry => entry.Value)
                    .FirstOrDefault();

            GameLocation? destination = string.IsNullOrWhiteSpace(currentStop?.targetLocationName)
                ? null
                : Game1.getLocationFromName(currentStop.targetLocationName);
            if (destination != null && currentStop != null)
            {
                Game1.warpCharacter(companion, destination, currentStop.targetTile.ToVector2());
                companion.previousEndPoint = currentStop.targetTile;
                NpcSpriteHelper.FaceDirection(companion, currentStop.facingDirection);
                companion.StartActivityRouteEndBehavior(currentStop.endOfRouteBehavior, currentStop.endOfRouteMessage);
            }
            else
            {
                this.ReturnHome(companion);
            }

            // The latest elapsed route is already completed; only future schedule entries should run.
            companion.lastAttemptedSchedule = Game1.timeOfDay;
            companion.checkSchedule(Game1.timeOfDay);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Couldn't restore {companion.Name}'s schedule: {ex}", LogLevel.Error);
            companion.ClearSchedule();
            this.ReturnHome(companion);
        }
    }

    private void ReturnHome(NPC companion)
    {
        GameLocation? home = companion.isMarried() && companion.getSpouse() is Farmer spouse
            ? Utility.getHomeOfFarmer(spouse)
            : Game1.getLocationFromName(companion.DefaultMap);
        if (home == null)
        {
            if (!this.ReturnToRecruitmentPosition(companion))
                this.monitor.Log($"Couldn't find a home or recruitment location for dismissed companion {companion.Name}.", LogLevel.Warn);
            return;
        }

        Vector2 tile = home is FarmHouse house && companion.isMarried()
            ? house.getKitchenStandingSpot().ToVector2()
            : companion.DefaultPosition / Game1.tileSize;
        Game1.warpCharacter(companion, home, tile);
        companion.previousEndPoint = tile.ToPoint();
        NpcSpriteHelper.FaceDirection(companion, companion.DefaultFacingDirection);
    }

    private bool ReturnToRecruitmentPosition(NPC companion)
    {
        GameLocation? location = this.StateMachine.BackedUpLocation;
        if (location == null)
            return false;

        Vector2 tile = this.StateMachine.BackedUpPosition / Game1.tileSize;
        Game1.warpCharacter(companion, location, tile);
        companion.previousEndPoint = tile.ToPoint();
        NpcSpriteHelper.FaceDirection(companion, this.StateMachine.BackedUpFacingDirection);
        return true;
    }
}
