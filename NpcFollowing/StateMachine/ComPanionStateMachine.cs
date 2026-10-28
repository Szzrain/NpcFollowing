using Microsoft.Xna.Framework;
using NpcFollowing.Model;
using NpcFollowing.Objects;
using NpcFollowing.StateMachine.StateFeatures;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Pathfinding;

namespace NpcFollowing.StateMachine;

/// <summary>Owns one NPC's session, inventory, and paired state entry/exit calls.</summary>
internal sealed class CompanionStateMachine : IDisposable
{
    public enum StateFlag { RESET, AVAILABLE, RECRUITED, UNAVAILABLE }

    private ICompanionState? currentState;
    public CompanionManager CompanionManager { get; }
    public NPC Companion { get; }
    public string Name { get; }
    public IMonitor Monitor { get; }
    public CompanionDefinition Definition { get; set; }
    public Chest Bag { get; private set; } = new();
    public Dictionary<StateFlag, ICompanionState> States { get; private set; } = new();
    public StateFlag CurrentStateFlag { get; private set; }
    public Dictionary<int, SchedulePathDescription>? BackedupSchedule { get; private set; }
    public string? BackedUpScheduleKey { get; private set; }
    public bool BackedUpIgnoreScheduleToday { get; private set; }
    public GameLocation? BackedUpLocation { get; private set; }
    public Vector2 BackedUpPosition { get; private set; }
    public int BackedUpFacingDirection { get; private set; }
    public bool BackedUpFollowSchedule { get; private set; }
    public bool BackedUpEventActor { get; private set; }
    public bool BackedUpFarmerPassesThrough { get; private set; }
    public bool IsDebugSession { get; private set; }
    public bool RecruitedToday { get; private set; }
    public bool IsKissedEarly { get; set; }
    public bool IsDisposed { get; private set; }

    public CompanionStateMachine(CompanionManager manager, NPC companion, CompanionDefinition definition, IMonitor monitor)
    {
        CompanionManager = manager;
        Companion = companion;
        Name = companion.Name;
        Definition = definition.Snapshot();
        Monitor = monitor;
    }

    public void Setup(Dictionary<StateFlag, ICompanionState> handlers)
    {
        if (States.Count != 0)
            throw new InvalidOperationException("Companion states are already set up.");
        States = handlers;
        ResetStateMachine();
    }

    public void ChangeState(StateFlag state)
    {
        if (IsDisposed)
            return;
        if (!States.TryGetValue(state, out var next))
            throw new InvalidStateException($"Missing {state} state for '{Name}'.");
        if (ReferenceEquals(currentState, next))
            return;

        currentState?.Exit();
        currentState = next;
        CurrentStateFlag = state;
        try
        {
            next.Entry();
        }
        catch
        {
            next.Exit();
            currentState = null;
            CurrentStateFlag = StateFlag.RESET;
            throw;
        }
    }

    public ICompanionState? GetCurrentStateBehavior() => currentState;

    public void SetupForNewDay()
    {
        if (CurrentStateFlag == StateFlag.RECRUITED)
            Dismiss();
        ResetStateMachine();
        RecruitedToday = false;
        ChangeState(Utility.isFestivalDay(Game1.dayOfMonth, Game1.season) || CompanionManager.IsUnavailableToday(Name)
            ? StateFlag.UNAVAILABLE : StateFlag.AVAILABLE);
    }

    public void MakeAvailable() => ChangeState(StateFlag.AVAILABLE);
    public void MakeUnavailable()
    {
        CompanionManager.MarkUnavailableToday(Name);
        ChangeState(StateFlag.UNAVAILABLE);
    }
    public void ResetStateMachine() => ChangeState(StateFlag.RESET);

    public void Kill()
    {
        currentState?.Exit();
        currentState = null;
        CurrentStateFlag = StateFlag.RESET;
    }

    internal void Dismiss(bool keepUnavailableOthers = false)
    {
        if (IsDisposed || CurrentStateFlag != StateFlag.RECRUITED)
            return;
        ResetStateMachine();
        if (currentState is ICompanionIntegrator integrator)
            integrator.ReintegrateCompanionNPC();
        ClearRecruitmentBackup();
        MakeUnavailable();
        CompanionManager.CompanionDissmised(this);
    }

