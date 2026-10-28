using NpcFollowing.AI;
using NpcFollowing.StateMachine.StateFeatures;
using NpcFollowing.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace NpcFollowing.StateMachine.State;

/// <summary>Owns a recruited companion's AI, player interactions, and event subscriptions.</summary>
internal class RecruitedState : CompanionState, IActionPerformer
{
    private AI_StateMachine? ai;

    public RecruitedState(CompanionStateMachine stateMachine, IModEvents events, IMonitor monitor)
        : base(stateMachine, events, monitor)
    {
    }

    public bool CanPerformAction { get; private set; }

    public AI_StateMachine? GetAI() => this.ai;

    public override void Entry()
    {
        NPC companion = this.StateMachine.Companion
            ?? throw new TransitionStateException("Companion NPC can't be null for recruit.");

        ResetState.StopScheduleBehavior(companion);
        this.FixProblemsWithNPC();
        companion.CurrentDialogue.Clear();
        this.ai = new AI_StateMachine(this.StateMachine, this.Events, this.monitor);
        this.ai.Setup();
        this.CanPerformAction = true;

        this.Events.GameLoop.UpdateTicked += this.GameLoop_UpdateTicked;
        this.Events.GameLoop.TimeChanged += this.GameLoop_TimeChanged;
        this.Events.Player.Warped += this.Player_Warped;
    }

    public override void Exit()
    {
        this.Events.GameLoop.UpdateTicked -= this.GameLoop_UpdateTicked;
        this.Events.GameLoop.TimeChanged -= this.GameLoop_TimeChanged;
        this.Events.Player.Warped -= this.Player_Warped;
        this.ai?.Dispose();
        this.ai = null;
        this.CanPerformAction = false;

        if (this.StateMachine.Companion is NPC companion)
        {
            companion.EventActor = false;
            companion.farmerPassesThrough = false;
        }
    }

    private void GameLoop_TimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (Context.IsWorldReady && !Context.IsMultiplayer && this.ai != null)
            this.UpdateFriendship(e.NewTime);
    }

    private void UpdateFriendship(int time)
    {
        if (time % 100 != 0)
            return;

        Farmer farmer = this.StateMachine.CompanionManager.Farmer;
        NPC companion = this.StateMachine.Companion;
        if (!companion.CanSocialize)
            return;
        int points = 2;
        if (farmer.friendshipData.TryGetValue(companion.Name, out Friendship friendship))
        {
            if (friendship.IsMarried())
                points = 5;
            else if (friendship.IsDating())
                points = 3;
        }

        if (companion.isBirthday())
            points *= 2;
        farmer.changeFriendship(points, companion);
        this.monitor.VerboseLog($"You gained {points} friendship points with {companion.Name}.");
    }

    private void GameLoop_UpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Context.IsMultiplayer || !Context.IsPlayerFree || this.ai == null)
            return;

        this.FixProblemsWithNPC();
        this.ai.Update(e);
    }

    private void FixProblemsWithNPC()
    {
        NPC companion = this.StateMachine.Companion;
        companion.controller = null;
        companion.temporaryController = null;
        companion.queuedSchedulePaths.Clear();
        companion.movementPause = 0;
        companion.ignoreScheduleToday = true;
        companion.ClearSchedule();
        companion.faceTowardFarmerTimer = 0;
        companion.farmerPassesThrough = true;
    }

    private void Player_Warped(object? sender, WarpedEventArgs e)
    {
        if (!Context.IsWorldReady || Context.IsMultiplayer || !e.IsLocalPlayer || this.ai == null)
            return;

        if (this.StateMachine.Companion.currentLocation != e.NewLocation)
            this.ai.ChangeLocation(e.NewLocation);
    }

    public bool PerformAction(Farmer who, GameLocation location)
    {
        if (!this.CanPerformAction)
            return false;

        CompanionManager manager = this.StateMachine.CompanionManager;
        NPC companion = this.StateMachine.Companion;
        Response[] responses =
        {
            new("bag", manager.Translate("companion.menu.bag")),
            new("dismiss", manager.Translate("companion.menu.dismiss", new { npc = companion.displayName })),
            new("nothing", manager.Translate("companion.menu.nothing"))
        };
        location.createQuestionDialogue(manager.Translate("companion.menu.question"), responses, (_, answer) =>
        {
            if (!this.CanPerformAction || answer == "nothing")
                return;

            companion.Halt();
            NpcSpriteHelper.FacePlayer(companion, who);
            switch (answer)
            {
                case "dismiss":
                    this.StateMachine.Dismiss();
                    break;
                case "bag":
                    companion.currentLocation.playSound("openBox");
                    Game1.activeClickableMenu = this.CreateOpenBagMenu(this.StateMachine.Bag);
                    break;
            }
        }, companion);
        return true;
    }

    private IClickableMenu CreateOpenBagMenu(Chest bag)
    {
        bool android = Constants.TargetPlatform == GamePlatform.Android;
        return new ItemGrabMenu(
            inventory: bag.Items,
            reverseGrab: android,
            showReceivingMenu: true,
            highlightFunction: InventoryMenu.highlightAllItems,
            behaviorOnItemSelectFunction: android ? null : bag.grabItemFromInventory,
            message: this.StateMachine.Companion.displayName,
            behaviorOnItemGrab: android ? null : bag.grabItemFromChest,
            canBeExitedWithKey: true,
            showOrganizeButton: true,
            source: ItemGrabMenu.source_chest,
            context: this.StateMachine.Companion);
    }
}
