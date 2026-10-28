using Microsoft.Xna.Framework;
using NpcFollowing.AI.Controller;
using NpcFollowing.StateMachine;
using NpcFollowing.Timing;
using NpcFollowing.Utils;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Monsters;

namespace NpcFollowing.AI;

/// <summary>Coordinates the active companion's movement and combat on the game thread.</summary>
internal partial class AI_StateMachine : NpcFollowing.Internal.IUpdateable, IDisposable
{
    public enum State
    {
        FOLLOW,
        FIGHT,
        IDLE,
        FORAGE,
        SPIRITUAL,
        FISH
    }

    private const string CHANGE_STATE_COOLDOWN = "changeStateCooldown";
    private const string SCARED_COOLDOWN = "scaredCooldown";
    private readonly IModEvents events;
    private readonly Countdown cooldown = new();
    private readonly Dictionary<State, IController> controllers = new();
    private IController? activeController;
    private double swimsuitUpdateDelayMs;
    private bool disposed;

    public readonly NPC npc;
    public readonly Farmer farmer;

    internal AI_StateMachine(CompanionStateMachine csm, IModEvents events, IMonitor monitor)
    {
        this.Csm = csm ?? throw new ArgumentNullException(nameof(csm));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        this.npc = csm.Companion;
        this.farmer = csm.CompanionManager.Farmer;
    }

    public State CurrentState { get; private set; }
    internal IController? CurrentController => this.activeController;
    internal CompanionStateMachine Csm { get; }
    internal IMonitor Monitor { get; }

    public event EventHandler<EventArgsLocationChanged>? LocationChanged;

    public void Setup()
    {
        if (this.disposed)
            throw new ObjectDisposedException(nameof(AI_StateMachine));
        if (this.controllers.Count > 0)
            return;

        this.controllers[State.FOLLOW] = new FollowController(this);
        this.controllers[State.FIGHT] = new FightController(this, this.events);
        this.controllers[State.SPIRITUAL] = new LovePeaceController(this, this.events);
        this.ChangeState(State.FOLLOW);
    }

    public bool IsLovedMonster(Monster monster)
    {
        return this.Csm.HasSkill("spiritual")
            && this.controllers.TryGetValue(State.SPIRITUAL, out IController? controller)
            && controller is LovePeaceController lovePeaceController
            && lovePeaceController.IsLovedMonster(monster);
    }

    public void ChangeState(State state)
    {
        if (this.disposed || !this.controllers.TryGetValue(state, out IController? controller))
            return;
        if (this.activeController == controller)
            return;

        this.Monitor.Log($"AI changes state {this.CurrentState} -> {state}", LogLevel.Trace);
        this.activeController?.Deactivate();
        this.CurrentState = state;
        this.activeController = controller;
        controller.Activate();
    }

    private bool IsThereAnyMonster(float distance)
    {
        Monster monster = Helper.GetNearestMonsterToCharacter(this.npc, distance);
        return monster != null && Helper.IsValidMonster(monster);
    }

    public bool IsFarmerNear(float maxDistance = 11f)
    {
        return Helper.Distance(this.farmer.Tile.ToPoint(), this.npc.Tile.ToPoint()) < maxDistance;
    }

    private void CheckPotentialStateChange()
    {
        if (this.npc.swimming.Value || this.swimsuit)
        {
            this.ChangeState(State.FOLLOW);
            return;
        }

        float defendRadius = this.Csm.HasSkill("warrior")
            ? FightController.DEFEND_TILE_RADIUS_WARRIOR
            : FightController.DEFEND_TILE_RADIUS;
        if (!this.cooldown.IsRunning(CHANGE_STATE_COOLDOWN)
            && this.CurrentState != State.FIGHT
            && this.IsFarmerNear()
            && this.IsThereAnyMonster(defendRadius))
        {
            this.ChangeState(State.FIGHT);
        }

        if (this.CurrentState != State.FOLLOW && this.activeController?.IsIdle == true)
        {
            this.cooldown.Set(CHANGE_STATE_COOLDOWN, 100);
            this.ChangeState(State.FOLLOW);
        }
    }

    public void Update(UpdateTickedEventArgs e)
    {
        if (this.disposed || !Context.IsWorldReady || this.npc.currentLocation == null)
            return;

        if (this.swimsuitUpdateDelayMs > 0)
        {
            this.swimsuitUpdateDelayMs -= Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
            if (this.swimsuitUpdateDelayMs <= 0)
                this.AfterWarpChangeSwimsuit();
        }

        if (e.IsMultipleOf(15))
        {
            this.DoSideEffects();
            this.CheckPotentialStateChange();
        }

        this.cooldown.Update(e);
        this.CheckSwimming();
        this.activeController?.Update(e);
        foreach (IController controller in this.controllers.Values)
            controller.SideUpdate(e);
    }

    private void DoSideEffects()
    {
        if (!this.Csm.HasSkill("scared") || !this.IsThereAnyMonster(4f)
            || this.cooldown.IsRunning(SCARED_COOLDOWN)
            || Game1.random.Next(0, this.GetNotBeScaredChanceIndex()) != 1)
        {
            return;
        }

        this.npc.Halt();
        this.npc.shake(1000);
        this.npc.jump();
        this.npc.currentLocation.playSound("batScreech");
        if (!this.npc.IsEmoting && Game1.random.Next(0, 8) == 1)
        {
            this.npc.doEmote(28);
            this.farmer.changeFriendship(-1, this.npc);
        }

        this.cooldown.Set(SCARED_COOLDOWN, Game1.random.Next(800, 2400));
        this.cooldown.Set(CHANGE_STATE_COOLDOWN, 100);
        this.ChangeState(State.FOLLOW);
    }

    private int GetNotBeScaredChanceIndex()
    {
        int chance = 4 + this.farmer.CombatLevel / 2;
        return this.Csm.HasSkill("warrior") ? chance * 2 : chance;
    }

    public void ChangeLocation(GameLocation location)
    {
        if (this.disposed)
            return;

        GameLocation previousLocation = this.npc.currentLocation;
        Helper.WarpTo(this.npc, location, this.farmer.Tile.ToPoint());
        this.cooldown.Set(CHANGE_STATE_COOLDOWN, 30);
        this.LocationChanged?.Invoke(this, new EventArgsLocationChanged
        {
            PreviousLocation = previousLocation,
            CurrentLocation = this.npc.currentLocation
        });

        // Asset and NPC changes must stay on the game's update thread.
        this.swimsuitUpdateDelayMs = 1000;
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.swimsuitUpdateDelayMs = 0;
        this.activeController?.Deactivate();
        this.activeController = null;
        foreach (IDisposable controller in this.controllers.Values.OfType<IDisposable>())
            controller.Dispose();
        this.controllers.Clear();
        this.cooldown.Reset();
        this.LocationChanged = null;
    }
}