    public bool Recruit(bool force = false)
    {
        if (IsDisposed || CurrentStateFlag != StateFlag.AVAILABLE || !CompanionManager.CanRecruit()
            || (!force && !CompanionManager.IsCandidate(Companion)))
            return false;
        BackedupSchedule = Companion.Schedule;
        BackedUpScheduleKey = Companion.dayScheduleName.Value;
        BackedUpIgnoreScheduleToday = Companion.ignoreScheduleToday;
        BackedUpLocation = Companion.currentLocation;
        BackedUpPosition = Companion.Position;
        BackedUpFacingDirection = Companion.FacingDirection;
        BackedUpFollowSchedule = Companion.followSchedule;
        BackedUpEventActor = Companion.EventActor;
        BackedUpFarmerPassesThrough = Companion.farmerPassesThrough;
        IsDebugSession = force;
        Definition = Definition.Snapshot();
        if (!Companion.currentLocation.isTilePassable(Companion.GetBoundingBox(), Game1.viewport))
            Companion.setTileLocation(Companion.GetGrabTile());
        try
        {
            ChangeState(StateFlag.RECRUITED);
        }
        catch
        {
            ResetStateMachine();
            if (currentState is ICompanionIntegrator integrator)
                integrator.ReintegrateCompanionNPC();
            ClearRecruitmentBackup();
            throw;
        }
        RecruitedToday = true;
        CompanionManager.CompanionRecuited(Name);
        return true;
    }

    private void ClearRecruitmentBackup()
    {
        BackedupSchedule = null;
        BackedUpScheduleKey = null;
        BackedUpLocation = null;
        IsDebugSession = false;
    }

    /// <summary>Transfer actual items once, using the saved lost-and-found inventory if the house is full.</summary>
    public bool DumpBagInFarmHouse()
    {
        if (Bag.isEmpty())
            return true;
        var house = Utility.getHomeOfFarmer(Game1.player);
        Vector2? place = null;
        for (int y = 0; y < house.Map.Layers[0].LayerHeight && place is null; y++)
        {
            for (int x = 0; x < house.Map.Layers[0].LayerWidth; x++)
            {
                var tile = new Vector2(x, y);
                if (!house.objects.ContainsKey(tile) && house.CanItemBePlacedHere(tile) && !house.isTileOnWall(x, y))
                {
                    place = tile;
                    break;
                }
            }
        }
        if (place is null)
        {
            foreach (var item in Bag.Items.Where(item => item != null).ToArray())
                Game1.player.team.returnedDonations.Add(item);
            Game1.player.team.newLostAndFoundItems.Value = true;
            Bag.Items.Clear();
            Bag = new Chest();
            Monitor.Log($"The farmhouse has no free delivery tile. Returned '{Name}'s bag to the lost-and-found at the Mayor's Manor.", LogLevel.Warn);
            return true;
        }
        var package = new Package(Bag.Items.Where(item => item != null).ToList(), place.Value) { GivenFrom = Name };
        house.objects.Add(place.Value, package);
        Bag.Items.Clear();
        Bag = new Chest();
        return true;
    }

    // Existing AI skills and ambient dialogue are not enabled in this version.
    public bool HasSkill(string skill) => false;
    public bool HasSkills(params string[] skills) => skills.All(HasSkill);
    public bool HasSkillsAny(params string[] skills) => skills.Any(HasSkill);
    public Dialogue? GenerateLocationDialogue(GameLocation location, string suffix = "") => null;

    public bool CanPerformAction() => !IsDisposed && currentState is IActionPerformer action && action.CanPerformAction;
    public bool CheckAction(Farmer who, GameLocation location) => CanPerformAction()
        && ((IActionPerformer)currentState!).PerformAction(who, location);

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        currentState?.Exit();
        currentState = null;
        foreach (var state in States.Values.OfType<IDisposable>())
            state.Dispose();
        States.Clear();
        ClearRecruitmentBackup();
    }
}

internal abstract class CompanionStateException : Exception
{
    protected CompanionStateException(string message) : base(message) { }
}

internal sealed class InvalidStateException : CompanionStateException
{
    public InvalidStateException(string message) : base(message) { }
}

internal sealed class TransitionStateException : CompanionStateException
{
    public TransitionStateException(string message) : base(message) { }
}
